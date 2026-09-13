using System;
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
        public float ObserverX { get; set; }
        public float ObserverY { get; set; }
        public float ObserverZ { get; set; } = -2;
        private Submission _submission;
        private Ticket _ticket;
        public int? GameplaySceneHandle { get; private set; }
        public int? DestroyedAtFrame { get; private set; }
        public ReplaySession() { Current = this; }
        public void SetPhase(ReplayPhase phase) { Phase = phase; }
        public void Begin(MovementClip clip)
        {
            if (IsActive) throw new InvalidOperationException("リプレイは既に実行中です。");
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
