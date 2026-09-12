using Launcher.Abstractions;
using Launcher.LocaleEmulator;
using Launcher.Logging;
using Launcher.ProcessManagement;

namespace Launcher.Tests;

internal sealed class NullLogger : IAppLogger
{
    public void Write(
        LogLevel level,
        string component,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
    }
}

internal sealed class FakeProcessStarter : ILocaleEmulatorProcessStarter
{
    public LocaleEmulatorOptions? Options { get; private set; }

    public LaunchRequest? Request { get; private set; }

    public ILaunchedProcess Start(LocaleEmulatorOptions options, LaunchRequest request)
    {
        Options = options;
        Request = request;
        return new FakeLaunchedProcess();
    }

    private sealed class FakeLaunchedProcess : ILaunchedProcess
    {
        public int Id => 42;

        public void Dispose()
        {
        }
    }
}

internal sealed class FakeProcessMonitor : IProcessMonitor
{
    public string? GameExecutable { get; private set; }

    public string MonitoredProcessName { get; init; } = "monitored-process";

    public Func<CancellationToken, Task<string>>? Behavior { get; init; }

    public async Task<string> WaitForStartAndExitAsync(
        string gameExecutable,
        TimeSpan pollingInterval,
        CancellationToken cancellationToken = default,
        Func<string, CancellationToken, Task>? onStarted = null)
    {
        GameExecutable = gameExecutable;
        await (Behavior?.Invoke(cancellationToken) ?? Task.CompletedTask);
        if (onStarted is not null)
        {
            await onStarted(MonitoredProcessName, cancellationToken);
        }

        return MonitoredProcessName;
    }
}

internal sealed class FakeProcessCleanup : IProcessCleanup
{
    public string? MonitoredProcessName { get; private set; }

    public Task<ProcessCleanupResult> TerminateForMonitoredProcessAsync(
        string? monitoredExecutableOrProcessName,
        CancellationToken cancellationToken = default)
    {
        MonitoredProcessName = monitoredExecutableOrProcessName;
        return Task.FromResult(new ProcessCleanupResult(0, 0, []));
    }
}

internal sealed class FakeServiceStopper : IServiceStopper
{
    public List<string> ServiceNames { get; } = [];

    public ServiceStopResult Result { get; init; } = new(true);

    public Task<ServiceStopResult> StopAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        ServiceNames.Add(serviceName);
        return Task.FromResult(Result);
    }
}

internal sealed class FakeLauncher : ILauncher
{
    public Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(LaunchResult.Succeeded());
}
