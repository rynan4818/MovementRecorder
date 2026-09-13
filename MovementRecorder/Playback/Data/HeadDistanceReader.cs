using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LiteDB;
using LiteDB.Engine;

namespace MovementRecorder.Playback.Data
{
    internal sealed class HeadDistanceResult
    {
        public bool Available { get; set; }
        public string Error { get; set; }
        public List<HeadDistanceMatch> Matches { get; set; } = new List<HeadDistanceMatch>();
        public bool CacheHit { get; set; }
        public long SnapshotBytes { get; set; }
        public double SnapshotMilliseconds { get; set; }
        public double LockMilliseconds { get; set; }
        public double ParseMilliseconds { get; set; }
        public double MatchMilliseconds { get; set; }
    }

    internal sealed class HeadDistanceReader
    {
        private sealed class CacheFile
        {
            public int Version { get; set; } = 1;
            public string ReaderVersion { get; set; } = "LiteDB-5.0.21/match-1";
            public string TimeZone { get; set; }
            public string SourceAttributes { get; set; }
            public string SnapshotHash { get; set; }
            public string FilesHash { get; set; }
            public List<HeadDistanceMatch> Matches { get; set; }
        }
        private readonly string _path, _cachePath;
        private readonly Action<string> _log;
        private CacheFile _cache;
        private readonly SemaphoreSlim _worker = new SemaphoreSlim(1, 1);
        public HeadDistanceReader(string databasePath, string cacheDirectory, Action<string> log)
        { _path = databasePath; _cachePath = Path.Combine(cacheDirectory, "head-distance-v1.json"); _log = log; }

        public HeadDistanceResult Read(IEnumerable<MovementFileMetadata> files, CancellationToken token, bool rebuild = false)
        {
            try
            {
                _worker.Wait(token);
                try { return ReadSnapshot(files.ToArray(), token, rebuild); }
                finally { _worker.Release(); }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log?.Invoke("Head distance display omitted: " + ex.Message);
                return new HeadDistanceResult { Error = ex.Message };
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private HeadDistanceResult ReadSnapshot(MovementFileMetadata[] files, CancellationToken token, bool rebuild)
        {
            var timer = Stopwatch.StartNew();
            using (var snapshot = HeadDistanceSnapshot.Capture(_path, token))
            {
                if (snapshot == null) return new HeadDistanceResult();
                var result = new HeadDistanceResult { Available = true, SnapshotBytes = snapshot.Data.Length + snapshot.Log.Length,
                    SnapshotMilliseconds = timer.Elapsed.TotalMilliseconds, LockMilliseconds = snapshot.LockMilliseconds };
                var zone = TimeZoneInfo.Local;
                string zoneKey = zone.ToSerializedString();
                string filesHash;
                using (var sha = SHA256.Create())
                    filesHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                        .Select(f => f.Path + "|" + f.HeaderHash + "|" + f.ChartKey + "|" + f.RecordedAtLocal?.Ticks + "|" + f.Error))))).Replace("-", "");
                if (_cache == null) _cache = JsonCache.Read<CacheFile>(_cachePath, _log);
                if (!rebuild && _cache?.Version == 1 && _cache.ReaderVersion == "LiteDB-5.0.21/match-1" && _cache.TimeZone == zoneKey &&
                    _cache.FilesHash == filesHash && _cache.SourceAttributes == snapshot.Attributes && _cache.SnapshotHash == snapshot.Fingerprint &&
                    ValidMatches(_cache.Matches, files, zone))
                { result.Matches = _cache.Matches; result.CacheHit = true; return result; }
                timer.Restart();
                var rows = ReadRows(snapshot, token);
                result.ParseMilliseconds = timer.Elapsed.TotalMilliseconds; timer.Restart();
                var matches = HeadDistanceMatcher.Match(files, rows, zone);
                result.MatchMilliseconds = timer.Elapsed.TotalMilliseconds;
                token.ThrowIfCancellationRequested();
                // Revalidate the external database on every menu refresh. Persist only matched historical rows;
                // an old cache is never evidence that an absent, locked or replaced DB is available now.
                _cache = new CacheFile { TimeZone = zoneKey, SourceAttributes = snapshot.Attributes, FilesHash = filesHash,
                    SnapshotHash = snapshot.Fingerprint, Matches = matches };
                JsonCache.Write(_cachePath, _cache, _log);
                result.Matches = matches; return result;
            }
        }

        private static bool ValidMatches(List<HeadDistanceMatch> matches, MovementFileMetadata[] files, TimeZoneInfo zone)
        {
            if (matches == null) return false;
            var byPath = files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var ids = new HashSet<int>();
            foreach (var match in matches)
            {
                if (match == null || match.Path == null || !Number.IsFinite(match.Distance) || match.Distance < 0 ||
                    !Number.IsFinite(match.DifferenceSeconds) || !paths.Add(match.Path) || !ids.Add(match.RowId) ||
                    !byPath.TryGetValue(match.Path, out var file) || file.Error != null || match.HeaderHash != file.HeaderHash ||
                    match.ChartKey != file.ChartKey || !file.RecordedAtLocal.HasValue || match.RecordedAtLocal != file.RecordedAtLocal.Value) return false;
                var local = DateTime.SpecifyKind(match.RecordedAtLocal, DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return false;
                double difference = (match.CreatedAtUtc - TimeZoneInfo.ConvertTimeToUtc(local, zone)).TotalSeconds;
                if (Math.Abs(difference) > 3 || Math.Abs(difference - match.DifferenceSeconds) > .001) return false;
            }
            return true;
        }

        internal static List<HeadDistanceRow> ReadRows(HeadDistanceSnapshot snapshot, CancellationToken token)
        {
            if (typeof(LiteDatabase).Assembly.GetName().Version != new Version(5, 0, 21, 0))
                throw new InvalidDataException("距離DBの読込ライブラリの版が未対応です。");
            using (var temporary = new MemoryStream())
            using (var engine = new LiteEngine(new EngineSettings { DataStream = snapshot.Data, LogStream = snapshot.Log,
                TempStream = temporary, ReadOnly = true, Upgrade = false, AutoRebuild = false }))
            using (var db = new LiteDatabase(engine, new BsonMapper(), false))
            {
                var collections = new HashSet<string>(db.GetCollectionNames(), StringComparer.Ordinal);
                if (!collections.Contains("DistanceInformation") || !collections.Contains("BeatmapCharacteristicText"))
                    throw new InvalidDataException("距離DBのコレクション形式が未対応です。");
                var characteristics = new Dictionary<int, string>();
                foreach (var row in db.GetCollection("BeatmapCharacteristicText").FindAll())
                {
                    if (!row["_id"].IsInt32 || !row["Key"].IsString) continue;
                    string value = Characteristic(row["Key"].AsString);
                    if (value != null && row["DisplayName"].IsString && row["DisplayName"].AsString == value)
                        characteristics[row["_id"].AsInt32] = value;
                }
                var result = new List<HeadDistanceRow>(); int scanned = 0;
                foreach (var row in db.GetCollection("DistanceInformation").FindAll())
                {
                    token.ThrowIfCancellationRequested();
                    if (++scanned > 1000000) throw new InvalidDataException("距離DBの件数が上限を超えています。");
                    if (!row["_id"].IsInt32 || !row["CreatedAt"].IsDateTime || !row["LevelID"].IsString ||
                        !row["Difficurity"].IsString || !row["BeatmapCharacteristicTextId"].IsInt32 || !row["Distance"].IsNumber) continue;
                    if (!characteristics.TryGetValue(row["BeatmapCharacteristicTextId"].AsInt32, out string characteristic)) continue;
                    string key = ChartIdentity.Key(row["LevelID"].AsString, characteristic, ChartIdentity.Difficulty(row["Difficurity"].AsString));
                    double distance = row["Distance"].AsDouble;
                    if (key == null || !Number.IsFinite(distance) || distance < 0) continue;
                    result.Add(new HeadDistanceRow { Id = row["_id"].AsInt32, ChartKey = key,
                        CreatedAtUtc = row["CreatedAt"].AsDateTime.ToUniversalTime(), Distance = distance });
                }
                return result;
            }
        }

        private static string Characteristic(string key)
        {
            switch (key)
            {
                case "LEVEL_STANDARD": return "Standard";
                case "LEVEL_ONE_SABER": return "OneSaber";
                case "LEVEL_360DEGREE": return "360Degree";
                case "LEVEL_90DEGREE": return "90Degree";
                case "Lawless": return "Lawless";
                case "Lightshow": return "Lightshow";
                default: return null;
            }
        }
    }
}
