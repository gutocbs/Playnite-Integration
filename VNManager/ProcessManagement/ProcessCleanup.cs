using System.Diagnostics;
using Launcher.Logging;

namespace Launcher.ProcessManagement;

public sealed class ProcessCleanup(
    ProcessManagementOptions options,
    IAppLogger logger) : IProcessCleanup
{
    public async Task<ProcessCleanupResult> TerminateForMonitoredProcessAsync(
        string? monitoredExecutableOrProcessName,
        CancellationToken cancellationToken = default)
    {
        var monitoredProcessName = string.IsNullOrWhiteSpace(monitoredExecutableOrProcessName)
            ? null
            : ProcessName.Normalize(monitoredExecutableOrProcessName);
        var processNames = BuildCleanupPlan(monitoredProcessName);

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

        return new ProcessCleanupResult(found, terminated, failures);
    }

    private IReadOnlyList<string> BuildCleanupPlan(string? monitoredProcessName)
    {
        var specificProcesses = monitoredProcessName is null
            ? Enumerable.Empty<string>()
            : options.Executables
            .Where(executable => string.Equals(
                ProcessName.Normalize(executable.Name),
                monitoredProcessName,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(executable => executable.CleanupProcesses)
            .ToArray();

        return options.GlobalCleanupProcesses
            .Concat(specificProcesses)
            .SelectMany(name => name.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(name => name.Trim().Trim('"'))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(ProcessName.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
