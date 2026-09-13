using System;
using System.Reflection;
using MovementRecorder.Playback.Data;
using UnityEngine;
using Zenject;

namespace MovementRecorder.Playback.Compatibility
{
    // Camera2 types appear only as reflection names, never as assembly references.
    internal sealed class Camera2ReplayInterop : IInitializable, IDisposable
    {
        private readonly Func<Assembly> _findAssembly;
        private readonly Func<Transform> _initialHead;
        private readonly Action<string> _warn, _info;
        private Source _source;
        private Guid _sessionId;
        private bool _initialized, _disposed, _failed;

        [Inject]
        public Camera2ReplayInterop() : this(
            () => IPA.Loader.PluginManager.GetPluginFromId("Camera2")?.Assembly,
            () => Camera.main == null ? null : Camera.main.transform,
            message => Plugin.Log?.Warn(message), message => Plugin.Log?.Info(message)) { }

        internal Camera2ReplayInterop(Func<Assembly> findAssembly, Func<Transform> initialHead,
            Action<string> warn, Action<string> info = null)
        { _findAssembly = findAssembly; _initialHead = initialHead; _warn = warn; _info = info; }

        public void Initialize()
        {
            if (_initialized || _disposed) return;
            _initialized = true;
            MovementReplay.SessionChanged += OnSessionChanged;
            if (MovementReplay.IsActive) OnSessionChanged(MovementReplay.SessionId, true);
        }

        private void OnSessionChanged(Guid id, bool active)
        {
            if (_disposed) return;
            if (!active)
            {
                if (id == _sessionId) EndSession();
                return;
            }
            if (_failed || id == _sessionId) return;
            EndSession();
            if (_failed) return;
            _sessionId = id;
            try
            {
                var assembly = _findAssembly();
                if (assembly == null) return; // CameraPlus and camera-MOD-free installs need no adapter.
                _source = new Source(assembly);
                var head = _initialHead();
                if (head == null) throw new InvalidOperationException("初期HMD姿勢を取得できません。");
                var position = head.position;
                var rotation = head.rotation;
                if (!NormalizePose(position, ref rotation)) throw new InvalidOperationException("初期HMD姿勢が不正です。");
                _source.Update(ref position, ref rotation);
                _source.Register();
                _source.SetActive(true);
                Log(_info, "Camera2 replay source registered: MovementRecorderReplay");
            }
            catch (Exception ex) { Disable(ex); }
        }

        public void UpdatePose(Guid sessionId, Vector3 position, Quaternion rotation)
        {
            if (_disposed || _source == null || _failed || sessionId != _sessionId || !NormalizePose(position, ref rotation)) return;
            try { _source.Update(ref position, ref rotation); }
            catch (Exception ex) { Disable(ex); }
        }

        private static bool NormalizePose(Vector3 position, ref Quaternion rotation)
        {
            var pose = new RecordedPose { X = position.x, Y = position.y, Z = position.z,
                Qx = rotation.x, Qy = rotation.y, Qz = rotation.z, Qw = rotation.w };
            if (!pose.IsFinite || !pose.NormalizeRotation()) return false;
            rotation = new Quaternion(pose.Qx, pose.Qy, pose.Qz, pose.Qw);
            return true;
        }

        private void EndSession()
        {
            var source = _source;
            _source = null; _sessionId = Guid.Empty;
            if (source == null) return;
            // Attempt both operations even if one fails, including a Register that threw after adding.
            try { source.SetActive(false); } catch (Exception ex) { WarnOnce(ex); }
            try { source.Unregister(); } catch (Exception ex) { WarnOnce(ex); }
            Log(_info, "Camera2 replay source released: MovementRecorderReplay");
        }

        private void Disable(Exception exception) { WarnOnce(exception); EndSession(); }
        private void WarnOnce(Exception exception)
        {
            if (_failed) return;
            _failed = true;
            while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
            Log(_warn, "Camera2のリプレイ連携を無効にしました。リプレイは続行できます: " + exception.Message);
        }
        private static void Log(Action<string> logger, string message) { try { logger?.Invoke(message); } catch { } }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_initialized) MovementReplay.SessionChanged -= OnSessionChanged;
            EndSession();
        }

        private sealed class Source
        {
            internal delegate void PoseUpdater(ref Vector3 position, ref Quaternion rotation);
            private readonly object _instance;
            private readonly MethodInfo _register, _unregister;
            private readonly object[] _args;
            private bool _registrationAttempted;
            public readonly Action<bool> SetActive;
            public readonly PoseUpdater Update;

            public Source(Assembly assembly)
            {
                var sdk = assembly.GetType("Camera2.SDK.ReplaySources", false);
                var contract = sdk?.GetNestedType("ISource", BindingFlags.Public);
                var generic = sdk?.GetNestedType("GenericSource", BindingFlags.Public);
                if (sdk == null || contract == null || !contract.IsInterface || generic == null || !contract.IsAssignableFrom(generic))
                    throw new NotSupportedException("Camera2.SDK.ReplaySourcesの公開APIがありません。");
                var constructor = generic.GetConstructor(new[] { typeof(string) });
                if (constructor == null) throw new MissingMethodException(generic.FullName, ".ctor(string)");
                _register = RequireMethod(sdk, "Register", true, contract);
                _unregister = RequireMethod(sdk, "Unregister", true, contract);
                var activate = RequireMethod(generic, "SetActive", false, typeof(bool));
                var update = RequireMethod(generic, "Update", false, typeof(Vector3).MakeByRefType(), typeof(Quaternion).MakeByRefType());
                _instance = constructor.Invoke(new object[] { "MovementRecorderReplay" });
                _args = new[] { _instance };
                SetActive = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), _instance, activate);
                Update = (PoseUpdater)Delegate.CreateDelegate(typeof(PoseUpdater), _instance, update);
            }

            private static MethodInfo RequireMethod(Type type, string name, bool isStatic, params Type[] parameters)
            {
                var method = type.GetMethod(name, BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance),
                    null, parameters, null);
                if (method == null || method.ReturnType != typeof(void) || method.ContainsGenericParameters)
                    throw new MissingMethodException(type.FullName, name);
                var actual = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                    if (actual[i].ParameterType != parameters[i] || actual[i].IsOut)
                        throw new MissingMethodException(type.FullName, name + " signature");
                return method;
            }

            public void Register() { _registrationAttempted = true; _register.Invoke(null, _args); }
            public void Unregister() { if (_registrationAttempted) _unregister.Invoke(null, _args); }
        }
    }
}
