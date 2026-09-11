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

        var configuredProcesses = new[] { gameExecutable }
            .Concat(options.MonitorProcesses)
            .Select(ProcessName.Normalize)
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
        var startedAt = Stopwatch.GetTimestamp();
        while (processName is null)
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var activeCandidates = configuredProcesses
                .Where(process => elapsed < process.Timeout)
                .ToArray();

            processName = activeCandidates
                .Select(process => process.Name)
                .FirstOrDefault(IsRunning);
            if (processName is not null)
            {
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
