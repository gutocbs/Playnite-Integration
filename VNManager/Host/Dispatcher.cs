using Launcher.Abstractions;

namespace Launcher.Host;

public sealed class Dispatcher(LauncherRegistry registry)
{
    public Task<LaunchResult> DispatchAsync(
        DispatcherRequest request,
        Func<string, CancellationToken, Task>? onStarted = null,
        CancellationToken cancellationToken = default)
    {
        DispatcherRequestValidator.Validate(request);
        var launcher = registry.Resolve(request.Launcher, request.ExecutionId);
        var launchRequest = new LaunchRequest(
            request.ExecutionId,
            request.Executable,
            request.Arguments,
            onStarted);

        return launcher.LaunchAsync(launchRequest, cancellationToken);
    }
}
