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
    public sealed class ModelPlaybackTests : IDisposable
    {
        public ModelPlaybackTests() { UObject.Objects.Clear(); }
        public void Dispose() { UObject.Objects.Clear(); }
        private static GameObject Child(GameObject parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); return child; }
        private static MeshRenderer Mesh(GameObject owner)
        {
            owner.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            var renderer = owner.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { new Material() }; return renderer;
        }
        private static MovementClip Clip(params Transform[] sources)
        {
            var header = new MovementJson { objectCount = sources.Length, recordCount = 2,
                objectNames = sources.Select(SceneModelResolver.PathOf).ToList(),
                objectScales = sources.Select(_ => new Scale { x = 1, y = 1, z = 1 }).ToList(),
                Settings = new List<Setting> { new Setting { type = "Other", searchStirngs = new List<string> { ".*" } } } };
            return new MovementClip(header, null, new[] { 0f, 1f }, Enumerable.Repeat(new RecordedPose { Qw = 1 }, sources.Length * 2).ToArray(),
                Enumerable.Repeat(float.PositiveInfinity, sources.Length).ToArray());
        }
        private static RenderModelClone Copy(GameObject root, params Transform[] tracks)
        {
            if (tracks.Length == 0) tracks = new[] { root.transform };
            return new RenderModelClone(Clip(tracks), new ModelBindingPlan { CloneRoots = new[] { root.transform }, Sources = tracks }, false);
        }

        [Fact] public void OtherMeshWithTrailStartsWithMeshAndRestoresBothSourceRenderers()
        {
            var root = new GameObject("Other"); var mesh = Mesh(root); var trail = Child(root, "Cube").AddComponent<TrailRenderer>();
            using (var clone = Copy(root))
            {
                Assert.Single(clone.SkippedRenderers); Assert.Contains("TrailRenderer / Other/Cube", clone.SkippedRenderers[0]);
                var copy = clone.Transforms[root.transform].GetComponent<MeshRenderer>();
                Assert.NotNull(copy); Assert.False(copy.forceRenderingOff);
                Assert.Empty(clone.Transforms[root.transform].GetComponentsInChildren<TrailRenderer>(true));
                Assert.True(mesh.forceRenderingOff); Assert.True(trail.forceRenderingOff);
                trail.forceRenderingOff = false; clone.KeepSourcesHidden(); Assert.True(trail.forceRenderingOff);
            }
            Assert.False(mesh.forceRenderingOff); Assert.False(trail.forceRenderingOff);
        }

        [Fact] public void AdditionalParticlesAndLinesDoNotBlockTheSkinnedModelOrRetainLiveBones()
        {
            var root = new GameObject("Avatar"); var bone = Child(root, "Bone");
            var skin = root.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = new Mesh();
            skin.bones = new[] { bone.transform }; skin.rootBone = bone.transform;
            Child(root, "Particles").AddComponent<ParticleSystemRenderer>(); Child(root, "Line").AddComponent<LineRenderer>();
            using var clone = Copy(root, root.transform, bone.transform);
            Assert.Equal(2, clone.SkippedRenderers.Count);
            var copy = clone.Transforms[root.transform].GetComponent<SkinnedMeshRenderer>();
            Assert.Same(clone.Transforms[bone.transform], Assert.Single(copy.bones)); Assert.Same(copy.bones[0], copy.rootBone);
            Assert.Same(skin.sharedMesh, copy.sharedMesh);
        }

        [Fact] public void LodPreservesCopiedMeshesAndDropsOnlyExplicitlyOmittedEffects()
        {
            var root = new GameObject("Saber"); var mesh = Mesh(root); var trail = Child(root, "Trail").AddComponent<TrailRenderer>();
            root.AddComponent<LODGroup>().SetLODs(new[] { new LOD(.4f, new Renderer[] { mesh, trail }) { fadeTransitionWidth = .2f }, new LOD(.1f, new Renderer[] { trail }) });
            using var clone = Copy(root);
            var copy = clone.Transforms[root.transform]; var lods = copy.GetComponent<LODGroup>().GetLODs();
            Assert.Same(copy.GetComponent<MeshRenderer>(), Assert.Single(lods[0].renderers));
            Assert.Equal(.4f, lods[0].screenRelativeTransitionHeight); Assert.Equal(.2f, lods[0].fadeTransitionWidth);
            Assert.Empty(lods[1].renderers);
        }

        [Fact] public void EffectsOnlyModelReportsItsLimitationWithoutHidingTheOriginal()
        {
            var root = new GameObject("OnlyTrail"); var trail = root.AddComponent<TrailRenderer>();
            var error = Assert.Throws<InvalidOperationException>(() => Copy(root));
            Assert.Contains("No replayable mesh found", error.Message); Assert.False(trail.forceRenderingOff);
            Assert.DoesNotContain(Resources.FindObjectsOfTypeAll<GameObject>(), o => o != null && o.name == SceneModelResolver.ReplayRootName);
        }

        [Fact] public void ExternalLodReferencesStillFailAndReleaseCopiesWithoutChangingLiveMaterials()
        {
            var root = new GameObject("Saber"); var mesh = Mesh(root); var external = Mesh(new GameObject("External"));
            root.AddComponent<LODGroup>().SetLODs(new[] { new LOD(.5f, new Renderer[] { mesh, external }) });
            Assert.Contains("An LOD reference", Assert.Throws<InvalidOperationException>(() => Copy(root)).Message);
            Assert.False(mesh.forceRenderingOff); Assert.False(external.forceRenderingOff);
            Assert.False(mesh.sharedMaterials[0].Destroyed);
            Assert.DoesNotContain(Resources.FindObjectsOfTypeAll<Material>(), m => m != null && !ReferenceEquals(m, mesh.sharedMaterials[0]) && !ReferenceEquals(m, external.sharedMaterials[0]));
        }

        [Fact] public void DisposalIsIdempotentAndPreservesPreviouslyHiddenSourceState()
        {
            var root = new GameObject("Saber"); var mesh = Mesh(root); var trail = Child(root, "Trail").AddComponent<TrailRenderer>();
            mesh.forceRenderingOff = true; var clone = Copy(root);
            var copy = clone.Transforms[root.transform].GetComponent<MeshRenderer>(); var material = copy.sharedMaterials[0];
            clone.Dispose(); clone.Dispose();
            Assert.True(mesh.forceRenderingOff); Assert.False(trail.forceRenderingOff);
            Assert.Equal(1, copy.DestroyCalls); Assert.Equal(1, material.DestroyCalls); Assert.False(mesh.sharedMaterials[0].Destroyed);
        }

        [Fact] public void DestroyedSnapshotCandidatesAreExcludedFromBindingsAndLookup()
        {
            var alive = new GameObject("Saber"); Mesh(alive); var transient = new GameObject("TransientUI"); Mesh(transient);
            var resolver = new SceneModelResolver(Clip(alive.transform)); var transientPath = SceneModelResolver.IdentityOf(transient.transform);
            UObject.Destroy(transient);
            Assert.Equal(new[] { SceneModelResolver.IdentityOf(alive.transform) }, resolver.RenderableRootChoices());
            Assert.Empty(resolver.FindSource(transientPath)); Assert.Empty(resolver.FindSource("TransientUI"));
            Assert.True(resolver.Resolve(new BindingProfile()).Ready);
            Assert.Single(new SceneModelResolver(Clip(alive.transform)).RenderableRootChoices());
        }
    }
}
