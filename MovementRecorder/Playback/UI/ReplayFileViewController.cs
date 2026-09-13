using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;

namespace MovementRecorder.Playback.UI
{
    internal sealed class ReplayFileRow
    {
        [UIValue("title")] public string Title { get; set; }
        [UIValue("detail")] public string Detail { get; set; }
        [UIValue("distance")] public string Distance { get; set; }
    }
    internal sealed class ReplayFileViewController : BSMLAutomaticViewController
    {
        private ReplayMenuService _service;
        private bool _details;
        private bool _settings;
        private object _shownFiles, _shownDistances;
        [UIComponent("files")] private CustomCellListTableData _list = null;
        [UIValue("show-files")] public bool ShowFiles => !_settings;
        [UIValue("show-settings")] public bool ShowSettings => _settings;
        [UIValue("settings-label")] public string SettingsLabel => _settings ? "一覧へ" : "鑑賞設定";
        [UIValue("rows")] public List<object> Rows { get; } = new List<object>();
        [UIValue("chart")] public string Chart => _service?.ChartText ?? "";
        [UIValue("status")] public string Status => _service?.Status ?? "";
        [UIValue("distance-heading")] public string DistanceHeading => _service?.Distances.Available == true ? "頭の移動距離（参考）" : "";
        [UIValue("details")] public string Details
        {
            get
            {
                var file = _service?.Candidate; if (file == null) return "一覧から記録を選択してください。";
                string summary = (file.Error ?? $"{file.FrameCount:N0} フレーム / {file.ObjectCount} 対象 / {file.StartTime:0.0}–{file.EndTime:0.0} 秒\n" + string.Join(", ", file.Groups ?? new string[0]));
                return summary + (_details ? "\n" + file.Path : "");
            }
        }
        [UIValue("can-select")] public bool CanSelect => _service?.Candidate?.Error == null && _service?.Candidate?.Confirmed == true && _service.Candidate.FrameCount > 0;
        [UIValue("no-fail")] public bool NoFail { get => _service.Session.NoFail; set => _service.Session.NoFail = value; }
        [UIValue("observer-x")] public float ObserverX { get => _service.Session.ObserverX; set => _service.Session.ObserverX = value; }
        [UIValue("observer-y")] public float ObserverY { get => _service.Session.ObserverY; set => _service.Session.ObserverY = value; }
        [UIValue("observer-z")] public float ObserverZ { get => _service.Session.ObserverZ; set => _service.Session.ObserverZ = value; }
        public void Configure(ReplayMenuService service) { _service = service; service.Changed += Refresh; }
        public void Refresh()
        {
            if (_shownFiles != _service.Files || _shownDistances != _service.Distances)
            {
                _shownFiles = _service.Files; _shownDistances = _service.Distances;
                Rows.Clear();
                foreach (var file in _service.Files)
                {
                    double? distance = _service.DistanceFor(file);
                    Rows.Add(new ReplayFileRow { Title = file.RecordedAtLocal?.ToString("yyyy/MM/dd HH:mm:ss") ?? Path.GetFileName(file.Path),
                        Detail = file.Error != null ? "読込不可: " + file.Error : $"{file.EndTime - file.StartTime:0.0} 秒 / {file.ObjectCount} 対象 / {file.Length / 1048576d:0.0} MiB",
                        Distance = distance.HasValue ? distance.Value.ToString("0.0", CultureInfo.CurrentCulture) + " m" : "" });
                }
                _list?.tableView.ReloadData();
                int selected = Array.IndexOf(_service.Files, _service.Candidate);
                if (selected >= 0) _list?.tableView.SelectCellWithIdx(selected, false);
            }
            foreach (string name in new[] { nameof(Chart), nameof(Status), nameof(DistanceHeading), nameof(Details), nameof(CanSelect) }) NotifyPropertyChanged(name);
        }
        [UIAction("#post-parse")] private void Parsed() { _shownFiles = null; Refresh(); }
        // custom-list passes the selected data object, unlike list's integer cell index.
        // An old cell from a replaced list resolves to -1 and cannot select a different recording.
        [UIAction("select-row")] private void Select(TableView view, ReplayFileRow row) { _service.SelectCandidate(Rows.IndexOf(row)); }
        [UIAction("refresh")] private void RefreshFiles() { _service.Refresh(false); }
        [UIAction("rebuild")] private void Rebuild() { _service.Refresh(true); }
        [UIAction("confirm")] private void Confirm() { _service.ConfirmSelection(); }
        [UIAction("details-toggle")] private void ToggleDetails() { _details = !_details; NotifyPropertyChanged(nameof(Details)); }
        [UIAction("settings-toggle")] private void ToggleSettings()
        {
            _settings = !_settings;
            foreach (var name in new[] { nameof(ShowFiles), nameof(ShowSettings), nameof(SettingsLabel) }) NotifyPropertyChanged(name);
        }
        protected override void OnDestroy() { if (_service != null) _service.Changed -= Refresh; base.OnDestroy(); }
    }
}
