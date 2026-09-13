using System;
using System.Collections.Generic;
using System.Linq;
using MovementRecorder.Playback.Data;
using UnityEngine;

namespace MovementRecorder.Playback.Models
{
    // Only renderer data is copied. No prefab Instantiate: Awake/OnEnable from a model's scripts must never run.
    internal sealed class RenderModelClone : IDisposable
    {
        private readonly GameObject _root;
        private readonly Dictionary<Transform, Transform> _transforms = new Dictionary<Transform, Transform>();
        private readonly HashSet<Transform> _geometrySources = new HashSet<Transform>();
        private readonly Dictionary<Renderer, Renderer> _renderers = new Dictionary<Renderer, Renderer>();
        private readonly HashSet<Renderer> _skippedRenderers = new HashSet<Renderer>();
        private readonly List<string> _skippedDescriptions = new List<string>();
        private readonly Dictionary<Renderer, bool> _hiddenSources = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
        private readonly MovementClip _clip;
        private readonly Transform[] _tracks;
        private readonly int[] _order;
        private readonly Renderer[][] _affectedRenderers;
        private readonly bool _freezeMissing;
        private readonly Transform[] _liveSaberRoots;
        private bool _disposed;
        public IReadOnlyDictionary<Transform, Transform> Transforms => _transforms;
        public IReadOnlyList<string> SkippedRenderers => _skippedDescriptions;
        public int ModelRootCount { get; }

        public RenderModelClone(MovementClip clip, ModelBindingPlan plan, bool freezeMissing, Transform[] liveSaberRoots = null)
        {
            _clip = clip; _freezeMissing = freezeMissing;
            _liveSaberRoots = liveSaberRoots ?? new Transform[0];
            var cloneRoots = plan.CloneRoots.Where(t => !IsLiveSaber(t)).ToArray();
            ModelRootCount = cloneRoots.Length;
            _root = new GameObject(SceneModelResolver.ReplayRootName); _root.SetActive(false);
            try
            {
                foreach (var source in cloneRoots) CopyTransforms(source, CopyAncestor(source.parent));
                foreach (var source in _geometrySources) CopyRenderers(source, _transforms[source]);
                foreach (var source in cloneRoots)
                    if (_skippedRenderers.Any(r => r.transform.IsChildOf(source)) && !_renderers.Keys.Any(r => r.transform.IsChildOf(source)))
                        throw new InvalidOperationException("再生できるメッシュがありません。未対応の描画だけで構成されたモデルです: " + SceneModelResolver.PathOf(source));
                foreach (var pair in _renderers) Rebind(pair.Key, pair.Value);
                foreach (var source in _geometrySources) CopyLod(source, _transforms[source]);
                _tracks = plan.Sources.Select(s => s != null && _transforms.TryGetValue(s, out var clone) ? clone : null).ToArray();
                _order = Enumerable.Range(0, _tracks.Length).OrderBy(i => _tracks[i] == null ? 0 : Depth(_tracks[i])).ToArray();
                _affectedRenderers = _tracks.Select(t => t == null ? new Renderer[0] : t.GetComponentsInChildren<Renderer>(true)).ToArray();
                // Bone tracks often are siblings of their SkinnedMeshRenderer, not its ancestors.
                for (int i = 0; i < _tracks.Length; i++) if (_tracks[i] != null)
                    _affectedRenderers[i] = _affectedRenderers[i].Concat(_renderers.Values.OfType<SkinnedMeshRenderer>()
                        .Where(r => r.bones.Contains(_tracks[i]) || r.rootBone == _tracks[i])).Distinct().ToArray();
                foreach (var source in _renderers.Keys.Concat(_skippedRenderers))
                { _hiddenSources[source] = source.forceRenderingOff; source.forceRenderingOff = true; }
                Apply(clip.StartTime);
                _root.SetActive(true);
            }
            catch { Dispose(); throw; }
        }
        private bool IsLiveSaber(Transform source) => source != null && _liveSaberRoots.Any(root => root != null && source.IsChildOf(root));
        private static int Depth(Transform t) { int depth = 0; for (; t != null; t = t.parent) depth++; return depth; }
        private Transform CopyAncestor(Transform source)
        {
            if (source == null) return _root.transform;
            if (_transforms.TryGetValue(source, out var existing)) return existing;
            var parent = CopyAncestor(source.parent);
            var clone = new GameObject(source.name).transform; clone.SetParent(parent, false);
            clone.localPosition = source.localPosition; clone.localRotation = source.localRotation; clone.localScale = source.localScale;
            _transforms.Add(source, clone); return clone;
        }
        private void CopyTransforms(Transform source, Transform parent)
        {
            if (IsLiveSaber(source) || _transforms.ContainsKey(source)) return;
            var clone = new GameObject(source.name).transform; clone.SetParent(parent, false);
            clone.gameObject.layer = 0; // Visible to the normal HMD, including meshes hidden by first-person avatar layers.
            clone.localPosition = source.localPosition; clone.localRotation = source.localRotation; clone.localScale = source.localScale;
            clone.gameObject.SetActive(source.gameObject.activeSelf);
            _transforms.Add(source, clone);
            _geometrySources.Add(source);
            foreach (Transform child in source) CopyTransforms(child, clone);
        }
        private Material[] CopyMaterials(Material[] originals) => originals.Select(m =>
        {
            if (m == null) return null;
            if (!_materials.TryGetValue(m, out var copy)) _materials[m] = copy = new Material(m);
            return copy;
        }).ToArray();
        private void CopyRenderers(Transform source, Transform target)
        {
            foreach (var original in source.GetComponents<Renderer>())
            {
                Renderer copy;
                if (original is SkinnedMeshRenderer skin)
                {
                    var result = target.gameObject.AddComponent<SkinnedMeshRenderer>(); copy = result;
                    result.sharedMesh = skin.sharedMesh; result.localBounds = skin.localBounds; result.quality = skin.quality;
                    result.updateWhenOffscreen = true; result.skinnedMotionVectors = skin.skinnedMotionVectors;
                    if (skin.sharedMesh != null)
                        for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) result.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                }
                else if (original is MeshRenderer)
                {
                    var filter = source.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("MeshFilterがありません: " + SceneModelResolver.PathOf(source));
                    var targetFilter = target.GetComponent<MeshFilter>() ?? target.gameObject.AddComponent<MeshFilter>();
                    targetFilter.sharedMesh = filter.sharedMesh; copy = target.gameObject.AddComponent<MeshRenderer>();
                }
                else
                {
                    // Optional trails/particles must not prevent a usable mesh model from playing.
                    _skippedRenderers.Add(original);
                    _skippedDescriptions.Add(original.GetType().Name + " / " + SceneModelResolver.PathOf(source));
                    continue;
                }
                copy.sharedMaterials = CopyMaterials(original.sharedMaterials);
                copy.enabled = original.enabled; copy.forceRenderingOff = false; copy.shadowCastingMode = original.shadowCastingMode;
                copy.receiveShadows = original.receiveShadows; copy.lightProbeUsage = original.lightProbeUsage;
                copy.reflectionProbeUsage = original.reflectionProbeUsage; copy.motionVectorGenerationMode = original.motionVectorGenerationMode;
                copy.sortingLayerID = original.sortingLayerID; copy.sortingOrder = original.sortingOrder;
                var block = new MaterialPropertyBlock(); original.GetPropertyBlock(block); copy.SetPropertyBlock(block);
                for (int i = 0; i < original.sharedMaterials.Length; i++)
                { block.Clear(); original.GetPropertyBlock(block, i); if (!block.isEmpty) copy.SetPropertyBlock(block, i); }
                _renderers.Add(original, copy);
            }
        }
        private Transform RebindTransform(Transform original, string purpose)
        {
            if (original == null) return null;
            if (_transforms.TryGetValue(original, out var target)) return target;
            throw new InvalidOperationException(purpose + " がコピー範囲の外にあります。モデル全体のルートを指定してください: " + SceneModelResolver.PathOf(original));
        }
        private void Rebind(Renderer original, Renderer copy)
        {
            copy.probeAnchor = RebindTransform(original.probeAnchor, "Probe anchor");
            if (original is SkinnedMeshRenderer skin && copy is SkinnedMeshRenderer target)
            {
                target.bones = skin.bones.Select(b => RebindTransform(b, "Bone")).ToArray();
                target.rootBone = RebindTransform(skin.rootBone, "Root bone");
            }
        }
        private void CopyLod(Transform original, Transform copy)
        {
            var source = original.GetComponent<LODGroup>(); if (source == null) return;
            var target = copy.gameObject.AddComponent<LODGroup>();
            target.localReferencePoint = source.localReferencePoint; target.size = source.size;
            target.fadeMode = source.fadeMode; target.animateCrossFading = source.animateCrossFading;
            target.SetLODs(source.GetLODs().Select(lod => new LOD(lod.screenRelativeTransitionHeight,
                lod.renderers.Where(r => r == null || (!IsLiveSaber(r.transform) && !_skippedRenderers.Contains(r)))
                    .Select(r => r == null ? null : _renderers.TryGetValue(r, out var targetRenderer) ? targetRenderer :
                    throw new InvalidOperationException("LOD参照がモデルの外にあります。")).ToArray()) { fadeTransitionWidth = lod.fadeTransitionWidth }).ToArray());
            target.enabled = source.enabled;
        }
        public void Apply(float time)
        {
            foreach (var renderer in _renderers.Values) if (renderer != null) renderer.forceRenderingOff = false;
            foreach (int i in _order)
            {
                var target = _tracks[i]; if (target == null) continue;
                if (!_clip.TryEvaluate(i, time, out var pose, out bool active)) { foreach (var r in _affectedRenderers[i]) if (r != null) r.forceRenderingOff = true; continue; }
                target.SetPositionAndRotation(new Vector3(pose.X, pose.Y, pose.Z), new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw));
                var scale = _clip.Header.objectScales[i]; target.localScale = new Vector3(scale.x, scale.y, scale.z);
                if (!active && !_freezeMissing) foreach (var r in _affectedRenderers[i]) if (r != null) r.forceRenderingOff = true;
            }
        }
        public void KeepSourcesHidden()
        {
            foreach (var source in _hiddenSources.Keys)
            {
                if (source == null) throw new InvalidOperationException("再生元のモデルがシーンから消失しました。");
                source.forceRenderingOff = true;
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var pair in _hiddenSources) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            _hiddenSources.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            foreach (var material in _materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            _materials.Clear();
        }
    }
}
