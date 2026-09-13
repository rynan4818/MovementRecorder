using System;
using MovementRecorder.Configuration;
using MovementRecorder.Playback.Data;
using SiraUtil.Submissions;

namespace MovementRecorder.Playback
{
    public enum ReplayPhase { Idle, Loading, Ready, Starting, Binding, Playing, Paused, Seeking, Completed, Disposing }

    public static class MovementReplay
    {
        public static bool IsActive => ReplaySession.Current?.IsActive == true;
        public static Guid SessionId => ReplaySession.Current?.Id ?? Guid.Empty;
        public static ReplayPhase Phase => ReplaySession.Current?.Phase ?? ReplayPhase.Idle;
        public static event Action<Guid, bool> SessionChanged;
        public static event Action<Guid, float, bool> SeekChanged;
        internal static void NotifySession(Guid id, bool active) => Notify(() => SessionChanged?.Invoke(id, active));
        internal static void NotifySeek(Guid id, float time, bool starting) => Notify(() => SeekChanged?.Invoke(id, time, starting));
        private static void Notify(Action action) { try { action(); } catch (Exception ex) { Plugin.Log?.Warn(ex.ToString()); } }
    }

    internal sealed class ReplaySession
    {
        public const string GameMode = "MovementRecorderReplay";
        internal static ReplaySession Current { get; private set; }
        public Guid Id { get; private set; }
        public ReplayPhase Phase { get; private set; }
        public bool IsActive => Phase >= ReplayPhase.Starting;
        public MovementClip Clip { get; private set; }
        public string Error { get; private set; }
        public bool NoFail { get; set; } = true;
        public bool ShowSourceAvatar { get; private set; }
        public bool OffsetSourceAvatarWithHmd { get; private set; }
        private float _observerX, _observerY, _observerZ = -2;
        // Both menu controls and in-game controls use these setters. Only edits write configuration.
        public float ObserverX
        {
            get => _observerX;
            set { _observerX = ObserverEdit(value, -5, 5); var config = PluginConfig.Instance;
                if (config != null && config.replayObserverX != _observerX) config.replayObserverX = _observerX; }
        }
        public float ObserverY
        {
            get => _observerY;
            set { _observerY = ObserverEdit(value, -3, 3); var config = PluginConfig.Instance;
                if (config != null && config.replayObserverY != _observerY) config.replayObserverY = _observerY; }
        }
        public float ObserverZ
        {
            get => _observerZ;
            set { _observerZ = ObserverEdit(value, -8, 5, -2); var config = PluginConfig.Instance;
                if (config != null && config.replayObserverZ != _observerZ) config.replayObserverZ = _observerZ; }
        }
        private Submission _submission;
        private Ticket _ticket;
        public int? GameplaySceneHandle { get; private set; }
        public int? DestroyedAtFrame { get; private set; }
        public ReplaySession() { Current = this; LoadObserverPosition(); }
        private static float ObserverEdit(float value, float min, float max, float fallback = 0)
        {
            value = PluginConfig.ObserverValue(value, min, max, fallback);
            // BSML accumulates float increments. Remove noise around tenths, especially when returning to zero.
            float tenth = (float)(Math.Round((double)value * 10) / 10);
            return Math.Abs(value - tenth) < .0001f ? tenth : value;
        }
        public void LoadObserverPosition()
        {
            if (IsActive) return;
            var config = PluginConfig.Instance;
            if (config == null) return;
            config.ValidateObserverPosition();
            _observerX = config.replayObserverX; _observerY = config.replayObserverY; _observerZ = config.replayObserverZ;
        }
        public void SetPhase(ReplayPhase phase) { Phase = phase; }
        public void Begin(MovementClip clip, bool showSourceAvatar = false, bool offsetSourceAvatarWithHmd = false)
        {
            if (IsActive) throw new InvalidOperationException("リプレイは既に実行中です。");
            ShowSourceAvatar = showSourceAvatar;
            OffsetSourceAvatarWithHmd = offsetSourceAvatarWithHmd;
            Clip = clip; Error = null; Id = Guid.NewGuid(); Phase = ReplayPhase.Starting; GameplaySceneHandle = DestroyedAtFrame = null;
            MovementReplay.NotifySession(Id, true);
        }
        public void DisableSubmission(Submission submission)
        {
            _submission = submission ?? throw new InvalidOperationException("スコア送信の抑止を準備できません。");
            _ticket = submission.DisableScoreSubmission("MovementRecorder", "Replay");
        }
        public void Fail(string message) { Error = message; Phase = ReplayPhase.Paused; }
        public void ClearError() { Error = null; }
        public void RuntimeStarted(int scene) { GameplaySceneHandle = scene; DestroyedAtFrame = null; }
        public void RuntimeDestroyed(int frame) { DestroyedAtFrame = frame; }
        public void Finish()
        {
            if (Phase == ReplayPhase.Idle) return;
            Guid id = Id; Phase = ReplayPhase.Disposing;
            // Only after MenuTransitionsHelper has popped the gameplay scene and run its teardown.
            if (_ticket != null) _submission?.Remove(_ticket);
            _ticket = null; _submission = null; Clip = null; Id = Guid.Empty; Phase = ReplayPhase.Idle;
            GameplaySceneHandle = DestroyedAtFrame = null;
            MovementReplay.NotifySession(id, false);
        }
    }
}
