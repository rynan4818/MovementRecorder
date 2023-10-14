using HarmonyLib;

namespace MovementRecorder.HarmonyPatches
{
    [HarmonyPatch(typeof(CustomPreviewBeatmapLevel), nameof(CustomPreviewBeatmapLevel.GetCoverImageAsync))]
    public class GetCoverImageAsyncPatch
    {
        public static string CustomLevelPath = string.Empty;
        static void Postfix(CustomPreviewBeatmapLevel __instance)
        {
                CustomLevelPath = __instance.customLevelPath;
        }
    }
}
