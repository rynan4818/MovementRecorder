using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using MovementRecorder.Playback.Data;
using Newtonsoft.Json;

// Optional read-only integration audit. All caches/report files go to the explicit output folder.
if (args.Length != 3) throw new ArgumentException("Usage: <recordings folder> <HMDDistance.litedb> <output folder>");
string records = Path.GetFullPath(args[0]), database = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
if (output.Equals(records, StringComparison.OrdinalIgnoreCase) || output.StartsWith(records + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
    output.Equals(Path.GetDirectoryName(database), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Output must be separate from source data.");
object Fingerprint(string path) => !File.Exists(path) ? null : new { Length = new FileInfo(path).Length,
    Modified = File.GetLastWriteTimeUtc(path), Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
string wal = HeadDistanceSnapshot.LogPath(database);
string before = JsonConvert.SerializeObject(new[] { Fingerprint(database), Fingerprint(wal) });
Directory.CreateDirectory(output);
var catalog = new MovementFileCatalog(output, Console.Error.WriteLine);
var timer = Stopwatch.StartNew();
var scan = await catalog.ScanAsync(new[] { records }, true, CancellationToken.None);
double coldMilliseconds = timer.Elapsed.TotalMilliseconds; timer.Restart();
var warm = await catalog.ScanAsync(new[] { records }, false, CancellationToken.None);
double warmMilliseconds = timer.Elapsed.TotalMilliseconds;
var distanceReader = new HeadDistanceReader(database, output, Console.Error.WriteLine);
var distances = distanceReader.Read(scan.Files, CancellationToken.None, true);
var cachedDistances = distanceReader.Read(scan.Files, CancellationToken.None);
var selected = scan.Files.FirstOrDefault(f => f.Error == null && f.FrameCount > 0 && f.Length < 128 * 1024 * 1024);
timer.Restart();
MovementClip clip = selected == null ? null : new MovementFileReader().ReadClip(selected.Path, selected, MovementFileReader.DefaultMemoryBudget, CancellationToken.None);
double clipMilliseconds = timer.Elapsed.TotalMilliseconds;
int evaluated = 0;
if (clip != null)
    foreach (float time in new[] { clip.StartTime, (clip.StartTime + clip.EndTime) / 2, clip.EndTime })
        for (int track = 0; track < clip.TrackCount; track++)
            if (clip.TryEvaluate(track, time, out var pose, out bool active) && pose.IsFinite) evaluated++;
object addition = null;
if (selected != null)
{
    string extraFolder = Path.Combine(output, "temporary-extra-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(extraFolder);
    string extra = Path.Combine(extraFolder, Path.GetFileName(selected.Path));
    try
    {
        File.Copy(selected.Path, extra); timer.Restart();
        var added = await catalog.ScanAsync(new[] { records, extraFolder }, false, CancellationToken.None);
        addition = new { Milliseconds = timer.Elapsed.TotalMilliseconds, added.HeadersRead, added.CacheHits, Files = added.Files.Length };
    }
    finally { File.Delete(extra); Directory.Delete(extraFolder); }
    await catalog.ScanAsync(new[] { records, extraFolder }, false, CancellationToken.None);
}
string after = JsonConvert.SerializeObject(new[] { Fingerprint(database), Fingerprint(wal) });
if (before != after) throw new InvalidOperationException("Source database/WAL changed during the audit.");
var report = new { TimeUtc = DateTime.UtcNow, Count = scan.Files.Length, HeaderErrors = scan.Files.Where(f => f.Error != null).Select(f => new { f.Path, f.Error }),
    scan.Warnings, DistanceAvailable = distances.Available, DistanceError = distances.Error, Matches = distances.Matches.Count,
    Sample = selected?.Path, Tracks = clip?.TrackCount, Frames = clip?.FrameCount, EvaluatedPoses = evaluated,
    SampleDistance = distances.Matches.FirstOrDefault(m => m.Path == selected?.Path)?.Distance, SourceUnchanged = before == after,
    MetadataCold = new { Milliseconds = coldMilliseconds, scan.HeadersRead, scan.CacheHits },
    MetadataWarm = new { Milliseconds = warmMilliseconds, warm.HeadersRead, warm.CacheHits }, MetadataAddOne = addition,
    FullClipMilliseconds = clipMilliseconds, ClipPoseBytes = clip == null ? 0 : (long)clip.FrameCount * (4 + clip.TrackCount * 28),
    DistanceCold = new { distances.CacheHit, distances.SnapshotBytes, distances.SnapshotMilliseconds, distances.LockMilliseconds, distances.ParseMilliseconds, distances.MatchMilliseconds },
    DistanceWarm = new { cachedDistances.CacheHit, cachedDistances.SnapshotBytes, cachedDistances.SnapshotMilliseconds, cachedDistances.LockMilliseconds, cachedDistances.ParseMilliseconds, cachedDistances.MatchMilliseconds } };
string json = JsonConvert.SerializeObject(report, Formatting.Indented);
File.WriteAllText(Path.Combine(output, "audit.json"), json); Console.WriteLine(json);
return scan.Warnings.Length == 0 && scan.Files.All(f => f.Error == null) && clip != null ? 0 : 1;
