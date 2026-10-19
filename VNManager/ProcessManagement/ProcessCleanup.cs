using System.Diagnostics;
using Launcher.Logging;

namespace Launcher.ProcessManagement;

public sealed class ProcessCleanup(
    ProcessManagementOptions options,
    IAppLogger logger,
    IServiceStopper? serviceStopper = null) : IProcessCleanup
{
    private readonly ProcessManagementOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));
    private readonly IAppLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IServiceStopper _serviceStopper = serviceStopper ?? new ElevatedServiceStopper();

    public async Task<ProcessCleanupResult> TerminateForMonitoredProcessAsync(
        string? monitoredExecutableOrProcessName,
        CancellationToken cancellationToken = default)
    {
        var monitoredProcessName = string.IsNullOrWhiteSpace(monitoredExecutableOrProcessName)
            ? null
            : ProcessName.Normalize(monitoredExecutableOrProcessName);
        var processNames = BuildCleanupPlan(
            _options.GlobalCleanupProcesses,
            executable => executable.CleanupProcesses,
            monitoredProcessName,
            normalize: true);
        var serviceNames = BuildCleanupPlan(
            _options.GlobalCleanupServices,
            executable => executable.CleanupServices,
            monitoredProcessName,
            normalize: false);
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

                    _logger.Write(
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

                    _logger.Write(
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
            var result = await _serviceStopper.StopAsync(serviceName, cancellationToken);

            if (result.Stopped)
            {
                _logger.Write(
                    LogLevel.Info,
                    "ProcessCleanup",
                    "Cleanup service stop requested",
                    new Dictionary<string, object?> { ["ServiceName"] = serviceName });
                continue;
            }

            var failure = $"service {serviceName}: {result.Failure ?? "unknown failure"}";
            failures.Add(failure);
            _logger.Write(
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

    private IReadOnlyList<string> BuildCleanupPlan(
        IReadOnlyList<string>? global,
        Func<ExecutableCleanupOptions, IReadOnlyList<string>?> specific,
        string? monitoredProcessName,
        bool normalize)
    {
        var specificNames = monitoredProcessName is null
            ? Enumerable.Empty<string>()
            : ExecutableEntries.For(_options, monitoredProcessName)
                .SelectMany(executable => specific(executable) ?? [])
                .ToArray();

        var names = (global ?? [])
            .Concat(specificNames)
            .SelectMany(name => name.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(name => name.Trim().Trim('"'))
            .Where(name => !string.IsNullOrWhiteSpace(name));

        return (normalize ? names.Select(ProcessName.Normalize) : names)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
