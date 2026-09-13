using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MovementRecorder.Models;
using MovementRecorder.Playback.Data;
using UnityEngine;

namespace MovementRecorder.Playback.Models
{
    internal sealed class BindingProfile
    {
        public int Version { get; set; } = 1;
        public string RecordingSignature { get; set; }
        public Dictionary<string, string> Roots { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Tracks { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Hierarchies { get; set; } = new Dictionary<string, string>();
        public List<string> OmittedRoots { get; set; } = new List<string>();
        public string LeftAnchor { get; set; }
        public string RightAnchor { get; set; }
        public bool FreezeMissing { get; set; }
    }

    internal sealed class BindingIssue
    {
        public string RecordedPath { get; set; }
        public string Root { get; set; }
        public string Message { get; set; }
    }

    internal sealed class ModelBindingPlan
    {
        public Transform[] Sources { get; set; }
        public string[] Types { get; set; }
        public string[] RecordedRoots { get; set; }
        public Transform[] CloneRoots { get; set; }
        public List<BindingIssue> Issues { get; set; }
        public bool Ready => Issues.Count == 0;

        public HashSet<Transform> FindAvatarTransforms(IEnumerable<Transform> geometry)
        {
            var types = new Dictionary<Transform, string>();
            for (int i = 0; i < Sources.Length; i++)
                if (Sources[i] != null && Types != null && i < Types.Length && Types[i] != null) types[Sources[i]] = Types[i];
            var result = new HashSet<Transform>();
            foreach (var source in geometry)
                for (var parent = source; parent != null; parent = parent.parent)
                    if (types.TryGetValue(parent, out string type))
                    {
                        // A more specific Other track keeps its own visibility policy inside an avatar.
                        if (type == "Avatar") result.Add(source);
                        break;
                    }
            return result;
        }
    }

    internal sealed class SceneModelResolver
    {
        public const string ReplayRootName = "MovementRecorder Replay Objects";
        private readonly MovementClip _clip;
        private readonly Dictionary<Transform, string> _paths;
        public IReadOnlyDictionary<Transform, string> Paths => _paths;

        public SceneModelResolver(MovementClip clip)
        {
            _clip = clip;
            _paths = Resources.FindObjectsOfTypeAll<Transform>()
                .Where(t => t != null && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded && !IsReplayObject(t)).ToDictionary(t => t, PathOf);
            if (_paths.Count > 100000) throw new InvalidOperationException("シーンのモデル数が探索上限を超えています。");
        }
        public static string PathOf(Transform transform)
        {
            var parts = new Stack<string>();
            for (var t = transform; t != null; t = t.parent) parts.Push(t.name);
            return string.Join("/", parts);
        }
        public static string IdentityOf(Transform transform)
        {
            var indices = new Stack<int>();
            for (var t = transform; t != null; t = t.parent) indices.Push(t.GetSiblingIndex());
            return PathOf(transform) + " [" + transform.gameObject.scene.name + ":" + string.Join(".", indices) + "]";
        }
        public Transform[] FindSource(string identity) => _paths.Where(p => p.Key != null && (p.Value == identity || IdentityOf(p.Key) == identity)).Select(p => p.Key).ToArray();
        public string[] RenderableRootChoices()
        {
            // The snapshot can outlive transient UI objects or a replaced avatar hierarchy.
            var candidates = new HashSet<Transform>();
            foreach (var transform in _paths.Keys.Where(t => t != null && t.GetComponent<Renderer>() != null))
                for (var t = transform; t != null; t = t.parent) if (_paths.ContainsKey(t)) candidates.Add(t);
            return candidates.Where(t => t != null).Select(IdentityOf).OrderBy(p => p).Take(1000).ToArray();
        }
        private static bool IsReplayObject(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == ReplayRootName) return true;
            return false;
        }
        internal static string Normalize(string path) => path.Replace("(Clone)", "");
        private static int Depth(string path) => path.Count(c => c == '/');
        private static bool Below(string child, string root) => child == root || child.StartsWith(root + "/", StringComparison.Ordinal);
        public static string Signature(MovementClip clip)
        {
            string text = string.Join("\n", clip.Header.objectNames) + Newtonsoft.Json.JsonConvert.SerializeObject(clip.Header.Settings);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
        public static string HierarchySignature(Transform root)
        {
            string rootPath = PathOf(root);
            string text = string.Join("\n", root.GetComponentsInChildren<Transform>(true).Select(t => PathOf(t).Substring(rootPath.Length) + ":" + t.GetSiblingIndex()).OrderBy(p => p));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
        private static Regex[] Patterns(IEnumerable<string> values) => (values ?? Enumerable.Empty<string>())
            .Select(p => new Regex(p, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25))).ToArray();

        public ModelBindingPlan Resolve(BindingProfile profile)
        {
            var names = _clip.Header.objectNames;
            var types = new string[names.Count];
            var keys = new Dictionary<int, Dictionary<string, List<Transform>>>();
            var settings = _clip.Header.Settings.Select(s => new Search(s)).ToArray();
            var sources = new Transform[names.Count]; var issues = new List<BindingIssue>();
            var recordedRoots = names.Where(n => !names.Any(parent => parent != n && Below(n, parent))).Distinct().OrderBy(Depth).ToArray();
            var manualRoots = new Dictionary<string, Dictionary<string, Transform[]>>();
            var changedRoots = new HashSet<string>();
            foreach (var entry in profile.Roots)
            {
                var roots = FindSource(entry.Value);
                if (roots.Length == 1 && profile.Hierarchies.TryGetValue(entry.Key, out string expected) && expected != HierarchySignature(roots[0]))
                { changedRoots.Add(entry.Key); continue; }
                manualRoots[entry.Key] = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)
                    .Select(t => new { Transform = t, Relative = Normalize(PathOf(t).Substring(PathOf(r).Length)) }))
                    .GroupBy(p => p.Relative).ToDictionary(g => g.Key, g => g.Select(p => p.Transform).ToArray());
            }
            for (int group = 0; group < settings.Length; group++)
            {
                var search = settings[group];
                var lookup = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
                foreach (var item in _paths)
                {
                    if (item.Key == null || !search.Includes(item.Value)) continue;
                    string key = search.Key(item.Value);
                    if (!lookup.TryGetValue(key, out var list)) lookup[key] = list = new List<Transform>();
                    list.Add(item.Key);
                }
                keys[group] = lookup;
            }
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i], root = recordedRoots.First(r => Below(name, r));
                int[] groups = Enumerable.Range(0, settings.Length).Where(g => settings[g].Includes(name)).ToArray();
                types[i] = groups.Length == 1 ? settings[groups[0]].Type : null;
                if (profile.OmittedRoots.Contains(root) && types[i] != "Saber") continue;
                Transform[] candidates;
                if (profile.Tracks.TryGetValue(name, out string trackOverride))
                    candidates = FindSource(trackOverride);
                else if (changedRoots.Contains(root))
                {
                    issues.Add(new BindingIssue { Root = root, RecordedPath = name, Message = "保存した対応先の階層が変わりました。ルートを選び直してください。" }); continue;
                }
                else if (manualRoots.TryGetValue(root, out var manual))
                    candidates = manual.TryGetValue(Normalize(name.Substring(root.Length)), out var manualMatches) ? manualMatches : new Transform[0];
                else if (groups.Length == 1)
                {
                    var exact = _paths.Where(p => p.Key != null && p.Value == name).Select(p => p.Key).ToArray();
                    if (exact.Length > 0) candidates = exact;
                    else candidates = keys[groups[0]].TryGetValue(settings[groups[0]].Key(name), out var matches) ? matches.ToArray() : new Transform[0];
                }
                else candidates = new Transform[0];
                if (candidates.Length == 1) sources[i] = candidates[0];
                else issues.Add(new BindingIssue { Root = root, RecordedPath = name,
                    Message = candidates.Length == 0 ? "対応するモデルが見つかりません。" : "対応するモデルが複数あります。" });
            }
            foreach (var collision in sources.Select((t, i) => new { t, i }).Where(x => x.t != null).GroupBy(x => x.t).Where(g => g.Count() > 1))
                foreach (var item in collision) issues.Add(new BindingIssue { RecordedPath = names[item.i], Root = recordedRoots.First(r => Below(names[item.i], r)), Message = "複数の記録が同じTransformに対応しています。" });
            // Saber tracks locate fixed anchors for the game's live sabers; they are not visual clones.
            var mapped = new HashSet<Transform>(sources.Where((t, i) => t != null && types[i] != "Saber"));
            var cloneRoots = mapped.Where(t => !Ancestors(t).Any(mapped.Contains)).ToArray();
            return new ModelBindingPlan { Sources = sources, Types = types, RecordedRoots = recordedRoots, CloneRoots = cloneRoots, Issues = issues };
        }
        private static IEnumerable<Transform> Ancestors(Transform t) { for (t = t.parent; t != null; t = t.parent) yield return t; }

        private sealed class Search
        {
            public string Type { get; }
            private readonly Regex[] _include, _exclude, _top;
            private readonly Regex _root;
            public Search(Setting setting)
            {
                Type = setting.type; _include = Patterns(setting.searchStirngs); _exclude = Patterns(setting.exclusionStrings); _top = Patterns(setting.topObjectStrings);
                _root = string.IsNullOrEmpty(setting.rescaleString) ? null : Patterns(new[] { setting.rescaleString })[0];
            }
            public bool Includes(string path) => _include.Any(p => p.IsMatch(path)) && !_exclude.Any(p => p.IsMatch(path));
            public string Key(string path)
            {
                if (_root?.IsMatch(path) == true) return "@root";
                for (int i = 0; i < _top.Length; i++)
                {
                    var match = _top[i].Match(path);
                    if (match.Success && match.Index == 0) return i + ":" + Normalize(path.Substring(match.Length));
                }
                return Normalize(path);
            }
        }
    }
}
