using Launcher.NoRegionLoader;

namespace Launcher.Tests;

public sealed class NoRegionLoaderOptionsLoaderTests
{
    [Fact]
    public void Load_ImportsAllValuesFromJson()
    {
        using var files = new TemporaryFiles();
        var configurationPath = files.CreateFile(
            "no-region-loader.json",
            """
            {
              "noRegionLoaderExecutable": "C:\\Tools\\NoRegion Loader.exe",
              "pollingInterval": "00:00:00.250"
            }
            """);

        var options = NoRegionLoaderOptionsLoader.Load(configurationPath);

        Assert.Equal(@"C:\Tools\NoRegion Loader.exe", options.NoRegionLoaderExecutable);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.PollingInterval);
    }
}
