using Launcher.Abstractions;
using Launcher.LocaleEmulator;
using Launcher.NoRegionLoader;

namespace Launcher.Tests;

public sealed class ProcessStarterTests
{
    [Fact]
    public void LocaleEmulatorStartInfo_PassesProfileGameAndArguments()
    {
        var startInfo = LocaleEmulatorProcessStarter.CreateStartInfo(
            new LocaleEmulatorOptions { LocaleEmulatorExecutable = @"C:\LE\LEProc.exe", ProfileId = "profile" },
            new LaunchRequest("id", @"C:\Game\game.exe", ["-a", "b c"]));

        Assert.Equal(@"C:\LE\LEProc.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(["-runas", "profile", @"C:\Game\game.exe", "-a", "b c"], startInfo.ArgumentList);
    }

    [Fact]
    public void NoRegionLoaderStartInfo_RunsFromGameDirectory()
    {
        var startInfo = NoRegionLoaderProcessStarter.CreateStartInfo(
            new NoRegionLoaderOptions { NoRegionLoaderExecutable = @"C:\NRL\NoRegion Loader.exe" },
            new LaunchRequest("id", @"C:\Game\game.exe", ["-a", "b c"]));

        Assert.Equal(@"C:\NRL\NoRegion Loader.exe", startInfo.FileName);
        Assert.Equal(@"C:\Game", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal([@"C:\Game\game.exe", "-a", "b c"], startInfo.ArgumentList);
    }
}
