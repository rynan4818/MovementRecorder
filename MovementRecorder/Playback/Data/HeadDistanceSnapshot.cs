using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MovementRecorder.Playback.Data
{
    // LiteDB never receives the source filename or a writable handle to the user's database.
    internal sealed class HeadDistanceSnapshot : IDisposable
    {
        internal const long MaxBytes = 128L * 1024 * 1024;
        public MemoryStream Data { get; private set; }
        public MemoryStream Log { get; private set; }
        public string Fingerprint { get; private set; }
        public string Attributes { get; private set; }
        public double LockMilliseconds { get; private set; }

        public static string LogPath(string path) => Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "-log" + Path.GetExtension(path));
        public static string MutexName(string path)
        {
            // Matches LiteDB 5 SharedEngine's name, including its path casing rule.
            using (var sha = SHA1.Create())
                return "Global\\" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLower()))).Replace("-", "") + ".Mutex";
        }

        public static HeadDistanceSnapshot Capture(string path, CancellationToken token)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) return null;
            var result = new HeadDistanceSnapshot();
            bool held = false;
            Stopwatch locked = null;
            using (var mutex = new Mutex(false, MutexName(path)))
            {
                try
                {
                    var watch = Stopwatch.StartNew();
                    while (!held && watch.ElapsedMilliseconds < 1000)
                    {
                        token.ThrowIfCancellationRequested();
                        try { held = mutex.WaitOne(50); } catch (AbandonedMutexException) { held = true; }
                    }
                    if (!held) throw new IOException("距離DBは更新中です。");
                    locked = Stopwatch.StartNew();
                    string logPath = LogPath(path), before = Stamp(path, logPath);
                    using (var data = Open(path))
                    using (var log = File.Exists(logPath) ? Open(logPath) : null)
                    {
                        if (data.Length == 0 || data.Length % 8192 != 0 || (log != null && log.Length % 8192 != 0) ||
                            data.Length + (log?.Length ?? 0) > MaxBytes)
                            throw new InvalidDataException("距離DBのサイズまたはページ境界が未対応です。");
                        result.Data = Copy(data, token, watch);
                        result.Log = log == null ? new MemoryStream() : Copy(log, token, watch);
                        result.Attributes = Stamp(path, logPath);
                        if (before != result.Attributes) throw new IOException("距離DBが読込中に更新されました。");
                    }
                }
                catch { result.Dispose(); throw; }
                finally { if (held) { result.LockMilliseconds = locked?.Elapsed.TotalMilliseconds ?? 0; mutex.ReleaseMutex(); } }
            }
            using (var sha = SHA256.Create())
            {
                string dataHash = BitConverter.ToString(sha.ComputeHash(result.Data)).Replace("-", "");
                string logHash = BitConverter.ToString(sha.ComputeHash(result.Log)).Replace("-", "");
                result.Fingerprint = dataHash + ":" + logHash;
                result.Data.Position = result.Log.Position = 0;
            }
            return result;
        }

        private static string Stamp(string path, string logPath)
        {
            var data = new FileInfo(path); var log = new FileInfo(logPath);
            return path + "|" + data.Length + "|" + data.LastWriteTimeUtc.Ticks + "|" +
                (log.Exists ? log.Length + "|" + log.LastWriteTimeUtc.Ticks : "no-log");
        }
        private static FileStream Open(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan);
        private static MemoryStream Copy(FileStream source, CancellationToken token, Stopwatch watch)
        {
            var result = new MemoryStream(checked((int)source.Length));
            try
            {
                var buffer = new byte[65536]; int length;
                while ((length = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (watch.ElapsedMilliseconds > 5000) throw new IOException("距離DBの読込がタイムアウトしました。");
                    result.Write(buffer, 0, length);
                }
                result.Position = 0; return result;
            }
            catch { result.Dispose(); throw; }
        }
        public void Dispose() { Data?.Dispose(); Log?.Dispose(); }
    }
}
