using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MovementRecorder.Playback.Data
{
    internal static class JsonCache
    {
        public static T Read<T>(string path, Action<string> log) where T : class
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Cache exceeds 32 MiB.");
                using (var input = File.OpenText(path))
                using (var reader = new JsonTextReader(input) { MaxDepth = 32, DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind })
                    return new JsonSerializer { TypeNameHandling = TypeNameHandling.None }.Deserialize<T>(reader);
            }
            catch (Exception ex) { log?.Invoke("Cache ignored: " + ex.Message); return null; }
        }

        public static void Write<T>(string path, T value, Action<string> log)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var output = new StreamWriter(temporary, false, new UTF8Encoding(false)))
                using (var writer = new JsonTextWriter(output)) new JsonSerializer { TypeNameHandling = TypeNameHandling.None }.Serialize(writer, value);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (Exception ex) { log?.Invoke("Cache write skipped: " + ex.Message); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }
}
