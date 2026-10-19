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
        var preferredNames = options.MonitorProcesses
            .Select(ProcessName.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(processName => !string.Equals(
                processName,
                gameProcess,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var configuredProcesses = new[] { gameProcess }
            .Concat(preferredNames)
            .Select(processName => new MonitoredProcess(
                processName,
                ResolveTimeout(processName)))
            .ToArray();

        logger.Write(
            LogLevel.Info,
            "ProcessMonitor",
            "Waiting for a configured target process to start",
            new Dictionary<string, object?>
            {
                ["ProcessNames"] = string.Join(",", configuredProcesses.Select(process => process.Name))
            });

        var processName = await WaitForStartAsync(
            gameProcess,
            new HashSet<string>(preferredNames, StringComparer.OrdinalIgnoreCase),
            configuredProcesses,
            pollingInterval,
            cancellationToken);

        logger.Write(
            LogLevel.Info,
            "ProcessMonitor",
            "Target process started",
            new Dictionary<string, object?> { ["ProcessName"] = processName });

        if (onStarted is not null)
        {
            await onStarted(processName, cancellationToken);
        }

        await WaitWhileRunningAsync(processName, pollingInterval, cancellationToken);

        logger.Write(
            LogLevel.Info,
            "ProcessMonitor",
            "Target process exited",
            new Dictionary<string, object?> { ["ProcessName"] = processName });

        return processName;
    }

    private async Task<string> WaitForStartAsync(
        string gameProcess,
        IReadOnlySet<string> preferredNames,
        IReadOnlyList<MonitoredProcess> configuredProcesses,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken)
    {
        var gameProcessWasStarted = false;
        var startedAt = Stopwatch.GetTimestamp();
        var preferredProcessDetectionWindow = TimeSpan.FromSeconds(
            options.PreferredProcessDetectionWindowSeconds);
        while (true)
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var activeCandidates = configuredProcesses
                .Where(process => elapsed < process.Timeout)
                .ToArray();

            // Engine processes are preferred over a short-lived executable
            // used only to bootstrap the actual game.
            var preferredProcess = activeCandidates
                .Select(process => process.Name)
                .Where(preferredNames.Contains)
                .FirstOrDefault(IsRunning);
            if (preferredProcess is not null)
            {
                return preferredProcess;
            }

            if (IsRunning(gameProcess))
            {
                gameProcessWasStarted = true;

                if (elapsed >= preferredProcessDetectionWindow)
                {
                    return gameProcess;
                }
            }
            else if (gameProcessWasStarted && elapsed >= preferredProcessDetectionWindow)
            {
                // The bootstrapper already exited and no configured engine
                // appeared during its detection window.
                return gameProcess;
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
    }

    private static async Task WaitWhileRunningAsync(
        string processName,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken)
    {
        while (IsRunning(processName))
        {
            await Task.Delay(pollingInterval, cancellationToken);
        }
    }

    private TimeSpan ResolveTimeout(string processName)
    {
        var configuredTimeout = ExecutableEntries.For(options, processName)
            .Select(executable => executable.Timeout)
            .FirstOrDefault(timeout => timeout.HasValue);

        return TimeSpan.FromSeconds(configuredTimeout ?? options.GlobalTimeout);
    }

    private static bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        var isRunning = processes.Length > 0;
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return isRunning;
    }

    private sealed record MonitoredProcess(string Name, TimeSpan Timeout);
}
