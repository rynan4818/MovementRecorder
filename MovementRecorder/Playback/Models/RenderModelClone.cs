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
        private readonly HashSet<Renderer> _sourceRenderers = new HashSet<Renderer>();
        private readonly bool _showSourceAvatar;
        private HashSet<Transform> _avatarSources;
        private SourceAvatarOffset _sourceOffset;
        private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
        private readonly List<BlendShapeBinding> _blendShapes = new List<BlendShapeBinding>();
        private readonly Dictionary<Animator, AnimatorCullingMode> _animatorCullingModes = new Dictionary<Animator, AnimatorCullingMode>();
        private readonly Action<string> _warning;
        private readonly MovementClip _clip;
        private readonly Transform[] _tracks;
        private readonly int[] _order;
        private readonly Renderer[][] _affectedRenderers;
        private readonly bool _freezeMissing;
        private readonly Transform[] _liveSaberRoots;
        private bool _disposed;
        private bool _liveExpressionsStopped;
        public IReadOnlyDictionary<Transform, Transform> Transforms => _transforms;
        public IReadOnlyList<string> SkippedRenderers => _skippedDescriptions;
        public int ModelRootCount { get; }

        public RenderModelClone(MovementClip clip, ModelBindingPlan plan, bool freezeMissing, Transform[] liveSaberRoots = null, Action<string> warning = null,
            bool showSourceAvatar = false)
        {
            _clip = clip; _freezeMissing = freezeMissing;
            _warning = warning;
            _showSourceAvatar = showSourceAvatar;
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
                        throw new InvalidOperationException("No replayable mesh found. The model contains only unsupported renderers: " + SceneModelResolver.PathOf(source));
                foreach (var pair in _renderers) Rebind(pair.Key, pair.Value);
                foreach (var source in _geometrySources) CopyLod(source, _transforms[source]);
                _tracks = plan.Sources.Select(s => s != null && _transforms.TryGetValue(s, out var clone) ? clone : null).ToArray();
                _order = Enumerable.Range(0, _tracks.Length).OrderBy(i => _tracks[i] == null ? 0 : Depth(_tracks[i])).ToArray();
                _affectedRenderers = _tracks.Select(t => t == null ? new Renderer[0] : t.GetComponentsInChildren<Renderer>(true)).ToArray();
                // Bone tracks often are siblings of their SkinnedMeshRenderer, not its ancestors.
                for (int i = 0; i < _tracks.Length; i++) if (_tracks[i] != null)
                    _affectedRenderers[i] = _affectedRenderers[i].Concat(_renderers.Values.OfType<SkinnedMeshRenderer>()
                        .Where(r => r.bones.Contains(_tracks[i]) || r.rootBone == _tracks[i])).Distinct().ToArray();
                KeepSourceAnimatorsUpdating();
                var visibleSources = showSourceAvatar ? plan.FindAvatarTransforms(_geometrySources) : new HashSet<Transform>();
                _avatarSources = visibleSources;
                foreach (var source in _renderers.Keys.Concat(_skippedRenderers))
                {
                    _sourceRenderers.Add(source);
                    if (visibleSources.Contains(source.transform)) continue;
                    _hiddenSources[source] = source.forceRenderingOff; source.forceRenderingOff = true;
                }
                Apply(clip.StartTime);
                SyncLiveExpressions(true);
                _root.SetActive(true);
            }
            catch { Dispose(); throw; }
        }
        private bool IsLiveSaber(Transform source) => source != null && _liveSaberRoots.Any(root => root != null && source.IsChildOf(root));
        public void SetSourceAvatarOffset(bool enabled, Vector3 offset, Transform[] protectedRoots, Action<Exception> failure)
        {
            // Gate before enumeration, helper allocation, camera subscription or frame-guard creation.
            if (_disposed || !_showSourceAvatar || !enabled || !SourceAvatarOffset.HasOffset(offset))
            { StopSourceAvatarOffset(); return; }
            if (_sourceOffset != null) _sourceOffset.SetOffset(offset);
            else _sourceOffset = SourceAvatarOffset.Create(_geometrySources, _avatarSources, _sourceRenderers,
                _liveSaberRoots.Concat(protectedRoots ?? new Transform[0]), offset, failure);
        }
        public void StopSourceAvatarOffset() { _sourceOffset?.Dispose(); _sourceOffset = null; }
        private static int Depth(Transform t) { int depth = 0; for (; t != null; t = t.parent) depth++; return depth; }
        private Transform CopyAncestor(Transform source)
        {
            if (source == null) return _root.transform;
            if (_transforms.TryGetValue(source, out var existing)) return existing;
            var parent = CopyAncestor(source.parent);
            var clone = new GameObject(source.name).transform; clone.SetParent(parent, false);
            clone.gameObject.layer = source.gameObject.layer;
            clone.localPosition = source.localPosition; clone.localRotation = source.localRotation; clone.localScale = source.localScale;
            _transforms.Add(source, clone); return clone;
        }
        private void CopyTransforms(Transform source, Transform parent)
        {
            if (IsLiveSaber(source) || _transforms.ContainsKey(source)) return;
            var clone = new GameObject(source.name).transform; clone.SetParent(parent, false);
            clone.gameObject.layer = source.gameObject.layer;
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
                    if (skin.sharedMesh != null && skin.sharedMesh.blendShapeCount > 0)
                        _blendShapes.Add(new BlendShapeBinding(skin, result));
                }
                else if (original is MeshRenderer)
                {
                    var filter = source.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) throw new InvalidOperationException("Missing MeshFilter: " + SceneModelResolver.PathOf(source));
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
            throw new InvalidOperationException(purpose + " is outside the cloned hierarchy. Select the root of the entire model: " + SceneModelResolver.PathOf(original));
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
                    throw new InvalidOperationException("An LOD reference is outside the model.")).ToArray()) { fadeTransitionWidth = lod.fadeTransitionWidth }).ToArray());
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
        public int SyncLiveRendererLayers()
        {
            int mask = 0;
            if (_disposed) return mask;
            foreach (var pair in _renderers)
            {
                if (pair.Key == null || pair.Value == null) continue;
                int layer = pair.Key.gameObject.layer;
                if (pair.Value.gameObject.layer != layer) pair.Value.gameObject.layer = layer;
                mask |= 1 << layer;
            }
            return mask;
        }
        public void KeepSourcesHidden()
        {
            foreach (var source in _sourceRenderers)
            {
                if (source == null) throw new InvalidOperationException("The source model is no longer in the scene.");
                if (_hiddenSources.ContainsKey(source)) source.forceRenderingOff = true;
            }
        }
        // Read the final renderer output, whether it was produced by Animator, VRM or another provider.
        // No provider scripts or animation state are copied, and Apply(time) stays independent of live expressions.
        public void SyncLiveExpressions(bool isPlaying)
        {
            if (_disposed || _liveExpressionsStopped || !isPlaying) return;
            foreach (var binding in _blendShapes)
            {
                if (binding.Disabled) continue;
                if (binding.Source == null || binding.Target == null || binding.Mesh == null ||
                    binding.Source.sharedMesh != binding.Mesh || binding.Target.sharedMesh != binding.Mesh ||
                    binding.Mesh.blendShapeCount != binding.Weights.Length)
                {
                    binding.Disabled = true;
                    Warn("表情の同期を停止しました。メッシュまたはRendererの対応が変わりました: " + binding.Path);
                    continue;
                }
                for (int i = 0; i < binding.Weights.Length; i++)
                {
                    float weight = binding.Source.GetBlendShapeWeight(i);
                    if (!Number.IsFinite(weight))
                    {
                        if (!binding.InvalidWeightReported)
                        {
                            binding.InvalidWeightReported = true;
                            Warn("非有限のBlendShape値を省略しました: " + binding.Path);
                        }
                        continue;
                    }
                    if (binding.Weights[i] == weight) continue;
                    binding.Target.SetBlendShapeWeight(i, weight);
                    binding.Weights[i] = weight;
                }
            }
        }
        private void KeepSourceAnimatorsUpdating()
        {
            foreach (var binding in _blendShapes)
                for (var source = binding.Source.transform; source != null; source = source.parent)
                    foreach (var animator in source.GetComponents<Animator>())
                    {
                        if (_animatorCullingModes.ContainsKey(animator) || animator.cullingMode == AnimatorCullingMode.AlwaysAnimate) continue;
                        _animatorCullingModes.Add(animator, animator.cullingMode);
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    }
        }
        public void StopLiveExpressions()
        {
            _liveExpressionsStopped = true;
            foreach (var pair in _animatorCullingModes)
            {
                if (pair.Key == null) continue;
                try { pair.Key.cullingMode = pair.Value; }
                catch (Exception ex) { Warn("Animatorの更新設定を復元できませんでした: " + ex.Message); }
            }
            _animatorCullingModes.Clear();
        }
        private void Warn(string message)
        {
            // A diagnostic callback must not break expression updates or restoration of the other animators.
            try { _warning?.Invoke(message); } catch { }
        }
        private sealed class BlendShapeBinding
        {
            public readonly SkinnedMeshRenderer Source, Target;
            public readonly Mesh Mesh;
            public readonly float[] Weights;
            public readonly string Path;
            public bool Disabled, InvalidWeightReported;
            public BlendShapeBinding(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
            {
                Source = source; Target = target; Mesh = source.sharedMesh;
                Path = SceneModelResolver.PathOf(source.transform);
                Weights = new float[Mesh.blendShapeCount];
                for (int i = 0; i < Weights.Length; i++) Weights[i] = float.NaN;
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            StopSourceAvatarOffset();
            StopLiveExpressions();
            foreach (var pair in _hiddenSources) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            _hiddenSources.Clear();
            _sourceRenderers.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            foreach (var material in _materials.Values) if (material != null) UnityEngine.Object.Destroy(material);
            _materials.Clear();
            _blendShapes.Clear();
        }
    }
}
