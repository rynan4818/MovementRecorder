using System;
using System.Collections;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.FloatingScreen;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Models;
using MovementRecorder.Playback.UI;
using SiraUtil.Submissions;
using UnityEngine;
using Zenject;

namespace MovementRecorder.Playback.Runtime
{
    [DefaultExecutionOrder(-1000)]
    internal sealed class PlaybackRuntime : MonoBehaviour
    {
        public static PlaybackRuntime Current { get; private set; }
        [Inject] private readonly ReplaySession _session = null;
        [Inject] private readonly AudioTimeSyncController _audio = null;
        [Inject] private readonly BeatmapCallbacksController _callbacks = null;
        [Inject] private readonly BeatmapCallbacksUpdater _updater = null;
        [Inject] private readonly BeatmapObjectManager _objects = null;
        [Inject] private readonly BeatmapObjectSpawnController _spawn = null;
        [Inject] private readonly SaberManager _sabers = null;
        [Inject] private readonly PlayerTransforms _player = null;
        [Inject] private readonly PauseController _pause = null;
        [Inject] private readonly IGamePause _gamePause = null;
        [Inject] private readonly IReturnToMenuController _return = null;
        [Inject] private readonly NoteCutSoundEffectManager _sounds = null;
        [Inject] private readonly ScoreController _score = null;
        [Inject] private readonly ComboController _combo = null;
        [Inject] private readonly GameEnergyCounter _energy = null;
        [Inject] private readonly PlayerHeadAndObstacleInteraction _head = null;
        [Inject] private readonly BeatmapObjectExecutionRatingsRecorder _ratings = null;
        [Inject] private readonly GameSongController _song = null;
        [Inject] private readonly DiContainer _container = null;
        [Inject] private readonly Submission _submission = null;
        private SpectatorRig _rig;
        private RenderModelClone _model;
        private RecordedSaberDriver _driver;
        private PlaybackSeekController _seek;
        private FloatingScreen _screen;
        private ReplayControlsViewController _view;
        private Coroutine _binding;
        private bool _ready, _exiting, _dragging, _interaction, _wasPlaying, _pendingFinal;
        private float? _pending;
        private float? _audioPausedAt;
        private float _nextSeek;
        private bool _loggedClockAdvance;
        private float _resumedSongTime;
        private string _profilePath;
        public BindingProfile Profile { get; private set; }
        public SceneModelResolver Resolver { get; private set; }
        public ModelBindingPlan Plan { get; private set; }
        public ReplaySession Session => _session;
        public bool Ready => _ready;
        public bool JudgementBlocked => !_ready || _exiting || _session.Phase != ReplayPhase.Playing || _seek?.IsSeeking == true;
        public float StartTime => Mathf.Max(0, _session.Clip.StartTime);
        public float EndTime => Mathf.Min(_session.Clip.EndTime, _audio.songEndTime - .01f);
        public float TimePosition => _pending ?? _audio.songTime;
        public string Message { get; private set; } = "モデルを準備しています…";
        private string PlaybackMessage => "リプレイ：スコア・履歴は保存しません。" +
            (_model?.SkippedRenderers.Count > 0 ? "\n軌跡など一部の追加エフェクトを省略しています。" : "");

        private void Awake() { Current = this; gameObject.AddComponent<ReplayLatePoseWriter>().Runtime = this; }
        private IEnumerator Start()
        {
            _session.RuntimeStarted(gameObject.scene.handle);
            _session.SetPhase(ReplayPhase.Binding);
            try
            {
                _session.DisableSubmission(_submission);
                string signature = SceneModelResolver.Signature(_session.Clip);
                _profilePath = Path.Combine(IPA.Utilities.UnityGame.UserDataPath, "MovementRecorder", "Profiles", signature + ".json");
                Profile = JsonCache.Read<BindingProfile>(_profilePath, s => Plugin.Log?.Warn(s));
                if (Profile?.Version != 1 || Profile.RecordingSignature != signature || Profile.Roots == null || Profile.Tracks == null || Profile.Hierarchies == null || Profile.OmittedRoots == null)
                    Profile = new BindingProfile { RecordingSignature = signature };
            }
            catch (Exception ex) { Fail(ex); yield break; }
            yield return null;
            _binding = StartCoroutine(BindModels());
        }
        private IEnumerator BindModels()
        {
            float deadline = Time.unscaledTime + 10; Exception lastError = null;
            while (!_exiting && Time.unscaledTime < deadline)
            {
                if (!_audio.isReady || !GameAccess.Get<bool>(_spawn, "_isInitialized")) { yield return null; continue; }
                Pause(false);
                try
                {
                    if (_rig == null) _rig = new SpectatorRig(_session, _player, _container);
                    EnsurePanel();
                    if (_seek == null)
                        _seek = new PlaybackSeekController(_audio, _callbacks, _objects, _spawn, _sounds, _song,
                            new NativeSegmentState(_score, _combo, _energy, _head, _ratings));
                    Resolver = new SceneModelResolver(_session.Clip); Plan = Resolver.Resolve(Profile);
                    if (Plan.Ready)
                    {
                        _driver = new RecordedSaberDriver(_session.Clip, Plan, _sabers, Profile);
                        break;
                    }
                    Message = Plan.Issues.Count + " 件の対応を確認しています…";
                }
                catch (Exception ex) { lastError = ex; }
                yield return new WaitForSecondsRealtime(.25f);
            }
            if (_exiting) yield break;
            if (Plan?.Ready != true || _driver == null)
            {
                Message = lastError?.Message ?? "モデルを自動で対応付けできません。対応設定でルートを選ぶか、同じモデルを読み込んで再確認してください。";
                if (_view == null) Fail(lastError ?? new InvalidOperationException("曲の準備が完了しませんでした。曲選択へ戻って再試行してください。"));
                else
                {
                    Plugin.Log?.Warn(Message);
                    if (Plan != null) foreach (var issue in Plan.Issues.Take(8)) Plugin.Log?.Warn(issue.RecordedPath + ": " + issue.Message);
                    _view.ShowBindings();
                }
                _binding = null; yield break;
            }
            // Validate a fixed model-to-native-saber relationship across several live controller updates.
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                try { _driver.ValidateStableAnchors(); }
                catch (Exception ex) { Fail(ex); _binding = null; yield break; }
            }
            try
            {
                if (EndTime <= StartTime) throw new InvalidOperationException("記録と音声の再生範囲が重なりません。");
                _driver.TakeControl();
                _model = new RenderModelClone(_session.Clip, Plan, Profile.FreezeMissing, _driver.SaberRoots, s => Plugin.Log?.Warn(s));
                foreach (string skipped in _model.SkippedRenderers) Plugin.Log?.Warn("Replay renderer omitted: " + skipped);
                Plugin.Log?.Info($"Replay models ready: {Plan.Sources.Count(t => t != null)} tracks, {_model.ModelRootCount} cloned avatar/other roots, 2 live sabers, {StartTime:0.000}–{EndTime:0.000}s");
                _ready = true; _session.SetPhase(ReplayPhase.Seeking);
                _seek.Seek(StartTime, _model.Apply, _driver.PrepareHistory);
                Plugin.Log?.Info($"Replay initial seek and saber history ready at {_audio.songTime:0.000}s");
                _audioPausedAt = Time.timeSinceLevelLoad;
                _session.SetPhase(ReplayPhase.Paused); Message = PlaybackMessage;
                _view?.BindingFinished(); Resume();
            }
            catch (Exception ex) { Fail(ex); }
            _binding = null;
        }
        private void EnsurePanel()
        {
            if (_screen != null) return;
            _view = BeatSaberUI.CreateViewController<ReplayControlsViewController>(); _view.Configure(this);
            _screen = FloatingScreen.CreateFloatingScreen(new Vector2(125, 74), false, Vector3.zero, Quaternion.identity);
            _screen.gameObject.name = "MovementRecorder replay controls";
            _screen.SetRootViewController(_view, HMUI.ViewController.AnimationType.None); PositionPanel();
            _screen.gameObject.SetActive(false);
        }
        private void PositionPanel()
        {
            if (_screen == null) return;
            Transform head = _rig?.Camera.transform ?? GameAccess.Get<Transform>(_player, "_headTransform");
            Quaternion yaw = Quaternion.Euler(0, head.eulerAngles.y, 0);
            _screen.transform.SetPositionAndRotation(head.position + yaw * new Vector3(0, -.35f, 1.5f), yaw);
        }
        public void ShowPanel()
        {
            if (_exiting || _session.Phase == ReplayPhase.Playing) return;
            EnsurePanel(); _rig?.SetControlsVisible(true);
            _screen.gameObject.SetActive(true); PositionPanel();
        }
        private void HidePanel()
        {
            if (_screen != null) _screen.gameObject.SetActive(false);
            _rig?.SetControlsVisible(false);
        }
        public void HoldStartup()
        {
            if (_exiting) return;
            // StartSong has already loaded the clip and initialized all clock fields through SeekTo.
            // Vanilla sets isReady in its first playing Update, which cannot run while held paused.
            GameAccess.Set(_audio, "_isReady", true);
            GameAccess.Set(_audio, "_lastFrameDeltaSongTime", 0f);
            if (!_audioPausedAt.HasValue) _audioPausedAt = Time.timeSinceLevelLoad;
            _audio.Pause(); _updater.Pause();
        }
        public void Pause(bool show)
        {
            if (_exiting) return;
            _driver?.PauseHistory();
            if (!_audioPausedAt.HasValue) _audioPausedAt = Time.timeSinceLevelLoad;
            _gamePause.Pause(); _audio.Pause(); _updater.Pause();
            _objects.PauseAllBeatmapObjects(true); _objects.HideAllBeatmapObjects(false);
            GameAccess.Set(_pause, "_paused", true); GameAccess.Set(_pause, "_wantsToPause", false);
            if (_session.Phase == ReplayPhase.Playing) _session.SetPhase(ReplayPhase.Paused);
            if (show) { CancelDrag(); ShowPanel(); }
        }
        public void Resume()
        {
            if (!_ready || _exiting || _session.Error != null || _dragging) return;
            if (_audio.songTime >= EndTime) return;
            try
            {
                _driver.ResumeHistory(); _model.Apply(_audio.songTime);
                if (_audioPausedAt.HasValue)
                {
                    // AudioTimeSyncController uses level time, which keeps advancing while paused.
                    // Rebase it before native Resume; do not rely on the later audio drift correction.
                    float elapsed = Mathf.Max(0, Time.timeSinceLevelLoad - _audioPausedAt.Value) * _audio.timeScale;
                    GameAccess.Set(_audio, "_audioStartTimeOffsetSinceStart", GameAccess.Get<float>(_audio, "_audioStartTimeOffsetSinceStart") + elapsed);
                    _audioPausedAt = null;
                }
                _objects.HideAllBeatmapObjects(false); _objects.PauseAllBeatmapObjects(false);
                GameAccess.Set(_pause, "_paused", false); GameAccess.Set(_pause, "_wantsToPause", false);
                _resumedSongTime = _audio.songTime;
                _session.SetPhase(ReplayPhase.Playing); _gamePause.WillResume(); _gamePause.Resume();
                _audio.Resume(); _updater.Resume();
                // A seek can leave the initial negative audio lead-in before the source ever played.
                // In that case UnPause has no paused voice to resume, so start at SeekTo's sample.
                var source = GameAccess.Get<AudioSource>(_audio, "_audioSource");
                if (GameAccess.Get<bool>(_audio, "_audioStarted") && !source.isPlaying) source.Play();
                Plugin.Log?.Info($"Replay resumed at {_audio.songTime:0.000}s; audio playing={source.isPlaying}");
                Message = PlaybackMessage;
                HidePanel();
            }
            catch (Exception ex) { Fail(ex); }
        }
        public void Complete()
        {
            if (!_ready || _exiting) return;
            Pause(false); _session.SetPhase(ReplayPhase.Completed); Message = "再生が終了しました。前へ戻して再生できます。"; ShowPanel();
        }
        public bool BeforeSaberUpdate()
        {
            if (JudgementBlocked) return false;
            try { _driver.Apply(_audio.songTime); return true; }
            catch (Exception ex) { Fail(ex); return false; }
        }
        private void Update()
        {
            if (_exiting) return;
            try
            {
                _rig?.Update();
                if (!_loggedClockAdvance && _ready && _session.Phase == ReplayPhase.Playing && _audio.songTime > _resumedSongTime + .05f)
                {
                    _loggedClockAdvance = true;
                    Plugin.Log?.Info($"Replay clock advancing at {_audio.songTime:0.000}s");
                }
                if (_ready && _session.Phase == ReplayPhase.Playing && _audio.songTime >= EndTime) Complete();
                if (_ready && _pending.HasValue && (_pendingFinal || Time.unscaledTime >= _nextSeek)) ApplyPendingSeek();
                _view?.UpdateDisplay();
            }
            catch (Exception ex) { Fail(ex); }
        }
        internal void WriteLatePoses()
        {
            if (!_ready || _exiting) return;
            try
            {
                _model.KeepSourcesHidden(); _model.Apply(_audio.songTime); _driver.Apply(_audio.songTime);
                _model.SyncLiveExpressions(_session.Phase == ReplayPhase.Playing);
            }
            catch (Exception ex) { Fail(ex); }
        }
        public void BeginDrag() { _dragging = true; BeginInteraction(); }
        private void BeginInteraction()
        {
            if (_interaction) return;
            _interaction = true; _wasPlaying = _session.Phase == ReplayPhase.Playing; Pause(false);
        }
        public void RequestSeek(float time)
        {
            if (!_ready || _exiting || !Number.IsFinite(time)) return;
            BeginInteraction(); _session.SetPhase(ReplayPhase.Seeking); _pending = Mathf.Clamp(time, StartTime, EndTime);
        }
        public void EndDrag() { _dragging = false; _pendingFinal = true; if (!_pending.HasValue) EndInteraction(); }
        public void CancelDrag() { _wasPlaying = false; _dragging = false; _pendingFinal = true; if (!_pending.HasValue) EndInteraction(); }
        public void SeekRelative(float seconds) { RequestSeek(TimePosition + seconds); EndDrag(); }
        public void SeekStart() { RequestSeek(StartTime); EndDrag(); }
        private void ApplyPendingSeek()
        {
            float target = _pending.Value; _pending = null; _pendingFinal = false; _nextSeek = Time.unscaledTime + .1f;
            _seek.Seek(target, _model.Apply, _driver.PrepareHistory);
            _audioPausedAt = Time.timeSinceLevelLoad;
            if (!_dragging) EndInteraction();
        }
        private void EndInteraction()
        {
            if (!_interaction) return;
            bool resume = _wasPlaying; _interaction = false; _wasPlaying = false;
            _session.SetPhase(_audio.songTime >= EndTime ? ReplayPhase.Completed : ReplayPhase.Paused);
            Message = "シーク後のスコアは、この位置からの区間で計算します。";
            if (resume) Resume();
        }
        public void RemoveExpiredNote(float time) { _seek?.Segment.RemoveExpiredNote(time); }
        public void RetryBinding()
        {
            if (_exiting) return;
            Pause(false); _ready = false; _model?.Dispose(); _model = null; _driver?.Dispose(); _driver = null;
            if (_binding != null) StopCoroutine(_binding);
            _session.ClearError(); _session.SetPhase(ReplayPhase.Binding); _binding = StartCoroutine(BindModels());
        }
        public void SaveProfile() { JsonCache.Write(_profilePath, Profile, s => Plugin.Log?.Warn(s)); }
        public void ObserverChanged() { if (_session.Phase == ReplayPhase.Playing) Pause(false); _rig?.Update(); PositionPanel(); }
        public void Exit()
        {
            if (_exiting) return;
            Pause(false); HidePanel(); _exiting = true; _model?.StopLiveExpressions();
            _session.SetPhase(ReplayPhase.Disposing); _return.ReturnToMenu();
        }
        private void Fail(Exception exception)
        {
            if (_exiting) return;
            try { Pause(false); } catch (Exception nested) { Plugin.Log?.Warn(nested.ToString()); }
            _ready = false; _pending = null; _interaction = _dragging = false;
            _model?.StopLiveExpressions();
            _session.Fail(exception.Message); Message = exception.Message; Plugin.Log?.Error(exception.ToString());
            try { ShowPanel(); _view?.ShowBindings(); }
            catch (Exception uiError)
            {
                Plugin.Log?.Error(uiError.ToString());
                ReplayRuntimeHooks.NativePauseFallback = true;
                try { GameAccess.Set(_pause, "_paused", false); _pause.Pause(); }
                finally { ReplayRuntimeHooks.NativePauseFallback = false; }
            }
        }
        private void OnDestroy()
        {
            _exiting = true;
            _session?.RuntimeDestroyed(Time.frameCount);
            if (_binding != null) StopCoroutine(_binding);
            try { _model?.Dispose(); } catch (Exception ex) { Plugin.Log?.Error(ex.ToString()); }
            try { _driver?.Dispose(); } catch (Exception ex) { Plugin.Log?.Error(ex.ToString()); }
            try { _rig?.Dispose(); } catch (Exception ex) { Plugin.Log?.Error(ex.ToString()); }
            try { if (_screen != null) Destroy(_screen.gameObject); if (_view != null) Destroy(_view.gameObject); }
            finally { if (Current == this) Current = null; }
            // The session and submission ticket survive until the menu transition's finished callback.
        }
    }

    // UI controller input is positioned early. Visual poses are applied after avatar IK/Animator.
    [DefaultExecutionOrder(30000)]
    internal sealed class ReplayLatePoseWriter : MonoBehaviour
    {
        public PlaybackRuntime Runtime;
        private void LateUpdate() { Runtime?.WriteLatePoses(); }
    }
}
