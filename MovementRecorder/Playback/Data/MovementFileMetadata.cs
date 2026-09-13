using System;
using System.Globalization;
using System.IO;

namespace MovementRecorder.Playback.Data
{
    internal sealed class MovementFileMetadata
    {
        public string Path { get; set; }
        public string Folder { get; set; }
        public long Length { get; set; }
        public long LastWriteUtcTicks { get; set; }
        public string HeaderHash { get; set; }
        public string LevelId { get; set; }
        public string Characteristic { get; set; }
        public int? Difficulty { get; set; }
        public string SongName { get; set; }
        public DateTime? RecordedAtLocal { get; set; }
        public int ObjectCount { get; set; }
        public int FrameCount { get; set; }
        public int RecordFrameRate { get; set; }
        public float StartTime { get; set; }
        public float EndTime { get; set; }
        public string[] Groups { get; set; }
        public string Error { get; set; }
        public bool TransientError { get; set; }
        public bool Confirmed { get; set; }

        public bool SameSource(MovementFileMetadata other) => other != null &&
            StringComparer.OrdinalIgnoreCase.Equals(Path, other.Path) && Length == other.Length &&
            LastWriteUtcTicks == other.LastWriteUtcTicks && HeaderHash == other.HeaderHash;

        public bool MatchesAttributes(FileInfo file) => file.Exists && Length == file.Length && LastWriteUtcTicks == file.LastWriteTimeUtc.Ticks;
        public bool MatchesChart(string level, string characteristic, int difficulty) =>
            ChartIdentity.Level(LevelId) == ChartIdentity.Level(level) && Characteristic == characteristic && Difficulty == difficulty;
        public string ChartKey => ChartIdentity.Key(LevelId, Characteristic, Difficulty);
        public MovementFileMetadata Copy() => (MovementFileMetadata)MemberwiseClone();
    }

    internal static class ChartIdentity
    {
        public static string Level(string id)
        {
            const string prefix = "custom_level_";
            if (id != null && id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && id.Length == prefix.Length + 40)
            {
                string hash = id.Substring(prefix.Length);
                foreach (char c in hash) if (!Uri.IsHexDigit(c)) return id;
                return prefix + hash.ToUpperInvariant();
            }
            return id;
        }

        public static string Key(string level, string characteristic, int? difficulty) =>
            string.IsNullOrEmpty(level) || string.IsNullOrEmpty(characteristic) || !difficulty.HasValue ? null :
            Level(level) + "\u001f" + characteristic + "\u001f" + difficulty.Value.ToString(CultureInfo.InvariantCulture);

        public static int? Difficulty(string name)
        {
            switch (name)
            {
                case "Easy": return 0;
                case "Normal": return 1;
                case "Hard": return 2;
                case "Expert": return 3;
                case "Expert+": case "ExpertPlus": return 4;
                default: return null;
            }
        }

        public static DateTime? FilenameTime(string path)
        {
            string name = System.IO.Path.GetFileName(path);
            if (name.Length < 16 || name[14] != '-') return null;
            return DateTime.TryParseExact(name.Substring(0, 14), "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time) ? DateTime.SpecifyKind(time, DateTimeKind.Unspecified) : (DateTime?)null;
        }
    }
}
