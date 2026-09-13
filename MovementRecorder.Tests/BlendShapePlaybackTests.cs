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
    public sealed class BlendShapePlaybackTests : IDisposable
    {
        public BlendShapePlaybackTests() { UObject.Objects.Clear(); }
        public void Dispose() { UObject.Objects.Clear(); }
        private static GameObject Child(GameObject parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
        private static SkinnedMeshRenderer Skin(GameObject owner, params float[] weights)
        {
            var skin = owner.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = new Mesh { blendShapeCount = weights.Length };
            for (int i = 0; i < weights.Length; i++) skin.SetBlendShapeWeight(i, weights[i]);
            return skin;
        }
        private static MovementClip Clip(GameObject root)
        {
            var header = new MovementJson { objectCount = 1, recordCount = 2,
                objectNames = new List<string> { SceneModelResolver.PathOf(root.transform) },
                objectScales = new List<Scale> { new Scale { x = 1, y = 1, z = 1 } }, Settings = new List<Setting>() };
            return new MovementClip(header, null, new[] { 0f, 1f },
                new[] { new RecordedPose { Qw = 1 }, new RecordedPose { X = 1, Qw = 1 } }, new[] { float.PositiveInfinity });
        }
        private static RenderModelClone Copy(GameObject root, Action<string> warning = null, MovementClip clip = null)
        {
            return new RenderModelClone(clip ?? Clip(root), new ModelBindingPlan {
                CloneRoots = new[] { root.transform }, Sources = new[] { root.transform } }, false, warning: warning);
        }
        private static SkinnedMeshRenderer Copied(RenderModelClone clone, SkinnedMeshRenderer source)
            => clone.Transforms[source.transform].GetComponent<SkinnedMeshRenderer>();

        [Fact] public void RendererOutputChangesWithoutAnimatorAndLeavesRecordedPoseAndSourceUntouched()
        {
            var root = new GameObject("VRMAvatar");
            var first = Skin(Child(root, "AnyMeshName"), 15, 60); var second = Skin(Child(root, "AnotherMesh"), 30);
            first.sharedMaterials = new[] { new Material() }; root.transform.position = new Vector3(20, 0, 0);
            using var clone = Copy(root);
            var a = Copied(clone, first); var b = Copied(clone, second);
            Assert.Equal(15, a.GetBlendShapeWeight(0)); Assert.Equal(60, a.GetBlendShapeWeight(1)); Assert.Equal(30, b.GetBlendShapeWeight(0));
            first.SetBlendShapeWeight(0, 140); first.SetBlendShapeWeight(1, 0); second.SetBlendShapeWeight(0, -25);
            clone.Apply(1); clone.SyncLiveExpressions(true);
            Assert.Equal(140, a.GetBlendShapeWeight(0)); Assert.Equal(0, a.GetBlendShapeWeight(1)); Assert.Equal(-25, b.GetBlendShapeWeight(0));
            Assert.Equal(20, root.transform.position.x); Assert.Equal(1, clone.Transforms[root.transform].position.x);
            Assert.Equal(140, first.GetBlendShapeWeight(0)); Assert.NotSame(first.sharedMaterials[0], a.sharedMaterials[0]);
            Assert.Empty(clone.Transforms[root.transform].GetComponentsInChildren<Animator>(true));
            int writes = a.BlendShapeWrites + b.BlendShapeWrites;
            clone.SyncLiveExpressions(true); Assert.Equal(writes, a.BlendShapeWrites + b.BlendShapeWrites);
        }

        [Fact] public void PauseAndSeekHoldTheLastExpressionAndResumeReadsTheLatestOutput()
        {
            var root = new GameObject("Avatar"); var skin = Skin(root, 10); using var clone = Copy(root); var target = Copied(clone, skin);
            skin.SetBlendShapeWeight(0, 20); clone.SyncLiveExpressions(true);
            skin.SetBlendShapeWeight(0, 70);
            foreach (float time in new[] { 1f, 0f, .5f })
            {
                clone.Apply(time); clone.SyncLiveExpressions(false);
                Assert.Equal(20, target.GetBlendShapeWeight(0));
                Assert.Equal(time, clone.Transforms[root.transform].position.x);
            }
            clone.SyncLiveExpressions(true); Assert.Equal(70, target.GetBlendShapeWeight(0));
        }

        [Theory] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
        public void NonFiniteWeightsKeepTheLastFiniteValueAndWarnOnlyOnce(float invalid)
        {
            var root = new GameObject("Avatar"); var skin = Skin(root, invalid); var warnings = new List<string>();
            using var clone = Copy(root, warnings.Add); var target = Copied(clone, skin);
            Assert.Equal(0, target.GetBlendShapeWeight(0)); Assert.Single(warnings);
            skin.SetBlendShapeWeight(0, 40); clone.SyncLiveExpressions(true); Assert.Equal(40, target.GetBlendShapeWeight(0));
            skin.SetBlendShapeWeight(0, invalid); clone.SyncLiveExpressions(true); clone.SyncLiveExpressions(true);
            Assert.Equal(40, target.GetBlendShapeWeight(0)); Assert.Single(warnings);
            skin.SetBlendShapeWeight(0, 0); clone.SyncLiveExpressions(true); Assert.Equal(0, target.GetBlendShapeWeight(0));
        }

        [Theory] [InlineData(true)] [InlineData(false)]
        public void ReplacedMeshesDoNotReuseMatchingBlendShapeIndices(bool replaceSource)
        {
            var root = new GameObject("Avatar"); var skin = Skin(root, 15); var warnings = new List<string>();
            using var clone = Copy(root, warnings.Add); var target = Copied(clone, skin); var mesh = skin.sharedMesh;
            skin.SetBlendShapeWeight(0, 80);
            (replaceSource ? skin : target).sharedMesh = new Mesh { blendShapeCount = 1 };
            int writes = target.BlendShapeWrites;
            clone.SyncLiveExpressions(true); clone.SyncLiveExpressions(true);
            Assert.Single(warnings); Assert.Equal(writes, target.BlendShapeWrites);
            (replaceSource ? skin : target).sharedMesh = mesh;
            clone.SyncLiveExpressions(true); Assert.Equal(writes, target.BlendShapeWrites);
        }

        [Theory] [InlineData("count")] [InlineData("mesh")] [InlineData("source")] [InlineData("target")]
        public void LostBindingsStopBeforeAccessingInvalidObjectsOrIndices(string change)
        {
            var root = new GameObject("Avatar"); var skin = Skin(root, 15); var warnings = new List<string>();
            using var clone = Copy(root, warnings.Add); var target = Copied(clone, skin);
            if (change == "count") skin.sharedMesh.blendShapeCount = 0;
            else if (change == "mesh") UObject.Destroy(skin.sharedMesh);
            else if (change == "source") UObject.Destroy(skin);
            else UObject.Destroy(target);
            clone.SyncLiveExpressions(true); clone.SyncLiveExpressions(true); Assert.Single(warnings);
        }

        [Fact] public void OnlySourceAncestorAnimatorsAreAdjustedAndAllOtherSettingsArePreserved()
        {
            var root = new GameObject("Avatar"); var parent = root.AddComponent<Animator>(); parent.cullingMode = AnimatorCullingMode.CullCompletely;
            var face = Child(root, "Face"); var disabled = face.AddComponent<Animator>(); disabled.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            disabled.enabled = false; disabled.speed = .75f; disabled.applyRootMotion = true;
            var controller = disabled.runtimeAnimatorController = new RuntimeAnimatorController();
            Skin(face, 10); Skin(Child(root, "Eyes"), 20);
            var unrelated = Child(root, "Unrelated").AddComponent<Animator>(); unrelated.cullingMode = AnimatorCullingMode.CullCompletely;
            var meshWithoutShapes = Child(root, "NoMorphs"); Skin(meshWithoutShapes);
            var noMorphAnimator = meshWithoutShapes.AddComponent<Animator>(); noMorphAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var clone = Copy(root);
            Assert.Equal(AnimatorCullingMode.AlwaysAnimate, parent.cullingMode); Assert.Equal(2, parent.CullingWrites);
            Assert.Equal(AnimatorCullingMode.AlwaysAnimate, disabled.cullingMode);
            Assert.False(disabled.enabled); Assert.Equal(.75f, disabled.speed); Assert.True(disabled.applyRootMotion); Assert.Same(controller, disabled.runtimeAnimatorController);
            Assert.Equal(AnimatorCullingMode.CullCompletely, unrelated.cullingMode); Assert.Equal(AnimatorCullingMode.CullUpdateTransforms, noMorphAnimator.cullingMode);
            Assert.Empty(clone.Transforms[root.transform].GetComponentsInChildren<Animator>(true));
            clone.Dispose(); clone.Dispose();
            Assert.Equal(AnimatorCullingMode.CullCompletely, parent.cullingMode); Assert.Equal(3, parent.CullingWrites);
            Assert.Equal(AnimatorCullingMode.CullUpdateTransforms, disabled.cullingMode); Assert.False(disabled.enabled);
        }

        [Fact] public void InitialAnimationSettingsAreRestoredAfterConstructionFails()
        {
            var root = new GameObject("Avatar"); var skin = Skin(root, 20); var animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var clip = Clip(root); clip.Header.objectScales.Clear();
            Assert.Throws<ArgumentOutOfRangeException>(() => Copy(root, clip: clip));
            Assert.Equal(AnimatorCullingMode.CullCompletely, animator.cullingMode); Assert.Equal(3, animator.CullingWrites);
            Assert.False(skin.forceRenderingOff);
            Assert.DoesNotContain(Resources.FindObjectsOfTypeAll<GameObject>(), o => o != null && o.name == SceneModelResolver.ReplayRootName);
        }

        [Fact] public void StoppingAfterErrorRestoresOnceAndRebindingCanStartFresh()
        {
            var root = new GameObject("Avatar"); var skin = Skin(root, 20); var animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
            var clone = Copy(root); var target = Copied(clone, skin);
            clone.StopLiveExpressions(); clone.StopLiveExpressions();
            Assert.Equal(AnimatorCullingMode.CullCompletely, animator.cullingMode); Assert.Equal(3, animator.CullingWrites);
            skin.SetBlendShapeWeight(0, 70); clone.SyncLiveExpressions(true); Assert.Equal(20, target.GetBlendShapeWeight(0));
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            clone.Dispose(); clone.SyncLiveExpressions(true); Assert.Equal(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
            using (var next = Copy(root)) { Assert.Equal(70, Copied(next, skin).GetBlendShapeWeight(0)); Assert.Equal(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode); }
            Assert.Equal(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
        }

        [Fact] public void UnchangedAnimatorSettingsAreNotClaimedAndDestroyedAnimatorsDoNotBlockRestoration()
        {
            var root = new GameObject("Avatar"); var animator = root.AddComponent<Animator>(); Skin(root, 20);
            var child = Child(root, "Face"); Skin(child, 30); var doomed = child.AddComponent<Animator>(); doomed.cullingMode = AnimatorCullingMode.CullCompletely;
            var other = Child(root, "OtherFace"); Skin(other, 30); var surviving = other.AddComponent<Animator>(); surviving.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var clone = Copy(root); Assert.Equal(0, animator.CullingWrites);
            animator.cullingMode = AnimatorCullingMode.CullCompletely; UObject.Destroy(doomed);
            clone.Dispose(); Assert.Equal(AnimatorCullingMode.CullCompletely, animator.cullingMode);
            Assert.Equal(AnimatorCullingMode.CullUpdateTransforms, surviving.cullingMode);
        }
    }
}
