using System;
using System.Collections.Generic;
using System.Linq;
using MovementRecorder.Models;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Models;
using MovementRecorder.Playback.Runtime;
using UnityEngine;
using Xunit;
using UObject = UnityEngine.Object;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class CameraLayerPlaybackTests : IDisposable
    {
        public CameraLayerPlaybackTests() { UObject.Objects.Clear(); ActivationProbe.Starts = 0; }
        public void Dispose() { UObject.Objects.Clear(); }
        public sealed class ActivationProbe : MonoBehaviour { public static int Starts; public void Awake() { Starts++; } }
        public sealed class RequiredControl : MonoBehaviour { }
        [RequireComponent(typeof(RequiredControl))] public sealed class DependentControl : MonoBehaviour { }
        [RequireComponent(typeof(CycleB))] public sealed class CycleA : MonoBehaviour { }
        [RequireComponent(typeof(CycleA))] public sealed class CycleB : MonoBehaviour { }
        private static GameObject Inactive(string name) { var result = new GameObject(name); result.SetActive(false); return result; }
        private static GameObject Child(GameObject parent, string name, int layer)
        { var child = new GameObject(name); child.transform.SetParent(parent.transform, false); child.layer = layer; return child; }
        private static MeshRenderer Mesh(GameObject owner)
        { owner.AddComponent<MeshFilter>().sharedMesh = new Mesh(); return owner.AddComponent<MeshRenderer>(); }
        private static RenderModelClone Copy(GameObject root)
        {
            var header = new MovementJson { objectCount = 1, recordCount = 2,
                objectNames = new List<string> { SceneModelResolver.PathOf(root.transform) },
                objectScales = new List<Scale> { new Scale { x = 1, y = 1, z = 1 } },
                Settings = new List<Setting> { new Setting { type = "Other", searchStirngs = new List<string> { ".*" } } } };
            var clip = new MovementClip(header, null, new[] { 0f, 1f }, new[] { new RecordedPose { Qw = 1 }, new RecordedPose { Qw = 1 } }, new[] { float.PositiveInfinity });
            return new RenderModelClone(clip, new ModelBindingPlan { CloneRoots = new[] { root.transform }, Sources = new[] { root.transform } }, false);
        }

        [Fact] public void CameraCloneKeepsRenderingButNeverStartsCopiedControlsOrSharesRuntimeRenderData()
        {
            var source = Inactive("Main HMD"); source.tag = "MainCamera"; source.layer = 7;
            var camera = source.AddComponent<Camera>(); camera.nearClipPlane = .02f; camera.farClipPlane = 600; camera.cullingMask = (1 << 8) | (1 << 10);
            camera.targetTexture = new RenderTexture();
            source.AddComponent<MainCamera>(); source.AddComponent<AudioListener>(); source.AddComponent<ActivationProbe>();
            var child = Child(source, "Mod child", 0); child.AddComponent<ActivationProbe>(); child.AddComponent<Camera>();
            var data = new BloomPrePassRenderDataSO(); data.data.bloomPrePassRenderTexture = new RenderTexture();
            var bloom = source.AddComponent<BloomPrePass>(); GameAccess.Set(bloom, "_bloomPrePassRenderData", data); bloom.SetMode(BloomPrePass.Mode.SetDataOnly);
            var effect = source.AddComponent<MainEffectController>(); effect._mainEffectContainer = new UObject();
            var depth = source.AddComponent<CameraDepthTextureMode>(); depth._depthTextureMode = 3;
            source.SetActive(true);
            var sourceImageEffect = source.GetComponent<ImageEffectController>();
            int notifications = 0; GameAccess.Set(effect, "afterImageEffectEvent", new Action<RenderTexture>(_ => notifications++));
            var stage = Inactive("Spectator stage"); var logs = new List<string>();
            var clone = SpectatorCameraClone.Create(camera, stage.transform, logs.Add);
            Assert.Equal(2, ActivationProbe.Starts); Assert.False(clone.gameObject.activeInHierarchy);
            Assert.Null(clone.GetComponent<MainCamera>()); Assert.Null(clone.GetComponent<AudioListener>());
            Assert.Null(clone.GetComponent<ActivationProbe>()); Assert.Empty(clone.transform.Children);
            Assert.Equal("Untagged", clone.gameObject.tag); Assert.Equal(7, clone.gameObject.layer);
            Assert.Equal(camera.nearClipPlane, clone.nearClipPlane); Assert.Equal(camera.farClipPlane, clone.farClipPlane);
            Assert.Equal(camera.cullingMask, clone.cullingMask); Assert.Null(clone.targetTexture);
            Assert.Null(clone.GetComponent<ImageEffectController>());
            Assert.Same(effect._mainEffectContainer, clone.GetComponent<MainEffectController>()._mainEffectContainer);
            var copyBloom = clone.GetComponent<BloomPrePass>();
            Assert.Null(GameAccess.Get<BloomPrePassRenderDataSO>(copyBloom, "_bloomPrePassRenderData"));
            Assert.Equal(BloomPrePass.Mode.RenderAndSetData, GameAccess.Get<BloomPrePass.Mode>(copyBloom, "_mode"));
            Assert.Contains(nameof(ActivationProbe), Assert.Single(logs));
            stage.SetActive(true);
            Assert.Equal(2, ActivationProbe.Starts); Assert.Equal(3, clone.depthTextureMode);
            Assert.Same(clone.GetComponent<MainEffectController>(), clone.GetComponent<ImageEffectController>().Owner);
            Assert.Same(effect, sourceImageEffect.Owner);
            clone.GetComponent<MainEffectController>().RenderTestFrame(); Assert.Equal(0, notifications);
            effect.RenderTestFrame(); Assert.Equal(1, notifications);
            var copyData = GameAccess.Get<BloomPrePassRenderDataSO.Data>(copyBloom, "_renderData");
            Assert.NotSame(data.data, copyData); var copyTexture = copyData.bloomPrePassRenderTexture = new RenderTexture();
            UObject.Destroy(stage);
            Assert.True(copyTexture.Released); Assert.False(data.data.bloomPrePassRenderTexture.Released);
            Assert.True(camera.enabled); Assert.True(source.activeInHierarchy); Assert.Equal("MainCamera", source.tag);
            Assert.NotNull(camera.targetTexture); Assert.False(sourceImageEffect.Destroyed); Assert.False(bloom.Destroyed);
        }

        [Fact] public void ActiveParentIsRejectedBeforeAnyCameraIsCloned()
        {
            var source = new GameObject("Main").AddComponent<Camera>(); var parent = new GameObject("Active parent");
            int count = UObject.Objects.Count;
            Assert.Throws<InvalidOperationException>(() => SpectatorCameraClone.Create(source, parent.transform));
            Assert.Equal(count, UObject.Objects.Count); Assert.True(source.enabled);
        }

        [Fact] public void UnwantedControlsAreRemovedInDependencyOrder()
        {
            var source = Inactive("Main"); var camera = source.AddComponent<Camera>();
            source.AddComponent<DependentControl>(); source.AddComponent<RequiredControl>();
            var stage = Inactive("Stage"); var clone = SpectatorCameraClone.Create(camera, stage.transform);
            Assert.Null(clone.GetComponent<DependentControl>()); Assert.Null(clone.GetComponent<RequiredControl>());
            Assert.NotNull(source.GetComponent<DependentControl>()); Assert.NotNull(source.GetComponent<RequiredControl>());
        }

        [Fact] public void DependencyCycleCleansUpBeforeActivation()
        {
            var source = Inactive("Main"); var camera = source.AddComponent<Camera>();
            source.AddComponent<CycleA>(); source.AddComponent<CycleB>(); source.AddComponent<ActivationProbe>();
            var stage = Inactive("Stage");
            Assert.Contains("circular dependencies", Assert.Throws<InvalidOperationException>(() => SpectatorCameraClone.Create(camera, stage.transform)).Message);
            Assert.Empty(stage.transform.Children); Assert.Equal(0, ActivationProbe.Starts); Assert.False(camera.Destroyed);
        }

        [Fact] public void DisabledRenderingComponentsStayDisabledAndMissingEffectsAreNotInvented()
        {
            var source = Inactive("Main"); var camera = source.AddComponent<Camera>(); source.AddComponent<BloomPrePass>().enabled = false;
            var stage = Inactive("Stage"); var clone = SpectatorCameraClone.Create(camera, stage.transform); stage.SetActive(true);
            Assert.False(clone.GetComponent<BloomPrePass>().enabled);
            Assert.Null(clone.GetComponent<MainEffectController>()); Assert.Null(clone.GetComponent<ImageEffectController>());
        }

        [Theory] [InlineData(0)] [InlineData(3)] [InlineData(6)] [InlineData(10)] [InlineData(31)]
        public void ModelPreservesAncestorAndPerObjectLayersAndFollowsChanges(int layer)
        {
            var ancestor = new GameObject("Provider root") { layer = 7 };
            var root = Child(ancestor, "Avatar", 10); Mesh(root);
            var face = Child(root, "Face", layer); Mesh(face); Child(root, "Non-rendered bone", 5);
            using var clone = Copy(root);
            Assert.Equal(7, clone.Transforms[ancestor.transform].gameObject.layer);
            Assert.Equal(layer, clone.Transforms[face.transform].gameObject.layer);
            Assert.Equal((1 << 10) | (1 << layer), clone.SyncLiveRendererLayers());
            face.layer = 12; root.layer = 3;
            Assert.Equal((1 << 12) | (1 << 3), clone.SyncLiveRendererLayers());
            Assert.Equal(12, clone.Transforms[face.transform].gameObject.layer);
            Assert.Equal(3, clone.Transforms[root.transform].gameObject.layer);
            clone.Dispose(); Assert.Equal(12, face.layer); Assert.Equal(3, root.layer); Assert.Equal(0, clone.SyncLiveRendererLayers());
        }

        [Fact] public void LayerChangesRemainLiveWhileExpressionsArePaused()
        {
            var root = new GameObject("Avatar"); var face = Child(root, "Face", 3);
            var skin = face.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = new Mesh { blendShapeCount = 1 }; skin.SetBlendShapeWeight(0, 20);
            using var clone = Copy(root); var copy = clone.Transforms[face.transform].GetComponent<SkinnedMeshRenderer>();
            face.layer = 10; skin.SetBlendShapeWeight(0, 70);
            clone.Apply(.5f); clone.SyncLiveExpressions(false);
            Assert.Equal(1 << 10, clone.SyncLiveRendererLayers()); Assert.Equal(10, copy.gameObject.layer); Assert.Equal(20, copy.GetBlendShapeWeight(0));
            clone.SyncLiveExpressions(true); Assert.Equal(70, copy.GetBlendShapeWeight(0));
            UObject.Destroy(copy); Assert.Equal(0, clone.SyncLiveRendererLayers());
        }
    }
}
