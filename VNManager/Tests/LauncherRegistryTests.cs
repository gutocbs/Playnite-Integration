using Launcher.Host;

namespace Launcher.Tests;

public sealed class LauncherRegistryTests
{
    [Fact]
    public void Resolve_MatchesRegisteredLauncherCaseInsensitively()
    {
        var launcher = new FakeLauncher();
        var registry = new LauncherRegistry(
        [
            new LauncherRegistration("Locale Emulator", _ => launcher)
        ]);

        var resolved = registry.Resolve("locale emulator", "execution-id");

        Assert.Same(launcher, resolved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../LocaleEmulator")]
    [InlineData("C:\\LocaleEmulator.dll")]
    [InlineData("https://example.com/launcher")]
    public void Resolve_RejectsInvalidLauncherNames(string launcherName)
    {
        var registry = new LauncherRegistry([]);

        var exception = Assert.Throws<LauncherResolutionException>(
            () => registry.Resolve(launcherName, "execution-id"));

        Assert.Equal("INVALID_LAUNCHER_NAME", exception.Code);
    }
}
