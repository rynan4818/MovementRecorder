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

        [UIValue("avatarMovementChoices")]
        public List<object> avatarMovementChoices { get; set; } = new List<object>();
        [UIValue("saberMovementChoices")]
        public List<object> saberMovementChoices { get; set; } = new List<object>();
        [UIValue("otherMovementChoices")]
        public List<object> otherMovementChoices { get; set; } = new List<object>();
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
            this.avatarMovementChoices.Add(PluginConfig.NoneCapture);
            this.saberMovementChoices.Add(PluginConfig.NoneCapture);
            this.otherMovementChoices.Add(PluginConfig.NoneCapture);
            foreach (var searchSetting in PluginConfig.Instance.searchSettings)
            {
                if (searchSetting.type == PluginConfig.AvatarType)
                    this.avatarMovementChoices.Add(searchSetting.name);
                else if (searchSetting.type == PluginConfig.SaberType)
                    this.saberMovementChoices.Add(searchSetting.name);
                else if (searchSetting.type == PluginConfig.OtherType)
                    this.otherMovementChoices.Add(searchSetting.name);
            }
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
        [UIValue("avatarMovement")]
        public string avatarMovement
        {
            get => PluginConfig.Instance.movementNames[0];
            set => PluginConfig.Instance.movementNames[0] = value;
        }
        [UIValue("saberMovement")]
        public string saberMovement
        {
            get => PluginConfig.Instance.movementNames[1];
            set => PluginConfig.Instance.movementNames[1] = value;
        }
        [UIValue("otherMovement1")]
        public string otherMovement1
        {
            get => PluginConfig.Instance.movementNames[2];
            set => PluginConfig.Instance.movementNames[2] = value;
        }
        [UIValue("otherMovement2")]
        public string otherMovement2
        {
            get => PluginConfig.Instance.movementNames[3];
            set => PluginConfig.Instance.movementNames[3] = value;
        }
        [UIValue("otherMovement3")]
        public string otherMovement3
        {
            get => PluginConfig.Instance.movementNames[4];
            set => PluginConfig.Instance.movementNames[4] = value;
        }
        [UIValue("recordFrameRate")]
        public int recordFrameRate
        {
            get => PluginConfig.Instance.recordFrameRate;
            set => PluginConfig.Instance.recordFrameRate = value;
        }
        [UIValue("movementResearch")]
        public bool motionResearch
        {
            get => PluginConfig.Instance.movementResearch;
            set => PluginConfig.Instance.movementResearch = value;
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
