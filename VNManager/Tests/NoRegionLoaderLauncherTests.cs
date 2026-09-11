using Launcher.Abstractions;
using Launcher.NoRegionLoader;
using Launcher.ProcessManagement;

namespace Launcher.Tests;

public sealed class NoRegionLoaderLauncherTests
{
    [Fact]
    public async Task LaunchAsync_StartsMonitorsAndCleansUpConfiguredProcesses()
    {
        using var files = new TemporaryFiles();
        var gamePath = files.CreateFile("game.exe");
        var loaderPath = files.CreateFile("NoRegion Loader.exe");
        var starter = new FakeNoRegionLoaderProcessStarter();
        var monitor = new FakeProcessMonitor { MonitoredProcessName = "child-game" };
        var cleanup = new FakeProcessCleanup();
        var launcher = new NoRegionLoaderLauncher(
            CreateOptions(loaderPath), starter, monitor, cleanup, new NullLogger());
        var request = new LaunchRequest("execution-id", gamePath, ["--flag", "value"]);

        var result = await launcher.LaunchAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Same(request, starter.Request);
        Assert.Equal("child-game", cleanup.MonitoredProcessName);
        Assert.Equal(64, result.Data?["NoRegionLoaderProcessId"]);
    }

    [Fact]
    public async Task LaunchAsync_WhenLoaderDoesNotExist_DoesNotStartTheProcess()
    {
        using var files = new TemporaryFiles();
        var starter = new FakeNoRegionLoaderProcessStarter();
        var launcher = new NoRegionLoaderLauncher(
            CreateOptions(Path.Combine(files.DirectoryPath, "missing.exe")),
            starter,
            new FakeProcessMonitor(),
            new FakeProcessCleanup(),
            new NullLogger());

        var result = await launcher.LaunchAsync(
            new LaunchRequest("execution-id", files.CreateFile("game.exe"), []),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("NO_REGION_LOADER_NOT_FOUND", result.Error?.Code);
        Assert.Null(starter.Request);
    }

    [Fact]
    public async Task LaunchAsync_WhenProcessStartTimesOut_ReturnsSpecificFailure()
    {
        using var files = new TemporaryFiles();
        var monitor = new FakeProcessMonitor
        {
            Behavior = _ => Task.FromException<string>(new ProcessStartTimeoutException("Timed out."))
        };
        var launcher = new NoRegionLoaderLauncher(
            CreateOptions(files.CreateFile("NoRegion Loader.exe")),
            new FakeNoRegionLoaderProcessStarter(),
            monitor,
            new FakeProcessCleanup(),
            new NullLogger());

        var result = await launcher.LaunchAsync(
            new LaunchRequest("execution-id", files.CreateFile("game.exe"), []),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("PROCESS_START_TIMEOUT", result.Error?.Code);
    }

    private static NoRegionLoaderOptions CreateOptions(string executable) => new()
    {
        NoRegionLoaderExecutable = executable,
        PollingInterval = TimeSpan.FromMilliseconds(10)
    };

    private sealed class FakeNoRegionLoaderProcessStarter : INoRegionLoaderProcessStarter
    {
        public LaunchRequest? Request { get; private set; }

        public INoRegionLoaderProcess Start(NoRegionLoaderOptions options, LaunchRequest request)
        {
            Request = request;
            return new FakeProcess();
        }
    }

    private sealed class FakeProcess : INoRegionLoaderProcess
    {
        public int Id => 64;

        public void Dispose()
        {
        }
    }
}
