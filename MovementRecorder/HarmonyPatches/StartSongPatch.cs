using HarmonyLib;
using System;

namespace MovementRecorder.HarmonyPatches
{
    [HarmonyPatch(typeof(AudioTimeSyncController), nameof(AudioTimeSyncController.StartSong))]
    public class StartSongPatch
    {
        public static event Action StartSong;
        public static void Postfix()
        {
            StartSong?.Invoke();
        }
    }
}
