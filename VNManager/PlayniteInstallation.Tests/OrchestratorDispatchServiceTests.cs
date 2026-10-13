using VnManager.PlayniteInstallation.Contracts;
using VnManager.PlayniteInstallation.Transport;

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

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static InstallationRequest Request() => new(1, Guid.NewGuid(), Guid.NewGuid(), "Game", @"K:\\Game.rar", "fingerprint", DateTimeOffset.UtcNow);

    private sealed class RecordingLauncher(AtomicTransportFileStore store) : IOrchestratorLauncher
    {
        public bool ThrowOnActivate { get; set; }
        public bool RequestWasPresentWhenActivated { get; private set; }
        public Task ActivateAsync(CancellationToken cancellationToken)
        {
            RequestWasPresentWhenActivated = Directory.Exists(store.Paths.InboxDirectory) && Directory.GetFiles(store.Paths.InboxDirectory, "*.json").Length == 1;
            if (ThrowOnActivate) throw new InvalidOperationException("not running");
            return Task.CompletedTask;
        }
    }
}
