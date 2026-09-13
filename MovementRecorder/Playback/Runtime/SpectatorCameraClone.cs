using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MovementRecorder.Playback.Runtime
{
    internal static class SpectatorCameraClone
    {
        public static Camera Create(Camera source, Transform inactiveParent, Action<string> log = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (inactiveParent == null || inactiveParent.gameObject.activeInHierarchy)
                throw new InvalidOperationException("鑑賞カメラの複製先は非アクティブである必要があります。");

            // Suppress Awake/OnEnable at Instantiate, before any provider can register the clone.
            var camera = UnityEngine.Object.Instantiate(source, inactiveParent, false);
            try
            {
                camera.gameObject.SetActive(false);
                camera.enabled = false;
                camera.name = "Spectator Camera";
                camera.gameObject.tag = "Untagged";
                camera.targetTexture = null;

                var removed = new HashSet<string>();
                foreach (var component in camera.GetComponentsInChildren<Component>(true))
                    if (component != null && (component.transform != camera.transform || !Keep(component)))
                        removed.Add(component.GetType().FullName);
                foreach (var child in camera.transform.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);

                // Remove dependants first: Unity rejects removing a component while it is still required.
                var pending = camera.GetComponents<Component>().Where(c => c != null && !Keep(c)).ToList();
                while (pending.Count > 0)
                {
                    var next = pending.FirstOrDefault(candidate => !pending.Any(other => other != candidate && Requires(other, candidate)));
                    if (next == null) throw new InvalidOperationException("鑑賞カメラの不要コンポーネントに循環依存があります。");
                    UnityEngine.Object.DestroyImmediate(next);
                    if (next != null) throw new InvalidOperationException("鑑賞カメラの不要コンポーネントを除去できません。");
                    pending.Remove(next);
                }

                foreach (var bloom in camera.GetComponents<BloomPrePass>())
                {
                    // The source may share data with a LIV camera. Our destruction must not free its texture.
                    GameAccess.Set(bloom, "_bloomPrePassRenderData", null);
                    GameAccess.Set(bloom, "_renderData", null);
                    bloom.SetMode(BloomPrePass.Mode.RenderAndSetData);
                }
                foreach (var effect in camera.GetComponents<MainEffectController>())
                {
                    // OnEnable creates an ImageEffectController and binds it to this clone.
                    GameAccess.Set(effect, "_imageEffectController", null);
                    GameAccess.Set(effect, "afterImageEffectEvent", null);
                }
                try { log?.Invoke("Replay camera cloned; removed components: " + string.Join(", ", removed.OrderBy(s => s))); } catch { }
                camera.gameObject.SetActive(true); // Parent remains inactive until the whole spectator rig is ready.
                return camera;
            }
            catch { UnityEngine.Object.DestroyImmediate(camera.gameObject); throw; }
        }

        private static bool Keep(Component component)
        {
            var type = component.GetType();
            return component is Transform || type == typeof(Camera) || type == typeof(BloomPrePass) ||
                type == typeof(MainEffectController) || type == typeof(CameraDepthTextureMode);
        }

        private static bool Requires(Component owner, Component dependency)
        {
            var type = dependency.GetType();
            foreach (RequireComponent required in owner.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                if (Matches(required.m_Type0, type) || Matches(required.m_Type1, type) || Matches(required.m_Type2, type)) return true;
            return false;
        }
        private static bool Matches(Type required, Type actual) => required != null && required.IsAssignableFrom(actual);
    }
}
