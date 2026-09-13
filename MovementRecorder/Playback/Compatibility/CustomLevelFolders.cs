using System.Collections.Generic;
using MovementRecorder.Playback.Runtime;

namespace MovementRecorder.Playback.Compatibility
{
    internal static class CustomLevelFolders
    {
        public static string GetPath(string levelId)
        {
            var loader = SongCore.Loader.CustomLevelLoader;
            if (loader == null || string.IsNullOrEmpty(levelId)) return null;
            var data = GameAccess.Get<Dictionary<string, CustomLevelLoader.LoadedSaveData>>(loader, "_loadedBeatmapSaveData");
            return data.TryGetValue(levelId, out var level) ? level.customLevelFolderInfo.folderPath : null;
        }
    }
}
