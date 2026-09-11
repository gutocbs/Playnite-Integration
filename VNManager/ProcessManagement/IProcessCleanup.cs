namespace Launcher.ProcessManagement;

public interface IProcessCleanup
{
    Task<ProcessCleanupResult> TerminateForMonitoredProcessAsync(
        string? monitoredExecutableOrProcessName,
        CancellationToken cancellationToken = default);
}
