using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MovementRecorder.Models;

namespace MovementRecorder.Playback.Data
{
    internal sealed class MovementFileReader
    {
        public const int MaxHeaderBytes = 8 * 1024 * 1024;
        public const long DefaultMemoryBudget = 512L * 1024 * 1024;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public MovementFileMetadata ReadMetadata(string path, CancellationToken token = default(CancellationToken))
        {
            using (var stream = Open(path))
            using (var reader = new BinaryReader(stream, Utf8, true))
            {
                token.ThrowIfCancellationRequested();
                ReadHeader(reader, path, out var header, out var metadata);
                if (header.recordCount > 0)
                {
                    metadata.StartTime = reader.ReadSingle();
                    stream.Position = stream.Length - FrameBytes(header.objectCount);
                    metadata.EndTime = reader.ReadSingle();
                    ValidateTimes(metadata.StartTime, metadata.EndTime);
                }
                return metadata;
            }
        }

        public MovementClip ReadClip(string path, MovementFileMetadata expected, long memoryBudget, CancellationToken token)
        {
            using (var stream = Open(path))
            using (var reader = new BinaryReader(stream, Utf8, true))
            {
                ReadHeader(reader, path, out var header, out var metadata);
                if (expected != null && !metadata.SameSource(expected))
                    throw new IOException("記録ファイルが更新されました。一覧を更新して選び直してください。");
                if (header.recordCount == 0) throw new InvalidDataException("記録フレームがありません。");
                long size = checked((long)header.recordCount * FrameBytes(header.objectCount));
                if (memoryBudget < 0 || size + 32L * 1024 * 1024 > memoryBudget || (long)header.recordCount * header.objectCount > int.MaxValue)
                    throw new InvalidDataException("記録データが再生用メモリの上限を超えています。");
                token.ThrowIfCancellationRequested();
                var times = new float[header.recordCount];
                var poses = new RecordedPose[checked(header.recordCount * header.objectCount)];
                var missing = Enumerable.Repeat(float.PositiveInfinity, header.objectCount).ToArray();
                foreach (var item in header.recordNullObjects ?? Enumerable.Empty<NUllObject>())
                    missing[item.objIndex] = Math.Min(missing[item.objIndex], item.songTime);
                for (int frame = 0; frame < times.Length; frame++)
                {
                    token.ThrowIfCancellationRequested();
                    float time = times[frame] = reader.ReadSingle();
                    if (!Number.IsFinite(time) || (frame > 0 && time < times[frame - 1]))
                        throw new InvalidDataException("記録の曲時刻が不正、または逆行しています。");
                    for (int track = 0; track < header.objectCount; track++)
                    {
                        var pose = new RecordedPose { X = reader.ReadSingle(), Y = reader.ReadSingle(), Z = reader.ReadSingle(),
                            Qx = reader.ReadSingle(), Qy = reader.ReadSingle(), Qz = reader.ReadSingle(), Qw = reader.ReadSingle() };
                        if (!pose.IsFinite || (time < missing[track] && !pose.NormalizeRotation()))
                            throw new InvalidDataException($"姿勢データが不正です（フレーム {frame}、対象 {track}）。");
                        poses[frame * header.objectCount + track] = pose;
                    }
                }
                metadata.StartTime = times[0]; metadata.EndTime = times[times.Length - 1];
                foreach (var item in header.recordNullObjects ?? Enumerable.Empty<NUllObject>())
                    if (item.songTime < metadata.StartTime || item.songTime > metadata.EndTime)
                        throw new InvalidDataException("欠損イベントの時刻が記録範囲外です。");
                return new MovementClip(header, metadata, times, poses, missing);
            }
        }

        private static FileStream Open(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
        private static long FrameBytes(int objects) => checked(4L + objects * 28L);
        private static void ValidateTimes(float first, float last)
        {
            if (!Number.IsFinite(first) || !Number.IsFinite(last) || last < first)
                throw new InvalidDataException("記録範囲の曲時刻が不正です。");
        }

        private static void ReadHeader(BinaryReader reader, string path, out MovementJson header, out MovementFileMetadata metadata)
        {
            int length = ReadStringLength(reader);
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException("メタデータが途中で切れています。");
            JObject json;
            using (var text = new StringReader(Utf8.GetString(bytes)))
            using (var jsonReader = new JsonTextReader(text) { MaxDepth = 32, DateParseHandling = DateParseHandling.None })
            {
                json = JObject.Load(jsonReader);
                while (jsonReader.Read()) if (jsonReader.TokenType != JsonToken.Comment) throw new InvalidDataException("メタデータの末尾に余分な内容があります。");
            }
            header = json.ToObject<MovementJson>(new JsonSerializer { TypeNameHandling = TypeNameHandling.None });
            if (header == null || header.objectCount <= 0 || header.objectCount > 32768 || header.recordCount < 0 ||
                header.objectNames == null || header.objectScales == null || header.objectNames.Count != header.objectCount || header.objectScales.Count != header.objectCount)
                throw new InvalidDataException("記録の対象件数・スケール・フレーム数が不正です。");
            if (reader.BaseStream.Length != checked(reader.BaseStream.Position + (long)header.recordCount * FrameBytes(header.objectCount)))
                throw new InvalidDataException("記録ファイルの長さとヘッダーが一致しません。保存途中または未対応の形式です。");
            foreach (string name in header.objectNames)
                if (string.IsNullOrEmpty(name) || name.Length > 8192) throw new InvalidDataException("記録対象のパスが不正です。");
            foreach (var scale in header.objectScales)
                if (scale == null || !Number.IsFinite(scale.x) || !Number.IsFinite(scale.y) || !Number.IsFinite(scale.z))
                    throw new InvalidDataException("記録の初期スケールが不正です。");
            if (header.Settings == null || header.Settings.Count > 64) throw new InvalidDataException("記録のモデル設定が不正です。");
            foreach (var setting in header.Settings)
            {
                if (setting == null) throw new InvalidDataException("空のモデル設定があります。");
                foreach (var patterns in new[] { setting.topObjectStrings, setting.searchStirngs, setting.exclusionStrings })
                    if (patterns != null && (patterns.Count > 128 || patterns.Any(p => p == null || p.Length > 2048)))
                        throw new InvalidDataException("モデルの検索設定が上限を超えています。");
                if (setting.rescaleString != null && setting.rescaleString.Length > 2048) throw new InvalidDataException("ルート検索設定が長すぎます。");
            }
            foreach (var item in header.recordNullObjects ?? Enumerable.Empty<NUllObject>())
                if (item == null || item.objIndex < 0 || item.objIndex >= header.objectCount || !Number.IsFinite(item.songTime))
                    throw new InvalidDataException("対象の欠損イベントが不正です。");
            int? difficulty = ChartIdentity.Difficulty(header.difficulty);
            if (json.TryGetValue("difficultyNum", out var number) && number.Type != JTokenType.Null)
            {
                int value = number.Value<int>();
                if (value < 0 || value > 4 || (difficulty.HasValue && value != difficulty))
                    throw new InvalidDataException("記録の難易度表記が一致しません。");
                difficulty = value;
            }
            string fullPath = System.IO.Path.GetFullPath(path);
            var file = new FileInfo(fullPath);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            metadata = new MovementFileMetadata { Path = fullPath, Folder = file.DirectoryName, Length = reader.BaseStream.Length,
                LastWriteUtcTicks = file.LastWriteTimeUtc.Ticks, HeaderHash = hash, LevelId = header.levelID,
                Characteristic = header.serializedName, Difficulty = difficulty, SongName = header.songName,
                RecordedAtLocal = ChartIdentity.FilenameTime(fullPath), ObjectCount = header.objectCount, FrameCount = header.recordCount,
                RecordFrameRate = header.recordFrameRate, Groups = header.Settings.Select(s => s.type).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray(), Confirmed = true };
        }

        private static int ReadStringLength(BinaryReader reader)
        {
            uint length = 0;
            for (int i = 0; i < 5; i++)
            {
                byte value = reader.ReadByte();
                if (i == 4 && (value & 0xf0) != 0) throw new InvalidDataException("メタデータの長さが不正です。");
                length |= (uint)(value & 0x7f) << (7 * i);
                if ((value & 0x80) == 0)
                {
                    if (length == 0 || length > MaxHeaderBytes) throw new InvalidDataException("メタデータのサイズが上限を超えています。");
                    return (int)length;
                }
            }
            throw new InvalidDataException("メタデータの長さが不正です。");
        }
    }
}
