using System;
using MovementRecorder.Playback.Compatibility;
using Xunit;

namespace MovementRecorder.Tests
{
    public class ScoreSaberReplayApiTests
    {
        private sealed class Recorder { public void InstallBindings() { } }
        private sealed class Submission { public void HandleStandardLevelFinished() { } public void Three() { } }
        private static class Registry { public static bool IsPlaybackEnabled { get; set; } }
        private sealed class State { public bool IsPlaybackEnabled; }
        private sealed class LegacyPlugin {
            public static LegacyPlugin Instance { get; set; } = new LegacyPlugin();
            public State ReplayState { get; } = new State();
        }
        private static Type Find(string name) => name.EndsWith(".RecordInstaller") ? typeof(Recorder) :
            name.EndsWith(".ScoreSubmissionController") || name.EndsWith(".UploadDaemon") ? typeof(Submission) :
            name == "ScoreSaber.Plugin" ? typeof(LegacyPlugin) : null;

        [Fact]
        public void ModernStateIsReadAgainForEachStart()
        {
            var api = ScoreSaberReplayApi.Resolve(n => n.EndsWith(".ReplayStateRegistry") ? typeof(Registry) : Find(n));
            Registry.IsPlaybackEnabled = false;
            Assert.False(api.IsPlaybackEnabled());
            Registry.IsPlaybackEnabled = true;
            Assert.True(api.IsPlaybackEnabled());
            Assert.Equal("HandleStandardLevelFinished", api.Submit.Name);
            Assert.Equal("InstallBindings", api.Record.Name);
            Registry.IsPlaybackEnabled = false;
        }
        [Fact]
        public void LegacyStateComesFromCurrentPluginInstance()
        {
            var api = ScoreSaberReplayApi.Resolve(Find);
            LegacyPlugin.Instance = new LegacyPlugin();
            Assert.False(api.IsPlaybackEnabled());
            LegacyPlugin.Instance = new LegacyPlugin();
            LegacyPlugin.Instance.ReplayState.IsPlaybackEnabled = true;
            Assert.True(api.IsPlaybackEnabled());
            Assert.Equal("Three", api.Submit.Name);
        }
        [Fact]
        public void MissingInstanceBlocksStart()
        {
            var api = ScoreSaberReplayApi.Resolve(Find);
            LegacyPlugin.Instance = null;
            Assert.Throws<InvalidOperationException>(() => api.IsPlaybackEnabled());
            LegacyPlugin.Instance = new LegacyPlugin();
        }
        [Fact]
        public void MissingSubmissionApiBlocksStart()
        {
            Assert.Throws<InvalidOperationException>(() => ScoreSaberReplayApi.Resolve(n => n.EndsWith(".UploadDaemon") ? null : Find(n)));
        }
    }
}
