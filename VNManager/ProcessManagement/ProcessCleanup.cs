using System.Diagnostics;
using Launcher.Logging;

namespace Launcher.ProcessManagement;

public sealed class ProcessCleanup : IProcessCleanup
{
    private readonly ProcessManagementOptions options;
    private readonly IAppLogger logger;
    private readonly IServiceStopper serviceStopper;

    // Keep this overload so existing launcher binaries can use a newer
    // ProcessManagement.dll during an incremental extension deployment.
    public ProcessCleanup(ProcessManagementOptions options, IAppLogger logger)
        : this(options, logger, new ElevatedServiceStopper())
    {
    }

    public ProcessCleanup(
        ProcessManagementOptions options,
        IAppLogger logger,
        IServiceStopper? serviceStopper)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.serviceStopper = serviceStopper ?? new ElevatedServiceStopper();
    }

    public async Task<ProcessCleanupResult> TerminateForMonitoredProcessAsync(
        string? monitoredExecutableOrProcessName,
        CancellationToken cancellationToken = default)
    {
        var monitoredProcessName = string.IsNullOrWhiteSpace(monitoredExecutableOrProcessName)
            ? null
            : ProcessName.Normalize(monitoredExecutableOrProcessName);
        var processNames = BuildProcessCleanupPlan(monitoredProcessName);
        var serviceNames = BuildServiceCleanupPlan(monitoredProcessName);
        var found = 0;
        var terminated = 0;
        var failures = new List<string>();

        foreach (var processName in processNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var processes = Process.GetProcessesByName(processName);
            found += processes.Length;

            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    await process.WaitForExitAsync(cancellationToken);
                    terminated++;

                    logger.Write(
                        LogLevel.Info,
                        "ProcessCleanup",
                        "Cleanup process terminated",
                        new Dictionary<string, object?>
                        {
                            ["ProcessName"] = processName,
                            ["ProcessId"] = process.Id
                        });
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var failure = $"{processName} (PID {process.Id}): {exception.Message}";
                    failures.Add(failure);

                    logger.Write(
                        LogLevel.Warning,
                        "ProcessCleanup",
                        "Cleanup process could not be terminated",
                        new Dictionary<string, object?>
                        {
                            ["ProcessName"] = processName,
                            ["ProcessId"] = process.Id,
                            ["Error"] = exception.Message
                        });
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        foreach (var serviceName in serviceNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await serviceStopper.StopAsync(serviceName, cancellationToken);

            if (result.Stopped)
            {
                logger.Write(
                    LogLevel.Info,
                    "ProcessCleanup",
                    "Cleanup service stop requested",
                    new Dictionary<string, object?> { ["ServiceName"] = serviceName });
                continue;
            }

            var failure = $"service {serviceName}: {result.Failure ?? "unknown failure"}";
            failures.Add(failure);
            logger.Write(
                LogLevel.Warning,
                "ProcessCleanup",
                "Cleanup service could not be stopped",
                new Dictionary<string, object?>
                {
                    ["ServiceName"] = serviceName,
                    ["Error"] = result.Failure
                });
        }

        return new ProcessCleanupResult(found, terminated, failures);
    }

    private IReadOnlyList<string> BuildProcessCleanupPlan(string? monitoredProcessName)
    {
        var specificProcesses = monitoredProcessName is null
            ? Enumerable.Empty<string>()
            : options.Executables
            .Where(executable => string.Equals(
                ProcessName.Normalize(executable.Name),
                monitoredProcessName,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(executable => executable.CleanupProcesses ?? [])
            .ToArray();

        return (options.GlobalCleanupProcesses ?? [])
            .Concat(specificProcesses)
            .SelectMany(name => name.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(name => name.Trim().Trim('"'))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(ProcessName.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildServiceCleanupPlan(string? monitoredProcessName)
    {
        var specificServices = monitoredProcessName is null
            ? Enumerable.Empty<string>()
            : options.Executables
            .Where(executable => string.Equals(
                ProcessName.Normalize(executable.Name),
                monitoredProcessName,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(executable => executable.CleanupServices ?? [])
            .ToArray();

        return (options.GlobalCleanupServices ?? [])
            .Concat(specificServices)
            .SelectMany(name => name.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(name => name.Trim().Trim('"'))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
