using MovementRecorder.Configuration;
using MovementRecorder.HarmonyPatches;
using UnityEngine;
using Zenject;

namespace MovementRecorder.Models
{
    [DefaultExecutionOrder(30000)]
	public class MovementRecorderController : MonoBehaviour
    {
        private IAudioTimeSource _audioTimeSource;
        private GameplayCoreSceneSetupData _gameplayCoreSceneSetupData;
        private RecordData _recordData;
        public bool _songStart;
        public float _recordInterval;
        public bool _init;

        [Inject]
        private void Constractor(IAudioTimeSource audioTimeSource, GameplayCoreSceneSetupData gameplayCoreSceneSetupData, RecordData recordData)
        {
            this._audioTimeSource = audioTimeSource;
            this._gameplayCoreSceneSetupData = gameplayCoreSceneSetupData;
            this._recordData = recordData;
            this._init = this._recordData.InitializeCheck();
        }

        private void Awake()
        {
            if (!this._init)
                return;
            this._songStart = false;
            if (!PluginConfig.Instance.enabled)
                return;
            StartSongPatch.StartSong += this.OnStartSong;
            if (PluginConfig.Instance.recordFrameRate > 1)
                this._recordInterval = 1.0f / (float)PluginConfig.Instance.recordFrameRate;
            else
                this._recordInterval = 1.0f;
        }
        private void LateUpdate()
        {
            if (!this._init)
                return;
            //LateUpdateで呼ばないとオブジェクト座標が正しく取れないものがある。
            //VRIKの座標更新はLateUpdateのため、念のためDefaultExecutionOrderを30000にする。
            if (!this._songStart)
                return;
            if (this._audioTimeSource.songTime - this._recordData.GetLastRecordTiem() < this._recordInterval)
                return;
            this._recordData.TransformRecord(this._audioTimeSource.songTime);
        }

        private void OnDestroy()
        {
            if (!this._init)
                return;
            if (!PluginConfig.Instance.enabled)
                return;
            StartSongPatch.StartSong -= this.OnStartSong;
            if (!this._songStart)
                return;
            this._recordData.SavePlaydata();
        }
        public void OnStartSong()
        {
            if (!this._init)
                return;
            var songLength = this._audioTimeSource.songLength;
            var minLength = (float)(PluginConfig.Instance.minMemoryAllocation * 60);
            if (PluginConfig.Instance.notDisposeMemory && songLength < minLength)
                songLength = minLength;
            var recordSize = (int)(songLength / this._recordInterval) + 100;
            var resetRsult = this._recordData.InitializeData(recordSize, this._gameplayCoreSceneSetupData, this._audioTimeSource.songTime);
            if (!resetRsult)
                return;
            Plugin.Log?.Info($"Record Initialize Size:{recordSize} Initialize Time:{this._recordData._initializeTime}ms");
            this._recordData.TransformRecord(this._audioTimeSource.songTime, false);
            this._songStart = true;
        }
    }
}
