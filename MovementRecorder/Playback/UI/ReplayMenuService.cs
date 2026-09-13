using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using MovementRecorder.Configuration;
using MovementRecorder.Models;
using MovementRecorder.Playback.Compatibility;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace MovementRecorder.Playback.UI
{
    internal sealed class ReplayMenuService : IInitializable, ITickable, IDisposable
    {
        private readonly ReplaySession _session;
        private readonly RecordData _record;
        private readonly ReplaySaveGuards _guards;
        private readonly MenuTransitionsHelper _transitions;
        private readonly GameplaySetupViewController _setup;
        private Task<MovementFileCatalog> _catalog;
        private HeadDistanceReader _distance;
        private CancellationTokenSource _scanCancellation, _loadCancellation;
        private int _scanGeneration, _loadGeneration, _saved;
        private float _nextPoll;
        private readonly Queue<float> _refreshTimes = new Queue<float>();
        private ReplayFileFlowCoordinator _flow;
        private IDifficultyBeatmap _chart;
        private string _chartKey;
        private bool _disposed;
        public event Action Changed;
        public MovementFileMetadata Selected { get; private set; }
        public MovementFileMetadata[] Files { get; private set; } = new MovementFileMetadata[0];
        public HeadDistanceResult Distances { get; private set; } = new HeadDistanceResult();
        public string Status { get; private set; } = "記録ファイルを選択してください。";
        public bool Busy { get; private set; }
        public string CacheDirectory { get; private set; }
        public string RecordDirectory { get; private set; }
        public bool CanEdit => !_disposed && !Busy && _flow?.Closing != true && !MovementReplay.IsActive;
        public bool CanCancel => Busy && _flow?.Closing != true && !MovementReplay.IsActive;
        public bool CanReplay => CanEdit && _flow?.Opened == true && Selected?.Error == null && Selected?.FrameCount > 0 &&
            Selected.Confirmed && Selected.ChartKey == _chartKey;
        public ReplaySession Session => _session;
        private string ChartScope => _chart == null ? "" : _chart.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName + " / " +
            (_chart.difficulty == BeatmapDifficulty.ExpertPlus ? "Expert+" : _chart.difficulty.ToString());
        public string ChartText => _chart == null ? "Soloで曲と難易度を選択してください。" :
            _chart.level.songName + " / " + ChartScope;

        public ReplayMenuService(ReplaySession session, RecordData record, ReplaySaveGuards guards,
            MenuTransitionsHelper transitions, GameplaySetupViewController setup)
        { _session = session; _record = record; _guards = guards; _transitions = transitions; _setup = setup; }
        public void Initialize()
        {
            RecordDirectory = Path.Combine(IPA.Utilities.UnityGame.UserDataPath, "MovementRecorder");
            CacheDirectory = Path.Combine(RecordDirectory, "Cache");
            _catalog = Task.Run(() => new MovementFileCatalog(CacheDirectory, message => Plugin.Log?.Warn(message)));
            _distance = new HeadDistanceReader(Path.Combine(Application.persistentDataPath, "HMDDistance.litedb"), CacheDirectory, message => Plugin.Log?.Debug(message));
            _record.FileSaved += OnFileSaved;
            SceneManager.activeSceneChanged += OnSceneChanged;
            MovementReplay.SessionChanged += OnSessionChanged;
        }
        private void OnFileSaved(string _) { Interlocked.Exchange(ref _saved, 1); }
        private void OnSessionChanged(Guid id, bool active)
        {
            if (active) { CancelScan(); return; }
            _refreshTimes.Enqueue(Time.unscaledTime + .5f); _refreshTimes.Enqueue(Time.unscaledTime + 2f);
            Notify();
        }
        private void OnSceneChanged(Scene oldScene, Scene newScene)
        {
            if (!_session.IsActive) CancelLoad();
        }
        private static IDifficultyBeatmap CurrentChart()
        {
            var views = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>()
                .Where(v => v.gameObject.scene.IsValid() && v.gameObject.activeInHierarchy).ToArray();
            return views.Length == 1 ? views[0].selectedDifficultyBeatmap : null;
        }
        public void Tick()
        {
            // Fallback for scene exits that bypass the usual finish callback. Keep every guard active
            // until the whole gameplay scene is unloaded, including all other MODs' OnDestroy hooks.
            if (!_disposed && _session.IsActive && _session.DestroyedAtFrame.HasValue && Time.frameCount > _session.DestroyedAtFrame.Value + 1 &&
                _session.GameplaySceneHandle.HasValue && !Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                    .Any(scene => scene.handle == _session.GameplaySceneHandle.Value && scene.isLoaded))
                _session.Finish();
            if (_disposed || MovementReplay.IsActive || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + .25f;
            if (_flow == null || !_flow.Opened)
            {
                SetChart(CurrentChart());
            }
            if (Interlocked.Exchange(ref _saved, 0) != 0)
            { _refreshTimes.Enqueue(Time.unscaledTime + .5f); _refreshTimes.Enqueue(Time.unscaledTime + 2f); }
            if (_chart != null && _refreshTimes.Count > 0 && _refreshTimes.Peek() <= Time.unscaledTime && !Busy)
            { _refreshTimes.Dequeue(); _ = Refresh(false); }
        }
        private static string KeyOf(IDifficultyBeatmap chart) => chart == null ? null :
            ChartIdentity.Key(chart.level.levelID, chart.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName, (int)chart.difficulty);
        private void SetChart(IDifficultyBeatmap chart)
        {
            string key = KeyOf(chart);
            _chart = chart;
            if (key == _chartKey) return;
            CancelLoad(); CancelScan(); _chartKey = key;
            Selected = null; Files = new MovementFileMetadata[0]; Distances = new HeadDistanceResult();
            Status = chart == null ? "Soloで曲と難易度を選択してください。" : "記録ファイルを選択してください。";
            Notify();
        }
        public void OpenMenu()
        {
            if (!CanEdit || _flow?.Opened == true) return;
            _session.LoadObserverPosition();
            SetChart(CurrentChart());
            if (_flow == null) { _flow = BeatSaberUI.CreateFlowCoordinator<ReplayFileFlowCoordinator>(); _flow.Configure(this); }
            _flow.Show(); _ = Refresh(false);
        }
        private IEnumerable<string> Folders()
        {
            yield return RecordDirectory;
            // Resolve from the selected chart, never from the last cover-image request.
            if (_chart?.level is CustomPreviewBeatmapLevel custom && !string.IsNullOrEmpty(custom.customLevelPath))
                yield return Path.Combine(custom.customLevelPath, "MovementRecorder");
        }
        public async Task Refresh(bool rebuild)
        {
            if (!CanEdit || _chart == null) return;
            CancelScan(); var cancellation = _scanCancellation = new CancellationTokenSource(); var token = cancellation.Token; int generation = ++_scanGeneration;
            var folders = Folders().ToArray(); string key = _chartKey;
            Status = "記録ファイルを確認しています…"; Notify();
            try
            {
                var catalog = await _catalog;
                var snapshot = await catalog.ScanAsync(folders, rebuild, token);
                var distances = await Task.Run(() => _distance.Read(snapshot.Files, token, rebuild), token);
                if (_disposed || generation != _scanGeneration || key != _chartKey || Busy || MovementReplay.IsActive) return;
                Files = snapshot.FilesForChart(folders, key);
                Distances = distances;
                if (Selected != null) Selected = Files.FirstOrDefault(f => f.SameSource(Selected));
                Status = Files.Length == 0 ? ChartScope + " の記録は見つかりません。" : ChartScope + ": " + Files.Length + " 件の記録。スコア・履歴は保存しません。";
                if (snapshot.Warnings.Length > 0) Status += "\n一部のフォルダを確認できませんでした。";
                Plugin.Log?.Debug($"Replay catalog: {_chart.level.levelID}, {ChartScope}, matches={Files.Length}, files={snapshot.Files.Length}, headers={snapshot.HeadersRead}, cache={snapshot.CacheHits}");
                Notify();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (generation == _scanGeneration) { Status = "一覧を更新できません: " + ex.Message; Notify(); } Plugin.Log?.Warn(ex.ToString()); }
        }
        public void SelectFile(int index)
        {
            if (!CanEdit || index < 0 || index >= Files.Length) return;
            Selected = Files[index]; Notify();
        }
        public void MenuClosed() { CancelScan(); Notify(); }
        public void MenuClosing() { CancelScan(); Notify(); }
        public double? DistanceFor(MovementFileMetadata file) => !Distances.Available || file == null ? null :
            Distances.Matches.Where(m => m.Path == file.Path && m.HeaderHash == file.HeaderHash).Select(m => (double?)m.Distance).FirstOrDefault();
        public async Task StartReplay()
        {
            if (!CanReplay || _chart == null) return;
            CancelScan();
            CancelLoad(); var cancellation = _loadCancellation = new CancellationTokenSource(); var token = cancellation.Token; int generation = ++_loadGeneration;
            var selected = Selected.Copy(); var chart = _chart; string key = _chartKey;
            bool showSourceAvatar = PluginConfig.Instance.showReplaySourceAvatar;
            bool offsetSourceAvatar = PluginConfig.Instance.offsetReplaySourceAvatarWithHmd;
            _session.LoadObserverPosition();
            Busy = true; Status = "記録を読み込んでいます…"; Notify();
            try
            {
                if (chart.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName != "Standard")
                    throw new InvalidOperationException("初期版は両手セイバーのStandard譜面に対応しています。");
                var requirements = SongCore.Collections.RetrieveDifficultyData(chart)?.additionalDifficultyData?._requirements;
                if (requirements?.Length > 0)
                    throw new InvalidOperationException("必須拡張がある譜面は初期版の対象外です: " + string.Join(", ", requirements));
                _guards.Prepare(); ReplayRuntimeHooks.Prepare();
                if (_record._saveTask != null && !_record._saveTask.IsCompleted)
                { Status = "直前の記録の保存完了を待っています…"; Notify(); await WaitWithCancellation(_record._saveTask, token); }
                token.ThrowIfCancellationRequested();
                long retained = _record._recordData == null ? 0 : (long)_record._recordData.Length * (16L + _record._transformSize * 28L);
                var clip = await Task.Run(() => new MovementFileReader().ReadClip(selected.Path, selected,
                    MovementFileReader.DefaultMemoryBudget - retained, token), token);
                token.ThrowIfCancellationRequested();
                if (_disposed || generation != _loadGeneration || _chartKey != key) return;
                if (!clip.Header.Settings.Any(s => s.type == "Saber")) throw new InvalidOperationException("左右のセイバーを記録したファイルを選択してください。");
                Status = "リプレイを開始します…"; Notify();
                // The underlying chart view is inactive while the menu is presented.
                // Read it again only after dismissal, and keep cancellation valid through the animation.
                await WaitWithCancellation(_flow.CloseForReplay(), token);
                token.ThrowIfCancellationRequested();
                if (_disposed || generation != _loadGeneration) return;
                var current = CurrentChart();
                if (_chartKey != key || KeyOf(current) != key)
                {
                    SetChart(current); Status = "選択中の譜面が変わりました。記録を選び直してください。";
                    _flow.Show(); Notify(); return;
                }
                if (Selected == null || !selected.SameSource(Selected) || !selected.MatchesAttributes(new FileInfo(selected.Path)))
                {
                    Selected = null;
                    throw new InvalidOperationException("選択した記録が変更されました。一覧を更新して選び直してください。");
                }
                _guards.Prepare();
                _session.Begin(clip, showSourceAvatar, offsetSourceAvatar);
                var modifiers = _setup.gameplayModifiers.CopyWith(noFailOn0Energy: _session.NoFail, songSpeed: GameplayModifiers.SongSpeed.Normal);
                _transitions.StartStandardLevel(ReplaySession.GameMode, chart, chart.level, _setup.environmentOverrideSettings,
                    _setup.colorSchemesSettings.GetOverrideColorScheme(), modifiers, _setup.playerSettings.CopyWith(autoRestart: false),
                    new PracticeSettings { startSongTime = 0, songSpeedMul = 1 }, "曲選択へ", false, true, null,
                    (setup, result) => { _session.Finish(); Status = "リプレイが終了しました。スコア・プレイ履歴は保存していません。"; Notify(); }, null);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (generation == _loadGeneration)
                {
                    Status = "リプレイを開始できません: " + ex.Message;
                    if (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException) Selected = null;
                    if (_session.IsActive) _session.Finish();
                    if (!_disposed && _flow?.Opened == false) _flow.Show();
                    Notify();
                }
                Plugin.Log?.Error(ex.ToString());
            }
            finally
            {
                if (generation == _loadGeneration) { _loadCancellation = null; Busy = false; Notify(); }
                cancellation.Dispose();
            }
        }
        private static async Task WaitWithCancellation(Task task, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using (token.Register(() => cancelled.TrySetCanceled()))
            { await await Task.WhenAny(task, cancelled.Task); token.ThrowIfCancellationRequested(); }
        }
        public void CancelLoad()
        {
            bool wasBusy = Busy;
            _loadGeneration++; _loadCancellation?.Cancel(); _loadCancellation?.Dispose(); _loadCancellation = null; Busy = false;
            if (wasBusy) { Status = "読み込みを中止しました。"; Notify(); }
        }
        private void CancelScan() { _scanGeneration++; _scanCancellation?.Cancel(); _scanCancellation?.Dispose(); _scanCancellation = null; }
        private void Notify() { if (!_disposed) Changed?.Invoke(); }
        public void Dispose()
        {
            _disposed = true; CancelLoad(); CancelScan(); _record.FileSaved -= OnFileSaved;
            SceneManager.activeSceneChanged -= OnSceneChanged; MovementReplay.SessionChanged -= OnSessionChanged;
            if (_flow != null) UnityEngine.Object.Destroy(_flow.gameObject);
        }
    }
}
