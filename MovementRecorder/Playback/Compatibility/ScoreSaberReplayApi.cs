using System;
using System.Linq;
using System.Reflection;

namespace MovementRecorder.Playback.Compatibility
{
    // ScoreSaber 3.3 and 3.4 expose different APIs independently of the game version.
    internal sealed class ScoreSaberReplayApi
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        public MethodInfo Record { get; private set; }
        public MethodInfo Submit { get; private set; }
        public Func<bool> IsPlaybackEnabled { get; private set; }

        public static ScoreSaberReplayApi Resolve(Func<string, Type> find)
        {
            var registry = find("ScoreSaber.Features.Replays.ReplayStateRegistry");
            if (registry != null)
            {
                var enabled = registry.GetProperty("IsPlaybackEnabled", All)?.GetGetMethod(true);
                if (enabled == null || !enabled.IsStatic || enabled.ReturnType != typeof(bool)) throw Unsupported();
                return new ScoreSaberReplayApi {
                    Record = Method(find("ScoreSaber.Features.Replays.Installers.RecordInstaller"), "InstallBindings"),
                    Submit = Method(find("ScoreSaber.Features.ScoreSubmission.ScoreSubmissionController"), "HandleStandardLevelFinished"),
                    IsPlaybackEnabled = () => (bool)enabled.Invoke(null, null)
                };
            }

            var plugin = find("ScoreSaber.Plugin");
            var instance = plugin?.GetProperty("Instance", All)?.GetGetMethod(true);
            var state = plugin?.GetProperty("ReplayState", All)?.GetGetMethod(true);
            var playback = state?.ReturnType.GetField("IsPlaybackEnabled", All);
            if (instance == null || !instance.IsStatic || state == null || state.IsStatic ||
                playback == null || playback.IsStatic || playback.FieldType != typeof(bool)) throw Unsupported();
            return new ScoreSaberReplayApi {
                Record = Method(find("ScoreSaber.Core.ReplaySystem.Installers.RecordInstaller"), "InstallBindings"),
                // Three is the legacy UploadDaemon's solo level-finished callback, before local saving/upload.
                Submit = Method(find("ScoreSaber.Core.Daemons.UploadDaemon"), "Three"),
                IsPlaybackEnabled = () => {
                    var owner = instance.Invoke(null, null) ?? throw Unsupported();
                    var value = state.Invoke(owner, null) ?? throw Unsupported();
                    return (bool)playback.GetValue(value);
                }
            };
        }

        private static MethodInfo Method(Type type, string name)
        {
            var methods = type?.GetMethods(All | BindingFlags.DeclaredOnly).Where(m => m.Name == name).ToArray();
            if (methods == null || methods.Length != 1 || methods[0].ReturnType != typeof(void)) throw Unsupported();
            return methods[0];
        }
        private static Exception Unsupported() => new InvalidOperationException("Cannot find the API required to prevent ScoreSaber from saving replay results.");
    }
}
