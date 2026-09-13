using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MovementRecorder.Playback.Data
{
    internal sealed class CatalogSnapshot
    {
        public MovementFileMetadata[] Files { get; set; }
        public string[] Warnings { get; set; }
        public int HeadersRead { get; set; }
        public int CacheHits { get; set; }
        public MovementFileMetadata[] FilesForChart(IEnumerable<string> folders, string chartKey) =>
            Files.Where(f => folders.Contains(f.Folder, StringComparer.OrdinalIgnoreCase) && (f.ChartKey == chartKey || f.Error != null)).ToArray();
    }

    // All disk work runs on one worker. Consumers publish results only for their current UI generation.
    internal sealed class MovementFileCatalog
    {
        private sealed class CacheFile
        {
            public int Version { get; set; } = 1;
            public int ReaderVersion { get; set; } = 2; // Re-read cached Japanese diagnostics for the English UI.
            public List<MovementFileMetadata> Files { get; set; }
        }

        private readonly SemaphoreSlim _worker = new SemaphoreSlim(1, 1);
        private readonly Dictionary<string, MovementFileMetadata> _files = new Dictionary<string, MovementFileMetadata>(StringComparer.OrdinalIgnoreCase);
        private readonly MovementFileReader _reader = new MovementFileReader();
        private readonly string _cachePath;
        private readonly Action<string> _log;

        public MovementFileCatalog(string cacheDirectory, Action<string> log)
        {
            _cachePath = Path.Combine(cacheDirectory, "file-metadata-v1.json"); _log = log;
            var cache = JsonCache.Read<CacheFile>(_cachePath, log);
            if (cache?.Version != 1 || cache.ReaderVersion != 2 || cache.Files == null) return;
            foreach (var file in cache.Files)
            {
                if (file == null || string.IsNullOrEmpty(file.Path) || string.IsNullOrEmpty(file.Folder)) continue;
                file.Confirmed = false;
                _files[file.Path] = file;
            }
        }

        public async Task<CatalogSnapshot> ScanAsync(IEnumerable<string> folders, bool rebuild, CancellationToken token)
        {
            string[] paths = folders.Where(p => !string.IsNullOrEmpty(p)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            await _worker.WaitAsync(token).ConfigureAwait(false);
            try { return await Task.Run(() => Scan(paths, rebuild, token), token).ConfigureAwait(false); }
            finally { _worker.Release(); }
        }

        private CatalogSnapshot Scan(string[] folders, bool rebuild, CancellationToken token)
        {
            var warnings = new List<string>();
            int headersRead = 0, cacheHits = 0;
            var updated = _files.ToDictionary(p => p.Key, p => p.Value.Copy(), StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                token.ThrowIfCancellationRequested();
                string[] paths;
                try { paths = Directory.GetFiles(folder, "*.mvrec", SearchOption.TopDirectoryOnly); }
                catch (DirectoryNotFoundException) { paths = new string[0]; }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    warnings.Add(folder + ": " + ex.Message);
                    foreach (var old in updated.Values.Where(f => StringComparer.OrdinalIgnoreCase.Equals(f.Folder, folder)))
                        old.Confirmed = false;
                    continue;
                }
                var present = new HashSet<string>(paths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
                foreach (string removed in updated.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Value.Folder, folder) && !present.Contains(p.Key)).Select(p => p.Key).ToArray())
                    updated.Remove(removed);
                foreach (string path in present)
                {
                    token.ThrowIfCancellationRequested();
                    var info = new FileInfo(path);
                    try
                    {
                        if (!rebuild && updated.TryGetValue(path, out var cached) && !cached.TransientError && cached.MatchesAttributes(info))
                        { cached.Confirmed = true; cacheHits++; continue; }
                        headersRead++;
                        updated[path] = _reader.ReadMetadata(path, token);
                    }
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException ||
                        ex is Newtonsoft.Json.JsonException || ex is OverflowException)
                    {
                        var error = new MovementFileMetadata { Path = path, Folder = folder, Error = ex.Message,
                            TransientError = ex is UnauthorizedAccessException || ex is IOException && !(ex is InvalidDataException), Confirmed = false,
                            RecordedAtLocal = ChartIdentity.FilenameTime(path) };
                        try { error.Length = info.Length; error.LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks; } catch (IOException) { }
                        updated[path] = error;
                    }
                }
            }
            token.ThrowIfCancellationRequested();
            _files.Clear(); foreach (var entry in updated) _files.Add(entry.Key, entry.Value);
            JsonCache.Write(_cachePath, new CacheFile { Files = _files.Values.ToList() }, _log);
            return new CatalogSnapshot { Files = _files.Values.OrderByDescending(f => f.RecordedAtLocal).ThenBy(f => f.Path).ToArray(),
                Warnings = warnings.ToArray(), HeadersRead = headersRead, CacheHits = cacheHits };
        }
    }
}
