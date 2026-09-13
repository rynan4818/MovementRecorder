using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.ViewControllers;
using MovementRecorder.Playback.Models;
using MovementRecorder.Playback.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MovementRecorder.Playback.UI
{
    internal sealed class SeekDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler
    {
        public PlaybackRuntime Runtime;
        private bool _down;
        public void OnPointerDown(PointerEventData data) { _down = true; Runtime?.BeginDrag(); }
        public void OnBeginDrag(PointerEventData data) { if (!_down) OnPointerDown(data); }
        public void OnPointerUp(PointerEventData data) { _down = false; Runtime?.EndDrag(); }
        public void OnEndDrag(PointerEventData data) { OnPointerUp(data); }
        private void OnDisable() { if (_down) { _down = false; Runtime?.CancelDrag(); } }
    }

    internal sealed class ReplayControlsViewController : BSMLAutomaticViewController
    {
        private PlaybackRuntime _runtime;
        private bool _bindingSettings;
        private bool _syncSlider;
        private float _nextText;
        [UIComponent("timeline")] private SliderSetting _timeline = null;
        [UIComponent("roots")] private DropDownListSetting _roots = null;
        [UIComponent("sources")] private DropDownListSetting _sources = null;
        [UIComponent("tracks")] private DropDownListSetting _tracks = null;
        [UIValue("binding-settings")] public bool BindingSettings => _bindingSettings;
        [UIValue("playback-controls")] public bool PlaybackControls => !_bindingSettings;
        [UIValue("ready")] public bool Ready => _runtime?.Ready == true;
        [UIValue("message")] public string Message => _runtime?.Message ?? "Preparing…";
        [UIValue("clock")] public string Clock => _runtime == null ? "" : Format(_runtime.TimePosition) + " / " + Format(_runtime.EndTime);
        [UIValue("pause-label")] public string PauseLabel => _runtime?.Session.Phase == ReplayPhase.Playing ? "Pause" : "Resume";
        [UIValue("position")] public float Position { get => _runtime?.TimePosition ?? 0; set { if (!_syncSlider) _runtime?.RequestSeek(value); } }
        [UIValue("observer-x")] public float ObserverX { get => _runtime.Session.ObserverX; set { _runtime.Session.ObserverX = value; _runtime.ObserverChanged(); } }
        [UIValue("observer-y")] public float ObserverY { get => _runtime.Session.ObserverY; set { _runtime.Session.ObserverY = value; _runtime.ObserverChanged(); } }
        [UIValue("observer-z")] public float ObserverZ { get => _runtime.Session.ObserverZ; set { _runtime.Session.ObserverZ = value; _runtime.ObserverChanged(); } }
        [UIValue("root-choices")] public List<object> RootChoices { get; } = new List<object> { "" };
        [UIValue("root-choice")] public string RootChoice { get; set; } = "";
        [UIValue("source-choices")] public List<object> SourceChoices { get; } = new List<object> { "" };
        [UIValue("source-choice")] public string SourceChoice { get; set; } = "";
        [UIValue("track-choices")] public List<object> TrackChoices { get; } = new List<object> { "" };
        [UIValue("track-choice")] public string TrackChoice { get; set; } = "";
        [UIValue("source-path")] public string SourcePath { get; set; } = "";
        [UIValue("binding-message")] public string BindingMessage { get; private set; } = "Use your usual mod to load the same model used in the recording.";
        [UIValue("freeze-missing")] public bool FreezeMissing { get => _runtime.Profile?.FreezeMissing == true; set { if (_runtime.Profile != null) _runtime.Profile.FreezeMissing = value; } }
        public void Configure(PlaybackRuntime runtime) { _runtime = runtime; }
        private static string Format(float time) => !Data.Number.IsFinite(time) ? "--:--" :
            TimeSpan.FromSeconds(Mathf.Max(0, time)).ToString(time >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
        [UIAction("format-time")] private string FormatTime(float time) { return Format(time); }
        [UIAction("#post-parse")] private void Parsed()
        {
            if (_timeline != null)
            {
                var handler = _timeline.slider.gameObject.AddComponent<SeekDragHandler>(); handler.Runtime = _runtime;
                UpdateDisplay(true);
            }
        }
        public void UpdateDisplay(bool force = false)
        {
            if (_runtime == null) return;
            if (_timeline != null && _runtime.Ready)
            {
                _syncSlider = true;
                try
                {
                    _timeline.slider.minValue = _runtime.StartTime; _timeline.slider.maxValue = _runtime.EndTime;
                    _timeline.slider.numberOfSteps = Mathf.Max(2, Mathf.CeilToInt((_runtime.EndTime - _runtime.StartTime) * 10) + 1);
                    _timeline.slider.SetNormalizedValue(Mathf.InverseLerp(_runtime.StartTime, _runtime.EndTime, _runtime.TimePosition), false);
                }
                finally { _syncSlider = false; }
            }
            if (!force && Time.unscaledTime < _nextText) return;
            _nextText = Time.unscaledTime + .1f;
            foreach (string property in new[] { nameof(Clock), nameof(Message), nameof(Ready), nameof(PauseLabel) }) NotifyPropertyChanged(property);
        }
        [UIAction("pause-resume")] private void PauseResume()
        { if (_runtime.Session.Phase == ReplayPhase.Playing) _runtime.Pause(false); else _runtime.Resume(); }
        [UIAction("start")] private void StartPosition() { _runtime.SeekStart(); }
        [UIAction("back")] private void Back() { _runtime.SeekRelative(-5); }
        [UIAction("forward")] private void Forward() { _runtime.SeekRelative(5); }
        [UIAction("exit")] private void Exit() { _runtime.Exit(); }
        [UIAction("binding")] public void ShowBindings()
        {
            _runtime.Pause(false); _bindingSettings = true;
            RootChoices.Clear(); SourceChoices.Clear(); TrackChoices.Clear();
            if (_runtime.Plan != null) RootChoices.AddRange(_runtime.Plan.RecordedRoots.Cast<object>());
            TrackChoices.AddRange(_runtime.Session.Clip.Header.objectNames.Cast<object>());
            if (_runtime.Resolver != null)
            {
                SourceChoices.AddRange(_runtime.Resolver.RenderableRootChoices().Cast<object>());
            }
            // BSML's dropdown requires at least one value, including before any model has loaded.
            if (RootChoices.Count == 0) RootChoices.Add("");
            if (SourceChoices.Count == 0) SourceChoices.Add("");
            if (TrackChoices.Count == 0) TrackChoices.Add("");
            if (!RootChoices.Contains(RootChoice)) RootChoice = RootChoices.FirstOrDefault() as string ?? "";
            if (!SourceChoices.Contains(SourceChoice)) SourceChoice = SourceChoices.FirstOrDefault() as string ?? "";
            if (!TrackChoices.Contains(TrackChoice)) TrackChoice = _runtime.Plan?.Issues.FirstOrDefault()?.RecordedPath ?? TrackChoices.FirstOrDefault() as string ?? "";
            BindingMessage = _runtime.Plan?.Issues.FirstOrDefault()?.Message ?? "Choose a root mapping or a fixed saber anchor.";
            foreach (var dropdown in new[] { _roots, _sources, _tracks })
                if (dropdown != null) { dropdown.UpdateChoices(); dropdown.ReceiveValue(); }
            NotifyBindings();
            _runtime.ShowPanel();
        }
        public void BindingFinished() { _bindingSettings = false; NotifyBindings(); }
        private void NotifyBindings()
        {
            foreach (string property in new[] { nameof(BindingSettings), nameof(PlaybackControls), nameof(RootChoices), nameof(SourceChoices),
                nameof(TrackChoices), nameof(RootChoice), nameof(SourceChoice), nameof(TrackChoice), nameof(BindingMessage), nameof(FreezeMissing) }) NotifyPropertyChanged(property);
        }
        [UIAction("bind-root")] private void BindRoot()
        {
            try
            {
                var roots = _runtime.Resolver.FindSource(SourceChoice);
                if (roots.Length != 1 || string.IsNullOrEmpty(RootChoice)) throw new InvalidOperationException("Select a single matching target root.");
                _runtime.Profile.Roots[RootChoice] = SourceChoice;
                _runtime.Profile.Hierarchies[RootChoice] = SceneModelResolver.HierarchySignature(roots[0]);
                _runtime.Profile.OmittedRoots.Remove(RootChoice); SaveBinding("Root mapping saved. Select Recheck.");
            }
            catch (Exception ex) { SaveBinding(ex.Message, false); }
        }
        [UIAction("bind-track")] private void BindTrack()
        {
            if (_runtime.Resolver?.FindSource(SourcePath).Length != 1 || string.IsNullOrEmpty(TrackChoice))
            { SaveBinding("The target path must identify exactly one Transform in the scene.", false); return; }
            _runtime.Profile.Tracks[TrackChoice] = SourcePath; SaveBinding("Object mapping saved. Select Recheck.");
        }
        [UIAction("left-anchor")] private void LeftAnchor() { _runtime.Profile.LeftAnchor = TrackChoice; SaveBinding("Left saber anchor saved."); }
        [UIAction("right-anchor")] private void RightAnchor() { _runtime.Profile.RightAnchor = TrackChoice; SaveBinding("Right saber anchor saved."); }
        [UIAction("omit-root")] private void OmitRoot()
        {
            if (string.IsNullOrEmpty(RootChoice)) return;
            if (!_runtime.Profile.OmittedRoots.Contains(RootChoice)) _runtime.Profile.OmittedRoots.Add(RootChoice);
            SaveBinding("This root will be skipped. Saber recordings cannot be skipped.");
        }
        [UIAction("reset-binding")] private void ResetBinding()
        {
            _runtime.Profile.Roots.Clear(); _runtime.Profile.Tracks.Clear(); _runtime.Profile.Hierarchies.Clear(); _runtime.Profile.OmittedRoots.Clear();
            _runtime.Profile.LeftAnchor = _runtime.Profile.RightAnchor = null; SaveBinding("Automatic mapping restored.");
        }
        private void SaveBinding(string message, bool save = true)
        { if (save) _runtime.SaveProfile(); BindingMessage = message; NotifyPropertyChanged(nameof(BindingMessage)); }
        [UIAction("retry")] private void Retry() { _runtime.SaveProfile(); _runtime.RetryBinding(); }
        [UIAction("close-binding")] private void CloseBinding() { BindingFinished(); }
    }
}
