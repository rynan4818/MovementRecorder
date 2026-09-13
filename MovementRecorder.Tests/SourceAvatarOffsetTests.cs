using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MovementRecorder.Models;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Models;
using UnityEngine;
using Xunit;
using UObject = UnityEngine.Object;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class SourceAvatarOffsetTests : IDisposable
    {
        public SourceAvatarOffsetTests() { UObject.Objects.Clear(); Camera.onPreCull = Camera.onPostRender = null; Time.frameCount = 1; }
        public void Dispose()
        {
            foreach (var guard in UObject.Objects.OfType<SourceAvatarOffsetFrameGuard>().ToArray()) guard.Owner?.Dispose();
            Camera.onPreCull = Camera.onPostRender = null; UObject.Objects.Clear();
        }
        private static GameObject Child(GameObject parent, string name)
        { var value = new GameObject(name); value.transform.SetParent(parent.transform, false); value.transform.localPosition = new Vector3(.2f, .4f, -.3f); return value; }
        private static MeshRenderer Mesh(GameObject owner)
        { owner.AddComponent<MeshFilter>().sharedMesh = new Mesh(); return owner.AddComponent<MeshRenderer>(); }
        private static MovementClip Clip(params Transform[] tracks) => new MovementClip(new MovementJson
        {
            objectCount = tracks.Length, recordCount = 2, objectNames = tracks.Select(SceneModelResolver.PathOf).ToList(),
            objectScales = tracks.Select(_ => new Scale { x = 1, y = 1, z = 1 }).ToList(), Settings = new List<Setting>()
        }, null, new[] { 0f, 1f }, Enumerable.Repeat(new RecordedPose { X = 10, Qw = 1 }, tracks.Length * 2).ToArray(),
            Enumerable.Repeat(float.PositiveInfinity, tracks.Length).ToArray());
        private static RenderModelClone Model(GameObject root, bool show = true) => new RenderModelClone(Clip(root.transform),
            new ModelBindingPlan { CloneRoots = new[] { root.transform }, Sources = new[] { root.transform }, Types = new[] { "Avatar" } }, false, showSourceAvatar: show);
        private static void Same(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < .0001f,
            $"Expected ({expected.x}, {expected.y}, {expected.z}), actual ({actual.x}, {actual.y}, {actual.z})");
        private static SourceAvatarOffsetFrameGuard Guard => UObject.Objects.OfType<SourceAvatarOffsetFrameGuard>().SingleOrDefault(g => g != null);

        [Theory]
        [InlineData(false, false, -2)] [InlineData(false, true, -2)] [InlineData(true, false, -2)]
        [InlineData(false, true, 0)] [InlineData(true, true, 0)]
        public void DisabledOrHiddenOrZeroCreatesNoOffsetResourcesOrPerFramePositionAccess(bool show, bool enabled, float z)
        {
            var root = new GameObject("Avatar"); Mesh(Child(root, "Body"));
            using var model = Model(root, show); var camera = new GameObject("External camera").AddComponent<Camera>();
            var transforms = UObject.Objects.OfType<Transform>().ToArray();
            int objects = UObject.Objects.Count; var reads = transforms.Select(t => t.PositionReads).ToArray();
            var writes = transforms.Select(t => t.PositionWrites).ToArray();
            model.SetSourceAvatarOffset(enabled, new Vector3(0, 0, z), null, _ => throw new Exception("Unexpected callback"));
            for (int frame = 0; frame < 60; frame++)
            { Time.frameCount++; Camera.onPreCull?.Invoke(camera); Camera.onPostRender?.Invoke(camera); }
            Assert.Null(Guard); Assert.Null(Camera.onPreCull); Assert.Null(Camera.onPostRender);
            Assert.Equal(objects, UObject.Objects.Count);
            Assert.Equal(reads, transforms.Select(t => t.PositionReads)); Assert.Equal(writes, transforms.Select(t => t.PositionWrites));
        }

        [Fact] public void MovesMeshAndSiblingBonesButProtectsCamerasSabersOtherAndLogicalHead()
        {
            var room = new GameObject("Room"); room.transform.position = new Vector3(9, 2, -4);
            room.transform.localRotation = new Quaternion(0, MathF.Sqrt(.5f), 0, MathF.Sqrt(.5f)); room.transform.localScale = new Vector3(2, 3, 4);
            var avatar = Child(room, "Avatar"); var skin = Child(avatar, "Face").AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = new Mesh { blendShapeCount = 1 }; skin.gameObject.layer = 10;
            var skeleton = Child(avatar, "Armature"); var bone = Child(skeleton, "Head bone"); skin.bones = new[] { bone.transform }; skin.rootBone = skeleton.transform;
            var other = Child(avatar, "Other"); Mesh(other);
            var nestedAvatar = Child(other, "Second avatar"); Mesh(nestedAvatar);
            var saber = Child(avatar, "Live Saber"); Mesh(saber);
            var logicalHead = Child(avatar, "Logical head"); var controller = Child(logicalHead, "Controller");
            var camera = Child(avatar, "Native camera").AddComponent<Camera>();
            var tracks = new[] { avatar.transform, other.transform, nestedAvatar.transform, saber.transform };
            var plan = new ModelBindingPlan { Sources = tracks, CloneRoots = new[] { avatar.transform }, Types = new[] { "Avatar", "Other", "Avatar", "Saber" } };
            using var model = new RenderModelClone(Clip(tracks), plan, false, new[] { saber.transform }, showSourceAvatar: true);
            var moved = new[] { avatar.transform, skin.transform, skeleton.transform, bone.transform, nestedAvatar.transform };
            var stationary = new[] { room.transform, other.transform, saber.transform, camera.transform, logicalHead.transform, controller.transform };
            var all = moved.Concat(stationary).ToArray(); var positions = all.ToDictionary(t => t, t => t.position);
            var locals = all.ToDictionary(t => t, t => t.localPosition); var clonePosition = model.Transforms[avatar.transform].position;
            var offset = new Vector3(1.5f, -.5f, -3); var failures = new List<Exception>();
            model.SetSourceAvatarOffset(true, offset, new[] { logicalHead.transform }, failures.Add);
            Assert.NotNull(Guard); Assert.True(skin.updateWhenOffscreen);
            Camera.onPreCull(camera);
            foreach (var transform in moved) Same(positions[transform] + offset, transform.position);
            foreach (var transform in stationary) Same(positions[transform], transform.position);
            Same(clonePosition, model.Transforms[avatar.transform].position);
            skin.SetBlendShapeWeight(0, 70); model.SyncLiveExpressions(true);
            Assert.Equal(70, model.Transforms[skin.transform].GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(0));
            Assert.Equal(10, skin.gameObject.layer);
            Camera.onPostRender(camera);
            foreach (var transform in all) { Same(positions[transform], transform.position); Same(locals[transform], transform.localPosition); }
            model.StopSourceAvatarOffset(); Assert.False(skin.updateWhenOffscreen); Assert.Null(Guard); Assert.Empty(failures);
        }

        [Fact] public void NestedAndSequentialCamerasApplyOnceAndReadTheLatestProviderPose()
        {
            var root = new GameObject("Avatar"); Mesh(root); using var model = Model(root);
            var outer = new GameObject("HMD").AddComponent<Camera>(); var mirror = new GameObject("Mirror").AddComponent<Camera>();
            var first = new Vector3(1, 0, -2); var next = new Vector3(-2, .5f, -4);
            model.SetSourceAvatarOffset(true, first, null, null);
            for (int frame = 0; frame < 3; frame++)
            {
                Time.frameCount++; var pose = new Vector3(frame, 1, .5f); root.transform.position = pose;
                Camera.onPreCull(outer); Camera.onPreCull(mirror); Same(pose + first, root.transform.position);
                model.SetSourceAvatarOffset(true, next, null, null); Same(pose + first, root.transform.position);
                Camera.onPostRender(mirror); Same(pose + first, root.transform.position);
                Camera.onPostRender(outer); Same(pose, root.transform.position);
                Camera.onPreCull(mirror); Same(pose + next, root.transform.position);
                Camera.onPostRender(mirror); Same(pose, root.transform.position);
                model.SetSourceAvatarOffset(true, first, null, null);
            }
        }

        [Theory] [InlineData(false)] [InlineData(true)]
        public void DisablingOrSettingZeroRestoresPendingRenderAndCanBeEnabledAgain(bool zero)
        {
            var root = new GameObject("Avatar"); Mesh(root); using var model = Model(root);
            var camera = new GameObject("Camera").AddComponent<Camera>(); var offset = new Vector3(0, 0, -2);
            var original = root.transform.position;
            Camera.CameraCallback existing = _ => { }; Camera.onPreCull = Camera.onPostRender = existing;
            model.SetSourceAvatarOffset(true, offset, null, null); Camera.onPreCull(camera); Same(original + offset, root.transform.position);
            model.SetSourceAvatarOffset(zero, zero ? Vector3.zero : offset, null, null);
            Same(original, root.transform.position); Assert.Null(Guard); Assert.Equal(existing, Camera.onPreCull); Assert.Equal(existing, Camera.onPostRender);
            model.SetSourceAvatarOffset(true, offset, null, null); Camera.onPreCull(camera); Same(original + offset, root.transform.position);
            model.Dispose(); Same(original, root.transform.position); Assert.Null(Guard);
            Assert.Equal(existing, Camera.onPreCull); Assert.Equal(existing, Camera.onPostRender);
        }

        [Theory] [InlineData("Update")] [InlineData("FixedUpdate")] [InlineData("EndOfFrame")]
        public void MissingPostRenderIsRestoredBeforeSimulationOrAtEndOfFrame(string boundary)
        {
            var root = new GameObject("Avatar"); Mesh(root); using var model = Model(root);
            var camera = new GameObject("Camera").AddComponent<Camera>(); var original = root.transform.position;
            model.SetSourceAvatarOffset(true, new Vector3(0, 0, -2), null, null);
            Camera.onPreCull(camera);
            if (boundary == "EndOfFrame")
            {
                var routine = (IEnumerator)typeof(SourceAvatarOffsetFrameGuard).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Guard, null);
                Assert.True(routine.MoveNext()); Assert.IsType<WaitForEndOfFrame>(routine.Current); routine.MoveNext();
            }
            else Guard.Callback(boundary);
            Same(original, root.transform.position);
            Camera.onPreCull(camera); Same(original + new Vector3(0, 0, -2), root.transform.position);
            Camera.onPostRender(camera); Same(original, root.transform.position);
        }

        [Fact] public void MissingNestedPostAndNextFramePreCullRecoverWithoutAccumulation()
        {
            var root = new GameObject("Avatar"); Mesh(root); using var model = Model(root);
            var camera = new GameObject("Camera").AddComponent<Camera>(); var mirror = new GameObject("Mirror").AddComponent<Camera>();
            var original = root.transform.position; var offset = new Vector3(0, 0, -2);
            model.SetSourceAvatarOffset(true, offset, null, null);
            Camera.onPreCull(camera); Camera.onPreCull(mirror); Camera.onPostRender(camera); Same(original, root.transform.position);
            Camera.onPreCull(camera); Time.frameCount++; Camera.onPreCull(camera); Same(original + offset, root.transform.position);
            Camera.onPostRender(camera); Same(original, root.transform.position);
        }

        [Fact] public void ProviderHierarchyChangeReportsOnceAndDoesNotLeaveOtherRootsMoved()
        {
            var root = new GameObject("Avatar"); Mesh(root); var other = Child(root, "Other"); Mesh(other);
            var tracks = new[] { root.transform, other.transform };
            using var model = new RenderModelClone(Clip(tracks), new ModelBindingPlan
                { CloneRoots = new[] { root.transform }, Sources = tracks, Types = new[] { "Avatar", "Other" } }, false, showSourceAvatar: true);
            var camera = new GameObject("Camera").AddComponent<Camera>(); var failures = new List<Exception>();
            var original = root.transform.position;
            model.SetSourceAvatarOffset(true, new Vector3(0, 0, -2), null, e => { failures.Add(e); throw new Exception("Logger failed"); });
            other.transform.SetParent(null, true); Camera.onPreCull(camera);
            Assert.Single(failures); Same(original, root.transform.position);
            Assert.Null(Camera.onPreCull); Assert.Null(Camera.onPostRender); Assert.Null(Guard);
        }

        [Fact] public void DestroyedBranchAndGuardDeactivationStillRestoreSurvivingTransforms()
        {
            var root = new GameObject("Avatar"); Mesh(root); var other = Child(root, "Other"); Mesh(other);
            var tracks = new[] { root.transform, other.transform };
            using var model = new RenderModelClone(Clip(tracks), new ModelBindingPlan
                { CloneRoots = new[] { root.transform }, Sources = tracks, Types = new[] { "Avatar", "Other" } }, false, showSourceAvatar: true);
            var original = root.transform.position; var camera = new GameObject("Camera").AddComponent<Camera>();
            model.SetSourceAvatarOffset(true, new Vector3(0, 0, -2), null, null);
            Camera.onPreCull(camera); UObject.Destroy(other); Guard.gameObject.SetActive(false);
            Same(original, root.transform.position); Assert.Null(Camera.onPreCull); Assert.Null(Camera.onPostRender);
        }

        [Fact] public void BoneOutsideAvatarRangeFailsOnlyWhenEnabledBeforeAllocatingHelpers()
        {
            var root = new GameObject("Avatar"); var skin = Child(root, "Face").AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = new Mesh();
            var bone = Child(root, "Other bone").transform; skin.bones = new[] { bone };
            var tracks = new[] { root.transform, bone };
            using var model = new RenderModelClone(Clip(tracks), new ModelBindingPlan
                { CloneRoots = new[] { root.transform }, Sources = tracks, Types = new[] { "Avatar", "Other" } }, false, showSourceAvatar: true);
            model.SetSourceAvatarOffset(false, new Vector3(0, 0, -2), null, null);
            Assert.Throws<InvalidOperationException>(() => model.SetSourceAvatarOffset(true, new Vector3(0, 0, -2), null, null));
            Assert.False(skin.updateWhenOffscreen); Assert.Null(Guard); Assert.Null(Camera.onPreCull); Assert.Null(Camera.onPostRender);
        }
    }
}
