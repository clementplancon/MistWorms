using Xunit;

namespace MistWorms.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void PluginConstants()
        {
            // The GUID names the config file and the Harmony id; changing it orphans every user's config.
            Assert.Equal("dev.pointvirgule.mistworms", Plugin.ModGuid);
            Assert.Equal("MistWorms", Plugin.ModName);
        }
    }
}
