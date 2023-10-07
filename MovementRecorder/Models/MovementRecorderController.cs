using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        public static readonly float RecordInterval = 0.033f;
        private readonly CancellationTokenSource connectionClosed = new CancellationTokenSource();
        private IAudioTimeSource _audioTimeSource;
        private RecordData _recordData;
        public bool _songStart;

        [Inject]
        private void Constractor(IAudioTimeSource audioTimeSource, RecordData recordData)
        {
            this._audioTimeSource = audioTimeSource;
            this._recordData = recordData;
        }

        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
        private void Awake()
        {
        }
        /// <summary>
        /// Only ever called once on the first frame the script is Enabled. Start is called after every other script's Awake() and before Update().
        /// </summary>
        private void Start()
        {
            this._songStart = false;
            _ = this.SongStartWait();
        }

        /// <summary>
        /// Called every frame if the script is enabled.
        /// </summary>
        private void Update()
        {
            if (!this._songStart)
                return;
            if (this._audioTimeSource.songTime - this._recordData.GetLastRecordTiem() < RecordInterval)
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
            this._recordData._avatarTransforms = null;
            this.connectionClosed.Cancel();
        }
        #endregion
        /// <summary>
        /// 曲スタートまでstart更新を待機
        /// </summary>
        /// <returns></returns>
        public async Task SongStartWait()
        {
            var songTime = this._audioTimeSource.songTime;
            var token = connectionClosed.Token;
            try
            {
                while (this._audioTimeSource.songTime <= songTime)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(10);
                }
            }
            catch (Exception)
            {
                return;
            }
            Plugin.Log?.Info($"SongStart:{this._audioTimeSource.songTime}s");
            var recordSize = (int)(this._audioTimeSource.songLength / RecordInterval) + 100;
            Plugin.Log?.Info($"Record Size:{recordSize}");
            var timaer = new Stopwatch();
            timaer.Start();
            var resetRsult = this._recordData.ResetData(recordSize);
            Plugin.Log?.Info($"Reset Time:{timaer.Elapsed.TotalMilliseconds}ms");
            timaer.Stop();
            if (!resetRsult)
                return;
            this._recordData.TransformRecord(this._audioTimeSource.songTime);
            this._songStart = true;
        }
    }
}
