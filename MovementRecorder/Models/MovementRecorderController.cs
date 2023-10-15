using MovementRecorder.Configuration;
using MovementRecorder.HarmonyPatches;
using UnityEngine;
using Zenject;

namespace MovementRecorder.Models
{
    /// <summary>
    /// Monobehaviours (scripts) are added to GameObjects.
    /// For a full list of Messages a Monobehaviour can receive from the game, see https://docs.unity3d.com/ScriptReference/MonoBehaviour.html.
    /// </summary>
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

        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
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
        /// <summary>
        /// Only ever called once on the first frame the script is Enabled. Start is called after every other script's Awake() and before Update().
        /// </summary>
        private void Start()
        {
        }

        /// <summary>
        /// Called every frame if the script is enabled.
        /// </summary>
        private void Update()
        {
            if (!this._songStart)
                return;
            if (this._audioTimeSource.songTime - this._recordData.GetLastRecordTiem() < this._recordInterval)
                return;
            this._recordData.TransformRecord(this._audioTimeSource.songTime);
        }

        /// <summary>
        /// Called every frame after every other enabled script's Update().
        /// </summary>
        private void LateUpdate()
        {

        }

        /// <summary>
        /// Called when the script becomes enabled and active
        /// </summary>
        private void OnEnable()
        {

        }

        /// <summary>
        /// Called when the script becomes disabled or when it is being destroyed.
        /// </summary>
        private void OnDisable()
        {

        }

        /// <summary>
        /// Called when the script is being destroyed.
        /// </summary>
        private void OnDestroy()
        {
            if (!PluginConfig.Instance.enabled)
                return;
            StartSongPatch.StartSong -= this.OnStartSong;
            if (!this._songStart)
                return;
            _ = this._recordData.SavePlaydataAsync();
        }
        #endregion
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
