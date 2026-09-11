using System.Diagnostics;
using Launcher.Abstractions;
using Launcher.LocaleEmulator;
using Launcher.ProcessManagement;

namespace Launcher.Tests;

[Collection("Process integration")]
public sealed class ProcessIntegrationTests
{
    [Fact]
    public async Task ProcessMonitor_ObservesShortLivedExecutableUntilItExits()
    {
        var executable = TestExecutable("ShortLived", "VnTestShort.exe");
        var monitor = new ProcessMonitor(
            new ProcessManagementOptions
            {
                MonitorProcesses = ["VnTestShort.exe"],
                GlobalTimeout = 10
            },
            new NullLogger());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var monitoring = monitor.WaitForStartAndExitAsync(
            executable,
            TimeSpan.FromMilliseconds(25),
            timeout.Token);

        await Task.Delay(100, timeout.Token);
        using var process = Start(executable, "500");

        var monitoredProcessName = await monitoring;

        Assert.Equal("VnTestShort", monitoredProcessName);
        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task ProcessCleanup_TerminatesMultipleConfiguredExecutables()
    {
        var executableA = TestExecutable("PersistentA", "VnTestStayA.exe");
        var executableB = TestExecutable("PersistentB", "VnTestStayB.exe");
        using var processA = Start(executableA);
        using var processB = Start(executableB);

        try
        {
            var cleanup = new ProcessCleanup(
                new ProcessManagementOptions
                {
                    GlobalCleanupProcesses = ["VnTestStayA.exe"],
                    Executables =
                    [
                        new ExecutableCleanupOptions
                        {
                            Name = "VnTestShort.exe",
                            CleanupProcesses = ["VnTestStayB.exe"]
                        }
                    ]
                },
                new NullLogger());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var result = await cleanup.TerminateForMonitoredProcessAsync(
                "vntestshort",
                timeout.Token);

            Assert.Equal(2, result.ProcessesFound);
            Assert.Equal(2, result.ProcessesTerminated);
            Assert.Empty(result.Failures);
            Assert.True(processA.HasExited);
            Assert.True(processB.HasExited);
        }
        finally
        {
            KillIfRunning(processA);
            KillIfRunning(processB);
        }
    }

    [Fact]
    public async Task ProcessMonitor_WhenNoCandidateStarts_ThrowsSpecificTimeout()
    {
        var missingProcessName = $"VnMissing{Guid.NewGuid():N}.exe";
        var monitor = new ProcessMonitor(
            new ProcessManagementOptions
            {
                MonitorProcesses = [missingProcessName],
                GlobalTimeout = 1
            },
            new NullLogger());

        var exception = await Assert.ThrowsAsync<ProcessStartTimeoutException>(() =>
            monitor.WaitForStartAndExitAsync(
                missingProcessName,
                TimeSpan.FromMilliseconds(25),
                TestContext.Current.CancellationToken));

        Assert.Contains("within the allowed time", exception.Message);
        Assert.Contains("1s", exception.Message);
    }

    [Fact]
    public async Task LocaleEmulatorLauncher_StartsStubAndMonitorsLaunchedGame()
    {
        var localeEmulator = TestExecutable("LocaleEmulatorStub", "VnTestLEProc.exe");
        var game = TestExecutable("ShortLived", "VnTestShort.exe");
        var logger = new NullLogger();
        var launcher = new LocaleEmulatorLauncher(
            new LocaleEmulatorOptions
            {
                LocaleEmulatorExecutable = localeEmulator,
                ProfileId = "integration-profile",
                PollingInterval = TimeSpan.FromMilliseconds(25)
            },
            new LocaleEmulatorProcessStarter(),
            new ProcessMonitor(
                new ProcessManagementOptions
                {
                    MonitorProcesses = ["VnTestShort.exe"],
                    GlobalTimeout = 10
                },
                logger),
            new ProcessCleanup(new ProcessManagementOptions(), logger),
            logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await launcher.LaunchAsync(
            new LaunchRequest("integration-execution", game, ["500"]),
            timeout.Token);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("VnTestShort", result.Data?["MonitoredProcess"]);
    }

    private static string TestExecutable(string directory, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", directory, fileName);

    private static Process Start(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException(
            $"Could not start test executable: {executable}");
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
        }
        catch
        {
        }
    }
}

[CollectionDefinition("Process integration", DisableParallelization = true)]
public sealed class ProcessIntegrationCollection;
