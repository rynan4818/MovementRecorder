using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace MovementRecorder.Playback.Compatibility
{
    internal sealed class ReplaySaveGuards
    {
        private readonly Harmony _harmony = new Harmony(Plugin.HARMONY_ID + ".ReplayCompatibility");
        private readonly HashSet<MethodBase> _patched = new HashSet<MethodBase>();
        private readonly ConditionalWeakTable<object, object> _replayOwners = new ConditionalWeakTable<object, object>();
        private readonly List<Func<bool>> _otherReplayChecks = new List<Func<bool>>();
        private static ReplaySaveGuards _instance;
        public ReplaySaveGuards() { _instance = this; }

        public void Prepare()
        {
            _otherReplayChecks.Clear();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name == "BeatLeader")
                {
                    Hook(assembly, "BeatLeader.Installers.OnGameplayCoreInstaller", "InitRecorder");
                    Hook(assembly, "BeatLeader.Utils.ScoreUtil", "ProcessReplay");
                    Type launcher = RequireType(assembly, "BeatLeader.Replayer.ReplayerLauncher");
                    var property = AccessTools.Property(launcher, "IsStartedAsReplay");
                    var field = property == null ? AccessTools.Field(launcher, "IsStartedAsReplay") : null;
                    if (property == null && field == null) throw Unsupported(name, "replay state");
                    _otherReplayChecks.Add(() => (bool)(property != null ? property.GetValue(null, null) : field.GetValue(null)));
                }
                else if (name == "ScoreSaber")
                {
                    var api = ScoreSaberReplayApi.Resolve(type => assembly.GetType(type, false));
                    Hook(api.Record);
                    Hook(api.Submit);
                    _otherReplayChecks.Add(api.IsPlaybackEnabled);
                }
                else if (name == "SongPlayHistoryContinued" || name == "SongPlayHistory")
                {
                    if (assembly.GetType("SongPlayHistoryContinued.Plugin", false) != null)
                    {
                        Hook(assembly, "SongPlayHistoryContinued.Plugin", "SaveRecord");
                        continue;
                    }
                    Type tracker = RequireType(assembly, "SongPlayHistory.SongPlayTracking.SongPlayTracker");
                    MethodInfo initialize = tracker.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .SingleOrDefault(m => (m.Name == "Initialize" || m.Name.EndsWith(".Initialize", StringComparison.Ordinal)) && m.GetParameters().Length == 0);
                    Hook(initialize ?? throw Unsupported(name, "history initialization"));
                    Hook(assembly, tracker.FullName, "HandleLevelFinished");
                }
            }
            if (_otherReplayChecks.Any(check => check())) throw new InvalidOperationException("Another mod's replay is running. End it and select a recording again.");
        }

        private static Type RequireType(Assembly assembly, string name) => assembly.GetType(name, false) ?? throw Unsupported(assembly.GetName().Name, name);
        private static Exception Unsupported(string mod, string member) => new InvalidOperationException("Cannot find the API required to prevent " + mod + " from saving replay results: " + member);
        private void Hook(Assembly assembly, string type, string method)
        {
            var methods = RequireType(assembly, type).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == method).ToArray();
            if (methods.Length != 1) throw Unsupported(assembly.GetName().Name, type + "." + method);
            Hook(methods[0]);
        }
        private void Hook(MethodInfo method)
        {
            if (method.ReturnType != typeof(void)) throw Unsupported(method.DeclaringType.Assembly.GetName().Name, method.Name + " signature");
            if (_patched.Contains(method)) return;
            _harmony.Patch(method, prefix: new HarmonyMethod(typeof(ReplaySaveGuards), method.IsStatic ? nameof(StaticPrefix) : nameof(InstancePrefix)) { priority = Priority.First });
            _patched.Add(method);
            Plugin.Log?.Info("Replay save guard: " + method.DeclaringType.FullName + "." + method.Name);
        }
        private static bool StaticPrefix() => !MovementReplay.IsActive;
        private static bool InstancePrefix(object __instance, MethodBase __originalMethod)
        {
            // Only the per-game tracker can receive a delayed finish callback. Installers and app-wide
            // submission controllers may be reused on the next play and must not keep a sticky flag.
            bool rememberOwner = __originalMethod.DeclaringType.FullName == "SongPlayHistory.SongPlayTracking.SongPlayTracker";
            if (MovementReplay.IsActive)
            {
                if (rememberOwner) _instance._replayOwners.GetValue(__instance, _ => new object());
                return false;
            }
            return !rememberOwner || !_instance._replayOwners.TryGetValue(__instance, out _);
        }
    }
}
