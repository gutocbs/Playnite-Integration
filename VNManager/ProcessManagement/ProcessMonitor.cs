using System.Diagnostics;
using System.Globalization;
using Launcher.Logging;

namespace Launcher.ProcessManagement;

public sealed class ProcessMonitor(
    ProcessManagementOptions options,
    IAppLogger logger) : IProcessMonitor
{
    public async Task<string> WaitForStartAndExitAsync(
        string gameExecutable,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? onStarted = null)
    {
        if (pollingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollingInterval),
                "The polling interval must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(gameExecutable);

        if (options.GlobalTimeout <= 0)
        {
            throw new InvalidOperationException(
                "Process management globalTimeout must be greater than zero seconds.");
        }

        var gameProcess = ProcessName.Normalize(gameExecutable);
        var preferredProcesses = options.MonitorProcesses
            .Select(ProcessName.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(processName => !string.Equals(
                processName,
                gameProcess,
                StringComparison.OrdinalIgnoreCase))
            .Select(processName => new MonitoredProcess(
                processName,
                ResolveTimeout(processName)))
            .ToArray();
        var configuredProcesses = new[] { gameProcess }
            .Concat(preferredProcesses.Select(process => process.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(processName => new MonitoredProcess(
                processName,
                ResolveTimeout(processName)))
            .ToArray();

        if (configuredProcesses.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one process must be configured for monitoring.");
        }

        logger.Write(
            LogLevel.Info,
            "ProcessMonitor",
            "Waiting for a configured target process to start",
            new Dictionary<string, object?>
            {
                ["ProcessNames"] = string.Join(",", configuredProcesses.Select(process => process.Name))
            });

        string? processName = null;
        var gameProcessWasStarted = false;
        var startedAt = Stopwatch.GetTimestamp();
        var preferredProcessDetectionWindow = TimeSpan.FromSeconds(
            options.PreferredProcessDetectionWindowSeconds);
        while (processName is null)
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var activeCandidates = configuredProcesses
                .Where(process => elapsed < process.Timeout)
                .ToArray();

            // Engine processes are preferred over a short-lived executable
            // used only to bootstrap the actual game.
            processName = activeCandidates
                .Where(process => preferredProcesses.Any(preferred => string.Equals(
                    preferred.Name,
                    process.Name,
                    StringComparison.OrdinalIgnoreCase)))
                .Select(process => process.Name)
                .FirstOrDefault(IsRunning);
            if (processName is not null)
            {
                break;
            }

            if (IsRunning(gameProcess))
            {
                gameProcessWasStarted = true;

                if (elapsed >= preferredProcessDetectionWindow)
                {
                    processName = gameProcess;
                    break;
                }
            }
            else if (gameProcessWasStarted && elapsed >= preferredProcessDetectionWindow)
            {
                // The bootstrapper already exited and no configured engine
                // appeared during its detection window.
                processName = gameProcess;
                break;
            }

            if (activeCandidates.Length == 0)
            {
                var configuredTimeouts = string.Join(
                    ", ",
                    configuredProcesses.Select(process =>
                        $"{process.Name} ({process.Timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)}s)"));

                throw new ProcessStartTimeoutException(
                    $"None of the configured game or monitor processes started within the allowed time: {configuredTimeouts}.");
            }

            await Task.Delay(pollingInterval, cancellationToken);
        }

        logger.Write(
            LogLevel.Info,
            "ProcessMonitor",
            "Target process started",
            new Dictionary<string, object?> { ["ProcessName"] = processName });

        if (onStarted is not null)
        {
            await onStarted(processName, cancellationToken);
        }

        while (IsRunning(processName))
        {
            await Task.Delay(pollingInterval, cancellationToken);
        }

        logger.Write(
            LogLevel.Info,
            "ProcessMonitor",
            "Target process exited",
            new Dictionary<string, object?> { ["ProcessName"] = processName });

        return processName;
    }

    private TimeSpan ResolveTimeout(string processName)
    {
        var configuredTimeout = options.Executables
            .Where(executable => string.Equals(
                ProcessName.Normalize(executable.Name),
                processName,
                StringComparison.OrdinalIgnoreCase))
            .Select(executable => executable.Timeout)
            .FirstOrDefault(timeout => timeout.HasValue);

        return TimeSpan.FromSeconds(configuredTimeout ?? options.GlobalTimeout);
    }

    private static bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);

        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private sealed record MonitoredProcess(string Name, TimeSpan Timeout);
}
