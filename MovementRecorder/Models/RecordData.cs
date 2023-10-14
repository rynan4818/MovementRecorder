using MovementRecorder.HarmonyPatches;
using MovementRecorder.Utility;
using MovementRecorder.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;

namespace MovementRecorder.Models
{
    public class RecordData
    {
        public static readonly string MovementRecorderDirectory = "MovementRecorder";
        public static SemaphoreSlim RecordsSemaphore = new SemaphoreSlim(1, 1);
        public Transform[] _transforms;
        public List<string> _objectNames;
        public List<Vector3> _scales;
        public (float, (Vector3, Quaternion)[])[] _recordData;
        public string _levelID;
        public string _songName;
        public string _serializedName;
        public string _difficulty;
        public bool _wipLevel;
        public int _recordCount;
        public double _initializeTime;
        public double _recordTimeMax;
        public double _recordTimeMin;
        public double _recordTimeTotal;
        public double _saveTime;

        public void InitializeData()
        {
            this._levelID = null;
            this._songName = null;
            this._serializedName = null;
            this._difficulty = null;
            this._transforms = null;
            this._objectNames = null;
            this._scales = null;
            this._recordData = null;
        }
        public void InitializeCount()
        {
            this._wipLevel = false;
            this._initializeTime = 0;
            this._recordCount = 0;
            this._recordTimeMax = 0;
            this._recordTimeMin = 0;
            this._recordTimeTotal = 0;
            this._saveTime = 0;
        }

        public bool ResetData(int recordSize, IDifficultyBeatmap difficultyBeatmap)
        {
            var timaer = new Stopwatch();
            timaer.Start();
            this.InitializeData();
            this.InitializeCount();
            this._levelID = difficultyBeatmap.level.levelID;
            this._songName = difficultyBeatmap.level.songName;
            this._serializedName = difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
            this._difficulty = difficultyBeatmap.difficulty.Name();
            foreach (var customWIPLevel in SongCore.Loader.CustomWIPLevels)
            {
                if (customWIPLevel.Value.levelID == this._levelID)
                {
                    this._wipLevel = true;
                    break;
                }
            }
            if (PluginConfig.Instance.wipOnly && !this._wipLevel)
            {
                timaer.Stop();
                return false;
            }
            var allTransforms = UnityUtility.GetFullPathNames(UnityEngine.Object.FindObjectsOfType(typeof(Transform)) as Transform[]);
            var transforms = new List<Transform>();
            this._objectNames = new List<string>();
            foreach (var motionCapture in PluginConfig.Instance.motionCaptures)
            {
                if (motionCapture == PluginConfig.NoneCapture)
                    continue;
                foreach (var searchSetting in PluginConfig.Instance.searchSettings)
                {
                    if (searchSetting.name != motionCapture)
                        continue;
                    foreach(var searchStirng in searchSetting.searchStirngs)
                    {
                        var addTransforms = UnityUtility.FindGetTransform(allTransforms, searchStirng, searchSetting.exclusionStrings);
                        transforms.AddRange(addTransforms.Item1);
                        this._objectNames.AddRange(addTransforms.Item2);
                    }
                }
            }
            if (transforms == null)
            {
                timaer.Stop();
                return false;
            }
            this._transforms = transforms.ToArray();
            Plugin.Log?.Info($"Capture Object Count : {this._transforms.Length}");
            this._recordData = new (float, (Vector3, Quaternion)[])[recordSize];
            for (int i = 0; i < recordSize; i++)
                this._recordData[i].Item2 = new (Vector3, Quaternion)[this._transforms.Length];
            this._scales = new List<Vector3>();
            foreach (var tarnsform in this._transforms)
                this._scales.Add(tarnsform.localScale);
            this._initializeTime = timaer.Elapsed.TotalMilliseconds;
            timaer.Stop();
            return true;
        }
        public float GetLastRecordTiem()
        {
            if (this._recordCount == 0)
                return 0;
            return this._recordData[this._recordCount - 1].Item1;
        }
        public void TransformRecord(float songTime, bool timeCount = true)
        {
            var timaer = new Stopwatch();
            timaer.Start();
            this._recordData[this._recordCount].Item1 = songTime;
            for (int i = 0; i < this._transforms.Length; i++)
                this._recordData[this._recordCount].Item2[i] = (this._transforms[i].position, this._transforms[i].rotation);
            this._recordCount++;
            if (this._recordData.Length <= this._recordCount)
                Array.Resize(ref this._recordData, this._recordData.Length + 100);
            if (timeCount)
            {
                if (this._recordTimeMax < timaer.Elapsed.TotalMilliseconds)
                    this._recordTimeMax = timaer.Elapsed.TotalMilliseconds;
                if (this._recordTimeMin > timaer.Elapsed.TotalMilliseconds || this._recordTimeMin == 0)
                    this._recordTimeMin = timaer.Elapsed.TotalMilliseconds;
                this._recordTimeTotal += timaer.Elapsed.TotalMilliseconds;
            }
            timaer.Stop();
        }
        public async Task SavePlaydataAsync()
        {
            if (this._recordCount == 0)
                return;
            var timaer = new Stopwatch();
            timaer.Start();
            var saveData = new MovementJson();
            saveData.objectNames = new List<string>();
            foreach (var item in this._objectNames)
                saveData.objectNames.Add(item);
            saveData.objectScales = new List<Scale>();
            foreach (var item in this._scales)
                saveData.objectScales.Add(new Scale() { x = item.x, y = item.y, z = item.z });
            saveData.records = new List<Record>();
            for (int i = 0; i < this._recordCount; i++)
            {
                var record = new Record();
                record.songTIme = this._recordData[i].Item1;
                record.posX = new List<float>();
                record.posY = new List<float>();
                record.posZ = new List<float>();
                record.rotX = new List<float>();
                record.rotY = new List<float>();
                record.rotZ = new List<float>();
                record.rotW = new List<float>();
                foreach(var item in this._recordData[i].Item2)
                {
                    record.posX.Add(item.Item1.x);
                    record.posY.Add(item.Item1.y);
                    record.posZ.Add(item.Item1.z);
                    record.rotX.Add(item.Item2.x);
                    record.rotY.Add(item.Item2.y);
                    record.rotZ.Add(item.Item2.z);
                    record.rotW.Add(item.Item2.w);
                }
                saveData.records.Add(record);
            }
            var savePath = Path.Combine(GetCoverImageAsyncPatch.CustomLevelPath, MovementRecorderDirectory);
            if (!this._wipLevel)
                savePath = Path.Combine(IPA.Utilities.UnityGame.UserDataPath, MovementRecorderDirectory);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            var filename = $"{DateTime.Now:yyyyMMddHHmmss}-{this._songName}-{this._difficulty}-{this._serializedName}-{(int)GetLastRecordTiem()}s.json";
            var regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            filename = r.Replace(filename, "_");
            try
            {
                var serialized = JsonConvert.SerializeObject(saveData, Formatting.None);
                if (!await this.WriteAllTextAsync(Path.Combine(savePath, filename), serialized))
                    throw new Exception("Failed save Movement Data");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error(ex.ToString());
            }
            this.InitializeData();
            this._saveTime = timaer.Elapsed.TotalMilliseconds;
            Plugin.Log?.Info($"Save Time:{this._saveTime}ms  Record Count:{this._recordCount} One Time Record Ave:{this._recordTimeTotal / this._recordCount}ms Max:{this._recordTimeMax}ms Min:{this._recordTimeMin}ms");
            timaer.Stop();
        }
        public async Task<bool> WriteAllTextAsync(string path, string contents)
        {
            bool result;
            await RecordsSemaphore.WaitAsync();
            try
            {
                using (var sw = new StreamWriter(path))
                {
                    await sw.WriteAsync(contents);
                }
                result = true;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error(e.ToString());
                result = false;
            }
            finally
            {
                RecordsSemaphore.Release();
            }
            return result;
        }
    }
}
