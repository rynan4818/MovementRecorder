using MovementRecorder.Playback.Compatibility;
using Xunit;

namespace MovementRecorder.Tests
{
    [Collection("Unity fixtures")]
    public sealed class CustomLevelFoldersTests
    {
        [Fact] public void ResolvesTheSelectedLevelWithoutFallingBackToAnotherFolder()
        {
            var loader = SongCore.Loader.CustomLevelLoader = new CustomLevelLoader();
            try
            {
                loader.Add("custom_level_A", "CustomLevels/A");
                loader.Add("custom_level_B", "CustomWIPLevels/B");
                Assert.Equal("CustomWIPLevels/B", CustomLevelFolders.GetPath("custom_level_B"));
                Assert.Equal("CustomLevels/A", CustomLevelFolders.GetPath("custom_level_A"));
                Assert.Null(CustomLevelFolders.GetPath("unknown"));
                Assert.Null(CustomLevelFolders.GetPath(null));
            }
            finally { SongCore.Loader.CustomLevelLoader = null; }
        }

        [Fact] public void MissingLoaderHasNoCustomFolder()
        {
            SongCore.Loader.CustomLevelLoader = null;
            Assert.Null(CustomLevelFolders.GetPath("custom_level_A"));
        }
    }
}
