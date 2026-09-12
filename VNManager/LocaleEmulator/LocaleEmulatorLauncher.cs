using Launcher.Abstractions;
using Launcher.Logging;
using Launcher.ProcessManagement;

namespace Launcher.LocaleEmulator;

public sealed class LocaleEmulatorLauncher(
    LocaleEmulatorOptions options,
    ILocaleEmulatorProcessStarter processStarter,
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
            using var localeEmulatorProcess = processStarter.Start(options, request);

            logger.Write(
                LogLevel.Info,
                "LocaleEmulator",
                "Locale Emulator command started",
                new Dictionary<string, object?>
                {
                    ["ProcessId"] = localeEmulatorProcess.Id,
                    ["Executable"] = request.Executable
                });

            monitoredProcessName = await processMonitor.WaitForStartAndExitAsync(
                request.Executable,
                options.PollingInterval,
                cancellationToken,
                request.OnStarted);

            return LaunchResult.Succeeded(new Dictionary<string, object?>
            {
                ["LocaleEmulatorProcessId"] = localeEmulatorProcess.Id,
                ["MonitoredProcess"] = monitoredProcessName
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Write(
                LogLevel.Info,
                "LocaleEmulator",
                "Process supervision was cancelled");

            return LaunchResult.Failed(
                "LAUNCH_CANCELLED",
                "Locale Emulator process supervision was cancelled.");
        }
        catch (ProcessStartTimeoutException exception)
        {
            logger.Write(
                LogLevel.Error,
                "LocaleEmulator",
                "Game process start timed out",
                new Dictionary<string, object?> { ["Error"] = exception.Message });

            return LaunchResult.Failed("PROCESS_START_TIMEOUT", exception.Message);
        }
        catch (Exception exception)
        {
            logger.Write(
                LogLevel.Error,
                "LocaleEmulator",
                "Locale Emulator launch failed",
                new Dictionary<string, object?> { ["Error"] = exception.Message });

            return LaunchResult.Failed("LOCALE_EMULATOR_LAUNCH_FAILED", exception.Message);
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
            return LaunchResult.Failed(
                "INVALID_GAME_EXECUTABLE",
                "The game executable must be an absolute path.");
        }

        if (!File.Exists(request.Executable))
        {
            return LaunchResult.Failed(
                "GAME_EXECUTABLE_NOT_FOUND",
                $"The game executable was not found: {request.Executable}");
        }

        if (string.IsNullOrWhiteSpace(options.LocaleEmulatorExecutable) ||
            !Path.IsPathFullyQualified(options.LocaleEmulatorExecutable) ||
            !File.Exists(options.LocaleEmulatorExecutable))
        {
            return LaunchResult.Failed(
                "LOCALE_EMULATOR_NOT_FOUND",
                $"The Locale Emulator executable was not found: {options.LocaleEmulatorExecutable}");
        }

        if (string.IsNullOrWhiteSpace(options.ProfileId))
        {
            return LaunchResult.Failed(
                "INVALID_LOCALE_EMULATOR_PROFILE",
                "The Locale Emulator profile identifier is required.");
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
        // Cleanup has its own bounded lifetime and must still run after supervision cancellation.
        using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await processCleanup.TerminateForMonitoredProcessAsync(
                monitoredProcessName,
                cleanupCancellation.Token);
        }
        catch (Exception exception)
        {
            logger.Write(
                LogLevel.Warning,
                "LocaleEmulator",
                "Process cleanup did not complete",
                CleanupErrorData(exception));
        }
    }

    private static IReadOnlyDictionary<string, object?> CleanupErrorData(Exception exception) =>
        new Dictionary<string, object?>
        {
            ["ExceptionType"] = exception.GetType().FullName,
            ["Error"] = exception.Message,
            ["StackTrace"] = exception.ToString()
        };
}
