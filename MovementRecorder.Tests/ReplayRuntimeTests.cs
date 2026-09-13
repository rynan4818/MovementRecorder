using System;
using System.Collections.Generic;
using System.Linq;
using MovementRecorder.Models;
using MovementRecorder.Playback;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Models;
using MovementRecorder.Playback.Runtime;
using SiraUtil.Tools.FPFC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using VRUIControls;
using Xunit;
using Zenject;
using UObject = UnityEngine.Object;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class ReplayRuntimeTests : IDisposable
    {
        public ReplayRuntimeTests() { UObject.Objects.Clear(); Application.Clear(); TimeHelper.time = 10;
            Configuration.PluginConfig.Instance = new Configuration.PluginConfig(); }
        public void Dispose() { UObject.Objects.Clear(); Application.Clear(); }
        private static GameObject Child(GameObject parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
        private static MeshRenderer Mesh(GameObject owner)
        { owner.AddComponent<MeshFilter>().sharedMesh = new Mesh(); return owner.AddComponent<MeshRenderer>(); }
        private static void SamePosition(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < .0001f);
        private static MovementClip Clip(params Transform[] tracks)
        {
            var header = new MovementJson { objectCount = tracks.Length, recordCount = 2,
                objectNames = tracks.Select(SceneModelResolver.PathOf).ToList(),
                objectScales = tracks.Select(_ => new Scale { x = 1, y = 1, z = 1 }).ToList(),
                Settings = new List<Setting> { new Setting { type = "Saber", searchStirngs = new List<string> { ".*" } } } };
            var poses = new List<RecordedPose>();
            for (int frame = 0; frame < 2; frame++) foreach (var track in tracks)
                poses.Add(new RecordedPose { X = track.position.x + frame * 2, Y = track.position.y, Z = track.position.z, Qw = 1 });
            return new MovementClip(header, null, new[] { 0f, 2f }, poses.ToArray(), Enumerable.Repeat(float.PositiveInfinity, tracks.Length).ToArray());
        }
        private static (Saber saber, Transform anchor, VRController tracker) Hand(string name)
        {
            var tracker = new GameObject(name + " Controller").AddComponent<VRController>();
            var saber = Child(tracker.gameObject, name).AddComponent<Saber>(); saber.saberType = name;
            var anchor = Child(saber.gameObject, "Model").transform; anchor.localPosition = new Vector3(.2f, 0, 0);
            Mesh(anchor.gameObject);
            saber._saberBladeBottomTransform = Child(saber.gameObject, "Bottom").transform;
            saber._saberBladeTopTransform = Child(saber.gameObject, "Top").transform;
            saber._saberBladeTopTransform.localPosition = new Vector3(0, 0, 1);
            return (saber, anchor, tracker);
        }
        private static RecordedSaberDriver Driver((Saber saber, Transform anchor, VRController tracker) left,
            (Saber saber, Transform anchor, VRController tracker) right)
        {
            var tracks = new[] { left.anchor, right.anchor };
            return new RecordedSaberDriver(Clip(tracks), new ModelBindingPlan { Sources = tracks, Types = new[] { "Saber", "Saber" } },
                new SaberManager { leftSaber = left.saber, rightSaber = right.saber }, new BindingProfile());
        }

        [Fact] public void StartupSeekAndResumeNeverInvokeProviderTrailReset()
        {
            var left = Hand("Left"); var right = Hand("Right");
            var native = left.anchor.gameObject.AddComponent<SaberTrail>();
            var custom = right.anchor.gameObject.AddComponent<TransformSampledTrail>();
            using var driver = Driver(left, right);
            driver.TakeControl(); driver.PrepareHistory(0); TimeHelper.time += 1; driver.ResumeHistory();
            driver.PauseHistory(); driver.PrepareHistory(1); TimeHelper.time += 2; driver.ResumeHistory();
            Assert.Equal(0, native.ResetCalls); Assert.Equal(0, custom.ResetCalls);
            Assert.True(native.enabled); Assert.True(custom.enabled);
            SamePosition(new Vector3(1, 0, 0), left.saber.transform.position);
        }
        [Fact] public void HistoryRebuildPreservesMovementInstanceAndResumePreservesPendingCutWindow()
        {
            var left = Hand("Left"); var right = Hand("Right"); using var driver = Driver(left, right);
            var movement = left.saber.movementDataForLogic;
            var processors = movement._dataProcessors;
            driver.PrepareHistory(1);
            Assert.Same(movement, left.saber.movementDataForLogic); Assert.Same(processors, movement._dataProcessors);
            Assert.Equal(49, movement._validCount); Assert.Equal(49, movement.AddCalls);
            Assert.Equal(9.5999f, movement._data[0].time, 4); Assert.Equal(9.9999f, movement._data[48].time, 4);
            SamePosition(new Vector3(.6f, 0, 1), movement._data[0].topPos);
            driver.ResumeHistory();
            var counter = new SaberSwingRatingCounter { _cutTime = 9.9f }; processors.items.Add(counter);
            driver.PauseHistory(); TimeHelper.time = 20; driver.PauseHistory(); driver.ResumeHistory(); driver.ResumeHistory();
            Assert.Equal(49, movement.AddCalls); Assert.Equal(19.9f, counter._cutTime, 4);
            Assert.Equal(19.9999f, movement._data[48].time, 4);
        }
        [Fact] public void LiveSaberTrackingIsRestoredOnceIncludingAlreadyDisabledControllers()
        {
            var left = Hand("Left"); var right = Hand("Right"); right.tracker.enabled = false;
            var driver = Driver(left, right); driver.ValidateStableAnchors(); driver.TakeControl(); driver.TakeControl();
            Assert.False(left.tracker.enabled); Assert.False(right.tracker.enabled);
            driver.Apply(1); SamePosition(new Vector3(1, 0, 0), left.saber.transform.position);
            driver.Dispose(); Assert.True(left.tracker.enabled); Assert.False(right.tracker.enabled);
            left.tracker.enabled = false; driver.Dispose(); Assert.False(left.tracker.enabled);
        }
        [Fact] public void BindingKeepsSaberAnchorsButDoesNotMakeSaberCloneRoots()
        {
            var left = Hand("Left"); var right = Hand("Right"); var clip = Clip(left.anchor, right.anchor);
            var plan = new SceneModelResolver(clip).Resolve(new BindingProfile());
            Assert.True(plan.Ready); Assert.Equal(new[] { left.anchor, right.anchor }, plan.Sources); Assert.Empty(plan.CloneRoots);
            using var driver = new RecordedSaberDriver(clip, plan, new SaberManager { leftSaber = left.saber, rightSaber = right.saber }, new BindingProfile());
            using var clone = new RenderModelClone(clip, plan, false, driver.SaberRoots);
            Assert.Empty(clone.Transforms); Assert.Empty(clone.SkippedRenderers);
            Assert.False(left.anchor.GetComponent<MeshRenderer>().forceRenderingOff);
        }
        [Theory] [InlineData(false)] [InlineData(true)]
        public void LiveSabersAreNotCopiedOrHiddenEvenInsideAnotherModelRoot(bool nested)
        {
            var avatar = new GameObject("Avatar"); var body = Mesh(avatar);
            var left = Hand("Left"); var right = Hand("Right");
            if (nested) left.tracker.transform.SetParent(avatar.transform, false);
            var particles = Child(left.anchor.gameObject, "ProviderParticles").AddComponent<ParticleSystemRenderer>();
            var nativeTrail = right.anchor.gameObject.AddComponent<TransformSampledTrail>();
            var skin = right.anchor.gameObject.AddComponent<SkinnedMeshRenderer>();
            var leftMesh = left.anchor.GetComponent<MeshRenderer>();
            avatar.AddComponent<LODGroup>().SetLODs(new[] { new LOD(.5f, new Renderer[] { body, leftMesh }) });
            var tracks = new[] { avatar.transform, left.anchor, right.anchor };
            var clip = Clip(tracks);
            using (var clone = new RenderModelClone(clip, new ModelBindingPlan { Sources = tracks,
                CloneRoots = new[] { avatar.transform, left.anchor, right.anchor } }, false, new[] { left.saber.transform, right.saber.transform }))
            {
                clone.Apply(1); clone.KeepSourcesHidden();
                Assert.False(clone.Transforms.ContainsKey(left.anchor)); Assert.False(clone.Transforms.ContainsKey(right.anchor));
                Assert.True(body.forceRenderingOff); Assert.False(leftMesh.forceRenderingOff);
                Assert.False(particles.forceRenderingOff); Assert.False(skin.forceRenderingOff); Assert.True(nativeTrail.enabled);
                Assert.Empty(clone.SkippedRenderers); Assert.Equal(1, clone.ModelRootCount);
                Assert.Single(clone.Transforms[avatar.transform].GetComponent<LODGroup>().GetLODs()[0].renderers);
            }
            Assert.False(body.forceRenderingOff); Assert.False(leftMesh.forceRenderingOff); Assert.False(skin.Destroyed); Assert.False(nativeTrail.Destroyed);
        }

        private sealed class Platform : IVRPlatformHelper
        {
            public int Reads;
            public Vector3 Position = new Vector3(.1f, 1.6f, .2f);
            public Vector3 Left = new Vector3(-.3f, 1.1f, .5f), Right = new Vector3(.4f, 1.2f, .6f);
            public bool GetNodePose(XRNode node, int index, out Vector3 position, out Quaternion rotation)
            { if (node == XRNode.Head) Reads++; position = node == XRNode.Head ? Position : node == XRNode.LeftHand ? Left : Right; rotation = Quaternion.identity; return true; }
        }
        private sealed class Fpfc : IFPFCSettings { public bool Enabled { get; set; } }
        private static (MainCamera main, PlayerTransforms player, VRPointer pointer, VRController left, VRController right, DiContainer container, Platform platform, Fpfc fpfc, EventSystem previous) Scene(bool fpfc)
        {
            var origin = new GameObject("Origin");
            var main = Child(origin, "MainCamera").AddComponent<MainCamera>(); main.gameObject.AddComponent<Camera>();
            if (fpfc) main.camera.stereoTargetEye = StereoTargetEyeMask.None;
            var player = new GameObject("Player").AddComponent<PlayerTransforms>();
            player._headTransform = new GameObject("Logical head without a camera").transform;
            var menu = new GameObject("Native pause menu"); menu.SetActive(false); menu.transform.position = new Vector3(9, -1, 3);
            var left = Child(menu, "UI left").AddComponent<VRController>(); left.node = XRNode.LeftHand;
            var right = Child(menu, "UI right").AddComponent<VRController>(); right.node = XRNode.RightHand;
            Mesh(Child(left.gameObject, "Controller body")); Mesh(Child(right.gameObject, "Controller body"));
            var pointer = Child(menu, "VRPointer").AddComponent<VRPointer>(); pointer.gameObject.AddComponent<EventSystem>();
            pointer._leftVRController = left; pointer._rightVRController = right; pointer._lastSelectedVrController = right;
            pointer._laserPointerPrefab = new object(); pointer._cursorPrefab = new object();
            var module = pointer.gameObject.AddComponent<VRInputModule>(); module._vrPointer = pointer; module._rumblePreset = new object();
            var previous = new GameObject("Previous EventSystem").AddComponent<EventSystem>(); EventSystem.current = previous;
            var container = new DiContainer(); var platform = new Platform(); var settings = new Fpfc { Enabled = fpfc };
            container.Bind(main); container.Bind<IVRPlatformHelper>(platform); container.Bind<IFPFCSettings>(settings); container.Bind(module);
            return (main, player, pointer, left, right, container, platform, settings, previous);
        }
        private static VRPointer ObserverPointer(VRPointer original) => Resources.FindObjectsOfTypeAll<VRPointer>().Single(p => p != null && p != original);
        [Theory] [InlineData(false)] [InlineData(true)]
        public void SpectatorUsesMainCameraWhenLogicalHeadHasNoCameraAndRestoresUi(bool fpfc)
        {
            var scene = Scene(fpfc); scene.main.transform.localPosition = new Vector3(0, 1.8f, 0);
            var session = new ReplaySession(); var rig = new SpectatorRig(session, scene.player, scene.container);
            var observer = rig.Camera; var pointer = ObserverPointer(scene.pointer); var copy = pointer._rightVRController;
            Assert.False(pointer.gameObject.activeInHierarchy); Assert.Same(scene.previous, EventSystem.current);
            rig.SetControlsVisible(true);
            Assert.False(scene.main.camera.enabled); Assert.NotSame(scene.right, copy);
            Assert.Null(copy.GetComponentInChildren<Saber>(true)); Assert.Equal(!fpfc, copy.enabled);
            Assert.NotNull(copy.GetComponentInChildren<MeshRenderer>(true)); Assert.True(copy.gameObject.activeInHierarchy);
            Assert.False(scene.pointer.gameObject.activeInHierarchy); Assert.Same(pointer.GetComponent<EventSystem>(), EventSystem.current);
            Assert.True(pointer.GetComponent<VRInputModule>().enabled);
            Assert.Same(scene.pointer._laserPointerPrefab, pointer._laserPointerPrefab);
            Assert.Same(scene.pointer._cursorPrefab, pointer._cursorPrefab);
            SamePosition(scene.main.transform.position + new Vector3(0, 0, -2), observer.transform.position);
            Assert.Equal(fpfc ? 0 : 2, scene.platform.Reads);
            rig.Dispose(); rig.Dispose();
            Assert.True(scene.main.camera.enabled); Assert.Same(scene.right, scene.pointer._rightVRController);
            Assert.Same(scene.right, scene.pointer.lastSelectedVrController); Assert.Equal(1, observer.DestroyCalls);
            Assert.Same(scene.previous, EventSystem.current); Assert.False(scene.right.Destroyed);
            Application.BeforeRender(); // No remaining callback may access the disposed camera.
        }
        [Fact]
        public void ExternalCameraPoseIncludesRoomTransformOnceAndExcludesObserverOffset()
        {
            var scene = Scene(false);
            scene.main.transform.parent.SetPositionAndRotation(new Vector3(3, 0, -1), new Quaternion(0, .70710678f, 0, .70710678f));
            var session = new ReplaySession { ObserverX = 5, ObserverZ = -4 };
            var poses = new List<(Vector3 position, Quaternion rotation)>();
            var rig = new SpectatorRig(session, scene.player, scene.container, headPoseUpdated: (p, r) => poses.Add((p, r)));
            SamePosition(new Vector3(3.2f, 1.6f, -1.1f), poses.Last().position);
            Assert.True(Quaternion.Angle(scene.main.transform.rotation, poses.Last().rotation) < .1f);
            SamePosition(poses.Last().position + rig.Offset, rig.Camera.transform.position);
            Assert.False(scene.main.camera.enabled);
            session.ObserverZ = 10; rig.Update();
            SamePosition(new Vector3(3.2f, 1.6f, -1.1f), poses.Last().position);
            // A native Update can change the tracked head before Camera2's LateUpdate.
            scene.main.transform.localPosition = new Vector3(.4f, 1.8f, .6f); rig.PublishHeadPose();
            SamePosition(new Vector3(3.6f, 1.8f, -1.4f), poses.Last().position);
            scene.main.transform.parent.position = new Vector3(-2, 0, 1);
            scene.platform.Position = new Vector3(.3f, 1.9f, .7f); Application.BeforeRender();
            SamePosition(new Vector3(-1.3f, 1.9f, .7f), poses.Last().position);
            rig.Dispose(); int count = poses.Count; rig.PublishHeadPose(); Application.BeforeRender();
            Assert.Equal(count, poses.Count);
        }
        [Fact] public void VisibleSourceKeepsItsPositionRelativeToSpectatorAcrossPauseSeekAndOffsetEdits()
        {
            var scene = Scene(false);
            scene.main.transform.parent.SetPositionAndRotation(new Vector3(3, 0, -1), new Quaternion(0, .70710678f, 0, .70710678f));
            var session = new ReplaySession { ObserverX = 1.5f, ObserverY = .5f, ObserverZ = -4 };
            var avatar = new GameObject("Avatar"); Mesh(avatar);
            var clip = Clip(avatar.transform); session.Begin(clip, true, true);
            using var rig = new SpectatorRig(session, scene.player, scene.container);
            using var model = new RenderModelClone(clip, new ModelBindingPlan
                { CloneRoots = new[] { avatar.transform }, Sources = new[] { avatar.transform }, Types = new[] { "Avatar" } }, false, showSourceAvatar: true);
            try
            {
                foreach (var phase in new[] { ReplayPhase.Playing, ReplayPhase.Paused, ReplayPhase.Seeking, ReplayPhase.Completed })
                {
                    session.SetPhase(phase); session.ObserverZ += .5f; scene.platform.Position += new Vector3(.1f, 0, .1f);
                    rig.Update(); var bodyFromHead = new Vector3(0, -1, 0);
                    avatar.transform.position = rig.SourceHead.position + bodyFromHead; // Provider's current tracking output.
                    var original = avatar.transform.position; var head = rig.SourceHead.position;
                    model.SetSourceAvatarOffset(session.OffsetSourceAvatarWithHmd, rig.Offset, new[] { rig.SourceHead }, null);
                    Camera.onPreCull(rig.Camera);
                    SamePosition(rig.Camera.transform.position + bodyFromHead, avatar.transform.position);
                    SamePosition(head, rig.SourceHead.position);
                    Camera.onPostRender(rig.Camera); SamePosition(original, avatar.transform.position);
                }
                Assert.Equal(session.ObserverZ, Configuration.PluginConfig.Instance.replayObserverZ);
            }
            finally { session.Finish(); }
        }

        [Fact] public void FpfcToggleSynchronizesCameraAndPointerAndRestoresHeadTracking()
        {
            var scene = Scene(false); using var rig = new SpectatorRig(new ReplaySession(), scene.player, scene.container);
            rig.SetControlsVisible(true); var pointer = ObserverPointer(scene.pointer);
            int reads = scene.platform.Reads; scene.fpfc.Enabled = true;
            var fpfcOrigin = new GameObject("FPFC origin"); fpfcOrigin.transform.position = new Vector3(3, 2, 1);
            scene.main.transform.SetParent(fpfcOrigin.transform, false); scene.main.transform.localPosition = Vector3.zero;
            scene.right.transform.SetParent(fpfcOrigin.transform, false); scene.left.transform.SetParent(fpfcOrigin.transform, false);
            scene.main.camera.stereoTargetEye = StereoTargetEyeMask.None; scene.main.camera.fieldOfView = 75; scene.main.camera.aspect = 2;
            rig.Update();
            Assert.Equal(reads, scene.platform.Reads); Assert.Equal(StereoTargetEyeMask.None, rig.Camera.stereoTargetEye);
            Assert.Equal(75, rig.Camera.fieldOfView); Assert.Equal(2, rig.Camera.aspect);
            SamePosition(new Vector3(3, 2, -1), rig.Camera.transform.position);
            SamePosition(rig.Camera.transform.position, pointer._rightVRController.transform.position);
            Assert.False(pointer._rightVRController.enabled); Assert.True(pointer._rightVRController.mouseMode);
            scene.fpfc.Enabled = false; scene.main.camera.stereoTargetEye = StereoTargetEyeMask.Both; rig.Update();
            Assert.Equal(reads + 1, scene.platform.Reads); Assert.True(pointer._rightVRController.enabled);
            Assert.False(pointer._rightVRController.mouseMode);
            Assert.Equal(StereoTargetEyeMask.Both, rig.Camera.stereoTargetEye);
            SamePosition(scene.platform.Position, scene.main.transform.localPosition);
        }
        [Fact] public void BothHandsUseTheHmdTrackingOriginAndFollowChangedObserverOffsets()
        {
            var scene = Scene(false); var origin = scene.main.transform.parent;
            origin.localPosition = new Vector3(2, 3, 4); origin.localScale = new Vector3(2, 2, 2);
            origin.localRotation = new Quaternion(0, MathF.Sqrt(.5f), 0, MathF.Sqrt(.5f));
            var session = new ReplaySession { ObserverX = 1, ObserverY = -.5f, ObserverZ = -3 };
            using var rig = new SpectatorRig(session, scene.player, scene.container); rig.SetControlsVisible(true);
            var pointer = ObserverPointer(scene.pointer);
            foreach (var pair in new[] { (pointer._leftVRController, scene.platform.Left), (pointer._rightVRController, scene.platform.Right) })
            {
                SamePosition(origin.position + rig.Offset + origin.rotation * Vector3.Scale(origin.lossyScale, pair.Item2), pair.Item1.transform.position);
                Assert.True(pair.Item1.TrackingUpdates > 0);
            }
            var oldHand = pointer._rightVRController.transform.position; var oldHead = rig.Camera.transform.position;
            session.ObserverZ += 2; scene.platform.Right += new Vector3(.1f, 0, 0); rig.Update();
            SamePosition(oldHead + new Vector3(0, 0, 2), rig.Camera.transform.position);
            SamePosition(oldHand + new Vector3(0, 0, 2) + origin.rotation * new Vector3(.2f, 0, 0), pointer._rightVRController.transform.position);
        }
        [Fact] public void HidingAndReopeningControlsRestoresTheInputOwnerAndClearsSelection()
        {
            var scene = Scene(false); var nativeEvents = scene.pointer.GetComponent<EventSystem>(); nativeEvents.enabled = false;
            using var rig = new SpectatorRig(new ReplaySession(), scene.player, scene.container);
            var pointer = ObserverPointer(scene.pointer); var module = pointer.GetComponent<VRInputModule>();
            for (int i = 1; i <= 2; i++)
            {
                rig.SetControlsVisible(true); rig.SetControlsVisible(true);
                Assert.Same(pointer.GetComponent<EventSystem>(), EventSystem.current);
                Assert.True(pointer.gameObject.activeInHierarchy); Assert.True(pointer._leftVRController.gameObject.activeInHierarchy);
                rig.SetControlsVisible(false); rig.SetControlsVisible(false);
                Assert.Same(scene.previous, EventSystem.current); Assert.False(nativeEvents.enabled);
                Assert.False(pointer.gameObject.activeInHierarchy); Assert.False(pointer._leftVRController.gameObject.activeInHierarchy);
                Assert.Equal(i, module.Clears);
            }
        }
        [Theory] [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(false, false)]
        public void ActiveNativeInputCannotCompeteWithObserverAndKeepsItsPriorEnabledStates(bool pointerEnabled, bool moduleEnabled)
        {
            var scene = Scene(false);
            // The game's event object can stay active while its pause-menu controllers are hidden.
            scene.pointer.transform.SetParent(null, true);
            var nativeEvents = scene.pointer.GetComponent<EventSystem>();
            var nativeModule = scene.pointer.GetComponent<VRInputModule>();
            scene.pointer.enabled = pointerEnabled; nativeModule.enabled = moduleEnabled; EventSystem.current = nativeEvents;
            using var rig = new SpectatorRig(new ReplaySession(), scene.player, scene.container);
            for (int i = 0; i < 2; i++)
            {
                rig.SetControlsVisible(true);
                Assert.False(scene.pointer.isActiveAndEnabled); Assert.False(nativeModule.isActiveAndEnabled);
                Assert.False(nativeEvents.isActiveAndEnabled); Assert.NotSame(nativeEvents, EventSystem.current);
                rig.SetControlsVisible(false);
                Assert.Equal(pointerEnabled, scene.pointer.enabled); Assert.Equal(moduleEnabled, nativeModule.enabled);
                Assert.True(nativeEvents.isActiveAndEnabled); Assert.Same(nativeEvents, EventSystem.current);
            }
            rig.SetControlsVisible(true); rig.Dispose();
            Assert.Equal(pointerEnabled, scene.pointer.enabled); Assert.Equal(moduleEnabled, nativeModule.enabled);
            Assert.True(nativeEvents.isActiveAndEnabled); Assert.Same(nativeEvents, EventSystem.current);
        }
        [Fact] public void VrProjectionIsNotWrittenAndFlatProjectionIsUpdatedOnlyOnChange()
        {
            var scene = Scene(false); using var rig = new SpectatorRig(new ReplaySession(), scene.player, scene.container);
            for (int i = 0; i < 20; i++) { rig.Update(); Application.BeforeRender(); }
            Assert.Equal(0, rig.Camera.ProjectionWrites);
            scene.fpfc.Enabled = true; scene.main.camera.stereoTargetEye = StereoTargetEyeMask.None;
            scene.main.camera.fieldOfView = 70; scene.main.camera.aspect = 2; rig.Update();
            Assert.Equal(2, rig.Camera.ProjectionWrites);
            for (int i = 0; i < 20; i++) rig.Update();
            Assert.Equal(2, rig.Camera.ProjectionWrites);
            scene.fpfc.Enabled = false; scene.main.camera.stereoTargetEye = StereoTargetEyeMask.Both;
            rig.Update(); Assert.Equal(2, rig.Camera.ProjectionWrites);
        }
        [Theory] [InlineData(true)] [InlineData(false)]
        public void ObserverMaskTracksSourceAndModelChangesWithoutChangingOtherCameras(bool originallyEnabled)
        {
            var scene = Scene(false); var source = scene.main.camera; source.enabled = originallyEnabled;
            source.cullingMask = (1 << 8) | (1 << 6);
            var external = new GameObject("Camera2").AddComponent<Camera>(); external.cullingMask = 1 << 3;
            using var rig = new SpectatorRig(new ReplaySession(), scene.player, scene.container);
            rig.SetModelLayerMask((1 << 3) | (1 << 10) | (1 << 31));
            Assert.Equal((1 << 8) | (1 << 3) | (1 << 10) | (1 << 31), rig.Camera.cullingMask);
            source.cullingMask = (1 << 12) | (1 << 6); Application.BeforeRender();
            Assert.Equal((1 << 12) | (1 << 3) | (1 << 10) | (1 << 31), rig.Camera.cullingMask);
            rig.SetModelLayerMask(0); Assert.Equal(1 << 12, rig.Camera.cullingMask);
            Assert.Equal(1 << 3, external.cullingMask);
            rig.Dispose(); Assert.Equal(originallyEnabled, source.enabled); Assert.Equal((1 << 12) | (1 << 6), source.cullingMask);
        }
        [Fact] public void MenuControllerCopyRejectsSabersAndCleansUpPartialInitialization()
        {
            var scene = Scene(false); scene.right.gameObject.AddComponent<Saber>();
            Assert.Contains("menu controller", Assert.Throws<InvalidOperationException>(() =>
                new SpectatorRig(new ReplaySession(), scene.player, scene.container)).Message);
            Assert.True(scene.main.camera.enabled); Assert.Same(scene.previous, EventSystem.current);
            Assert.False(scene.left.Destroyed); Assert.False(scene.right.Destroyed);
            Assert.DoesNotContain(Resources.FindObjectsOfTypeAll<GameObject>(), o => o != null && o.name.StartsWith("Spectator"));
        }
    }
}
