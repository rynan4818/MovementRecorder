using System;
using System.Collections.Generic;
using System.Linq;
using MovementRecorder.Models;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Models;
using UnityEngine;
using Xunit;
using UObject = UnityEngine.Object;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class SourceAvatarVisibilityTests : IDisposable
    {
        public SourceAvatarVisibilityTests() { UObject.Objects.Clear(); }
        public void Dispose() { UObject.Objects.Clear(); }
        private static GameObject Child(GameObject parent, string name)
        { var value = new GameObject(name); value.transform.SetParent(parent.transform, false); return value; }
        private static MeshRenderer Mesh(GameObject owner)
        { owner.AddComponent<MeshFilter>().sharedMesh = new Mesh(); return owner.AddComponent<MeshRenderer>(); }
        private static MovementClip Clip(params Transform[] tracks) => new MovementClip(new MovementJson
        {
            objectCount = tracks.Length, recordCount = 2, objectNames = tracks.Select(SceneModelResolver.PathOf).ToList(),
            objectScales = tracks.Select(_ => new Scale { x = 1, y = 1, z = 1 }).ToList(), Settings = new List<Setting>()
        }, null, new[] { 0f, 1f }, Enumerable.Repeat(new RecordedPose { Qw = 1 }, tracks.Length * 2).ToArray(),
            Enumerable.Repeat(float.PositiveInfinity, tracks.Length).ToArray());

        [Theory] [InlineData(false)] [InlineData(true)]
        public void OptionOnlyAffectsAvatarAndLeavesLiveSabersAlone(bool show)
        {
            var root = new GameObject("UnknownProvider"); var body = Mesh(Child(root, "Body"));
            var trail = Child(root, "Effect").AddComponent<TrailRenderer>();
            var hidden = Mesh(Child(root, "Hidden")); hidden.forceRenderingOff = true; hidden.enabled = false;
            var other = Child(root, "Other"); var otherMesh = Mesh(other);
            var saber = Child(root, "LiveSaber"); var saberMesh = Mesh(saber);
            var tracks = new[] { root.transform, other.transform, saber.transform };
            var plan = new ModelBindingPlan { CloneRoots = new[] { root.transform }, Sources = tracks, Types = new[] { "Avatar", "Other", "Saber" } };
            using (var clone = new RenderModelClone(Clip(tracks), plan, false, new[] { saber.transform }, showSourceAvatar: show))
            {
                Assert.Equal(!show, body.forceRenderingOff); Assert.Equal(!show, trail.forceRenderingOff);
                Assert.True(hidden.forceRenderingOff); Assert.False(hidden.enabled);
                Assert.True(otherMesh.forceRenderingOff); Assert.False(saberMesh.forceRenderingOff);
                body.forceRenderingOff = show; otherMesh.forceRenderingOff = false;
                clone.KeepSourcesHidden(); Assert.True(body.forceRenderingOff); Assert.True(otherMesh.forceRenderingOff);
                body.forceRenderingOff = false;
                clone.KeepSourcesHidden(); Assert.Equal(!show, body.forceRenderingOff);
                Assert.False(clone.Transforms[body.transform].GetComponent<MeshRenderer>().forceRenderingOff);
            }
            Assert.False(body.forceRenderingOff); Assert.False(otherMesh.forceRenderingOff); Assert.False(trail.forceRenderingOff);
            Assert.True(hidden.forceRenderingOff); Assert.False(hidden.enabled); Assert.False(saberMesh.forceRenderingOff);
        }

        [Fact] public void VisibleSourceStillSuppliesExpressionsAndItsDisappearanceIsDetected()
        {
            var root = new GameObject("Avatar"); var animator = root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var skin = Child(root, "Face").AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = new Mesh { blendShapeCount = 1 };
            var plan = new ModelBindingPlan { CloneRoots = new[] { root.transform }, Sources = new[] { root.transform }, Types = new[] { "Avatar" } };
            using (var clone = new RenderModelClone(Clip(root.transform), plan, false, showSourceAvatar: true))
            {
                skin.SetBlendShapeWeight(0, 75); clone.SyncLiveExpressions(true);
                Assert.Equal(75, clone.Transforms[skin.transform].GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(0));
                Assert.False(skin.forceRenderingOff); Assert.Equal(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode);
                skin.forceRenderingOff = true; // A provider change remains owned by that provider, including at disposal.
            }
            Assert.True(skin.forceRenderingOff); Assert.Equal(AnimatorCullingMode.CullCompletely, animator.cullingMode);
            using (var clone = new RenderModelClone(Clip(root.transform), plan, false, showSourceAvatar: true))
            { UObject.Destroy(skin); Assert.Throws<InvalidOperationException>(() => clone.KeepSourcesHidden()); }
        }

        [Theory] [InlineData(false)] [InlineData(true)]
        public void FailedCreationDoesNotChangeOriginalVisibility(bool show)
        {
            var root = new GameObject("Avatar"); var renderer = Mesh(root); var hidden = Mesh(Child(root, "Hidden")); hidden.forceRenderingOff = true;
            var clip = Clip(root.transform); clip.Header.objectScales.Clear(); // Fails at the first pose, after source visibility was prepared.
            var plan = new ModelBindingPlan { CloneRoots = new[] { root.transform }, Sources = new[] { root.transform }, Types = new[] { "Avatar" } };
            Assert.ThrowsAny<Exception>(() => new RenderModelClone(clip, plan, false, showSourceAvatar: show));
            Assert.False(renderer.forceRenderingOff); Assert.True(hidden.forceRenderingOff);
            Assert.DoesNotContain(UObject.Objects.OfType<GameObject>(), o => o != null && o.name == SceneModelResolver.ReplayRootName);
        }
    }
}
