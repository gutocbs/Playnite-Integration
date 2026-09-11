using Launcher.LocaleEmulator;

namespace Launcher.Tests;

public sealed class LocaleEmulatorOptionsLoaderTests
{
    [Fact]
    public void Load_ImportsAllValuesFromJson()
    {
        using var files = new TemporaryFiles();
        var configurationPath = files.CreateFile(
            "locale-emulator.json",
            """
            {
              "localeEmulatorExecutable": "C:\\Tools\\LEProc.exe",
              "profileId": "profile-id",
              "pollingInterval": "00:00:00.250"
            }
            """);

        var options = LocaleEmulatorOptionsLoader.Load(configurationPath);

        Assert.Equal(@"C:\Tools\LEProc.exe", options.LocaleEmulatorExecutable);
        Assert.Equal("profile-id", options.ProfileId);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.PollingInterval);
    }
}
