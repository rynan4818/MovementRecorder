using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.GameplaySetup;
using MovementRecorder.Configuration;
using MovementRecorder.Models;
using System;
using System.Collections.Generic;
using TMPro;
using Zenject;

namespace MovementRecorder.Views
{
    public class SettingTabViewController : IInitializable, IDisposable
    {
        private bool _disposedValue;
        private readonly RecordData _recordData;
        private readonly GameplaySetup _gameplaySetup;
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

        private SettingTabViewController(RecordData recordData, GameplaySetup gameplaySetup)
        {
            this._recordData = recordData;
            this._gameplaySetup = gameplaySetup;
        }
        public void Initialize()
        {
            this._gameplaySetup.AddTab(TabName, this.ResourceName, this, MenuType.Solo);
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
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposedValue)
            {
                if (disposing)
                {
                    this._recordData.recorderLog -= this.OnRecorderLog;
                    this._gameplaySetup?.RemoveTab(TabName);
                }
                this._disposedValue = true;
            }
        }
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
            set
            {
                PluginConfig.Instance.enabled = value;
                this._recordData.ResetRecord();
            }
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
            set
            {
                PluginConfig.Instance.movementNames[0] = value;
                this._recordData.ResetRecord();
            }
        }
        [UIValue("saberMovement")]
        public string saberMovement
        {
            get => PluginConfig.Instance.movementNames[1];
            set
            {
                PluginConfig.Instance.movementNames[1] = value;
                this._recordData.ResetRecord();
            }
        }
        [UIValue("otherMovement1")]
        public string otherMovement1
        {
            get => PluginConfig.Instance.movementNames[2];
            set
            {
                PluginConfig.Instance.movementNames[2] = value;
                this._recordData.ResetRecord();
            }
        }
        [UIValue("otherMovement2")]
        public string otherMovement2
        {
            get => PluginConfig.Instance.movementNames[3];
            set
            {
                PluginConfig.Instance.movementNames[3] = value;
                this._recordData.ResetRecord();
            }
        }
        [UIValue("otherMovement3")]
        public string otherMovement3
        {
            get => PluginConfig.Instance.movementNames[4];
            set
            {
                PluginConfig.Instance.movementNames[4] = value;
                this._recordData.ResetRecord();
            }
        }
        [UIValue("recordFrameRate")]
        public int recordFrameRate
        {
            get => PluginConfig.Instance.recordFrameRate;
            set
            {
                PluginConfig.Instance.recordFrameRate = value;
                this._recordData.ResetRecord();
            }
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
        [UIValue("notDisposeMemory")]
        public bool notDisposeMemory
        {
            get => PluginConfig.Instance.notDisposeMemory;
            set
            {
                PluginConfig.Instance.notDisposeMemory = value;
                this._recordData.ResetRecord();
            }
        }
        [UIValue("minMemoryAllocation")]
        public int minMemoryAllocation
        {
            get => PluginConfig.Instance.minMemoryAllocation;
            set
            {
                PluginConfig.Instance.minMemoryAllocation = value;
                this._recordData.ResetRecord();
            }
        }
        public void OnRecorderLog(string log)
        {
            this.recorderLog.text = log;
        }
    }
}
