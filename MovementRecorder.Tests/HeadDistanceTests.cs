using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using LiteDB;
using MovementRecorder.Playback.Data;
using Xunit;

namespace MovementRecorder.Tests
{
    public sealed class HeadDistanceTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "MovementRecorder-HDT-tests-" + Guid.NewGuid().ToString("N"));
        private static readonly DateTime Recorded = new DateTime(2026, 9, 1, 23, 3, 29, DateTimeKind.Unspecified);
        private static readonly string Key = ChartIdentity.Key("level", "Standard", 3);
        public HeadDistanceTests() { Directory.CreateDirectory(_directory); }
        public void Dispose() { Directory.Delete(_directory, true); }
        private static MovementFileMetadata FileAt(string path, DateTime? time = null) => new MovementFileMetadata {
            Path = path, LevelId = "level", Characteristic = "Standard", Difficulty = 3, RecordedAtLocal = time ?? Recorded };
        private static HeadDistanceRow Row(int id, double seconds, double distance = 57.432) => new HeadDistanceRow {
            Id = id, ChartKey = Key, CreatedAtUtc = DateTime.SpecifyKind(Recorded.AddSeconds(seconds), DateTimeKind.Utc), Distance = distance };

        [Theory] [InlineData(-3, true)] [InlineData(3, true)] [InlineData(3.001, false)] [InlineData(-3.001, false)]
        public void MatchUsesInclusiveThreeSecondWindow(double offset, bool expected)
        { Assert.Equal(expected, HeadDistanceMatcher.Match(new[] { FileAt("one") }, new[] { Row(1, offset) }, TimeZoneInfo.Utc).Count == 1); }
        [Fact] public void MultipleCandidatesAndRowReuseRemainBlank()
        {
            Assert.Empty(HeadDistanceMatcher.Match(new[] { FileAt("one") }, new[] { Row(1, 0), Row(2, 1) }, TimeZoneInfo.Utc));
            Assert.Empty(HeadDistanceMatcher.Match(new[] { FileAt("one"), FileAt("two") }, new[] { Row(1, 0) }, TimeZoneInfo.Utc));
        }
        [Fact] public void AmbiguousFileAlsoReservesItsCandidates()
        {
            var files = new[] { FileAt("one"), FileAt("two", Recorded.AddSeconds(4)) };
            Assert.Empty(HeadDistanceMatcher.Match(files, new[] { Row(1, 1), Row(2, -1) }, TimeZoneInfo.Utc));
        }
        [Theory] [InlineData(0, true)] [InlineData(-1, false)] [InlineData(double.NaN, false)] [InlineData(double.PositiveInfinity, false)]
        public void ZeroDistanceIsRealButInvalidNumbersAreNot(double distance, bool expected)
        { Assert.Equal(expected, HeadDistanceMatcher.Match(new[] { FileAt("one") }, new[] { Row(1, 0, distance) }, TimeZoneInfo.Utc).Any()); }
        [Fact] public void MissingAndPartialPageDatabasesAreNeverCreatedOrTruncated()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb");
            Assert.Null(HeadDistanceSnapshot.Capture(path, CancellationToken.None)); Assert.False(File.Exists(path));
            File.WriteAllBytes(path, new byte[8193]); byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(() => HeadDistanceSnapshot.Capture(path, CancellationToken.None));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        private static void Populate(LiteDatabase database)
        {
            database.GetCollection("BeatmapCharacteristicText").Insert(new BsonDocument { ["_id"] = 91,
                ["Key"] = "LEVEL_STANDARD", ["DisplayName"] = "Standard" });
            database.GetCollection("DistanceInformation").Insert(new BsonDocument { ["_id"] = 12,
                ["CreatedAt"] = Recorded.ToUniversalTime(), ["LevelID"] = "level", ["Difficurity"] = "Expert",
                ["BeatmapCharacteristicTextId"] = 91, ["Distance"] = 57.432 });
        }
        [Fact] public void IndependentReaderJoinsDynamicIdsAndNeverModifiesSource()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb");
            using (var database = new LiteDatabase(path)) Populate(database);
            byte[] before = File.ReadAllBytes(path); long modified = new FileInfo(path).LastWriteTimeUtc.Ticks;
            var result = new HeadDistanceReader(path, Path.Combine(_directory, "Cache"), null).Read(new[] { FileAt("one") }, CancellationToken.None);
            Assert.True(result.Available, result.Error); Assert.Single(result.Matches); Assert.Equal(57.432, result.Matches[0].Distance, 3);
            Assert.Equal(before, File.ReadAllBytes(path)); Assert.Equal(modified, new FileInfo(path).LastWriteTimeUtc.Ticks);
            File.Delete(path);
            var missing = new HeadDistanceReader(path, Path.Combine(_directory, "Cache"), null).Read(new[] { FileAt("one") }, CancellationToken.None);
            Assert.False(missing.Available); Assert.Empty(missing.Matches);
        }
        [Fact] public void CommittedWalIsIncludedAndSourceWalStaysUnchanged()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb");
            // Shared mode closes its file handles between transactions and leaves WAL before checkpoint.
            using (var database = new LiteDatabase(new ConnectionString { Filename = path, Connection = ConnectionType.Shared }))
            {
                database.CheckpointSize = 0; Populate(database);
                string log = HeadDistanceSnapshot.LogPath(path);
                Assert.True(File.Exists(log)); byte[] data = File.ReadAllBytes(path), wal = File.ReadAllBytes(log);
                using (var snapshot = HeadDistanceSnapshot.Capture(path, CancellationToken.None))
                    Assert.Single(HeadDistanceReader.ReadRows(snapshot, CancellationToken.None));
                Assert.Equal(data, File.ReadAllBytes(path)); Assert.Equal(wal, File.ReadAllBytes(log));
            }
        }
        [Fact] public void WriterContentionOmitsDistanceWithoutUsingOldValues()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb");
            using (var database = new LiteDatabase(path)) Populate(database);
            using (var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var result = new HeadDistanceReader(path, _directory, null).Read(new[] { FileAt("one") }, CancellationToken.None);
                Assert.False(result.Available); Assert.Empty(result.Matches);
            }
        }
        [Fact] public void CacheIsInvalidatedByNewAmbiguousFileAndChangedDatabase()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb");
            using (var database = new LiteDatabase(path)) Populate(database);
            var reader = new HeadDistanceReader(path, Path.Combine(_directory, "Cache"), null);
            Assert.Single(reader.Read(new[] { FileAt("one") }, CancellationToken.None).Matches);
            Assert.Empty(reader.Read(new[] { FileAt("one"), FileAt("two") }, CancellationToken.None).Matches);
            using (var database = new LiteDatabase(path))
            {
                var row = database.GetCollection("DistanceInformation").FindById(12); row["Distance"] = 99.75;
                database.GetCollection("DistanceInformation").Update(row);
            }
            Assert.Equal(99.75, Assert.Single(reader.Read(new[] { FileAt("one") }, CancellationToken.None).Matches).Distance);
        }
        [Fact] public void WarmCacheDoesNotBypassSourceLock()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb");
            using (var database = new LiteDatabase(path)) Populate(database);
            var reader = new HeadDistanceReader(path, Path.Combine(_directory, "Cache"), null);
            Assert.Single(reader.Read(new[] { FileAt("one") }, CancellationToken.None).Matches);
            using (var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.False(reader.Read(new[] { FileAt("one") }, CancellationToken.None).Available);
            Assert.Single(reader.Read(new[] { FileAt("one") }, CancellationToken.None).Matches);
        }
        [Fact] public void CorruptMatchCacheIsRebuiltFromVerifiedSnapshot()
        {
            string path = Path.Combine(_directory, "HMDDistance.litedb"), cache = Path.Combine(_directory, "Cache");
            using (var database = new LiteDatabase(path)) Populate(database);
            Assert.Single(new HeadDistanceReader(path, cache, null).Read(new[] { FileAt("one") }, CancellationToken.None).Matches);
            string cachePath = Path.Combine(cache, "head-distance-v1.json");
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cachePath));
            json["Matches"][0]["Path"] = "wrong-file"; File.WriteAllText(cachePath, json.ToString());
            Assert.Equal("one", Assert.Single(new HeadDistanceReader(path, cache, null).Read(new[] { FileAt("one") }, CancellationToken.None).Matches).Path);
        }
        [Fact] public void AmbiguousAndInvalidDaylightSavingTimesAreNotGuessed()
        {
            var zone = TimeZoneInfo.CreateCustomTimeZone("Test DST", TimeSpan.FromHours(-5), "Test DST", "Test", "Test Summer",
                new[] { TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)) });
            foreach (var local in new[] { new DateTime(2026, 3, 8, 2, 30, 0), new DateTime(2026, 11, 1, 1, 30, 0) })
            {
                Assert.True(zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local));
                var row = new HeadDistanceRow { Id = 1, ChartKey = Key, CreatedAtUtc = DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc), Distance = 1 };
                Assert.Empty(HeadDistanceMatcher.Match(new[] { FileAt("one", local) }, new[] { row }, zone));
            }
        }
    }
}
