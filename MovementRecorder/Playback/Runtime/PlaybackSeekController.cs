using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MovementRecorder.Playback.Runtime
{
    internal sealed class PlaybackSeekController
    {
        private sealed class EventCall { public float Time; public int Order; public CallbacksInTime Bucket; public BeatmapEventData Event; }
        private readonly AudioTimeSyncController _audio;
        private readonly BeatmapCallbacksController _callbacks;
        private readonly BeatmapObjectManager _objects;
        private readonly NoteCutSoundEffectManager _sounds;
        private readonly GameSongController _song;
        private readonly IVariableMovementDataProvider _movement;
        private readonly LinkedList<BeatmapDataItem> _items;
        private readonly CallbacksInTime[] _buckets;
        private readonly EventCall[] _events;
        private readonly EnvironmentReplayState _environment;
        private readonly BeatmapCallbacksController.ICallCallbacksBehavior _callbackBehavior;
        private readonly NativeSegmentState _segment;
        private readonly Dictionary<Type, MethodInfo> _despawn = new Dictionary<Type, MethodInfo>();
        private readonly List<IBeatmapObjectController> _active;
        public bool IsSeeking { get; private set; }
        public NativeSegmentState Segment => _segment;

        public PlaybackSeekController(AudioTimeSyncController audio, BeatmapCallbacksController callbacks, BeatmapObjectManager objects,
            BeatmapObjectSpawnController spawn, NoteCutSoundEffectManager sounds, GameSongController song, NativeSegmentState segment)
        {
            _audio = audio; _callbacks = callbacks; _objects = objects; _sounds = sounds; _song = song; _segment = segment;
            _movement = GameAccess.Get<IVariableMovementDataProvider>(spawn, "_variableMovementDataProvider");
            _items = GameAccess.Get<IReadonlyBeatmapData>(callbacks, "_beatmapData").allBeatmapDataItems;
            _buckets = GameAccess.Get<Dictionary<float, CallbacksInTime>>(callbacks, "_callbacksInTimes").Values.ToArray();
            _active = GameAccess.Get<List<IBeatmapObjectController>>(objects, "_allBeatmapObjects");
            foreach (Type type in new[] { typeof(NoteController), typeof(ObstacleController), typeof(SliderController) })
                _despawn[type] = AccessTools.Method(typeof(BeatmapObjectManager), "Despawn", new[] { type }) ?? throw new MissingMethodException("BeatmapObjectManager.Despawn");
            var events = new List<EventCall>(); int order = 0;
            foreach (var item in _items)
                if (item is BeatmapEventData beatmapEvent)
                    foreach (var bucket in _buckets) events.Add(new EventCall { Time = item.time - bucket.aheadTime, Order = order++, Bucket = bucket, Event = beatmapEvent });
            _events = events.OrderBy(e => e.Time).ThenBy(e => e.Order).ToArray();
            _environment = new EnvironmentReplayState(_buckets, audio);
            _callbackBehavior = GameAccess.Get<BeatmapCallbacksController.ICallCallbacksBehavior>(callbacks, "_callCallbacksBehavior");
            foreach (string name in new[] { "_prevSongTime", "_songTime", "_startFilterTime" }) GameAccess.Field(typeof(BeatmapCallbacksController), name);
            GameAccess.Field(typeof(AudioTimeSyncController), "_startSongTime"); GameAccess.Field(typeof(AudioTimeSyncController), "_songTime");
        }

        public void Seek(float target, Action<float> applyModels, Action<float> prepareSabers)
        {
            if (IsSeeking) throw new InvalidOperationException("A seek is already in progress.");
            if (!_audio.isReady || float.IsNaN(target) || float.IsInfinity(target)) throw new InvalidOperationException("Cannot change the song position.");
            IsSeeking = true; MovementReplay.NotifySeek(MovementReplay.SessionId, target, true);
            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(1729);
                _audio.Pause();
                StopCutSounds(); _segment.Reset(); DespawnAll();
                if (_callbackBehavior is BeatmapCallbacksController.CallCallbacksBehaviorWithLastState)
                    GameAccess.Get<Dictionary<(Type, int), BeatmapDataItem>>(_callbackBehavior, "_replayState").Clear();
                GameAccess.Set(_audio, "_songTime", 0f); _environment.Reset();
                // Reconstruct event state chronologically, using the game clock for every tween creation.
                // This is an in-scene operation; neither the chart nor the recording is reloaded.
                foreach (var call in _events)
                {
                    if (call.Time > target) break;
                    GameAccess.Set(_audio, "_songTime", call.Time); _environment.Sample();
                    _environment.BeforeEvent(); _callbackBehavior.CallCallbacks(call.Bucket, call.Event);
                }
                GameAccess.Set(_audio, "_songTime", target); _environment.Sample();
                foreach (var bucket in _buckets)
                {
                    LinkedListNode<BeatmapDataItem> last = null;
                    for (var node = _items.First; node != null && node.Value.time - bucket.aheadTime <= target; node = node.Next)
                    {
                        var item = node.Value; last = node;
                        if (item is BeatmapObjectData && StillVisible(item, target)) _callbackBehavior.CallCallbacks(bucket, item);
                    }
                    bucket.lastProcessedNode = last; bucket.beatmapEventDataForCallbacksAfterNodeRemoval = null;
                }
                float start = GameAccess.Get<float>(_audio, "_startSongTime");
                _audio.SeekTo((target - start) / _audio.timeScale);
                GameAccess.Set(_audio, "_lastFrameDeltaSongTime", 0f);
                GameAccess.Set(_callbacks, "_prevSongTime", target); GameAccess.Set(_callbacks, "_songTime", target);
                GameAccess.Set(_song, "_songDidFinish", false);
                applyModels(target);
                foreach (var item in _active.ToArray())
                {
                    item.Pause(false); item.Hide(false);
                    if (item is NoteController note) note.ManualUpdate();
                    else if (item is ObstacleController wall) wall.ManualUpdate();
                    else if (item is SliderController slider) slider.ManualUpdate();
                }
                prepareSabers(target);
                _objects.PauseAllBeatmapObjects(true);
                _objects.HideAllBeatmapObjects(false);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                GameAccess.Set(_audio, "_lastFrameDeltaSongTime", 0f);
                IsSeeking = false; MovementReplay.NotifySeek(MovementReplay.SessionId, target, false);
            }
        }
        private bool StillVisible(BeatmapDataItem item, float target)
        {
            float end = item.time;
            if (item is ObstacleData wall) end += wall.duration;
            else if (item is SliderData slider) end = slider.tailTime;
            return end + _movement.jumpDuration * .5f + .01f >= target;
        }
        private void StopCutSounds()
        {
            var pool = GameAccess.Get<MemoryPoolContainer<NoteCutSoundEffect>>(_sounds, "_noteCutSoundEffectPoolContainer");
            if (pool != null) foreach (var sound in pool.activeItems.ToArray()) sound.StopPlayingAndFinish();
            GameAccess.Set(_sounds, "_prevNoteATime", -1f); GameAccess.Set(_sounds, "_prevNoteBTime", -1f);
        }
        private void DespawnAll()
        {
            foreach (var item in _active.ToArray())
            {
                item.Pause(false);
                Type type = item is NoteController ? typeof(NoteController) : item is ObstacleController ? typeof(ObstacleController) :
                    item is SliderController ? typeof(SliderController) : null;
                if (type == null) throw new InvalidOperationException("Unsupported map objects remain in the scene.");
                _despawn[type].Invoke(_objects, new object[] { item });
            }
        }
    }
}
