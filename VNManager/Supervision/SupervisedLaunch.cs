using Launcher.Abstractions;
using Launcher.Logging;
using Launcher.ProcessManagement;

namespace Launcher.Supervision;

public static class SupervisedLaunch
{
    public static async Task<LaunchResult> RunAsync(
        SupervisedTool tool,
        LaunchRequest request,
        TimeSpan pollingInterval,
        Func<ILaunchedProcess> startTool,
        IProcessMonitor monitor,
        IProcessCleanup cleanup,
        IAppLogger logger,
        CancellationToken cancellationToken)
    {
        string? monitoredProcessName = null;

        try
        {
            using var toolProcess = startTool();

            logger.Write(
                LogLevel.Info,
                tool.Component,
                $"{tool.DisplayName} command started",
                new Dictionary<string, object?>
                {
                    ["ProcessId"] = toolProcess.Id,
                    ["Executable"] = request.Executable
                });

            monitoredProcessName = await monitor.WaitForStartAndExitAsync(
                request.Executable,
                pollingInterval,
                cancellationToken,
                request.OnStarted);

            return LaunchResult.Succeeded(new Dictionary<string, object?>
            {
                [tool.ProcessIdDataKey] = toolProcess.Id,
                ["MonitoredProcess"] = monitoredProcessName
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Write(LogLevel.Info, tool.Component, "Process supervision was cancelled");

            return LaunchResult.Failed(
                LaunchErrorCodes.Cancelled,
                $"{tool.DisplayName} process supervision was cancelled.");
        }
        catch (ProcessStartTimeoutException exception)
        {
            logger.Write(
                LogLevel.Error,
                tool.Component,
                "Game process start timed out",
                new Dictionary<string, object?> { ["Error"] = exception.Message });

            return LaunchResult.Failed(LaunchErrorCodes.ProcessStartTimeout, exception.Message);
        }
        catch (Exception exception)
        {
            logger.Write(
                LogLevel.Error,
                tool.Component,
                $"{tool.DisplayName} launch failed",
                new Dictionary<string, object?> { ["Error"] = exception.Message });

            return LaunchResult.Failed(tool.FailureCode, exception.Message);
        }
        finally
        {
            await RunCleanupAsync(tool, cleanup, logger, monitoredProcessName);
        }
    }

    private static async Task RunCleanupAsync(
        SupervisedTool tool,
        IProcessCleanup cleanup,
        IAppLogger logger,
        string? monitoredProcessName)
    {
        // Cleanup has its own bounded lifetime and must still run after supervision cancellation.
        using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await cleanup.TerminateForMonitoredProcessAsync(
                monitoredProcessName,
                cleanupCancellation.Token);
        }
        catch (Exception exception)
        {
            logger.Write(
                LogLevel.Warning,
                tool.Component,
                "Process cleanup did not complete",
                new Dictionary<string, object?>
                {
                    ["ExceptionType"] = exception.GetType().FullName,
                    ["Error"] = exception.Message,
                    ["StackTrace"] = exception.ToString()
                });
        }
    }
}
