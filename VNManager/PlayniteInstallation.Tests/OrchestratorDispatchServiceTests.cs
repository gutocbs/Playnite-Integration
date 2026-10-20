using VNInstallation.Protocol.Contracts;
using VnManager.PlayniteInstallation.Transport;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class OrchestratorDispatchServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PlayniteInstallationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DispatchAsync_writes_before_activating_and_returns_when_activation_fails()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var launcher = new RecordingLauncher(store);
        launcher.ThrowOnActivate = true;
        var service = new OrchestratorDispatchService(store, launcher);

        var result = await service.DispatchAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(launcher.RequestWasPresentWhenActivated);
        Assert.True(result.RequestPersisted);
        Assert.False(result.OrchestratorActivated);
        Assert.NotNull(result.ActivationError);
    }

    [Fact]
    public async Task Persisting_a_request_does_not_activate_until_the_progress_callback_starts_extraction()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var launcher = new RecordingLauncher(store);
        var service = new OrchestratorDispatchService(store, launcher);

        var persisted = service.PersistRequest(Request());

        Assert.True(persisted.RequestPersisted);
        Assert.False(persisted.OrchestratorActivated);
        Assert.Equal(0, launcher.ActivationCount);
        await service.ActivateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, launcher.ActivationCount);
        Assert.True(launcher.RequestWasPresentWhenActivated);
    }

    [Fact]
    public void Result_ready_listener_invokes_callback_when_the_named_event_is_signalled()
    {
        var eventName = "Local\\VNOrchestrator.Tests." + Guid.NewGuid().ToString("N");
        using var callbackObserved = new ManualResetEventSlim();
        using var listener = new ResultReadyListener(eventName, callbackObserved.Set);
        using var publisher = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

        publisher.Set();

        Assert.True(callbackObserved.Wait(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Result_ready_synchronization_queues_reconciliation_on_the_ui_dispatcher()
    {
        var eventName = "Local\\VNOrchestrator.Tests." + Guid.NewGuid().ToString("N");
        var dispatcher = new RecordingUiDispatcher();
        var reconciled = false;
        using var synchronization = new ResultReadySynchronization(eventName, dispatcher, () => reconciled = true);
        using var publisher = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

        publisher.Set();

        Assert.True(SpinWait.SpinUntil(() => dispatcher.PendingAction is not null, TimeSpan.FromSeconds(1)));
        Assert.False(reconciled);
        dispatcher.PendingAction!();
        Assert.True(reconciled);
    }

    [Fact]
    public void Terminal_outcome_synchronization_queues_reconciliation_on_the_ui_dispatcher()
    {
        var eventName = "Local\\VNOrchestrator.Tests." + Guid.NewGuid().ToString("N");
        var dispatcher = new RecordingUiDispatcher();
        var reconciled = false;
        using var synchronization = new TerminalOutcomeSynchronization(eventName, dispatcher, () => reconciled = true);
        using var publisher = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

        publisher.Set();

        Assert.True(SpinWait.SpinUntil(() => dispatcher.PendingAction is not null, TimeSpan.FromSeconds(1)));
        Assert.False(reconciled);
        dispatcher.PendingAction!();
        Assert.True(reconciled);
    }

    [Fact]
    public void Orchestrator_wake_signal_notifies_an_open_orchestrator()
    {
        var eventName = "Local\\VNOrchestrator.Tests." + Guid.NewGuid().ToString("N");
        using var listener = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        var wakeSignal = new NamedOrchestratorWakeSignal(eventName);

        wakeSignal.Signal();

        Assert.True(listener.WaitOne(TimeSpan.FromSeconds(1)));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static InstallationRequest Request() => new(1, Guid.NewGuid(), Guid.NewGuid(), "Game", @"K:\\Game.rar", "fingerprint", DateTimeOffset.UtcNow);

    private sealed class RecordingLauncher(AtomicTransportFileStore store) : IOrchestratorLauncher
    {
        public bool ThrowOnActivate { get; set; }
        public bool RequestWasPresentWhenActivated { get; private set; }
        public int ActivationCount { get; private set; }
        public Task ActivateAsync(CancellationToken cancellationToken)
        {
            ActivationCount++;
            RequestWasPresentWhenActivated = Directory.Exists(store.Paths.InboxDirectory) && Directory.GetFiles(store.Paths.InboxDirectory, "*.json").Length == 1;
            if (ThrowOnActivate) throw new InvalidOperationException("not running");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingUiDispatcher : IResultReadyUiDispatcher
    {
        public Action? PendingAction { get; private set; }
        public void BeginInvoke(Action action) => PendingAction = action;
    }
}
