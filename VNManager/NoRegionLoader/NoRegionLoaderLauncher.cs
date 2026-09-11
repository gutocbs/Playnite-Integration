using Launcher.Abstractions;
using Launcher.Logging;
using Launcher.ProcessManagement;

namespace Launcher.NoRegionLoader;

public sealed class NoRegionLoaderLauncher(
    NoRegionLoaderOptions options,
    INoRegionLoaderProcessStarter processStarter,
    IProcessMonitor processMonitor,
    IProcessCleanup processCleanup,
    IAppLogger logger) : ILauncher
{
    public async Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        string? monitoredProcessName = null;

        try
        {
            using var noRegionLoaderProcess = processStarter.Start(options, request);

            logger.Write(LogLevel.Info, "NoRegionLoader", "NoRegionLoader command started",
                new Dictionary<string, object?>
                {
                    ["ProcessId"] = noRegionLoaderProcess.Id,
                    ["Executable"] = request.Executable
                });

            monitoredProcessName = await processMonitor.WaitForStartAndExitAsync(
                request.Executable,
                options.PollingInterval,
                cancellationToken,
                request.OnStarted);

            return LaunchResult.Succeeded(new Dictionary<string, object?>
            {
                ["NoRegionLoaderProcessId"] = noRegionLoaderProcess.Id,
                ["MonitoredProcess"] = monitoredProcessName
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Write(LogLevel.Info, "NoRegionLoader", "Process supervision was cancelled");
            return LaunchResult.Failed(
                "LAUNCH_CANCELLED",
                "NoRegionLoader process supervision was cancelled.");
        }
        catch (ProcessStartTimeoutException exception)
        {
            logger.Write(LogLevel.Error, "NoRegionLoader", "Game process start timed out",
                new Dictionary<string, object?> { ["Error"] = exception.Message });
            return LaunchResult.Failed("PROCESS_START_TIMEOUT", exception.Message);
        }
        catch (Exception exception)
        {
            logger.Write(LogLevel.Error, "NoRegionLoader", "NoRegionLoader launch failed",
                new Dictionary<string, object?> { ["Error"] = exception.Message });
            return LaunchResult.Failed("NO_REGION_LOADER_LAUNCH_FAILED", exception.Message);
        }
        finally
        {
            await RunCleanupAsync(monitoredProcessName);
        }
    }

    private LaunchResult? Validate(LaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Executable) || !Path.IsPathFullyQualified(request.Executable))
        {
            return LaunchResult.Failed("INVALID_GAME_EXECUTABLE", "The game executable must be an absolute path.");
        }

        if (!File.Exists(request.Executable))
        {
            return LaunchResult.Failed(
                "GAME_EXECUTABLE_NOT_FOUND",
                $"The game executable was not found: {request.Executable}");
        }

        if (string.IsNullOrWhiteSpace(options.NoRegionLoaderExecutable) ||
            !Path.IsPathFullyQualified(options.NoRegionLoaderExecutable) ||
            !File.Exists(options.NoRegionLoaderExecutable))
        {
            return LaunchResult.Failed(
                "NO_REGION_LOADER_NOT_FOUND",
                $"The NoRegionLoader executable was not found: {options.NoRegionLoaderExecutable}");
        }

        if (options.PollingInterval <= TimeSpan.Zero)
        {
            return LaunchResult.Failed(
                "INVALID_POLLING_INTERVAL",
                "The process polling interval must be greater than zero.");
        }

        return null;
    }

    private async Task RunCleanupAsync(string? monitoredProcessName)
    {
        using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await processCleanup.TerminateForMonitoredProcessAsync(
                monitoredProcessName,
                cleanupCancellation.Token);
        }
        catch (Exception exception)
        {
            logger.Write(LogLevel.Warning, "NoRegionLoader", "Process cleanup did not complete",
                new Dictionary<string, object?> { ["Error"] = exception.Message });
        }
    }
}
