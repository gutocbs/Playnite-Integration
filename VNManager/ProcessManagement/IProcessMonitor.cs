namespace Launcher.ProcessManagement;

public interface IProcessMonitor
{
    Task<string> WaitForStartAndExitAsync(
        string gameExecutable,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? onStarted = null);
}
