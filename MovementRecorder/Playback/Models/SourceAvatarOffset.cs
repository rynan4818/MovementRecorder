using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MovementRecorder.Playback.Data;
using UnityEngine;

namespace MovementRecorder.Playback.Models
{
    // Translate only while a camera renders. Provider IK and physics see the original pose.
    internal sealed class SourceAvatarOffset : IDisposable
    {
        private readonly Boundary[] _boundaries;
        private readonly List<Camera> _cameras = new List<Camera>();
        private readonly Action<Exception> _failure;
        private readonly Dictionary<SkinnedMeshRenderer, bool> _offscreen = new Dictionary<SkinnedMeshRenderer, bool>();
        private SourceAvatarOffsetFrameGuard _guard;
        private Vector3 _offset;
        private int _frame;
        private bool _captured, _disposed;

        internal static bool HasOffset(Vector3 offset) => offset.x != 0 || offset.y != 0 || offset.z != 0;

        internal static SourceAvatarOffset Create(IEnumerable<Transform> geometry, HashSet<Transform> avatars,
            IEnumerable<Renderer> renderers, IEnumerable<Transform> protectedRoots, Vector3 offset, Action<Exception> failure)
        {
            var protectedSet = new HashSet<Transform>((protectedRoots ?? Enumerable.Empty<Transform>()).Where(t => t != null));
            var selected = new HashSet<Transform>(avatars);
            // A broad manual binding must never translate a real camera or its children.
            foreach (var transform in geometry)
                if (transform != null && transform.GetComponent<Camera>() != null) protectedSet.Add(transform);
            selected.RemoveWhere(t => t == null || AncestorsAndSelf(t).Any(protectedSet.Contains));
            var skins = renderers.OfType<SkinnedMeshRenderer>().Where(r => r != null && selected.Contains(r.transform)).ToArray();
            foreach (var skin in skins)
                foreach (var bone in skin.bones.Concat(new[] { skin.rootBone }))
                    if (bone != null && !selected.Contains(bone))
                        throw new InvalidOperationException("A source avatar bone is outside the offset hierarchy. Select the root of the entire model: " + SceneModelResolver.PathOf(bone));

            var boundaries = new Dictionary<Transform, bool>();
            foreach (var transform in selected)
            {
                if (transform.parent == null || !selected.Contains(transform.parent)) boundaries[transform] = true;
                // Compensate excluded branches even when their parent belongs to the avatar.
                foreach (Transform child in transform)
                    if (!selected.Contains(child)) boundaries[child] = false;
            }
            if (boundaries.Count == 0) return null;
            return new SourceAvatarOffset(boundaries.OrderBy(p => AncestorsAndSelf(p.Key).Count())
                .Select(p => new Boundary(p.Key, p.Value)).ToArray(), skins, offset, failure);
        }

        private static IEnumerable<Transform> AncestorsAndSelf(Transform transform)
        { for (; transform != null; transform = transform.parent) yield return transform; }

        private SourceAvatarOffset(Boundary[] boundaries, SkinnedMeshRenderer[] skins, Vector3 offset, Action<Exception> failure)
        {
            _boundaries = boundaries; _failure = failure; SetOffset(offset);
            GameObject guardObject = null;
            try
            {
                foreach (var skin in skins)
                {
                    _offscreen.Add(skin, skin.updateWhenOffscreen);
                    // The provider pose can be outside the view before onPreCull applies the translation.
                    skin.updateWhenOffscreen = true;
                }
                guardObject = new GameObject(SceneModelResolver.ReplayRootName); guardObject.SetActive(false);
                _guard = guardObject.AddComponent<SourceAvatarOffsetFrameGuard>(); _guard.Owner = this;
                Camera.onPreCull += BeforeCamera; Camera.onPostRender += AfterCamera;
                guardObject.SetActive(true);
            }
            catch { Dispose(); if (_guard == null && guardObject != null) UnityEngine.Object.Destroy(guardObject); throw; }
        }

        internal void SetOffset(Vector3 offset)
        {
            if (!Number.IsFinite(offset.x) || !Number.IsFinite(offset.y) || !Number.IsFinite(offset.z))
                throw new ArgumentException("The avatar offset is invalid.");
            // Nested cameras use the same captured pose. A changed value takes effect at the next outer camera.
            _offset = offset;
        }

        private void BeforeCamera(Camera camera)
        {
            if (_disposed || camera == null) return;
            try
            {
                if (_captured && _frame != Time.frameCount) Restore();
                if (!_captured)
                {
                    // Snapshot every boundary before moving any parent.
                    foreach (var boundary in _boundaries) boundary.Capture();
                    _captured = true; _frame = Time.frameCount;
                    foreach (var boundary in _boundaries) boundary.Apply(_offset);
                }
                _cameras.Add(camera);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private void AfterCamera(Camera camera)
        {
            if (_disposed || !_captured) return;
            // Pop unfinished nested renders as well when their outer camera finishes.
            int index = _cameras.LastIndexOf(camera);
            if (index < 0) return;
            _cameras.RemoveRange(index, _cameras.Count - index);
            if (_cameras.Count == 0) Restore();
        }

        internal void Restore()
        {
            if (!_captured) return;
            _captured = false; _cameras.Clear();
            Exception error = null;
            foreach (var boundary in _boundaries)
                try { boundary.Restore(); } catch (Exception exception) { error = error ?? exception; }
            if (error != null) Fail(error);
        }

        private void Fail(Exception exception)
        {
            if (_disposed) return;
            Dispose();
            // Do not interrupt other camera MODs' callbacks, even if reporting the error fails.
            try { _failure?.Invoke(exception); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Camera.onPreCull -= BeforeCamera; Camera.onPostRender -= AfterCamera;
            Restore();
            foreach (var pair in _offscreen) if (pair.Key != null) pair.Key.updateWhenOffscreen = pair.Value;
            _offscreen.Clear();
            if (_guard != null)
            {
                _guard.Owner = null; _guard.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_guard.gameObject); _guard = null;
            }
        }

        private sealed class Boundary
        {
            private readonly Transform _transform, _parent;
            private readonly bool _move;
            private Vector3 _worldPosition, _localPosition;
            public Boundary(Transform transform, bool move) { _transform = transform; _parent = transform.parent; _move = move; }
            public void Capture()
            {
                if (_transform == null) throw new InvalidOperationException("A source avatar object to offset is no longer available.");
                if (_transform.parent != _parent) throw new InvalidOperationException("The source avatar hierarchy has changed. Set up the model again.");
                _worldPosition = _transform.position; _localPosition = _transform.localPosition;
            }
            public void Apply(Vector3 offset) { _transform.position = _move ? _worldPosition + offset : _worldPosition; }
            public void Restore()
            {
                if (_transform == null) return;
                if (_transform.parent == _parent) _transform.localPosition = _localPosition;
                else _transform.position = _worldPosition;
            }
        }
    }

    // Exists only while offsetting a visible source avatar. Restore before provider Update/FixedUpdate,
    // and after rendering, if a camera threw before invoking its onPostRender callback.
    [DefaultExecutionOrder(-32000)]
    internal sealed class SourceAvatarOffsetFrameGuard : MonoBehaviour
    {
        internal SourceAvatarOffset Owner;
        private void FixedUpdate() { Owner?.Restore(); }
        private void Update() { Owner?.Restore(); }
        private IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (Owner != null) { yield return endOfFrame; Owner?.Restore(); }
        }
        private void OnDisable() { Owner?.Dispose(); }
        private void OnDestroy() { Owner?.Dispose(); }
    }
}
