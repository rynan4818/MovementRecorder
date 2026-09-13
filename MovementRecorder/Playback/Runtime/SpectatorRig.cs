using System;
using SiraUtil.Tools.FPFC;
using UnityEngine;
using UnityEngine.XR;
using Zenject;

namespace MovementRecorder.Playback.Runtime
{
    internal sealed class SpectatorRig : IDisposable
    {
        private readonly Camera _source, _view;
        private readonly GameObject _root;
        private readonly bool _cameraEnabled;
        private readonly SpectatorInput _input;
        private readonly ReplaySession _session;
        private readonly IVRPlatformHelper _platform;
        private readonly IFPFCSettings _fpfc;
        private readonly Transform _viewOrigin;
        private readonly Action<Vector3, Quaternion> _headPoseUpdated;
        private int _modelLayerMask;
        private bool _disposed;
        public Vector3 Offset => new Vector3(_session.ObserverX, _session.ObserverY, _session.ObserverZ);
        public Camera Camera => _view;
        public Transform SourceHead => _source == null ? null : _source.transform;

        public SpectatorRig(ReplaySession session, PlayerTransforms player, DiContainer container, Action<string> log = null,
            Action<Vector3, Quaternion> headPoseUpdated = null)
        {
            _session = session;
            _headPoseUpdated = headPoseUpdated;
            _platform = container.Resolve<IVRPlatformHelper>();
            _fpfc = container.TryResolve<IFPFCSettings>();
            // FPFC can replace the logical head with a transform that has no camera.
            var mainCamera = container.TryResolve<MainCamera>();
            _source = mainCamera == null ? null : mainCamera.camera;
            if (_source == null)
            {
                var head = GameAccess.Get<Transform>(player, "_headTransform");
                if (head != null) _source = head.GetComponent<Camera>() ?? head.GetComponentInChildren<Camera>(true);
            }
            if (_source == null) throw new InvalidOperationException("実HMDの描画カメラを取得できません。");
            _cameraEnabled = _source.enabled;
            _root = new GameObject(SceneModelResolverName); _root.SetActive(false);
            try
            {
                _viewOrigin = new GameObject("Spectator Camera Origin").transform; _viewOrigin.SetParent(_root.transform, false);
                _view = SpectatorCameraClone.Create(_source, _viewOrigin, log);
                UpdateCullingMask(); _view.enabled = true;
                // The original tracked head and AudioListener remain the game's logical head.
                _source.enabled = false;
                _input = new SpectatorInput(_viewOrigin, container);
                _root.SetActive(true); Update(); Application.onBeforeRender += BeforeRender;
            }
            catch { Dispose(); throw; }
        }
        private const string SceneModelResolverName = Models.SceneModelResolver.ReplayRootName;
        public void SetModelLayerMask(int mask) { _modelLayerMask = mask; UpdateCullingMask(); }
        private void UpdateCullingMask()
        {
            // Third-person viewing excludes the VRM first-person-only mesh to avoid drawing it twice.
            if (_view != null && _source != null) _view.cullingMask = (_source.cullingMask | _modelLayerMask) & ~(1 << 6);
        }
        public void SetControlsVisible(bool visible) { _input.SetVisible(visible); if (visible) Update(); }
        private void BeforeRender()
        {
            if (_view == null || _source == null) return;
            UpdateCullingMask();
            // Keep the game's logical head live even on runtimes that stop tracking a disabled camera.
            // The observer offset belongs to a separate parent, so XR's final camera pose cannot erase it.
            if (_fpfc?.Enabled != true && _platform.GetNodePose(XRNode.Head, 0, out var position, out var rotation))
            { _source.transform.localPosition = position; _source.transform.localRotation = rotation; }
            PublishHeadPose();
            var parent = _source.transform.parent;
            _viewOrigin.SetPositionAndRotation((parent == null ? Vector3.zero : parent.position) + Offset, parent == null ? Quaternion.identity : parent.rotation);
            _viewOrigin.localScale = parent == null ? Vector3.one : parent.lossyScale;
            _view.transform.localPosition = _source.transform.localPosition; _view.transform.localRotation = _source.transform.localRotation;
            if (_view.stereoTargetEye != _source.stereoTargetEye) _view.stereoTargetEye = _source.stereoTargetEye;
            // XR owns projection while rendering to the headset; setters warn on every frame in VR.
            if (_view.stereoTargetEye == StereoTargetEyeMask.None)
            {
                if (_view.fieldOfView != _source.fieldOfView) _view.fieldOfView = _source.fieldOfView;
                if (_view.aspect != _source.aspect) _view.aspect = _source.aspect;
            }
        }
        public void Update()
        {
            if (_source == null) throw new InvalidOperationException("HMDカメラが消失しました。");
            BeforeRender();
            _input?.Update(_view, _fpfc?.Enabled == true);
        }
        public void PublishHeadPose()
        {
            if (_disposed || _source == null || _headPoseUpdated == null) return;
            // The source's parent already applies the game's room center/rotation once.
            // Camera2's replay API consumes that pose directly. The observer offset belongs to _view only.
            _headPoseUpdated(_source.transform.position, _source.transform.rotation);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.onBeforeRender -= BeforeRender;
            if (_root != null) _root.SetActive(false);
            try { _input?.Dispose(); }
            finally
            {
                if (_source != null) _source.enabled = _cameraEnabled;
                if (_root != null) UnityEngine.Object.Destroy(_root);
            }
        }
    }
}
