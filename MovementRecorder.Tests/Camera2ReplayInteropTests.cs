using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MovementRecorder.Playback;
using MovementRecorder.Playback.Compatibility;
using UnityEngine;
using Xunit;
using FakeSdk = Camera2.SDK.ReplaySources;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class Camera2ReplayInteropTests : IDisposable
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly ReplaySession _session = new ReplaySession();
        private readonly Transform _head = new GameObject("Live head").transform;
        private readonly Camera2ReplayInterop _interop;

        public Camera2ReplayInteropTests()
        {
            FakeSdk.Reset();
            _head.position = new Vector3(.25f, 1.7f, -.3f);
            _interop = Create(() => typeof(FakeSdk).Assembly);
            _interop.Initialize();
        }
        private Camera2ReplayInterop Create(Func<Assembly> assembly, Func<Transform> initialHead = null) =>
            new Camera2ReplayInterop(assembly, initialHead ?? (() => _head), _warnings.Add);
        public void Dispose()
        {
            _interop.Dispose(); _session.Finish(); FakeSdk.Reset();
            UnityEngine.Object.Objects.Clear(); Application.Clear(); IPA.Loader.PluginManager.Find = null;
        }
        private static void SamePose(Vector3 position, Quaternion rotation, FakeSdk.ISource source)
        {
            Assert.True(Vector3.Distance(position, source.localHeadPosition) < .0001f);
            Assert.True(Quaternion.Angle(rotation, source.localHeadRotation) < .01f);
        }

        [Fact]
        public void RegistersBeforeTransitionWithValidPoseAndOnlyWhileSessionIsActive()
        {
            Assert.Empty(FakeSdk.Sources);
            _session.Begin(null);
            var source = Assert.Single(FakeSdk.Sources);
            Assert.Equal("MovementRecorderReplay", source.name);
            Assert.True(source.isInReplay);
            Assert.Equal(ReplayPhase.Starting, _session.Phase);
            SamePose(_head.position, _head.rotation, source);
            Assert.True(FakeSdk.PoseWasSetBeforeRegister);
            _session.Finish();
            Assert.False(source.isInReplay); Assert.Empty(FakeSdk.Sources);
            Assert.Equal(1, FakeSdk.UnregisterCalls); Assert.Empty(_warnings);
        }

        [Fact]
        public void PauseSeekCompletionAndRuntimeTeardownKeepReplayClassificationUntilFinish()
        {
            _session.Begin(null);
            var source = Assert.Single(FakeSdk.Sources);
            foreach (var phase in new[] { ReplayPhase.Binding, ReplayPhase.Playing, ReplayPhase.Paused,
                ReplayPhase.Seeking, ReplayPhase.Completed, ReplayPhase.Disposing })
            {
                _session.SetPhase(phase);
                _interop.UpdatePose(_session.Id, new Vector3((int)phase, 2, 3), Quaternion.identity);
                Assert.True(source.isInReplay);
                Assert.Equal((float)phase, source.localHeadPosition.x);
            }
            _session.RuntimeDestroyed(42);
            Assert.True(source.isInReplay);
            _session.Finish();
            Assert.Empty(FakeSdk.Sources);
        }

        [Fact]
        public void ReinitializationAndRepeatedNotificationsDoNotDuplicateRegistration()
        {
            _interop.Initialize(); _session.Begin(null);
            MovementReplay.NotifySession(_session.Id, true);
            Assert.Single(FakeSdk.Sources); Assert.Equal(1, FakeSdk.RegisterCalls);
            _session.Finish(); _session.Finish();
            Assert.Equal(1, FakeSdk.UnregisterCalls);
        }

        [Fact]
        public void ConsecutiveReplaysIgnoreStalePoseAndFinishNotifications()
        {
            _session.Begin(null); var firstId = _session.Id; var first = Assert.Single(FakeSdk.Sources);
            _session.Finish(); _session.Begin(null); var second = Assert.Single(FakeSdk.Sources);
            Assert.NotSame(first, second); Assert.False(first.isInReplay);
            _interop.UpdatePose(firstId, new Vector3(100, 100, 100), Quaternion.identity);
            MovementReplay.NotifySession(firstId, false);
            SamePose(_head.position, _head.rotation, second); Assert.True(second.isInReplay);
            Assert.Equal(2, FakeSdk.RegisterCalls);
        }

        [Fact]
        public void StartupFailureFinishReleasesOurSourceAndPreservesOtherMods()
        {
            var external = new FakeSdk.GenericSource("Another replay"); external.SetActive(true); FakeSdk.Register(external);
            _session.Begin(null); Assert.Equal(2, FakeSdk.Sources.Count);
            _session.Finish();
            Assert.Same(external, Assert.Single(FakeSdk.Sources)); Assert.True(external.isInReplay);
        }

        [Fact]
        public void DisposeUnsubscribesAndReleasesAnActiveReplay()
        {
            _session.Begin(null); var source = Assert.Single(FakeSdk.Sources);
            _interop.Dispose(); _interop.Dispose();
            Assert.Empty(FakeSdk.Sources); Assert.False(source.isInReplay);
            _session.Finish(); _session.Begin(null);
            Assert.Empty(FakeSdk.Sources); Assert.Equal(1, FakeSdk.RegisterCalls);
        }

        [Fact]
        public void Camera2AbsentDoesNotReadHeadOrPreventSessionAndCanBeFoundOnNextStart()
        {
            _interop.Dispose(); bool present = false; int headReads = 0;
            using var interop = Create(() => present ? typeof(FakeSdk).Assembly : null, () => { headReads++; return _head; });
            interop.Initialize(); _session.Begin(null);
            interop.UpdatePose(_session.Id, Vector3.zero, Quaternion.identity);
            Assert.True(_session.IsActive); Assert.Empty(FakeSdk.Sources); Assert.Equal(0, headReads); Assert.Empty(_warnings);
            _session.Finish(); present = true; _session.Begin(null);
            Assert.True(Assert.Single(FakeSdk.Sources).isInReplay);
        }

        [Fact]
        public void ProductionConstructorUsesLoadedPluginLookupAndWorksWithCameraPlusOnly()
        {
            _interop.Dispose();
            var requests = new List<string>();
            IPA.Loader.PluginManager.Find = name => { requests.Add(name); return name == "CameraPlus" ? new IPA.Loader.PluginMetadata() : null; };
            using var interop = new Camera2ReplayInterop();
            interop.Initialize(); _session.Begin(null);
            Assert.Equal(new[] { "Camera2" }, requests); Assert.True(_session.IsActive); Assert.Empty(FakeSdk.Sources);
        }

        [Fact]
        public void MissingApiIsOptionalAndWarnsOnce()
        {
            _interop.Dispose();
            using var interop = Create(() => typeof(string).Assembly);
            interop.Initialize(); _session.Begin(null); _session.Finish(); _session.Begin(null);
            Assert.True(_session.IsActive); Assert.Empty(FakeSdk.Sources); Assert.Single(_warnings);
        }

        [Fact]
        public void IncompatiblePublicMethodIsRejectedBeforeAnyRegistration()
        {
            _interop.Dispose();
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OldCamera2"), AssemblyBuilderAccess.Run);
            var sdk = assembly.DefineDynamicModule("SDK").DefineType("Camera2.SDK.ReplaySources", TypeAttributes.Public);
            var contract = sdk.DefineNestedType("ISource", TypeAttributes.NestedPublic | TypeAttributes.Interface | TypeAttributes.Abstract);
            var source = sdk.DefineNestedType("GenericSource", TypeAttributes.NestedPublic);
            source.AddInterfaceImplementation(contract);
            var constructor = source.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(string) });
            var il = constructor.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)); il.Emit(OpCodes.Ret);
            // A name match is insufficient: this API takes object instead of the ISource interface.
            var register = sdk.DefineMethod("Register", MethodAttributes.Public | MethodAttributes.Static, typeof(void), new[] { typeof(object) });
            register.GetILGenerator().Emit(OpCodes.Ret);
            sdk.CreateType(); contract.CreateType(); source.CreateType();
            using var interop = Create(() => assembly);
            interop.Initialize(); _session.Begin(null);
            Assert.True(_session.IsActive); Assert.Empty(FakeSdk.Sources);
            Assert.Contains("Register", Assert.Single(_warnings));
        }

        [Fact]
        public void InitialPoseFailureNeverRegistersAnOriginCamera()
        {
            UnityEngine.Object.Destroy(_head.gameObject);
            _session.Begin(null);
            Assert.Empty(FakeSdk.Sources); Assert.True(_session.IsActive); Assert.Single(_warnings);
        }

        [Fact]
        public void InvalidPoseRetainsLastGoodPositionAndQuaternionIsNormalized()
        {
            _session.Begin(null); var source = Assert.Single(FakeSdk.Sources);
            _interop.UpdatePose(_session.Id, new Vector3(float.NaN, 0, 0), Quaternion.identity);
            _interop.UpdatePose(_session.Id, Vector3.zero, new Quaternion(0, 0, 0, 0));
            _interop.UpdatePose(_session.Id, Vector3.zero, new Quaternion(0, 0, 0, float.PositiveInfinity));
            SamePose(_head.position, _head.rotation, source);
            _interop.UpdatePose(_session.Id, new Vector3(1, 2, 3), new Quaternion(0, 0, 0, 2));
            SamePose(new Vector3(1, 2, 3), Quaternion.identity, source); Assert.Empty(_warnings);
        }

        [Theory]
        [InlineData("register")]
        [InlineData("activate")]
        [InlineData("update")]
        public void OptionalSdkFailuresCleanUpWithoutInterruptingOtherSessionSubscribers(string operation)
        {
            bool notified = false;
            Action<Guid, bool> listener = (id, active) => notified = true;
            MovementReplay.SessionChanged += listener;
            try
            {
                if (operation != "update") FakeSdk.FailOperation = operation;
                _session.Begin(null);
                if (operation == "update")
                { FakeSdk.FailOperation = operation; _interop.UpdatePose(_session.Id, _head.position, _head.rotation); }
                Assert.True(notified); Assert.True(_session.IsActive); Assert.Empty(FakeSdk.Sources); Assert.Single(_warnings);
                Assert.False(FakeSdk.LastCreated.isInReplay);
                Assert.Equal(1, FakeSdk.UnregisterCalls);
            }
            finally { MovementReplay.SessionChanged -= listener; }
        }

        [Fact]
        public void FailedDeactivationStillAttemptsUnregister()
        {
            _session.Begin(null); FakeSdk.FailOperation = "deactivate";
            _session.Finish();
            Assert.Empty(FakeSdk.Sources); Assert.Equal(1, FakeSdk.UnregisterCalls); Assert.Single(_warnings);
        }
    }
}

namespace IPA.Loader
{
    public sealed class PluginMetadata { public Assembly Assembly; }
    public static class PluginManager
    {
        public static Func<string, PluginMetadata> Find;
        public static PluginMetadata GetPluginFromId(string name) => Find?.Invoke(name);
    }
}

// A separate API surface loaded through reflection by the real product class.
// No Camera2 type is referenced by the product's fields, parameters or base types.
namespace Camera2.SDK
{
    public static class ReplaySources
    {
        public interface ISource
        {
            string name { get; }
            bool isInReplay { get; }
            Vector3 localHeadPosition { get; }
            Quaternion localHeadRotation { get; }
        }
        public sealed class GenericSource : ISource
        {
            public string name { get; }
            public bool isInReplay { get; private set; }
            public Vector3 localHeadPosition { get; private set; }
            public Quaternion localHeadRotation { get; private set; }
            internal bool HasPose;
            public GenericSource(string name) { this.name = name; LastCreated = this; }
            public void Update(ref Vector3 position, ref Quaternion rotation)
            { Fail("update"); localHeadPosition = position; localHeadRotation = rotation; HasPose = true; }
            public void SetActive(bool active) { Fail(active ? "activate" : "deactivate"); isInReplay = active; }
        }
        public static readonly HashSet<ISource> Sources = new HashSet<ISource>();
        public static string FailOperation;
        public static GenericSource LastCreated;
        public static int RegisterCalls, UnregisterCalls;
        public static bool PoseWasSetBeforeRegister;
        public static void Register(ISource source)
        { RegisterCalls++; PoseWasSetBeforeRegister = ((GenericSource)source).HasPose; Sources.Add(source); Fail("register"); }
        public static void Unregister(ISource source) { UnregisterCalls++; Sources.Remove(source); }
        private static void Fail(string operation) { if (FailOperation == operation) throw new InvalidOperationException("SDK failed: " + operation); }
        public static void Reset()
        { Sources.Clear(); FailOperation = null; LastCreated = null; RegisterCalls = UnregisterCalls = 0; PoseWasSetBeforeRegister = false; }
    }
}
