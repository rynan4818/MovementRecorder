using MovementRecorder.Utility;
using MovementRecorder.HarmonyPatches;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Linq;

namespace MovementRecorder.Models
{
    public class RecordData
    {
        public static SemaphoreSlim RecordsSemaphore = new SemaphoreSlim(1, 1);
        public Transform[] _avatarTransforms;
        public string[] _avatarObjectNames;
        public (float, (Vector3, Quaternion)[])[] _recordData;
        public Vector3 _avatarScale;
        public int _recordCount;
        public double _recordTimeMax;
        public double _recordTimeMin;
        public double _recordTimeTotal;

        public void InitializeData()
        {
            this._avatarTransforms = null;
            this._avatarObjectNames = null;
            this._recordData = null;
            this._avatarScale = Vector3.zero;
            this._recordCount = 0;
            this._recordTimeMax = 0;
            this._recordTimeMin = 0;
            this._recordTimeTotal = 0;
        }

        public bool ResetData(int recordSize)
        {
            this.InitializeData();
            var avatar = GameObject.Find("Avatar Container/SpawnedAvatar(EruruChan_D)")?.GetComponent<Transform>();
            if (avatar == null)
                return false;
            this._avatarScale = avatar.localScale;
            Plugin.Log?.Info($"{this._avatarScale}");
            var avatarArmature = GameObject.Find("Avatar Container/SpawnedAvatar(EruruChan_D)/CustomAvatar_Eruru_Twintail_Long_high/Armature")?.GetComponentsInChildren<Transform>();
            var leftSaber = GameObject.Find("VRGameCore/LeftHand/LeftSaber/SfSaberModelController(Clone)/SF Saber/LeftSaber(Clone)")?.GetComponentsInChildren<Transform>();
            var rightSaber = GameObject.Find("VRGameCore/RightHand/RightSaber/SfSaberModelController(Clone)/SF Saber/RightSaber(Clone)")?.GetComponentsInChildren<Transform>();
            this._avatarTransforms = avatarArmature.Union(leftSaber).Union(rightSaber).ToArray();
            if (this._avatarTransforms == null)
                return false;
            var avatarObjectLength = this._avatarTransforms.Length;
            Plugin.Log?.Info($"{avatarObjectLength}");
            this._avatarObjectNames = new string[avatarObjectLength];
            for (int i = 0; i < avatarObjectLength; i++)
                this._avatarObjectNames[i] = this._avatarTransforms[i].GetFullPathName();
            this._recordData = new (float, (Vector3, Quaternion)[])[recordSize];
            for (int i = 0; i < recordSize; i++)
                this._recordData[i].Item2 = new (Vector3, Quaternion)[avatarObjectLength];
            return true;
        }
        public float GetLastRecordTiem()
        {
            if (this._recordCount == 0)
                return 0;
            return this._recordData[this._recordCount - 1].Item1;
        }
        public void TransformRecord(float songTime)
        {
            var timaer = new Stopwatch();
            timaer.Start();
            this._recordData[this._recordCount].Item1 = songTime;
            for (int i = 0; i < this._avatarTransforms.Length; i++)
                this._recordData[this._recordCount].Item2[i] = (this._avatarTransforms[i].position, this._avatarTransforms[i].rotation);
            this._recordCount++;
            if (this._recordData.Length <= this._recordCount)
                Array.Resize(ref this._recordData, this._recordData.Length + 100);
            if (this._recordTimeMax < timaer.Elapsed.TotalMilliseconds)
                this._recordTimeMax = timaer.Elapsed.TotalMilliseconds;
            if (this._recordTimeMin > timaer.Elapsed.TotalMilliseconds || this._recordTimeMin == 0)
                this._recordTimeMin = timaer.Elapsed.TotalMilliseconds;
            this._recordTimeTotal += timaer.Elapsed.TotalMilliseconds;
            timaer.Stop();
        }
        public async Task SavePlaydataAsync()
        {
            if (this._recordCount == 0)
                return;
            var timaer = new Stopwatch();
            timaer.Start();
            var saveData = new MovementJson();
            saveData.avatarScale = new List<float>() { this._avatarScale.x, this._avatarScale.y, this._avatarScale.z };
            saveData.avatarObjectNames = new List<string>();
            foreach (var item in this._avatarObjectNames)
                saveData.avatarObjectNames.Add(item);
            saveData.record = new List<Record>();
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
                saveData.record.Add(record);
            }
            try
            {
                var serialized = JsonConvert.SerializeObject(saveData, Formatting.None);
                if (!await this.WriteAllTextAsync(Path.Combine(CustomPreviewBeatmapLevelPatch.CustomLevelPath, "Test_Movement.json"), serialized))
                    throw new Exception("Failed save Movement Data");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error(ex.ToString());
            }
            Plugin.Log?.Info($"Save Time:{timaer.Elapsed.TotalMilliseconds}ms  Record Count:{this._recordCount} One Time Record Ave:{this._recordTimeTotal / this._recordCount}ms Max:{this._recordTimeMax}ms Min:{this._recordTimeMin}ms");
            timaer.Stop();
            this.InitializeData();
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
