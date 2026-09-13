using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using Xunit;

// Deliberately small native API doubles. Tests execute the product driver and spectator rig,
// but do not simulate Unity frame order, audio, real tracking or trail rendering.
namespace MovementRecorder.Tests
{
    [CollectionDefinition("Unity fixtures")]
    public sealed class UnityFixtureCollection { }
}

namespace UnityEngine
{
    public class Behaviour : Component { public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy; }
    public class MonoBehaviour : Behaviour { }
    public enum StereoTargetEyeMask { None, Left, Right, Both }
    public sealed class Camera : Behaviour
    {
        public int cullingMask;
        private float _fieldOfView = 90, _aspect = 1.6f;
        public int ProjectionWrites;
        public float fieldOfView { get => _fieldOfView; set { CheckProjectionWrite(); _fieldOfView = value; } }
        public float aspect { get => _aspect; set { CheckProjectionWrite(); _aspect = value; } }
        public StereoTargetEyeMask stereoTargetEye = StereoTargetEyeMask.Both;
        public void CopyFrom(Camera source)
        { cullingMask = source.cullingMask; _fieldOfView = source.fieldOfView; _aspect = source.aspect; stereoTargetEye = source.stereoTargetEye; }
        private void CheckProjectionWrite()
        { if (stereoTargetEye != StereoTargetEyeMask.None) throw new InvalidOperationException("Cannot set projection while VR is enabled"); ProjectionWrites++; }
    }
    public static class Application
    {
        public static event Action onBeforeRender;
        public static void BeforeRender() => onBeforeRender?.Invoke();
        public static void Clear() { onBeforeRender = null; }
    }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
}
namespace UnityEngine.EventSystems
{
    public sealed class EventSystem : Behaviour
    {
        private static EventSystem _current;
        public static EventSystem current
        {
            get => _current != null && Object.Objects.Contains(_current) && _current.isActiveAndEnabled ? _current : Object.Objects.OfType<EventSystem>().FirstOrDefault(e => e != null && e.isActiveAndEnabled);
            set { if (value == null || value.isActiveAndEnabled) _current = value; }
        }
        public int pixelDragThreshold = 5, ModuleRefreshes;
        public bool sendNavigationEvents = true;
        public void UpdateModules() { ModuleRefreshes++; }
    }
}
namespace UnityEngine.XR { public enum XRNode { Head, LeftHand, RightHand } }

public sealed class MainCamera : MonoBehaviour { public Camera camera => GetComponent<Camera>(); }
public sealed class PlayerTransforms : MonoBehaviour { public Transform _headTransform; }
public interface IVRPlatformHelper { bool GetNodePose(XRNode node, int index, out Vector3 position, out Quaternion rotation); }
public sealed class VRController : MonoBehaviour
{
    public XRNode node; public int nodeIdx; public object _transformOffset;
    public IVRPlatformHelper Platform;
    public int TrackingUpdates;
    public void Update()
    {
        TrackingUpdates++;
        Platform.GetNodePose(node, nodeIdx, out var position, out var rotation);
        transform.localPosition = position; transform.localRotation = rotation;
    }
}
public sealed class SaberManager { public Saber leftSaber, rightSaber; }
public sealed class Saber : MonoBehaviour
{
    public Transform _saberBladeTopTransform, _saberBladeBottomTransform;
    public string saberType;
    public SaberMovementData movementData = new SaberMovementData();
    public void OverridePositionAndRotation(Vector3 position, Quaternion rotation) => transform.SetPositionAndRotation(position, rotation);
}
public static class TimeHelper { public static float time; }
public struct BladeMovementDataElement { public Vector3 topPos, bottomPos; public float time; }
public interface ISaberMovementDataProcessor { }
public sealed class SaberSwingRatingCounter : ISaberMovementDataProcessor { public float _cutTime; }
public sealed class LazyCopyHashSet<T> { public readonly List<T> items = new List<T>(); }
public sealed class SaberMovementData
{
    public BladeMovementDataElement[] _data = new BladeMovementDataElement[64];
    public int _nextAddIndex, _validCount;
    public float _bladeSpeed;
    public LazyCopyHashSet<ISaberMovementDataProcessor> _dataProcessors = new LazyCopyHashSet<ISaberMovementDataProcessor>();
    public int AddCalls;
    public void AddNewData(Vector3 top, Vector3 bottom, float time)
    {
        _data[_nextAddIndex] = new BladeMovementDataElement { topPos = top, bottomPos = bottom, time = time };
        _nextAddIndex = (_nextAddIndex + 1) % _data.Length;
        _validCount = Math.Min(_validCount + 1, _data.Length); AddCalls++;
    }
}
public class SaberTrail : MonoBehaviour
{
    public int ResetCalls;
    public virtual void ResetTrailData() { ResetCalls++; throw new NullReferenceException("No native movementData in this trail"); }
}
// Represents any provider that inherits SaberTrail without using native movementData.
public sealed class TransformSampledTrail : SaberTrail { }

namespace VRUIControls
{
    public sealed class VRPointer : MonoBehaviour
    {
        public VRController _leftVRController, _rightVRController, _vrController;
        public object _laserPointerPrefab, _cursorPrefab;
        public float _defaultLaserPointerLength = 10, _laserPointerWidth = .01f;
        public VRController vrController => _vrController;
        public int LaserResets;
        public void DestroyLaserAndHit() { LaserResets++; }
    }
    public sealed class VRInputModule : MonoBehaviour
    {
        public VRPointer _vrPointer;
        public object _rumblePreset;
        public bool useMouseForPressInput;
        public int Clears;
        public void ClearSelection() { Clears++; }
    }
}
namespace SiraUtil.Tools.FPFC { public interface IFPFCSettings { bool Enabled { get; } } }
namespace Zenject
{
    public sealed class DiContainer
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        public void Bind<T>(T instance) { _instances.Add(typeof(T), instance); }
        public T Resolve<T>() => (T)_instances[typeof(T)];
        public T TryResolve<T>() where T : class => _instances.TryGetValue(typeof(T), out var instance) ? (T)instance : null;
        public void Inject(object instance) { if (instance is VRController controller) controller.Platform = Resolve<IVRPlatformHelper>(); }
        public void InjectGameObject(GameObject gameObject)
        { foreach (var controller in gameObject.GetComponentsInChildren<VRController>(true)) Inject(controller); }
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var result = type.GetField(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (result != null) return result;
            }
            return null;
        }
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
    }
}
namespace MovementRecorder.Playback
{
    internal sealed class ReplaySession { public float ObserverX, ObserverY, ObserverZ = -2; }
}
