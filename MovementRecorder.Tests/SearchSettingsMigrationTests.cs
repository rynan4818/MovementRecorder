using System.Linq;
using MovementRecorder.Configuration;
using Xunit;

namespace MovementRecorder.Tests
{
    public class SearchSettingsMigrationTests
    {
        private static PluginConfig OldSettings()
        {
            var config = new PluginConfig { replayObserverX = 1.2f, replayObserverY = -.3f, replayObserverZ = -4.1f,
                showReplaySourceAvatar = true, offsetReplaySourceAvatarWithHmd = true };
            config.searchSettings.RemoveAll(s => s.name == "CustomSabersLite");
            var avatar = config.searchSettings.Single(s => s.name == "CustomAvatar");
            string Old(string value) => value.Replace("/SpawnedAvatar", "/Avatar Container/SpawnedAvatar");
            avatar.rescaleString = Old(avatar.rescaleString);
            avatar.topObjectStrings = avatar.topObjectStrings.Select(Old).ToList();
            avatar.searchStirngs = avatar.searchStirngs.Select(Old).ToList();
            avatar.exclusionStrings = avatar.exclusionStrings.Select(Old).ToList();
            return config;
        }
        [Fact]
        public void FormerDefaultsMigrateOnceAndKeepObserverSettings()
        {
            var config = OldSettings();
            config.OnReload();
            var list = config.searchSettings;
            Assert.DoesNotContain("Avatar Container", list.Single(s => s.name == "CustomAvatar").rescaleString);
            Assert.Single(list, s => s.name == "CustomSabersLite");
            config.OnReload(); Assert.Same(list, config.searchSettings);
            Assert.Equal(1.2f, config.replayObserverX); Assert.Equal(-.3f, config.replayObserverY); Assert.Equal(-4.1f, config.replayObserverZ);
            Assert.True(config.showReplaySourceAvatar); Assert.True(config.offsetReplaySourceAvatarWithHmd);
        }
        [Fact]
        public void PartlyCustomizedAvatarIsPreservedInFull()
        {
            var config = OldSettings(); var avatar = config.searchSettings.Single(s => s.name == "CustomAvatar");
            avatar.exclusionStrings.Add("MyExtraExclusion");
            config.OnReload();
            Assert.Same(avatar, config.searchSettings.Single(s => s.name == "CustomAvatar"));
            Assert.Contains("Avatar Container", avatar.rescaleString); Assert.Contains("MyExtraExclusion", avatar.exclusionStrings);
        }
    }
}
