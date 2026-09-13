using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Tweening;
using UnityEngine;

namespace MovementRecorder.Playback.Runtime
{
    // Capture the game's event handlers after initialization, before the first replay sample.
    // Replaying the event stream at each event's actual song time also reconstructs fades and v3 interpolation.
    internal sealed class EnvironmentReplayState
    {
        internal static bool IsSampling { get; private set; }
        private sealed class Value { public object Owner, Initial; public FieldInfo Field; }
        private readonly List<Value> _values = new List<Value>();
        private readonly HashSet<object> _captured = new HashSet<object>();
        private readonly List<Tween> _tweens = new List<Tween>();
        private readonly List<(Array Array, Array Initial)> _arrays = new List<(Array, Array)>();
        private readonly Dictionary<Behaviour, bool> _enabled = new Dictionary<Behaviour, bool>();
        private readonly Dictionary<GameObject, bool> _active = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Renderer, bool> _renderers = new Dictionary<Renderer, bool>();
        private readonly Dictionary<SongTimeTweeningManager, KeyValuePair<Tween, object>[]> _initialTweens = new Dictionary<SongTimeTweeningManager, KeyValuePair<Tween, object>[]>();
        private readonly Dictionary<TrackLaneRingsRotationEffect, TrackLaneRingsRotationEffect.RingRotationEffect[]> _ringEffects = new Dictionary<TrackLaneRingsRotationEffect, TrackLaneRingsRotationEffect.RingRotationEffect[]>();
        private readonly List<TrackLaneRing> _rings = new List<TrackLaneRing>();
        private readonly AudioTimeSyncController _audio;
        private float _lastTime;
        private double _fixedTime;
        private readonly Dictionary<Transform, (Vector3, Quaternion, Vector3)> _transforms = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
        private readonly SongTimeTweeningManager[] _managers;
        private readonly MethodInfo _tick = GameAccess.Method(typeof(TweeningManager), "LateUpdate");

        public EnvironmentReplayState(IEnumerable<CallbacksInTime> buckets, AudioTimeSyncController audio)
        {
            _audio = audio;
            _managers = Resources.FindObjectsOfTypeAll<SongTimeTweeningManager>().Where(m => m.gameObject.scene.IsValid() && m.gameObject.scene.isLoaded).ToArray();
            foreach (var bucket in buckets)
                foreach (string field in new[] { "_callbacks", "_callbacksWithSubtypeIdentifier" })
                    foreach (var list in GameAccess.Get<IDictionary>(bucket, field).Values)
                        foreach (BeatmapDataCallbackWrapper wrapper in (IEnumerable)list)
                            if (typeof(BeatmapEventData).IsAssignableFrom(wrapper.BasicBeatmapEventType))
                            {
                                var callback = GameAccess.Get<Delegate>(wrapper, "_callback");
                                foreach (var handler in callback.GetInvocationList()) Capture(handler.Target);
                            }
            foreach (var manager in _managers)
            {
                _initialTweens[manager] = GameAccess.Get<Dictionary<Tween, object>>(manager, "_ownerByTween").ToArray();
                foreach (var item in _initialTweens[manager]) Capture(item.Key);
            }
        }
        private void Capture(object owner)
        {
            if (owner == null || owner.GetType().Assembly != typeof(BeatmapCallbacksController).Assembly || !_captured.Add(owner)) return;
            if (owner is Tween tween) _tweens.Add(tween);
            if (owner is Behaviour behaviour) _enabled[behaviour] = behaviour.enabled;
            if (owner is TrackLaneRing ring) _rings.Add(ring);
            if (owner is TrackLaneRingsRotationEffect ringsEffect)
                _ringEffects[ringsEffect] = GameAccess.Get<List<TrackLaneRingsRotationEffect.RingRotationEffect>>(ringsEffect, "_activeRingRotationEffects")
                    .Select(r => new TrackLaneRingsRotationEffect.RingRotationEffect { progressPos = r.progressPos, rotationAngle = r.rotationAngle,
                        rotationStep = r.rotationStep, rotationFlexySpeed = r.rotationFlexySpeed, rotationPropagationSpeed = r.rotationPropagationSpeed }).ToArray();
            for (Type type = owner.GetType(); type != null && type.Assembly == typeof(BeatmapCallbacksController).Assembly; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object value = field.GetValue(owner); Type kind = field.FieldType;
                    if (value is Tween child) { Capture(child); continue; }
                    if (value is Transform transform)
                    {
                        if (!_transforms.ContainsKey(transform)) _transforms[transform] = (transform.localPosition, transform.localRotation, transform.localScale);
                        continue;
                    }
                    if (value is GameObject gameObject) { _active[gameObject] = gameObject.activeSelf; continue; }
                    if (value is Renderer renderer) { _renderers[renderer] = renderer.enabled; continue; }
                    if (value is Array array && IsValue(kind.GetElementType())) { _arrays.Add((array, (Array)array.Clone())); continue; }
                    if (value is TrackLaneRingsManager ringManager)
                    {
                        if (ringManager.Rings != null) foreach (var item in ringManager.Rings) Capture(item);
                        continue;
                    }
                    if (value is TrackLaneRingsRotationEffect || value is LightPairRotationEventEffect.RotationData || value is LightPairSinMoveEventEffect.MovementData)
                    { Capture(value); continue; }
                    if (!field.IsInitOnly && (IsValue(kind) || owner is Tween && typeof(Delegate).IsAssignableFrom(kind)))
                        _values.Add(new Value { Owner = owner, Field = field, Initial = value });
                }
        }
        private static bool IsValue(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(Vector3) || type == typeof(Vector4) ||
            type == typeof(Vector2) || type == typeof(Quaternion) || type == typeof(Color);
        public void Reset()
        {
            _lastTime = 0; _fixedTime = 0;
            foreach (var manager in _managers)
            {
                foreach (var tween in GameAccess.Get<List<Tween>>(manager, "_activeTweens").ToArray()) tween.Kill();
                _tick.Invoke(manager, null);
            }
            foreach (var item in _values) item.Field.SetValue(item.Owner, item.Initial);
            foreach (var item in _arrays) Array.Copy(item.Initial, item.Array, item.Array.Length);
            foreach (var item in _enabled) if (item.Key != null) item.Key.enabled = item.Value;
            foreach (var item in _active) if (item.Key != null) item.Key.SetActive(item.Value);
            foreach (var item in _renderers) if (item.Key != null) item.Key.enabled = item.Value;
            foreach (var item in _transforms) if (item.Key != null)
            { item.Key.localPosition = item.Value.Item1; item.Key.localRotation = item.Value.Item2; item.Key.localScale = item.Value.Item3; }
            foreach (var tween in _tweens) GameAccess.Call(tween, "ForceOnUpdate");
            foreach (var manager in _initialTweens)
                foreach (var tween in manager.Value)
                    GameAccess.Method(typeof(TweeningManager), "AddTweenToDataStructures").Invoke(manager.Key, new[] { (object)tween.Key, tween.Value });
            foreach (var item in _ringEffects)
            {
                var effects = GameAccess.Get<List<TrackLaneRingsRotationEffect.RingRotationEffect>>(item.Key, "_activeRingRotationEffects");
                foreach (var effect in effects) item.Key.RecycleRingRotationEffect(effect);
                effects.Clear();
                foreach (var effect in item.Value)
                {
                    item.Key.AddRingRotationEffect(effect.rotationAngle, effect.rotationStep, effect.rotationPropagationSpeed, effect.rotationFlexySpeed);
                    effects[effects.Count - 1].progressPos = effect.progressPos;
                }
            }
            foreach (var buffered in _captured.OfType<BufferedLightColorGroupEffect>())
            {
                GameAccess.Set(buffered, "_didReceiveEventThisFrame", true);
                buffered.HandleBeatmapCallbacksControllerDidProcessAllCallbacksThisFrame();
            }
        }
        public void Sample()
        {
            IsSampling = true;
            try { SampleCore(); }
            finally { IsSampling = false; }
        }
        private void SampleCore()
        {
            float time = _audio.songTime;
            GameAccess.Set(_audio, "_lastFrameDeltaSongTime", Mathf.Max(0, time - _lastTime));
            foreach (var effect in _captured.OfType<LightRotationEventEffect>()) if (effect.enabled) effect.Update();
            foreach (var effect in _captured.OfType<LightPairRotationEventEffect>()) if (effect.enabled) effect.Update();
            foreach (var effect in _captured.OfType<LightPairSinMoveEventEffect>()) if (effect.enabled) effect.Update();
            // These native effects are integrated on the same fixed time grid on every seek.
            double step = Math.Max(.001, Time.fixedDeltaTime);
            while (_rings.Count > 0 && _fixedTime + step <= time)
            {
                foreach (var effect in _ringEffects.Keys) effect.FixedUpdate();
                foreach (var ring in _rings) ring.FixedUpdateRing((float)step);
                _fixedTime += step;
            }
            foreach (var ring in _rings) ring.LateUpdateRing(1);
            foreach (var manager in _managers) if (manager != null) _tick.Invoke(manager, null);
            foreach (var buffered in _captured.OfType<BufferedLightColorGroupEffect>()) buffered.HandleBeatmapCallbacksControllerDidProcessAllCallbacksThisFrame();
            _lastTime = time;
        }
        public void BeforeEvent()
        {
            // A synchronous seek processes many virtual frames inside a single Unity frame.
            foreach (var effect in _captured.OfType<LightPairRotationEventEffect>()) GameAccess.Set(effect, "_randomGenerationFrameNum", -1);
            foreach (var effect in _captured.OfType<LightPairSinMoveEventEffect>()) GameAccess.Set(effect, "_randomGenerationFrameNum", -1);
        }
    }
}
