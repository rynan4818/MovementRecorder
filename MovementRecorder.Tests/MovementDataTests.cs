using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MovementRecorder.Models;
using MovementRecorder.Playback.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MovementRecorder.Tests
{
    public sealed class MovementDataTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "MovementRecorder-tests-" + Guid.NewGuid().ToString("N"));
        private readonly MovementFileReader _reader = new MovementFileReader();
        public MovementDataTests() { Directory.CreateDirectory(_directory); }
        public void Dispose() { Directory.Delete(_directory, true); }

        private string Fixture(float[] times = null, Action<JObject> editHeader = null, Action<BinaryWriter, int> writePose = null, string name = "20260901230329-song-Expert-Standard-10s.mvrec")
        {
            times = times ?? new[] { 0f, 1f, 2f };
            var header = JObject.FromObject(new MovementJson { objectCount = 1, recordCount = times.Length,
                levelID = "custom_level_" + new string('a', 40), serializedName = "Standard", difficulty = "Expert", difficultyNum = 3,
                objectNames = new List<string> { "Avatar/Root" }, objectScales = new List<Scale> { new Scale { x = 1, y = 1, z = 1 } },
                Settings = new List<Setting> { new Setting { name = "Test", type = "Avatar" } } });
            editHeader?.Invoke(header);
            string path = Path.Combine(_directory, name);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(header.ToString(Formatting.None));
                for (int i = 0; i < times.Length; i++)
                {
                    writer.Write(times[i]);
                    if (writePose != null) writePose(writer, i);
                    else { writer.Write((float)i); writer.Write(2f); writer.Write(3f); writer.Write(0f); writer.Write(0f); writer.Write(0f); writer.Write(i == 1 ? -1f : 1f); }
                }
            }
            return path;
        }
        private MovementClip Load(string path) => _reader.ReadClip(path, null, MovementFileReader.DefaultMemoryBudget, CancellationToken.None);

        [Fact] public void LegacyNullEventsAndMissingDifficultyNumberAreAcceptedWithoutCoordinateOffsets()
        {
            string path = Fixture(editHeader: json => json.Remove("difficultyNum"));
            var clip = Load(path);
            Assert.Equal(3, clip.Metadata.Difficulty);
            Assert.True(clip.TryEvaluate(0, .5f, out var pose, out bool active));
            Assert.True(active); Assert.Equal(.5f, pose.X, 5); Assert.Equal(2f, pose.Y); Assert.Equal(3f, pose.Z);
            Assert.Equal(1f, Math.Abs(pose.Qw), 5);
        }
        [Theory] [InlineData(-3f, 0f)] [InlineData(0f, 0f)] [InlineData(2f, 2f)] [InlineData(8f, 2f)]
        public void EvaluationClampsAtBothEnds(float time, float expected)
        { var clip = Load(Fixture()); Assert.True(clip.TryEvaluate(0, time, out var pose, out _)); Assert.Equal(expected, pose.X); }

        [Fact] public void DuplicateTimestampsChooseTheLastSampleAndBackwardSeekHasNoStaleCursor()
        {
            var clip = Load(Fixture(new[] { 0f, 0f, 0f, 1f, 2f }));
            clip.TryEvaluate(0, 2, out _, out _);
            clip.TryEvaluate(0, -.5f, out var before, out _); Assert.Equal(2, before.X);
            clip.TryEvaluate(0, 0, out var at, out _); Assert.Equal(2, at.X);
        }
        [Fact] public void MissingTrackNeverInterpolatesTowardInvalidZeroPoseAndReappearsWhenRewound()
        {
            var clip = Load(Fixture(editHeader: j => j["recordNullObjects"] = JArray.FromObject(new[] { new NUllObject { objIndex = 0, songTime = 1 } }),
                writePose: (w, i) => { for (int n = 0; n < 7; n++) w.Write(i == 0 && (n == 0 || n == 6) ? 1f : 0f); }));
            clip.TryEvaluate(0, .9f, out var pose, out bool active); Assert.True(active); Assert.Equal(1, pose.X);
            clip.TryEvaluate(0, 1f, out pose, out active); Assert.False(active); Assert.Equal(1, pose.X);
            clip.TryEvaluate(0, 0f, out _, out active); Assert.True(active);
        }
        [Theory] [InlineData("objectCount")] [InlineData("recordCount")] [InlineData("difficultyNum")]
        public void ContradictoryHeadersAreRejected(string field)
        { string path = Fixture(editHeader: j => j[field] = 7); Assert.Throws<InvalidDataException>(() => Load(path)); }

        [Fact] public void TruncationAndTrailingDataAreRejected()
        {
            string path = Fixture(); using (var stream = File.OpenWrite(path)) stream.SetLength(stream.Length - 1);
            Assert.Throws<InvalidDataException>(() => Load(path));
            path = Fixture(); using (var stream = new FileStream(path, FileMode.Append)) stream.WriteByte(42);
            Assert.Throws<InvalidDataException>(() => Load(path));
        }
        [Fact] public void HugeAndInvalidStringPrefixesDoNotAllocateTheirClaimedLength()
        {
            string path = Path.Combine(_directory, "bad.mvrec");
            File.WriteAllBytes(path, new byte[] { 255, 255, 255, 255, 127 });
            Assert.Throws<InvalidDataException>(() => Load(path));
        }
        [Theory] [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(-1f)]
        public void InvalidOrDecreasingMiddleTimestampsAreRejected(float time)
        { Assert.Throws<InvalidDataException>(() => Load(Fixture(new[] { 0f, time, 2f }))); }

        [Fact] public void InvalidQuaternionAndMemoryBudgetAreRejected()
        {
            string path = Fixture(writePose: (w, i) => { for (int n = 0; n < 7; n++) w.Write(0f); });
            Assert.Throws<InvalidDataException>(() => Load(path));
            path = Fixture(); Assert.Throws<InvalidDataException>(() => _reader.ReadClip(path, null, 1, CancellationToken.None));
        }
        [Fact] public void ChangedSelectionAndCancellationAreRejectedBeforePlayback()
        {
            string path = Fixture(); var metadata = _reader.ReadMetadata(path);
            Fixture(editHeader: j => j["songName"] = "changed");
            Assert.Throws<IOException>(() => _reader.ReadClip(path, metadata, MovementFileReader.DefaultMemoryBudget, CancellationToken.None));
            Assert.Throws<OperationCanceledException>(() => _reader.ReadClip(path, null, MovementFileReader.DefaultMemoryBudget, new CancellationToken(true)));
        }
        [Fact] public void LargeFinitePositionsInterpolateWithoutOverflow()
        {
            var a = new RecordedPose { X = -float.MaxValue, Qw = 1 }; var b = new RecordedPose { X = float.MaxValue, Qw = 1 };
            Assert.Equal(0, RecordedPose.Interpolate(a, b, .5f).X);
        }
        [Fact] public async Task CatalogDetectsAddUpdateDeleteAndRecoversCorruptCache()
        {
            string cache = Path.Combine(_directory, "Cache"); Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "file-metadata-v1.json"), "{broken");
            var catalog = new MovementFileCatalog(cache, null); string path = Fixture();
            var first = await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None);
            Assert.Single(first.Files); string hash = first.Files[0].HeaderHash;
            Fixture(editHeader: j => j["songName"] = "replaced");
            var changed = await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None);
            Assert.NotEqual(hash, changed.Files[0].HeaderHash);
            File.Delete(path);
            Assert.Empty((await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None)).Files);
        }
        [Fact] public void ChartAndFilenameRulesDoNotGuessUnknownInformation()
        {
            Assert.Equal("custom_level_" + new string('A', 40), ChartIdentity.Level("CUSTOM_LEVEL_" + new string('a', 40)));
            Assert.Equal(4, ChartIdentity.Difficulty("Expert+")); Assert.Null(ChartIdentity.Difficulty("Expert++"));
            Assert.Null(ChartIdentity.FilenameTime("song.mvrec"));
            Assert.Null(ChartIdentity.FilenameTime("20260230000000-song.mvrec"));
        }
        [Fact] public async Task ChartListRetainsEveryRecordingAndSeparatesDifficultiesAcrossCacheReload()
        {
            string expert = Fixture();
            var expertPlus = new List<string>();
            for (int i = 1; i <= 7; i++)
                expertPlus.Add(Fixture(name: $"202609012300{i:00}-song-Expert+-Standard-10s.mvrec",
                    editHeader: j => { j["difficulty"] = "Expert+"; j["difficultyNum"] = 4; }));
            for (int i = 1; i <= 2; i++)
                Fixture(name: $"202609012301{i:00}-another-song-Expert-Standard-10s.mvrec",
                    editHeader: j => j["levelID"] = "custom_level_" + new string('b', 40));
            string cache = Path.Combine(_directory, "Cache");
            for (int pass = 0; pass < 2; pass++)
            {
                var snapshot = await new MovementFileCatalog(cache, null).ScanAsync(new[] { _directory }, false, CancellationToken.None);
                Assert.Equal(10, snapshot.Files.Length);
                Assert.Equal(pass == 0 ? 10 : 0, snapshot.HeadersRead);
                Assert.Equal(expert, Assert.Single(snapshot.FilesForChart(new[] { _directory }, ChartIdentity.Key("custom_level_" + new string('a', 40), "Standard", 3))).Path);
                Assert.Equal(expertPlus.AsEnumerable().Reverse(), snapshot.FilesForChart(new[] { _directory }, ChartIdentity.Key("custom_level_" + new string('a', 40), "Standard", 4)).Select(f => f.Path));
                Assert.Equal(2, snapshot.FilesForChart(new[] { _directory }, ChartIdentity.Key("custom_level_" + new string('b', 40), "Standard", 3)).Length);
                Assert.Empty(snapshot.FilesForChart(new[] { _directory }, ChartIdentity.Key("custom_level_" + new string('a', 40), "Standard", 2)));
            }
        }
        [Fact] public async Task ForcedRebuildAndLaunchValidationDetectSameLengthSameTimestampReplacement()
        {
            string path = Fixture(editHeader: j => j["songName"] = "aaa");
            var catalog = new MovementFileCatalog(Path.Combine(_directory, "Cache"), null);
            var first = await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None);
            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            Fixture(editHeader: j => j["songName"] = "bbb"); File.SetLastWriteTimeUtc(path, new DateTime(ticks, DateTimeKind.Utc));
            var cached = await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None);
            Assert.Equal("aaa", Assert.Single(cached.Files).SongName);
            Assert.Throws<IOException>(() => _reader.ReadClip(path, first.Files[0], MovementFileReader.DefaultMemoryBudget, CancellationToken.None));
            var rebuilt = await catalog.ScanAsync(new[] { _directory }, true, CancellationToken.None);
            Assert.Equal("bbb", Assert.Single(rebuilt.Files).SongName);
            Assert.Equal("aaa", Assert.Single(first.Files).SongName);
        }
        [Fact] public async Task DeletedFolderAndCanceledScanDoNotPoisonLaterRefresh()
        {
            Fixture(); var catalog = new MovementFileCatalog(Path.Combine(_directory, "Cache"), null);
            Assert.Single((await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None)).Files);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.ScanAsync(new[] { _directory }, false, new CancellationToken(true)));
            Assert.Single((await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None)).Files);
            Directory.Delete(_directory, true);
            Assert.Empty((await catalog.ScanAsync(new[] { _directory }, false, CancellationToken.None)).Files);
        }
    }
}
