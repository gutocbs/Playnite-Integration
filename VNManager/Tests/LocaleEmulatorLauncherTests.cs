using Launcher.Abstractions;
using Launcher.LocaleEmulator;
using Launcher.ProcessManagement;

namespace Launcher.Tests;

public sealed class LocaleEmulatorLauncherTests
{
    [Fact]
    public async Task LaunchAsync_StartsMonitorsAndCleansUpConfiguredProcesses()
    {
        using var files = new TemporaryFiles();
        var gamePath = files.CreateFile("game.exe");
        var localeEmulatorPath = files.CreateFile("LEProc.exe");
        var options = CreateOptions(localeEmulatorPath);
        var starter = new FakeProcessStarter();
        var monitor = new FakeProcessMonitor { MonitoredProcessName = "child-game" };
        var cleanup = new FakeProcessCleanup();
        var launcher = new LocaleEmulatorLauncher(
            options,
            starter,
            monitor,
            cleanup,
            new NullLogger());
        var request = new LaunchRequest("execution-id", gamePath, ["--flag", "value"]);

        var result = await launcher.LaunchAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(request, starter.Request);
        Assert.Equal("child-game", cleanup.MonitoredProcessName);
        Assert.Equal(42, result.Data?["LocaleEmulatorProcessId"]);
    }

    [Fact]
    public async Task LaunchAsync_WhenMonitoringIsCancelled_ReturnsFailureAndRunsCleanup()
    {
        using var files = new TemporaryFiles();
        var gamePath = files.CreateFile("game.exe");
        var localeEmulatorPath = files.CreateFile("LEProc.exe");
        var options = CreateOptions(localeEmulatorPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var monitor = new FakeProcessMonitor
        {
            Behavior = token => Task.FromException<string>(new OperationCanceledException(token))
        };
        var cleanup = new FakeProcessCleanup();
        var launcher = new LocaleEmulatorLauncher(
            options,
            new FakeProcessStarter(),
            monitor,
            cleanup,
            new NullLogger());

        var result = await launcher.LaunchAsync(
            new LaunchRequest("execution-id", gamePath, []),
            cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal("LAUNCH_CANCELLED", result.Error?.Code);
        Assert.Null(cleanup.MonitoredProcessName);
    }

    [Fact]
    public async Task LaunchAsync_WhenGameDoesNotExist_DoesNotStartLocaleEmulator()
    {
        using var files = new TemporaryFiles();
        var localeEmulatorPath = files.CreateFile("LEProc.exe");
        var starter = new FakeProcessStarter();
        var launcher = new LocaleEmulatorLauncher(
            CreateOptions(localeEmulatorPath),
            starter,
            new FakeProcessMonitor(),
            new FakeProcessCleanup(),
            new NullLogger());

        var result = await launcher.LaunchAsync(
            new LaunchRequest(
                "execution-id",
                Path.Combine(files.DirectoryPath, "missing.exe"),
                []),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("GAME_EXECUTABLE_NOT_FOUND", result.Error?.Code);
        Assert.Null(starter.Request);
    }

    [Fact]
    public async Task LaunchAsync_WhenProcessStartTimesOut_ReturnsSpecificFailure()
    {
        using var files = new TemporaryFiles();
        var gamePath = files.CreateFile("game.exe");
        var localeEmulatorPath = files.CreateFile("LEProc.exe");
        var monitor = new FakeProcessMonitor
        {
            Behavior = _ => Task.FromException<string>(new ProcessStartTimeoutException(
                "The configured game process did not start within 30 seconds."))
        };
        var launcher = new LocaleEmulatorLauncher(
            CreateOptions(localeEmulatorPath),
            new FakeProcessStarter(),
            monitor,
            new FakeProcessCleanup(),
            new NullLogger());

        var result = await launcher.LaunchAsync(
            new LaunchRequest("execution-id", gamePath, []),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("PROCESS_START_TIMEOUT", result.Error?.Code);
        Assert.Contains("30 seconds", result.Error?.Message);
    }

    private static LocaleEmulatorOptions CreateOptions(string localeEmulatorPath) => new()
    {
        LocaleEmulatorExecutable = localeEmulatorPath,
        ProfileId = "profile-id",
        PollingInterval = TimeSpan.FromMilliseconds(10)
    };
}
