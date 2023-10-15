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
using System.Linq;

namespace MovementRecorder.Models
{
    public class RecordData
    {
        public static readonly string MovementRecorderDirectory = "MovementRecorder";
        public static SemaphoreSlim RecordsSemaphore = new SemaphoreSlim(1, 1);
        public List<(UnityEngine.Object, string)> _allObjects;
        public List<(Vector3, Quaternion, string)> _allPosRot;
        public List<string> _motionEnabled;
        public List<(Vector3, string)> _motionScales;
        public List<string> _motionCaptures;
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

        public void ResetData()
        {
            this._allObjects = null;
            this._allPosRot = null;
            this._motionEnabled = null;
            this._motionScales = null;
            this._levelID = null;
            this._songName = null;
            this._serializedName = null;
            this._difficulty = null;
            this._transforms = null;
            this._objectNames = null;
            this._scales = null;
            this._recordData = null;
        }
        public void ResetCount()
        {
            this._motionCaptures = new List<string>();
            this._wipLevel = false;
            this._initializeTime = 0;
            this._recordCount = 0;
            this._recordTimeMax = 0;
            this._recordTimeMin = 0;
            this._recordTimeTotal = 0;
            this._saveTime = 0;
        }

        public bool InitializeData(int recordSize, IDifficultyBeatmap difficultyBeatmap)
        {
            var timaer = new Stopwatch();
            timaer.Start();
            this.ResetData();
            this.ResetCount();
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
            this._allObjects = UnityUtility.GetFullPathNames(UnityEngine.Object.FindObjectsOfType(typeof(Transform)));
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
                    this._motionCaptures.Add(motionCapture);
                    foreach (var searchStirng in searchSetting.searchStirngs)
                    {
                        var addTransforms = UnityUtility.FindGetTransform(this._allObjects, searchStirng, searchSetting.exclusionStrings);
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
            if (PluginConfig.Instance.motionResearch)
            {
                this._motionScales = new List<(Vector3, string)>();
                this._allPosRot = new List<(Vector3, Quaternion, string)>();
                foreach (var obj in this._allObjects)
                {
                    if (obj.Item1 == null)
                        continue;
                    Transform transform = obj.Item1 as Transform;
                    if (transform == null)
                        continue;
                    this._allPosRot.Add((transform.localPosition, transform.localRotation, obj.Item2));
                    if (transform.localScale != Vector3.one)
                        this._motionScales.Add((transform.localScale, obj.Item2));
                }
            }
            this._initializeTime = timaer.Elapsed.TotalMilliseconds;
            timaer.Stop();
            return true;
        }
        public void MotionResearchCheck(float songTime)
        {
            if (!PluginConfig.Instance.motionResearch || this._motionEnabled != null || songTime < PluginConfig.Instance.researchCheckSongSec)
                return;
            this._motionEnabled = new List<string>();
            var allObjects = UnityUtility.GetFullPathNames(UnityEngine.Object.FindObjectsOfType(typeof(Transform)));
            foreach(var obj in allObjects)
            {
                foreach (var posRot in this._allPosRot)
                {
                    if (posRot.Item3 != obj.Item2)
                        continue;
                    if (obj.Item1 == null)
                        continue;
                    Transform transform = obj.Item1 as Transform;
                    if (transform == null)
                        continue;
                    if (posRot.Item1 != transform.localPosition || posRot.Item2 != transform.localRotation)
                        this._motionEnabled.Add(obj.Item2);
                }
            }
        }
        public float GetLastRecordTiem()
        {
            if (this._recordCount == 0)
                return 0;
            return this._recordData[this._recordCount - 1].Item1;
        }
        public void TransformRecord(float songTime, bool timeCount = true)
        {
            this.MotionResearchCheck(songTime);
            if (this._transforms.Length == 0)
                return;
            var timaer = new Stopwatch();
            timaer.Start();
            this._recordData[this._recordCount].Item1 = songTime;
            for (int i = 0; i < this._transforms.Length; i++)
            {
                if (this._transforms[i] != null)
                    this._recordData[this._recordCount].Item2[i] = (this._transforms[i].position, this._transforms[i].rotation);
            }
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
            var timaer = new Stopwatch();
            timaer.Start();
            var saveData = new MovementJson();
            saveData.recordInterval = PluginConfig.Instance.recordInterval;
            saveData.motionCaptures = this._motionCaptures;
            saveData.objectNames = this._objectNames;
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
            if (PluginConfig.Instance.motionResearch)
            {
                try
                {
                    this._motionEnabled.Sort();
                    this._motionScales.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                    var researchData = new ResearchJson();
                    researchData.motionEnabled = this._motionEnabled.Distinct().ToList();
                    researchData.motionScales = new List<MotionScales>();
                    foreach (var scale in this._motionScales.Distinct())
                    {
                        researchData.motionScales.Add(new MotionScales()
                        {
                            objectName = scale.Item2,
                            scale = $"{scale.Item1.x} {scale.Item1.y} {scale.Item1.z}"
                        });
                    }
                    var serialized = JsonConvert.SerializeObject(researchData, Formatting.Indented);
                    if (!await this.WriteAllTextAsync(Path.Combine(savePath, "Motion_Research_Data.json"), serialized))
                        throw new Exception("Failed save Research Data");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.Error(ex.ToString());
                }
            }
            this.ResetData();
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
