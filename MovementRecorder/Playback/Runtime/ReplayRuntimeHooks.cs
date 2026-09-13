using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace MovementRecorder.Playback.Runtime
{
    internal static class ReplayRuntimeHooks
    {
        private static readonly Harmony Harmony = new Harmony(Plugin.HARMONY_ID + ".ReplayRuntime");
        private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();
        private static bool Active => MovementReplay.IsActive;
        private static bool Blocked => Active && (PlaybackRuntime.Current == null || PlaybackRuntime.Current.JudgementBlocked);
        public static bool NativePauseFallback { get; set; }
        public static void Prepare()
        {
            Hook(typeof(SaberManager), "Update", nameof(Sabers));
            Hook(typeof(PauseController), "Pause", nameof(Pause));
            Hook(typeof(PauseController), "HandlePauseMenuManagerDidPressContinueButton", nameof(Resume));
            Hook(typeof(PauseController), "HandlePauseMenuManagerDidFinishResumeAnimation", nameof(Resume));
            Hook(typeof(PauseController), "HandlePauseMenuManagerDidPressRestartButton", nameof(Restart));
            Hook(typeof(StandardLevelGameplayManager), "HandleSongDidFinish", nameof(Completed));
            Hook(typeof(StandardLevelGameplayManager), "HandleGameEnergyDidReach0", nameof(Failed));
            Hook(typeof(AudioTimeSyncController), "StartSong", nameof(AudioStarted), true);
            Hook(typeof(AudioTimeSyncController), "Update", nameof(AudioUpdate));
            Hook(typeof(NoteController), "HandleNoteDidPassMissedMarkerEvent", nameof(NoteMissed));
            Hook(typeof(ScoreController), "LateUpdate", nameof(Judgement));
            Hook(typeof(GameEnergyCounter), "LateUpdate", nameof(Judgement));
            Hook(typeof(PlayerHeadAndObstacleInteraction), "Update", nameof(Judgement));
            Hook(typeof(NoteCutSoundEffectManager), "HandleNoteWasSpawned", nameof(Judgement));
            foreach (var type in new[] { typeof(ScoreController), typeof(ComboController), typeof(GameEnergyCounter) })
            {
                Hook(type, "HandleNoteWasCut", nameof(Judgement)); Hook(type, "HandleNoteWasMissed", nameof(Judgement));
            }
            Hook(typeof(BeatmapObjectExecutionRatingsRecorder), "HandleObstacleDidPassAvoidedMark", nameof(Judgement));
            Hook(typeof(BeatmapObjectExecutionRatingsRecorder), "HandlePlayerHeadDidEnterObstacle", nameof(Judgement));
            Hook(typeof(TrackLaneRingsRotationEffect), "FixedUpdate", nameof(EnvironmentUpdate));
            Hook(typeof(TrackLaneRingsManager), "FixedUpdate", nameof(EnvironmentUpdate));
            Hook(typeof(TrackLaneRingsManager), "LateUpdate", nameof(EnvironmentUpdate));
            foreach (var type in new[] { typeof(LightRotationEventEffect), typeof(LightPairRotationEventEffect), typeof(LightPairSinMoveEventEffect) })
                Hook(type, "Update", nameof(EnvironmentUpdate));
        }
        private static void Hook(Type type, string name, string callback, bool postfix = false)
        {
            var method = GameAccess.Method(type, name);
            if (Patched.Contains(method)) return;
            var patch = new HarmonyMethod(typeof(ReplayRuntimeHooks), callback) { priority = Priority.Last };
            Harmony.Patch(method, prefix: postfix ? null : patch, postfix: postfix ? patch : null); Patched.Add(method);
        }
        private static bool Sabers() => !Active || PlaybackRuntime.Current?.BeforeSaberUpdate() == true;
        private static bool Judgement() => !Blocked;
        private static bool EnvironmentUpdate() => !Active || MovementReplay.Phase == ReplayPhase.Playing || EnvironmentReplayState.IsSampling;
        private static bool Pause()
        {
            if (!Active || NativePauseFallback) return true;
            PlaybackRuntime.Current?.Pause(true); return false;
        }
        private static bool Resume() { if (!Active || NativePauseFallback) return true; PlaybackRuntime.Current?.Resume(); return false; }
        private static bool Restart() { if (!Active) return true; PlaybackRuntime.Current?.SeekStart(); return false; }
        private static bool Completed() { if (!Active) return true; PlaybackRuntime.Current?.Complete(); return false; }
        private static bool Failed()
        {
            if (!Active) return true;
            if (ReplaySession.Current?.NoFail != true) PlaybackRuntime.Current?.Complete();
            return false;
        }
        private static void AudioStarted() { if (Active) PlaybackRuntime.Current?.HoldStartup(); }
        private static bool AudioUpdate(AudioTimeSyncController __instance)
        {
            if (!Active || MovementReplay.Phase == ReplayPhase.Playing) return true;
            GameAccess.Set(__instance, "_lastFrameDeltaSongTime", 0f); return false;
        }
        private static bool NoteMissed(NoteController __instance)
        {
            if (!Blocked) return true;
            PlaybackRuntime.Current?.RemoveExpiredNote(__instance.noteData.time); return false;
        }
    }
}
