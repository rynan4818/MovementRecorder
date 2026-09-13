using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Components;
using MovementRecorder.Configuration;
using MovementRecorder.Models;
using MovementRecorder.Playback;
using MovementRecorder.Playback.Compatibility;
using MovementRecorder.Playback.Data;
using MovementRecorder.Playback.Runtime;
using MovementRecorder.Playback.UI;
using Newtonsoft.Json;
using UnityEngine;
using Xunit;
using UObject = UnityEngine.Object;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class ReplayMenuTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "MovementRecorder-menu-" + Guid.NewGuid().ToString("N"));
        private readonly ReplaySession _session;
        private readonly ReplayMenuService _service;
        private readonly RecordData _record = new RecordData();
        private readonly MenuTransitionsHelper _transitions = new MenuTransitionsHelper();
        private readonly StandardLevelDetailViewController _chart;
        private ReplayFileFlowCoordinator Flow => GameAccess.Get<ReplayFileFlowCoordinator>(_service, "_flow");
        private ReplayFileViewController View => GameAccess.Get<ReplayFileViewController>(Flow, "_view");
        public ReplayMenuTests()
        {
            UObject.Objects.Clear(); Plugin.Log.Errors.Clear();
            PluginConfig.Instance = new PluginConfig(); Time.unscaledTime = 0;
            Directory.CreateDirectory(Path.Combine(_directory, "MovementRecorder"));
            IPA.Utilities.UnityGame.UserDataPath = Application.persistentDataPath = _directory;
            BeatSaberUI.MainFlowCoordinator = new GameObject("Main menu").AddComponent<HMUI.FlowCoordinator>();
            BeatSaberUI.PendingDismiss = null; BeatSaberUI.Presented = null;
            _chart = new GameObject("Chart view").AddComponent<StandardLevelDetailViewController>();
            _chart.selectedDifficultyBeatmap = new TestBeatmap();
            WriteFile("20260901120000-first.mvrec"); WriteFile("20260902120000-second.mvrec");
            _session = new ReplaySession();
            _service = new ReplayMenuService(_session, _record, new ReplaySaveGuards(), _transitions, new GameplaySetupViewController());
            _service.Initialize();
        }
        public void Dispose()
        {
            _service.Dispose(); _session.Finish(); UObject.Objects.Clear(); BeatSaberUI.PendingDismiss = null;
            string path = Path.GetFullPath(_directory), temp = Path.GetFullPath(Path.GetTempPath());
            if (!path.StartsWith(temp, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Not a temporary test directory");
            Directory.Delete(path, true);
        }
        private void WriteFile(string name)
        {
            var header = new MovementJson { objectCount = 1, recordCount = 2, levelID = "song", serializedName = "Standard", difficulty = "Expert", difficultyNum = 3,
                objectNames = new List<string> { "Saber" }, objectScales = new List<Scale> { new Scale { x = 1, y = 1, z = 1 } },
                Settings = new List<Setting> { new Setting { name = "Test", type = "Saber", searchStirngs = new List<string> { "^Saber$" } } } };
            using (var writer = new BinaryWriter(File.Create(Path.Combine(_directory, "MovementRecorder", name))))
            {
                writer.Write(JsonConvert.SerializeObject(header));
                for (int frame = 0; frame < 2; frame++) { writer.Write((float)frame); for (int i = 0; i < 7; i++) writer.Write(i == 6 ? 1f : 0f); }
            }
        }
        private async Task Open()
        {
            _service.OpenMenu(); await _service.Refresh(false);
            Assert.Equal(2, _service.Files.Length); Assert.True(Flow.Opened); _service.SelectFile(0);
        }
        private static async Task WaitUntil(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition()) { Assert.True(DateTime.UtcNow < deadline, "Asynchronous menu operation timed out"); await Task.Delay(5); }
        }

        [Fact] public async Task StartsInsideMenuOnlyAfterDismissalAndSnapshotsSettings()
        {
            await Open(); Assert.False(_chart.gameObject.activeInHierarchy);
            View.ShowSourceAvatar = true; View.OffsetSourceAvatar = true;
            View.ObserverX = 1.5f; View.ObserverY = .5f; View.ObserverZ = -4;
            var save = new TaskCompletionSource<bool>(); _record._saveTask = save.Task;
            var selected = _service.Selected; var files = _service.Files; var run = _service.StartReplay();
            Assert.True(_service.Busy); Assert.True(_service.CanCancel); Assert.True(Flow.Opened);
            _service.SelectFile(1); View.ShowSourceAvatar = false; View.NoFail = false;
            View.OffsetSourceAvatar = false; View.ObserverX = -1; View.ObserverY = -2; View.ObserverZ = -7;
            await _service.Refresh(true); await _service.StartReplay();
            Assert.Same(selected, _service.Selected); Assert.Same(files, _service.Files);
            Assert.True(PluginConfig.Instance.showReplaySourceAvatar); Assert.True(_session.NoFail);
            save.SetResult(true); await WaitUntil(() => BeatSaberUI.PendingDismiss != null);
            Assert.Equal(0, _transitions.Starts); Assert.False(_session.IsActive); Assert.False(_service.CanCancel);
            PluginConfig.Instance.showReplaySourceAvatar = false; // External reload cannot alter an in-flight request.
            PluginConfig.Instance.offsetReplaySourceAvatarWithHmd = false;
            PluginConfig.Instance.replayObserverX = -5; PluginConfig.Instance.replayObserverY = -3; PluginConfig.Instance.replayObserverZ = -8;
            BeatSaberUI.PendingDismiss(); await run;
            Assert.Equal(1, _transitions.Starts); Assert.True(_session.ShowSourceAvatar); Assert.False(_service.Busy);
            Assert.True(_session.OffsetSourceAvatarWithHmd);
            Assert.Equal(1.5f, _session.ObserverX); Assert.Equal(.5f, _session.ObserverY); Assert.Equal(-4, _session.ObserverZ);
            Assert.False(Flow.Opened); Assert.Empty(Plugin.Log.Errors);
        }

        [Fact] public async Task MenuLoadsSavedOffsetsAndEditsPersistEvenWhenAvatarIsHidden()
        {
            PluginConfig.Instance.replayObserverX = 2; PluginConfig.Instance.replayObserverY = 1; PluginConfig.Instance.replayObserverZ = -5;
            await Open();
            Assert.False(View.ShowSourceAvatar); Assert.False(View.OffsetSourceAvatar);
            Assert.Equal(2, View.ObserverX); Assert.Equal(1, View.ObserverY); Assert.Equal(-5, View.ObserverZ);
            View.OffsetSourceAvatar = true; View.ObserverX = -2; View.ObserverY = -1; View.ObserverZ = 3;
            Assert.True(PluginConfig.Instance.offsetReplaySourceAvatarWithHmd);
            Assert.Equal(-2, PluginConfig.Instance.replayObserverX); Assert.Equal(-1, PluginConfig.Instance.replayObserverY);
            Assert.Equal(3, PluginConfig.Instance.replayObserverZ);
            _session.ObserverX = 4; // The in-game controls use the same persisted session properties.
            _session.LoadObserverPosition(); Assert.Equal(4, View.ObserverX);
        }

        [Theory] [InlineData(false)] [InlineData(true)]
        public async Task CancelPreventsLateLoadOrDismissalFromStarting(bool duringDismissal)
        {
            await Open(); var save = new TaskCompletionSource<bool>(); _record._saveTask = save.Task;
            var run = _service.StartReplay();
            if (duringDismissal) { save.SetResult(true); await WaitUntil(() => BeatSaberUI.PendingDismiss != null); }
            _service.CancelLoad(); await run;
            save.TrySetResult(true); BeatSaberUI.PendingDismiss?.Invoke();
            Assert.Equal(0, _transitions.Starts); Assert.False(_session.IsActive); Assert.False(_service.Busy);
            if (!duringDismissal) Assert.True(Flow.Opened);
            _record._saveTask = null;
            if (!Flow.Opened) { _service.OpenMenu(); await _service.Refresh(false); }
            Assert.True(_service.CanReplay);
        }

        [Fact] public async Task BackCancelsLoadAndReopenRetainsTheSameFile()
        {
            await Open(); string path = _service.Selected.Path;
            var save = new TaskCompletionSource<bool>(); _record._saveTask = save.Task;
            var run = _service.StartReplay(); Flow.Back(); BeatSaberUI.PendingDismiss(); await run; save.SetResult(true);
            Assert.Equal(0, _transitions.Starts); Assert.False(Flow.Opened);
            _service.OpenMenu(); await _service.Refresh(false);
            Assert.Equal(path, _service.Selected.Path); Assert.True(_service.CanReplay);
        }

        [Theory] [InlineData(false)] [InlineData(true)]
        public async Task RefreshClearsRemovedOrReplacedSelection(bool replace)
        {
            await Open(); string path = _service.Selected.Path;
            if (replace) File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1)); else File.Delete(path);
            await _service.Refresh(false);
            Assert.Null(_service.Selected); Assert.False(_service.CanReplay); Assert.DoesNotContain(View.Rows.Cast<ReplayFileRow>(), row => row.Title.StartsWith("Selected"));
        }

        [Fact] public async Task OlderCatalogCompletionCannotReplaceLoadingState()
        {
            await Open(); var files = _service.Files; var selected = _service.Selected;
            var catalog = new TaskCompletionSource<MovementFileCatalog>(); GameAccess.Set(_service, "_catalog", catalog.Task);
            Task refresh = _service.Refresh(false);
            var save = new TaskCompletionSource<bool>(); _record._saveTask = save.Task; var run = _service.StartReplay();
            string status = _service.Status; catalog.SetResult(new MovementFileCatalog(_service.CacheDirectory, null)); await refresh;
            Assert.Same(files, _service.Files); Assert.Same(selected, _service.Selected); Assert.Equal(status, _service.Status);
            _service.CancelLoad(); await run; save.SetResult(true);
        }

        [Fact] public async Task ChangedChartAfterDismissalReturnsToMenuWithoutStarting()
        {
            await Open(); var run = _service.StartReplay(); await WaitUntil(() => BeatSaberUI.PendingDismiss != null);
            _chart.selectedDifficultyBeatmap = new TestBeatmap { difficulty = BeatmapDifficulty.Hard };
            BeatSaberUI.PendingDismiss(); await run;
            Assert.Equal(0, _transitions.Starts); Assert.False(_session.IsActive); Assert.True(Flow.Opened);
            Assert.Null(_service.Selected); Assert.False(_service.CanReplay); Assert.Contains("The selected map has changed", _service.Status);
        }

        [Fact] public async Task LoadFailureRemainsInMenuWithActionableStatus()
        {
            await Open(); File.Delete(_service.Selected.Path); await _service.StartReplay();
            Assert.True(Flow.Opened); Assert.False(_service.Busy); Assert.Equal(0, _transitions.Starts);
            Assert.Contains("Cannot start replay", _service.Status); Assert.Null(BeatSaberUI.PendingDismiss);
            Assert.Null(_service.Selected); Assert.False(_service.CanReplay);
        }

        [Fact] public async Task FileRemovedDuringDismissalReturnsToMenuWithoutStarting()
        {
            await Open(); var run = _service.StartReplay(); await WaitUntil(() => BeatSaberUI.PendingDismiss != null);
            File.Delete(_service.Selected.Path); BeatSaberUI.PendingDismiss(); await run;
            Assert.Equal(0, _transitions.Starts); Assert.False(_session.IsActive); Assert.True(Flow.Opened);
            Assert.Null(_service.Selected); Assert.False(_service.CanReplay); Assert.Contains("The selected recording has changed", _service.Status);
        }

        [Fact] public async Task SelectionPaintsBothTextsAndNewCellsWithoutReloadingRows()
        {
            await Open(); var list = new CustomCellListTableData(); GameAccess.Set(View, "_list", list); View.Refresh();
            var rows = View.Rows.ToArray(); var previous = (ReplayFileRow)rows[0]; var next = (ReplayFileRow)rows[1];
            var previousInfo = new GameObject("Previous info").AddComponent<TMPro.TextMeshProUGUI>();
            var info = new GameObject("Selected info").AddComponent<TMPro.TextMeshProUGUI>();
            var distance = new GameObject("Selected distance").AddComponent<TMPro.TextMeshProUGUI>();
            GameAccess.Set(previous, "_infoText", previousInfo);
            GameAccess.Set(next, "_infoText", info); GameAccess.Set(next, "_distanceText", distance);
            GameAccess.Call(previous, "Parsed");
            _service.SelectFile(1);
            Assert.Equal(rows, View.Rows); Assert.Equal(0, list.tableView.Reloads);
            Assert.DoesNotContain("Selected", previousInfo.text); Assert.Equal(Color.white, previousInfo.color);
            Assert.StartsWith("Selected", info.text); Assert.Contains(next.Label, info.text); Assert.Contains(next.Detail, info.text);
            Assert.DoesNotContain("\n", info.text); Assert.NotEqual(Color.white, info.color); Assert.Equal(info.color, distance.color);
            // A cell recreated after scrolling must paint its current selection without a property event.
            UObject.Destroy(info); var replacement = new GameObject("Recreated info").AddComponent<TMPro.TextMeshProUGUI>();
            GameAccess.Set(next, "_infoText", replacement); GameAccess.Call(next, "Parsed");
            Assert.Equal(distance.color, replacement.color); Assert.StartsWith("Selected", replacement.text);
            GameAccess.Call(previous, "RefreshVisuals", true, true); // A rejected native click must not override the service's selection.
            Assert.Equal(Color.white, previousInfo.color);
            string path = _service.Selected.Path;
            GameAccess.Call(View, "ToggleSettings"); GameAccess.Call(View, "ToggleSettings");
            Assert.Equal(path, _service.Selected.Path); Assert.Contains(Path.GetFileName(path), View.SelectedFile);
            await _service.Refresh(false);
            Assert.Equal(path, _service.Selected.Path); Assert.Single(View.Rows.Cast<ReplayFileRow>(), row => row.Title.StartsWith("Selected"));
        }
    }
}
