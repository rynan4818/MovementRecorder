using System;
using UnityEngine;
using UnityEngine.EventSystems;
using VRUIControls;
using Zenject;

namespace MovementRecorder.Playback.Runtime
{
    // The native pause animation does not run for our panel. Own its input lifecycle explicitly.
    internal sealed class SpectatorInput : IDisposable
    {
        private readonly GameObject _root;
        private readonly EventSystem _baseEvents, _events;
        private readonly VRInputModule _baseModule, _module;
        private readonly VRPointer _basePointer, _pointer;
        private readonly VRController _left, _right;
        private EventSystem _previousEvents;
        private bool _baseEnabled, _baseModuleEnabled, _basePointerEnabled, _disposed;
        public bool Visible { get; private set; }

        public SpectatorInput(Transform trackingOrigin, DiContainer container)
        {
            var source = container.Resolve<VRInputModule>();
            var sourcePointer = GameAccess.Get<VRPointer>(source, "_vrPointer");
            _baseModule = source; _basePointer = sourcePointer;
            _baseEvents = source.GetComponent<EventSystem>();
            if (sourcePointer == null || _baseEvents == null) throw new InvalidOperationException("Cannot access the game's menu input.");
            _root = new GameObject("Spectator Menu Input"); _root.SetActive(false);
            _root.transform.SetParent(trackingOrigin, false);
            try
            {
                _left = CopyController(GameAccess.Get<VRController>(sourcePointer, "_leftVRController"), container);
                _right = CopyController(GameAccess.Get<VRController>(sourcePointer, "_rightVRController"), container);
                var eventObject = new GameObject("Spectator EventSystem"); eventObject.transform.SetParent(_root.transform, false);
                _events = eventObject.AddComponent<EventSystem>();
                _events.pixelDragThreshold = _baseEvents.pixelDragThreshold;
                _events.sendNavigationEvents = _baseEvents.sendNavigationEvents;
                _pointer = eventObject.AddComponent<VRPointer>();
                foreach (string field in new[] { "_laserPointerPrefab", "_cursorPrefab", "_defaultLaserPointerLength", "_laserPointerWidth" })
                    GameAccess.Set(_pointer, field, GameAccess.Get<object>(sourcePointer, field));
                GameAccess.Set(_pointer, "_leftVRController", _left); GameAccess.Set(_pointer, "_rightVRController", _right);
                GameAccess.Set(_pointer, "_vrController", _right);
                _module = eventObject.AddComponent<VRInputModule>();
                GameAccess.Set(_module, "_vrPointer", _pointer);
                GameAccess.Set(_module, "_rumblePreset", GameAccess.Get<object>(source, "_rumblePreset"));
                container.Inject(_module);
                // Activation is deferred until controllers, visuals, prefabs and DI are ready.
            }
            catch { Dispose(); throw; }
        }
        private VRController CopyController(VRController original, DiContainer container)
        {
            if (original == null || original.GetComponentsInChildren<Saber>(true).Length != 0)
                throw new InvalidOperationException("A menu controller without a saber is required.");
            var copy = UnityEngine.Object.Instantiate(original, _root.transform, false);
            copy.name = "Spectator Menu " + original.node;
            copy.gameObject.SetActive(true); copy.enabled = true;
            container.InjectGameObject(copy.gameObject);
            return copy;
        }
        public void SetVisible(bool visible)
        {
            if (_disposed || Visible == visible) return;
            Visible = visible;
            if (visible)
            {
                _previousEvents = EventSystem.current;
                _baseEnabled = _baseEvents.enabled;
                _baseModuleEnabled = _baseModule.enabled; _basePointerEnabled = _basePointer.enabled;
                // VRPointer has static hand-switch state. The original must not update alongside ours.
                _baseModule.enabled = false; _basePointer.enabled = false; _baseEvents.enabled = false;
                _root.SetActive(true); _events.enabled = true; _module.enabled = true; _pointer.enabled = true;
                EventSystem.current = _events;
                _events.UpdateModules();
            }
            else
            {
                _module.ClearSelection(); _pointer.DestroyLaserAndHit();
                _root.SetActive(false);
                if (_baseEvents != null) _baseEvents.enabled = _baseEnabled;
                if (_baseModule != null) _baseModule.enabled = _baseModuleEnabled;
                if (_basePointer != null) _basePointer.enabled = _basePointerEnabled;
                if (_previousEvents != null && _previousEvents.isActiveAndEnabled) EventSystem.current = _previousEvents;
                _previousEvents = null;
            }
        }
        public void Update(Camera view, bool fpfc)
        {
            if (!Visible) return;
            _module.useMouseForPressInput = fpfc;
            _left.enabled = _right.enabled = !fpfc;
            if (fpfc)
            {
                _left.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
                _right.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
            }
            else
            {
                // PlaybackRuntime runs before EventSystem.Update. Give raycasts this frame's hand pose.
                _left.Update(); _right.Update();
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            SetVisible(false); _disposed = true;
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }
}
