using System;
using System.Collections.Generic;
using System.Linq;

namespace MovementRecorder.Playback.Data
{
    internal sealed class HeadDistanceRow
    {
        public int Id { get; set; }
        public string ChartKey { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public double Distance { get; set; }
    }

    internal sealed class HeadDistanceMatch
    {
        public string Path { get; set; }
        public string HeaderHash { get; set; }
        public string ChartKey { get; set; }
        public int RowId { get; set; }
        public DateTime RecordedAtLocal { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public double DifferenceSeconds { get; set; }
        public double Distance { get; set; }
    }

    internal static class HeadDistanceMatcher
    {
        public static List<HeadDistanceMatch> Match(IEnumerable<MovementFileMetadata> files, IEnumerable<HeadDistanceRow> rows, TimeZoneInfo zone)
        {
            var byChart = rows.Where(r => r.ChartKey != null && Number.IsFinite(r.Distance) && r.Distance >= 0)
                .ToLookup(r => r.ChartKey);
            var candidates = new List<HeadDistanceMatch>();
            // Ambiguous files reserve all candidate rows too: another file must not steal a possible match.
            var claims = new Dictionary<int, HashSet<string>>();
            foreach (var file in files.Where(f => f.Error == null && f.ChartKey != null && f.RecordedAtLocal.HasValue)
                .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            {
                DateTime local = DateTime.SpecifyKind(file.RecordedAtLocal.Value, DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) continue;
                DateTime utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
                var matches = byChart[file.ChartKey].Where(r => Math.Abs((r.CreatedAtUtc - utc).TotalSeconds) <= 3).ToArray();
                foreach (var row in matches)
                {
                    if (!claims.TryGetValue(row.Id, out var owners)) claims[row.Id] = owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    owners.Add(file.Path);
                }
                if (matches.Length != 1) continue;
                var match = matches[0];
                candidates.Add(new HeadDistanceMatch { Path = file.Path, HeaderHash = file.HeaderHash, ChartKey = file.ChartKey,
                    RowId = match.Id, RecordedAtLocal = local, CreatedAtUtc = match.CreatedAtUtc,
                    DifferenceSeconds = (match.CreatedAtUtc - utc).TotalSeconds, Distance = match.Distance });
            }
            return candidates.Where(m => claims[m.RowId].Count == 1).ToList();
        }
    }
}
