namespace Launcher.ProcessManagement;

public interface IServiceStopper
{
    Task<ServiceStopResult> StopAsync(
        string serviceName,
        CancellationToken cancellationToken = default);
}

public sealed record ServiceStopResult(bool Stopped, string? Failure = null);
