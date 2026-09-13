using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using MovementRecorder.Configuration;
using TMPro;
using UnityEngine;

namespace MovementRecorder.Playback.UI
{
    internal sealed class ReplayFileRow
    {
        public string Label { get; set; }
        public bool HasError { get; set; }
        private bool _selected;
        [UIComponent("row-info")] private TextMeshProUGUI _infoText = null;
        [UIComponent("row-distance")] private TextMeshProUGUI _distanceText = null;
        private static readonly Color SelectedColor = new Color(102f / 255, 217f / 255, 239f / 255);
        private static readonly Color ErrorColor = new Color(1, 138f / 255, 128f / 255);
        [UIValue("title")] public string Title => (_selected ? "Selected  " : "") + Label + "  /  " + Detail;
        public string Detail { get; set; }
        [UIValue("distance")] public string Distance { get; set; }
        public void SetSelected(bool selected)
        {
            _selected = selected;
            UpdateText();
        }
        [UIAction("#post-parse")] private void Parsed() { UpdateText(); }
        // CustomCellTableCell calls this when a cell is selected, deselected or highlighted.
        // Use the service's selection, so a disabled/stale click cannot change the visual state.
        [UIAction("refresh-visuals")] private void RefreshVisuals(bool selected, bool highlighted) { UpdateText(); }
        private void UpdateText()
        {
            var color = _selected ? SelectedColor : Color.white;
            if (_infoText != null)
            {
                _infoText.text = Title;
                _infoText.color = HasError && !_selected ? ErrorColor : color;
            }
            if (_distanceText != null) { _distanceText.text = Distance; _distanceText.color = color; }
        }
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
        [UIValue("settings-label")] public string SettingsLabel => _settings ? "Back to List" : "View Settings";
        [UIValue("rows")] public List<object> Rows { get; } = new List<object>();
        [UIValue("chart")] public string Chart => _service?.ChartText ?? "";
        [UIValue("status")] public string Status => _service?.Status ?? "";
        [UIValue("selected-file")] public string SelectedFile => _service?.Selected == null ? "No recording selected" :
            "Selected: " + Path.GetFileName(_service.Selected.Path);
        [UIValue("selection-color")] public string SelectionColor => _service?.Selected == null ? "#FFFFFF" : "#66D9EF";
        [UIValue("distance-heading")] public string DistanceHeading => _service?.Distances.Available == true ? "Head Travel (Reference)" : "";
        [UIValue("details")] public string Details
        {
            get
            {
                var file = _service?.Selected; if (file == null) return "Select a recording from the list.";
                string summary = (file.Error ?? $"{file.FrameCount:N0} frames / {file.ObjectCount} objects / {file.StartTime:0.0}–{file.EndTime:0.0} s\n" + string.Join(", ", file.Groups ?? new string[0]));
                return summary + (_details ? "\n" + file.Path : "");
            }
        }
        [UIValue("can-replay")] public bool CanReplay => _service?.CanReplay == true;
        [UIValue("can-edit")] public bool CanEdit => _service?.CanEdit == true;
        [UIValue("can-cancel")] public bool CanCancel => _service?.CanCancel == true;
        [UIValue("info-color")] public string InfoColor => _service?.Selected?.Error == null ? SelectionColor : "#FF8A80";
        [UIValue("no-fail")] public bool NoFail { get => _service.Session.NoFail; set { if (CanEdit) _service.Session.NoFail = value; } }
        [UIValue("show-source-avatar")] public bool ShowSourceAvatar
        { get => PluginConfig.Instance.showReplaySourceAvatar; set { if (CanEdit) PluginConfig.Instance.showReplaySourceAvatar = value; } }
        [UIValue("offset-source-avatar")] public bool OffsetSourceAvatar
        { get => PluginConfig.Instance.offsetReplaySourceAvatarWithHmd; set { if (CanEdit) PluginConfig.Instance.offsetReplaySourceAvatarWithHmd = value; } }
        [UIValue("observer-x")] public float ObserverX { get => _service.Session.ObserverX; set { if (CanEdit) _service.Session.ObserverX = value; } }
        [UIValue("observer-y")] public float ObserverY { get => _service.Session.ObserverY; set { if (CanEdit) _service.Session.ObserverY = value; } }
        [UIValue("observer-z")] public float ObserverZ { get => _service.Session.ObserverZ; set { if (CanEdit) _service.Session.ObserverZ = value; } }
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
                    Rows.Add(new ReplayFileRow { Label = file.RecordedAtLocal?.ToString("yyyy/MM/dd HH:mm:ss") ?? Path.GetFileName(file.Path), HasError = file.Error != null,
                        Detail = file.Error != null ? "Cannot load: " + file.Error : $"{file.EndTime - file.StartTime:0.0} s / {file.ObjectCount} objects / {file.Length / 1048576d:0.0} MiB",
                        Distance = distance.HasValue ? distance.Value.ToString("0.0", CultureInfo.CurrentCulture) + " m" : "" });
                }
                _list?.TableView.ReloadData();
            }
            int selected = Array.IndexOf(_service.Files, _service.Selected);
            for (int i = 0; i < Rows.Count; i++) ((ReplayFileRow)Rows[i]).SetSelected(i == selected);
            if (selected >= 0) _list?.TableView.SelectCellWithIdx(selected, false);
            else _list?.TableView.ClearSelection();
            foreach (string name in new[] { nameof(Chart), nameof(Status), nameof(DistanceHeading), nameof(Details), nameof(CanReplay), nameof(CanEdit),
                nameof(CanCancel), nameof(SelectedFile), nameof(SelectionColor), nameof(InfoColor), nameof(ShowSourceAvatar), nameof(OffsetSourceAvatar),
                nameof(ObserverX), nameof(ObserverY), nameof(ObserverZ) }) NotifyPropertyChanged(name);
        }
        [UIAction("#post-parse")] private void Parsed() { _shownFiles = null; Refresh(); }
        // custom-list passes the selected data object, unlike list's integer cell index.
        // An old cell from a replaced list resolves to -1 and cannot select a different recording.
        [UIAction("select-row")] private void Select(TableView view, ReplayFileRow row) { _service.SelectFile(Rows.IndexOf(row)); Refresh(); }
        [UIAction("refresh")] private async void RefreshFiles() { await _service.Refresh(false); }
        [UIAction("rebuild")] private async void Rebuild() { await _service.Refresh(true); }
        [UIAction("start-replay")] private async void StartReplay() { await _service.StartReplay(); }
        [UIAction("cancel-load")] private void CancelLoad() { _service.CancelLoad(); }
        [UIAction("details-toggle")] private void ToggleDetails() { _details = !_details; NotifyPropertyChanged(nameof(Details)); }
        [UIAction("settings-toggle")] private void ToggleSettings()
        {
            _settings = !_settings;
            foreach (var name in new[] { nameof(ShowFiles), nameof(ShowSettings), nameof(SettingsLabel) }) NotifyPropertyChanged(name);
        }
        protected override void OnDestroy() { if (_service != null) _service.Changed -= Refresh; base.OnDestroy(); }
    }
}
