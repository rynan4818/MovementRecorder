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

        [Inject]
        private void Constractor(IAudioTimeSource audioTimeSource, GameplayCoreSceneSetupData gameplayCoreSceneSetupData, RecordData recordData)
        {
            this._audioTimeSource = audioTimeSource;
            this._gameplayCoreSceneSetupData = gameplayCoreSceneSetupData;
            this._recordData = recordData;
        }

        private void Awake()
        {
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
            if (!PluginConfig.Instance.enabled)
                return;
            StartSongPatch.StartSong -= this.OnStartSong;
            if (!this._songStart)
                return;
            _ = this._recordData.SavePlaydataAsync();
        }
        public void OnStartSong()
        {
            var recordSize = (int)(this._audioTimeSource.songLength / this._recordInterval) + 100;
            var resetRsult = this._recordData.InitializeData(recordSize, this._gameplayCoreSceneSetupData.difficultyBeatmap);
            if (!resetRsult)
                return;
            Plugin.Log?.Info($"Record Initialize Size:{recordSize} Initialize Time:{this._recordData._initializeTime}ms");
            this._recordData.TransformRecord(this._audioTimeSource.songTime, false);
            this._songStart = true;
        }
    }
}
