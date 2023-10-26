using MovementRecorder.HarmonyPatches;
using MovementRecorder.Utility;
using MovementRecorder.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

namespace MovementRecorder.Models
{
    public class RecordData
    {
        public static readonly string MovementRecorderDirectory = "MovementRecorder";
        public event Action<string> recorderLog;
        public List<(UnityEngine.Object, string)> _allObjects;
        public List<(Vector3, Vector3, Quaternion, Quaternion, string)> _allPosRot;
        public List<string> _motionLocalEnabled;
        public List<string> _motionWorldEnabled;
        public List<(Vector3, string)> _motionScales;
        public List<SearchSetting> _searchSettings;
        public Transform[] _transforms;
        public List<string> _objectNames;
        public List<Vector3> _scales;
        public (float, (Vector3, Quaternion)[])[] _recordData;
        public List<(float, int)> _recordNullObjects;
        public string _levelID;
        public string _songName;
        public string _serializedName;
        public string _difficulty;
        public float _startSongTime;
        public bool _wipLevel;
        public int _recordSize;
        public int _recordCount;
        public double _initializeTime;
        public double _recordTimeMax;
        public double _recordTimeMin;
        public double _recordTimeTotal;
        public double _saveTime;
        public Task _saveTask;
        public bool _saveTaskCheck;

        public void ResetData()
        {
            this._allObjects = null;
            this._allPosRot = null;
            this._motionLocalEnabled = null;
            this._motionWorldEnabled = null;
            this._motionScales = null;
            this._searchSettings = null;
            this._levelID = null;
            this._songName = null;
            this._serializedName = null;
            this._difficulty = null;
            this._transforms = null;
            this._objectNames = null;
            this._scales = null;
            this._recordData = null;
            this._recordNullObjects = null;
        }
        public void ResetCount()
        {
            this._wipLevel = false;
            this._startSongTime = 0;
            this._initializeTime = 0;
            this._recordCount = 0;
            this._recordSize = 0;
            this._recordTimeMax = 0;
            this._recordTimeMin = 0;
            this._recordTimeTotal = 0;
            this._saveTime = 0;
        }
        public bool InitializeCheck()
        {
            this._saveTaskCheck = true;
            if (this._saveTask != null)
            {
                var waitTime = 10000;
                if (this._transforms != null && this._transforms.Length > 0 && this._recordCount > 0)
                {
                    var checkTime = PluginConfig.Instance.oneObjectSaveTime * this._transforms.Length * this._recordCount;
                    if (checkTime > waitTime)
                        waitTime = (int)checkTime + 5000;
                }
                this._saveTaskCheck = this._saveTask.Wait(waitTime); //Waitするので、対象のTaskは孫メソッド中まで全てのawaitで.ConfigureAwait(false)しないとデッドロックするので注意
            }
            return this._saveTaskCheck;
        }

        public bool InitializeData(int recordSize, IDifficultyBeatmap difficultyBeatmap, float songTime)
        {
            var timaer = new Stopwatch();
            timaer.Start();
            this.ResetData();
            this.ResetCount();
            this._startSongTime = songTime;
            this._recordSize = recordSize;
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
            this._searchSettings = new List<SearchSetting>();
            foreach (var movementName in PluginConfig.Instance.movementNames)
            {
                if (movementName == PluginConfig.NoneCapture)
                    continue;
                foreach (var searchSetting in PluginConfig.Instance.searchSettings)
                {
                    if (searchSetting.name != movementName)
                        continue;
                    this._searchSettings.Add(searchSetting);
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
            this._recordNullObjects = new List<(float, int)>();
            this._scales = new List<Vector3>();
            foreach (var tarnsform in this._transforms)
                this._scales.Add(tarnsform.localScale);
            if (PluginConfig.Instance.movementResearch)
            {
                this._motionScales = new List<(Vector3, string)>();
                this._allPosRot = new List<(Vector3, Vector3, Quaternion, Quaternion, string)>();
                foreach (var obj in this._allObjects)
                {
                    if (obj.Item1 == null)
                        continue;
                    Transform transform = obj.Item1 as Transform;
                    if (transform == null)
                        continue;
                    this._allPosRot.Add((transform.position, transform.localPosition, transform.rotation, transform.localRotation, obj.Item2));
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
            if (!PluginConfig.Instance.movementResearch || this._motionLocalEnabled != null || this._motionWorldEnabled != null || (songTime - this._startSongTime) < PluginConfig.Instance.researchCheckSongSec)
                return;
            this._motionLocalEnabled = new List<string>();
            this._motionWorldEnabled = new List<string>();
            var allObjects = UnityUtility.GetFullPathNames(UnityEngine.Object.FindObjectsOfType(typeof(Transform)));
            foreach(var obj in allObjects)
            {
                foreach (var posRot in this._allPosRot)
                {
                    if (posRot.Item5 != obj.Item2)
                        continue;
                    if (obj.Item1 == null)
                        continue;
                    Transform transform = obj.Item1 as Transform;
                    if (transform == null)
                        continue;
                    if (posRot.Item1 != transform.position || posRot.Item3 != transform.rotation)
                        this._motionWorldEnabled.Add(obj.Item2);
                    if (posRot.Item2 != transform.localPosition || posRot.Item4 != transform.localRotation)
                        this._motionLocalEnabled.Add(obj.Item2);
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
                if (this._transforms[i] == null)
                {
                    this._recordNullObjects.Add((songTime, i));
                    this._recordData[this._recordCount].Item2[i] = (Vector3.zero, Quaternion.identity);
                    continue;
                }
                    this._recordData[this._recordCount].Item2[i] = (this._transforms[i].position, this._transforms[i].rotation);
            }
            this._recordCount++;
            if (this._recordData.Length <= this._recordCount)
            {
                Array.Resize(ref this._recordData, this._recordData.Length + 1000);
                for (int i = this._recordData.Length - 1000; i < this._recordData.Length; i++)
                    this._recordData[i].Item2 = new (Vector3, Quaternion)[this._transforms.Length];
            }
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
        public void SavePlaydata()
        {
            if (this._saveTask != null && !this._saveTask.IsCompleted)
                return;
            this._saveTaskCheck = true;
            this._saveTask = this.SavePlaydataAsync();
        }
        public async Task SavePlaydataAsync()
        {
            //Restart用にWaitするので、孫メソッド中まで全てのawaitで.ConfigureAwait(false)しないとデッドロックするので注意
            var timaer = new Stopwatch();
            timaer.Start();
            var savePath = Path.Combine(GetCoverImageAsyncPatch.CustomLevelPath, MovementRecorderDirectory);
            if (!this._wipLevel)
                savePath = Path.Combine(IPA.Utilities.UnityGame.UserDataPath, MovementRecorderDirectory);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            var filename = $"{DateTime.Now:yyyyMMddHHmmss}-{this._songName}-{this._difficulty}-{this._serializedName}-{(int)this.GetLastRecordTiem()}s.mvrec";
            var regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            filename = r.Replace(filename, "_");
            long saveFileSize = 0;
            if (PluginConfig.Instance.movementResearch)
            {
                try
                {
                    this._motionLocalEnabled.Sort();
                    this._motionWorldEnabled.Sort();
                    this._motionScales.Sort((a, b) => a.Item2.CompareTo(b.Item2));
                    var researchData = new ResearchJson
                    {
                        recordObjectNames = this._objectNames,
                        motionLocalEnabled = this._motionLocalEnabled.Distinct().ToList(),
                        motionWorldEnabled = this._motionWorldEnabled.Distinct().ToList(),
                        otherOneScales = new List<MotionScales>()
                    };
                    foreach (var scale in this._motionScales.Distinct())
                    {
                        researchData.otherOneScales.Add(new MotionScales()
                        {
                            objectName = scale.Item2,
                            scale = $"{scale.Item1.x} {scale.Item1.y} {scale.Item1.z}"
                        });
                    }
                    var serialized = await Task.Run(() => JsonConvert.SerializeObject(researchData, Formatting.Indented)).ConfigureAwait(false);
                    if (!await this.WriteAllTextAsync(Path.Combine(savePath, "Motion_Research_Data.json"), serialized).ConfigureAwait(false))
                        throw new Exception("Failed save Research Data");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.Error(ex.ToString());
                }
            }
            var meta = new MovementJson
            {
                objectCount = this._transforms.Length,
                recordCount = this._recordCount,
                levelID = this._levelID,
                songName = this._songName,
                serializedName = this._serializedName,
                difficulty = this._difficulty,
                Settings = new List<Setting>(),
                recordFrameRate = PluginConfig.Instance.recordFrameRate,
                objectNames = this._objectNames,
                objectScales = new List<Scale>()
            };
            foreach (var recordNullObject in this._recordNullObjects)
            {
                meta.recordNullObjects.Add(new NUllObject
                {
                    songTime = recordNullObject.Item1,
                    objIndex = recordNullObject.Item2
                });
            }
            foreach (var searchSetting in this._searchSettings)
            {
                meta.Settings.Add(new Setting
                {
                    name = searchSetting.name,
                    type = searchSetting.type,
                    topObjectStrings = searchSetting.topObjectStrings,
                    rescaleString = searchSetting.rescaleString,
                    searchStirngs = searchSetting.searchStirngs,
                    exclusionStrings = searchSetting.exclusionStrings
                });
            }
            foreach (var item in this._scales)
                meta.objectScales.Add(new Scale() { x = item.x, y = item.y, z = item.z });
            try
            {
                var metaSerialized = await Task.Run(() => JsonConvert.SerializeObject(meta, Formatting.None)).ConfigureAwait(false);
                await Task.Run(() =>
                {
                    using (var writer = new BinaryWriter(File.Open(Path.Combine(savePath, filename), FileMode.Create), Encoding.UTF8))
                    {
                        writer.Write(metaSerialized);
                        for (var i = 0; i < this._recordCount; i++)
                        {
                            writer.Write(this._recordData[i].Item1);
                            foreach (var item in this._recordData[i].Item2)
                            {
                                writer.Write(item.Item1.x);
                                writer.Write(item.Item1.y);
                                writer.Write(item.Item1.z);
                                writer.Write(item.Item2.x);
                                writer.Write(item.Item2.y);
                                writer.Write(item.Item2.z);
                                writer.Write(item.Item2.w);
                            }
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error(ex.ToString());
            }
            var fi = new FileInfo(Path.Combine(savePath, filename));
            saveFileSize = fi.Length;
            this._saveTime = timaer.Elapsed.TotalMilliseconds;
            PluginConfig.Instance.oneObjectSaveTime = this._saveTime / (this._transforms.Length * this._recordCount);
            if (!this._saveTaskCheck)
                return;
            Plugin.Log?.Info($"Save Time:{this._saveTime}ms  Record Count:{this._recordCount} One Time Record Ave:{this._recordTimeTotal / this._recordCount}ms Max:{this._recordTimeMax}ms Min:{this._recordTimeMin}ms");
            var log = $"=== Movement Recorder Log ===\nInitialize Time:{this._initializeTime}ms \t\tCapture Object Count:{this._transforms.Length}\nRecord Initialize Size:{this._recordSize} \t" +
                $"Record Count:{this._recordCount}\nLast Song Time:{this.GetLastRecordTiem()}s\tSave File Time:{this._saveTime}ms\n" +
                $"Save File Size:{Math.Floor(saveFileSize / 10485.76d) / 100}Mbyte\nOne Movement Recording Time\n" +
                $"Ave:{Math.Floor(this._recordTimeTotal / this._recordCount * 1000) / 1000}ms \tMax:{this._recordTimeMax}ms \tMin:{this._recordTimeMin}ms";
            this.recorderLog?.Invoke(log);
            this.ResetData();
            timaer.Stop();
        }
        public async Task<bool> WriteAllTextAsync(string path, string contents)
        {
            bool result;
            try
            {
                using (var sw = new StreamWriter(path))
                {
                    await sw.WriteAsync(contents).ConfigureAwait(false);  //WriteAsyncは内部のawaitが.ConfigureAwait(continueOnCapturedContext: false)されている
                }
                result = true;
            }
            catch (Exception e)
            {
                Plugin.Log?.Error(e.ToString());
                result = false;
            }
            return result;
        }
    }
}
