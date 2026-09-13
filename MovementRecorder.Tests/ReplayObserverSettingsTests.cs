using System;
using MovementRecorder.Configuration;
using MovementRecorder.Playback;
using Newtonsoft.Json;
using Xunit;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class ReplayObserverSettingsTests : IDisposable
    {
        public ReplayObserverSettingsTests() { PluginConfig.Instance = new PluginConfig(); }
        public void Dispose() { ReplaySession.Current?.Finish(); PluginConfig.Instance = null; }

        [Fact] public void OldSettingsUseExistingViewPositionAndDisableAvatarOffset()
        {
            PluginConfig.Instance = JsonConvert.DeserializeObject<PluginConfig>("{}");
            PluginConfig.Instance.OnReload(); var session = new ReplaySession();
            Assert.False(PluginConfig.Instance.offsetReplaySourceAvatarWithHmd);
            Assert.Equal(0, session.ObserverX); Assert.Equal(0, session.ObserverY); Assert.Equal(-2, session.ObserverZ);
        }

        [Fact] public void PositionEditsAndToggleSurviveConfigurationRoundTripAndNewSession()
        {
            var session = new ReplaySession(); session.Begin(null, true, true);
            PluginConfig.Instance.offsetReplaySourceAvatarWithHmd = true;
            session.ObserverX = 2.5f; session.ObserverY = -.5f; session.ObserverZ = -6;
            session.Finish();
            // Exercises product settings serialization and loading; the native BSIPA disk writer is not simulated.
            PluginConfig.Instance = JsonConvert.DeserializeObject<PluginConfig>(JsonConvert.SerializeObject(PluginConfig.Instance));
            PluginConfig.Instance.OnReload(); var next = new ReplaySession();
            Assert.True(PluginConfig.Instance.offsetReplaySourceAvatarWithHmd);
            Assert.Equal(2.5f, next.ObserverX); Assert.Equal(-.5f, next.ObserverY); Assert.Equal(-6, next.ObserverZ);
        }

        [Theory]
        [InlineData(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0, 0, -2)]
        [InlineData(20, -20, 20, 5, -3, 5)]
        [InlineData(-20, 20, -20, -5, 3, -8)]
        public void ReloadAndEditingNormalizeInvalidOrOutOfRangeValues(float x, float y, float z, float expectedX, float expectedY, float expectedZ)
        {
            var config = PluginConfig.Instance; config.replayObserverX = x; config.replayObserverY = y; config.replayObserverZ = z;
            config.OnReload(); var session = new ReplaySession();
            Assert.Equal(expectedX, session.ObserverX); Assert.Equal(expectedY, session.ObserverY); Assert.Equal(expectedZ, session.ObserverZ);
            session.ObserverX = x; session.ObserverY = y; session.ObserverZ = z;
            Assert.Equal(expectedX, config.replayObserverX); Assert.Equal(expectedY, config.replayObserverY); Assert.Equal(expectedZ, config.replayObserverZ);
        }

        [Fact] public void ExternalReloadDoesNotMoveAnActiveReplayAndReadingNeverWritesSettings()
        {
            var config = new CountingConfig(); PluginConfig.Instance = config;
            var session = new ReplaySession(); session.ObserverX = 1; session.ObserverY = 2; session.ObserverZ = -3;
            session.Begin(null, true, true);
            config.replayObserverX = -4; config.replayObserverY = -2; config.replayObserverZ = 1;
            config.offsetReplaySourceAvatarWithHmd = false; config.OnReload(); session.LoadObserverPosition();
            int writes = config.Writes;
            for (int i = 0; i < 60; i++)
            {
                Assert.Equal(1, session.ObserverX); Assert.Equal(2, session.ObserverY); Assert.Equal(-3, session.ObserverZ);
                Assert.True(session.OffsetSourceAvatarWithHmd);
            }
            Assert.Equal(writes, config.Writes);
            session.Finish(); session.LoadObserverPosition();
            Assert.Equal(-4, session.ObserverX); Assert.Equal(-2, session.ObserverY); Assert.Equal(1, session.ObserverZ);
            Assert.Equal(writes, config.Writes);
        }

        [Theory]
        [InlineData(-5f, .1f, 50)] [InlineData(-3f, .1f, 30)] [InlineData(-8f, .1f, 80)] [InlineData(5f, -.1f, 50)]
        public void TenthMeterButtonStepsReturnToExactZeroAndPersistThatValue(float start, float step, int count)
        {
            var session = new ReplaySession(); float controlValue = start;
            // BSML keeps its own float accumulator rather than reading the property after each click.
            for (int i = 0; i < count; i++)
            {
                controlValue += step;
                session.ObserverX = controlValue; session.ObserverY = controlValue; session.ObserverZ = controlValue;
            }
            Assert.Equal(0, session.ObserverX); Assert.Equal(0, session.ObserverY); Assert.Equal(0, session.ObserverZ);
            Assert.Equal(0, PluginConfig.Instance.replayObserverX); Assert.Equal(0, PluginConfig.Instance.replayObserverY);
            Assert.Equal(0, PluginConfig.Instance.replayObserverZ);
            Assert.False(Playback.Models.SourceAvatarOffset.HasOffset(new UnityEngine.Vector3(session.ObserverX, session.ObserverY, session.ObserverZ)));
        }

        [Fact] public void LoadingPreservesSavedPrecisionAndEditsDoNotForceArbitraryValuesOntoTheTenthGrid()
        {
            PluginConfig.Instance.replayObserverX = .30001f;
            PluginConfig.Instance.replayObserverY = .25f;
            var session = new ReplaySession();
            Assert.Equal(.30001f, session.ObserverX); Assert.Equal(.30001f, PluginConfig.Instance.replayObserverX);
            session.ObserverY += .1f; Assert.Equal(.35f, session.ObserverY);
        }

        private sealed class CountingConfig : PluginConfig
        {
            public int Writes;
            public override float replayObserverX { get => base.replayObserverX; set { Writes++; base.replayObserverX = value; } }
            public override float replayObserverY { get => base.replayObserverY; set { Writes++; base.replayObserverY = value; } }
            public override float replayObserverZ { get => base.replayObserverZ; set { Writes++; base.replayObserverZ = value; } }
        }
    }
}
