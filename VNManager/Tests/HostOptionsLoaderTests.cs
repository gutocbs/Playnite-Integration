using Launcher.Host;

namespace Launcher.Tests;

public sealed class HostOptionsLoaderTests
{
    [Fact]
    public void Load_ImportsTemporaryDirectoryMaximumAge()
    {
        using var files = new TemporaryFiles();
        var path = files.CreateFile(
            "host.json",
            """
            { "temporaryDirectoryMaximumAgeSeconds": 86400 }
            """);

        var options = HostOptionsLoader.Load(path);

        Assert.Equal(86_400, options.TemporaryDirectoryMaximumAgeSeconds);
    }
}
