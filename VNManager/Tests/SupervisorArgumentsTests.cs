using Launcher.Supervisor;

namespace Launcher.Tests;

public sealed class SupervisorArgumentsTests
{
    private const string BaseDirectory = @"C:\Plugin";

    [Fact]
    public void TryParse_CollectsArgumentsAfterSeparator()
    {
        var parsed = SupervisorArguments.TryParse(
            ["--executable", @"C:\G\g.exe", "--launcher", "NoRegionLoader", "--", "-a", "--b"],
            BaseDirectory,
            out var arguments,
            out _);

        Assert.True(parsed);
        Assert.Equal(@"C:\G\g.exe", arguments!.Executable);
        Assert.Equal("NoRegionLoader", arguments.LauncherName);
        Assert.Equal(["-a", "--b"], arguments.GameArguments);
    }

    [Fact]
    public void TryParse_DefaultsLauncherConfigurationAndLog()
    {
        var parsed = SupervisorArguments.TryParse(
            ["--executable", @"C:\G\g.exe"],
            BaseDirectory,
            out var arguments,
            out _);

        Assert.True(parsed);
        Assert.Equal("Locale Emulator", arguments!.LauncherName);
        Assert.Equal(@"C:\Plugin\Configuration", arguments.ConfigurationDirectory);
        Assert.Equal(@"C:\Plugin\launcher.log", arguments.LogPath);
        Assert.Empty(arguments.GameArguments);
    }

    [Fact]
    public void TryParse_RequiresExecutable()
    {
        var parsed = SupervisorArguments.TryParse(["--launcher", "X"], BaseDirectory, out _, out var error);

        Assert.False(parsed);
        Assert.Equal("O argumento --executable é obrigatório.", error);
    }

    [Fact]
    public void TryParse_RejectsOptionWithoutValue()
    {
        var parsed = SupervisorArguments.TryParse(["--executable"], BaseDirectory, out _, out var error);

        Assert.False(parsed);
        Assert.Equal(
            "Argumentos inválidos. Use --executable <caminho> [--launcher <nome>] [-- ...argumentos].",
            error);
    }
}
