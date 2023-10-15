using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using BeatSaberMarkupLanguage.ViewControllers;
using MovementRecorder.Configuration;
using MovementRecorder.Models;
using System.Collections.Generic;
using TMPro;
using Zenject;

namespace MovementRecorder.Views
{
    public class SettingTabViewController : BSMLAutomaticViewController, IInitializable
    {
        private RecordData _recordData;
        public static readonly string TabName = "MOVEMENT RECORDER";
        public string ResourceName => string.Join(".", this.GetType().Namespace, this.GetType().Name);

        [UIValue("motionCaptureChoices")]
        public List<object> motionCaptureChoices { get; set; } = new List<object>();
        [UIComponent("recorderLog")]
        public readonly TextMeshProUGUI recorderLog;

        [Inject]
        private void Constractor(RecordData recordData)
        {
            this._recordData = recordData;
        }
        public void Initialize()
        {
            GameplaySetup.instance.AddTab(TabName, this.ResourceName, this, MenuType.Solo);
            this.motionCaptureChoices.Add(PluginConfig.NoneCapture);
            foreach (var searchSetting in PluginConfig.Instance.searchSettings)
                this.motionCaptureChoices.Add(searchSetting.name);
            this._recordData.recorderLog += this.OnRecorderLog;
        }
        protected override void OnDestroy()
        {
            this._recordData.recorderLog -= this.OnRecorderLog;
            GameplaySetup.instance?.RemoveTab(TabName);
            base.OnDestroy();
        }

        [UIAction("#post-parse")]
        internal void PostParse()
        {
            this.OnRecorderLog("=== Movement Recorder Log ===");
        }
        [UIValue("recorderEnabled")]
        public bool recorderEnabled
        {
            get => PluginConfig.Instance.enabled;
            set => PluginConfig.Instance.enabled = value;
        }
        [UIValue("wipOnly")]
        public bool wipOnly
        {
            get => PluginConfig.Instance.wipOnly;
            set => PluginConfig.Instance.wipOnly = value;
        }
        [UIValue("motionCapture1")]
        public string motionCapture1
        {
            get => PluginConfig.Instance.motionCaptures[0];
            set => PluginConfig.Instance.motionCaptures[0] = value;
        }
        [UIValue("motionCapture2")]
        public string motionCapture2
        {
            get => PluginConfig.Instance.motionCaptures[1];
            set => PluginConfig.Instance.motionCaptures[1] = value;
        }
        [UIValue("motionCapture3")]
        public string motionCapture3
        {
            get => PluginConfig.Instance.motionCaptures[2];
            set => PluginConfig.Instance.motionCaptures[2] = value;
        }
        [UIValue("motionCapture4")]
        public string motionCapture4
        {
            get => PluginConfig.Instance.motionCaptures[3];
            set => PluginConfig.Instance.motionCaptures[3] = value;
        }
        [UIValue("motionCapture5")]
        public string motionCapture5
        {
            get => PluginConfig.Instance.motionCaptures[4];
            set => PluginConfig.Instance.motionCaptures[4] = value;
        }
        [UIValue("recordFrameRate")]
        public int recordFrameRate
        {
            get => PluginConfig.Instance.recordFrameRate;
            set => PluginConfig.Instance.recordFrameRate = value;
        }
        [UIValue("motionResearch")]
        public bool motionResearch
        {
            get => PluginConfig.Instance.motionResearch;
            set => PluginConfig.Instance.motionResearch = value;
        }
        [UIValue("worldSpace")]
        public bool worldSpace
        {
            get => PluginConfig.Instance.researchWorldSpace;
            set => PluginConfig.Instance.researchWorldSpace = value;
        }
        [UIValue("researchCheckSongSec")]
        public float researchCheckSongSec
        {
            get => PluginConfig.Instance.researchCheckSongSec;
            set => PluginConfig.Instance.researchCheckSongSec = value;
        }
        public void OnRecorderLog(string log)
        {
            this.recorderLog.text = log;
        }
    }
}
