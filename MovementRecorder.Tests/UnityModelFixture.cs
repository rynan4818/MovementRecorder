using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

// Test doubles for object ownership, pose composition and Unity's destroyed-object semantics.
// They do not simulate rendering, skinning, audio, XR tracking or frame scheduling.
namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> Objects = new List<Object>();
        public virtual string name { get; set; }
        public bool Destroyed { get; private set; }
        public int DestroyCalls { get; private set; }
        public Object() { Objects.Add(this); }
        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Component
        {
            var clone = CloneControllerObject(original.gameObject);
            clone.transform.SetParent(parent, worldPositionStays);
            return clone.GetComponent<T>();
        }
        private static GameObject CloneControllerObject(GameObject original)
        {
            var clone = new GameObject(original.name);
            clone.transform.localPosition = original.transform.localPosition;
            clone.transform.localRotation = original.transform.localRotation;
            clone.transform.localScale = original.transform.localScale;
            foreach (var component in original.Components.Where(c => !(c is Transform)))
            {
                var copy = (Component)Activator.CreateInstance(component.GetType()); copy.gameObject = clone;
                clone.Components.Add(copy);
                foreach (var field in component.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    if (!field.IsInitOnly) field.SetValue(copy, field.GetValue(component));
            }
            foreach (var child in original.transform.Children) CloneControllerObject(child.gameObject).transform.SetParent(clone.transform, false);
            clone.SetActive(original.activeSelf);
            return clone;
        }
        public static void Destroy(Object value)
        {
            if (ReferenceEquals(value, null)) return;
            value.DestroyCalls++;
            if (value.Destroyed) return;
            value.Destroyed = true;
            if (value is GameObject gameObject)
            {
                foreach (var child in gameObject.transform.Children.ToArray()) Destroy(child.gameObject);
                foreach (var component in gameObject.Components) Destroy(component);
            }
        }
        protected void CheckAlive() { if (Destroyed) throw new InvalidOperationException("A destroyed Unity object was accessed"); }
        public static bool operator ==(Object left, Object right)
        {
            bool a = ReferenceEquals(left, null) || left.Destroyed;
            bool b = ReferenceEquals(right, null) || right.Destroyed;
            return a || b ? a == b : ReferenceEquals(left, right);
        }
        public static bool operator !=(Object left, Object right) => !(left == right);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;
        public override string name { get => gameObject.name; set => gameObject.name = value; }
        public T GetComponent<T>() where T : Component { CheckAlive(); return gameObject.GetComponent<T>(); }
        public T[] GetComponents<T>() where T : Component { CheckAlive(); return gameObject.GetComponents<T>(); }
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component => GetComponentsInChildren<T>(includeInactive).FirstOrDefault();
        public T GetComponentInParent<T>() where T : Component
        { for (var t = transform; t != null; t = t.parent) { var result = t.GetComponent<T>(); if (result != null) return result; } return null; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        { CheckAlive(); return gameObject.GetComponentsInChildren<T>(includeInactive); }
    }

    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new List<Component>();
        public Transform transform { get; }
        public bool activeSelf { get; private set; } = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public int layer;
        public TestScene scene => TestScene.Default;
        public GameObject(string name) { this.name = name; transform = AddComponent<Transform>(); }
        public void SetActive(bool value) { CheckAlive(); activeSelf = value; }
        public T AddComponent<T>() where T : Component, new()
        {
            CheckAlive(); var component = new T { gameObject = this }; Components.Add(component); return component;
        }
        public T GetComponent<T>() where T : Component { CheckAlive(); return Components.OfType<T>().FirstOrDefault(c => c != null); }
        public T[] GetComponents<T>() where T : Component { CheckAlive(); return Components.OfType<T>().Where(c => c != null).ToArray(); }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            CheckAlive();
            return (includeInactive || activeSelf ? GetComponents<T>() : Array.Empty<T>())
                .Concat(transform.Children.Where(t => t != null).SelectMany(t => t.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
        }
    }

    public sealed class TestScene { public static readonly TestScene Default = new TestScene(); public bool IsValid() => true; public bool isLoaded => true; public string name => "Model test"; }
    public sealed class Transform : Component, IEnumerable<Transform>
    {
        internal readonly List<Transform> Children = new List<Transform>();
        public Transform parent { get; private set; }
        public Vector3 localPosition, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 lossyScale => parent == null ? localScale : Vector3.Scale(parent.lossyScale, localScale);
        public Vector3 position
        {
            get => parent == null ? localPosition : parent.position + parent.rotation * Vector3.Scale(parent.lossyScale, localPosition);
            set => localPosition = parent == null ? value : Vector3.Divide(Quaternion.Inverse(parent.rotation) * (value - parent.position), parent.lossyScale);
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation;
            set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
        public void SetParent(Transform value, bool worldPositionStays)
        { CheckAlive(); parent?.Children.Remove(this); parent = value; parent?.Children.Add(this); }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        { CheckAlive(); this.position = position; this.rotation = rotation; }
        public int GetSiblingIndex() { CheckAlive(); return parent == null ? 0 : parent.Children.IndexOf(this); }
        public bool IsChildOf(Transform value) { for (var t = this; t != null; t = t.parent) if (t == value) return true; return false; }
        public IEnumerator<Transform> GetEnumerator() => Children.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        public static float Distance(Vector3 a, Vector3 b) { var d = a - b; return MathF.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z); }
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        private System.Numerics.Quaternion Numerics => new System.Numerics.Quaternion(x, y, z, w);
        private static Quaternion From(System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.Numerics));
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.Numerics * b.Numerics);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        { var p = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(v.x, v.y, v.z), q.Numerics); return new Vector3(p.X, p.Y, p.Z); }
        public static float Angle(Quaternion a, Quaternion b) => 2 * MathF.Acos(Math.Min(1, Math.Abs(System.Numerics.Quaternion.Dot(a.Numerics, b.Numerics)))) * 180 / MathF.PI;
    }
    public sealed class Mesh : Object { public int blendShapeCount; }
    public enum AnimatorCullingMode { AlwaysAnimate, CullUpdateTransforms, CullCompletely }
    public sealed class RuntimeAnimatorController : Object { }
    public sealed class Animator : Behaviour
    {
        private AnimatorCullingMode _cullingMode;
        public int CullingWrites;
        public AnimatorCullingMode cullingMode
        {
            get { CheckAlive(); return _cullingMode; }
            set { CheckAlive(); CullingWrites++; _cullingMode = value; }
        }
        public RuntimeAnimatorController runtimeAnimatorController;
        public float speed = 1;
        public bool applyRootMotion;
    }
    public sealed class Material : Object { public Material() { } public Material(Material source) { } }
    public sealed class MaterialPropertyBlock { public bool isEmpty => true; public void Clear() { } }
    public class Renderer : Component
    {
        public bool enabled = true, forceRenderingOff, receiveShadows;
        public int shadowCastingMode, lightProbeUsage, reflectionProbeUsage, motionVectorGenerationMode, sortingLayerID, sortingOrder;
        public Material[] sharedMaterials = Array.Empty<Material>();
        public Transform probeAnchor;
        public void GetPropertyBlock(MaterialPropertyBlock block) { }
        public void GetPropertyBlock(MaterialPropertyBlock block, int index) { }
        public void SetPropertyBlock(MaterialPropertyBlock block) { }
        public void SetPropertyBlock(MaterialPropertyBlock block, int index) { }
    }
    public sealed class MeshRenderer : Renderer { }
    public sealed class MeshFilter : Component { public Mesh sharedMesh; }
    public sealed class TrailRenderer : Renderer { }
    public sealed class LineRenderer : Renderer { }
    public sealed class ParticleSystemRenderer : Renderer { }
    public sealed class SkinnedMeshRenderer : Renderer
    {
        private readonly Dictionary<int, float> _blendShapeWeights = new Dictionary<int, float>();
        public int BlendShapeWrites;
        public Mesh sharedMesh;
        public object localBounds;
        public int quality;
        public bool updateWhenOffscreen, skinnedMotionVectors;
        public Transform[] bones = Array.Empty<Transform>();
        public Transform rootBone;
        public float GetBlendShapeWeight(int index)
        { CheckBlendShapeIndex(index); return _blendShapeWeights.TryGetValue(index, out var value) ? value : 0; }
        public void SetBlendShapeWeight(int index, float weight)
        { CheckBlendShapeIndex(index); _blendShapeWeights[index] = weight; BlendShapeWrites++; }
        private void CheckBlendShapeIndex(int index)
        {
            CheckAlive();
            if (sharedMesh == null || index < 0 || index >= sharedMesh.blendShapeCount) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
    public struct LOD
    {
        public float screenRelativeTransitionHeight, fadeTransitionWidth;
        public Renderer[] renderers;
        public LOD(float height, Renderer[] renderers) { screenRelativeTransitionHeight = height; this.renderers = renderers; fadeTransitionWidth = 0; }
    }
    public sealed class LODGroup : Component
    {
        public Vector3 localReferencePoint;
        public float size;
        public int fadeMode;
        public bool animateCrossFading, enabled = true;
        private LOD[] _lods = Array.Empty<LOD>();
        public void SetLODs(LOD[] lods) { _lods = lods; }
        public LOD[] GetLODs() => _lods;
    }
    public static class Resources
    {
        // Intentionally includes destroyed objects so consumers must apply Unity's null check.
        public static T[] FindObjectsOfTypeAll<T>() where T : Object => Object.Objects.OfType<T>().ToArray();
    }
}
