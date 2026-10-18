using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Transport;

public sealed record DispatchResult(bool RequestPersisted, bool OrchestratorActivated, Exception? ActivationError);

public sealed class OrchestratorDispatchService(AtomicTransportFileStore store, IOrchestratorLauncher launcher)
{
    public DispatchResult PersistRequest(InstallationRequest request)
    {
        store.WriteRequest(request);
        return new DispatchResult(true, false, null);
    }

    public async Task<DispatchResult> ActivateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await launcher.ActivateAsync(cancellationToken).ConfigureAwait(false);
            return new DispatchResult(true, true, null);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return new DispatchResult(true, false, exception);
        }
    }

    public async Task<DispatchResult> DispatchAsync(InstallationRequest request, CancellationToken cancellationToken = default)
    {
        PersistRequest(request);
        return await ActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<DispatchResult> DispatchInstallerLaunchAsync(
        PreparationOutcome prepared,
        CancellationToken cancellationToken = default)
    {
        if (prepared.Outcome != PreparationOutcomeKind.Prepared)
        {
            throw new InvalidOperationException("Only a prepared outcome can start an installer.");
        }

        var command = new InstallerLaunchCommand(
            prepared.SchemaVersion,
            prepared.RequestId,
            prepared.WorkflowId,
            prepared.PlayniteGameId,
            DateTimeOffset.UtcNow);
        return DispatchCommandAsync(() => store.WriteInstallerLaunchCommand(command), cancellationToken);
    }

    public Task<DispatchResult> DispatchCancellationAsync(
        Guid requestId,
        Guid playniteGameId,
        CancellationToken cancellationToken = default)
    {
        var command = new InstallationCancellationCommand(
            ProtocolV1.SchemaVersion,
            requestId,
            playniteGameId,
            DateTimeOffset.UtcNow);
        return DispatchCommandAsync(() => store.WriteInstallationCancellationCommand(command), cancellationToken);
    }

    private async Task<DispatchResult> DispatchCommandAsync(Action persist, CancellationToken cancellationToken)
    {
        persist();
        try
        {
            await launcher.ActivateAsync(cancellationToken).ConfigureAwait(false);
            return new DispatchResult(true, true, null);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return new DispatchResult(true, false, exception);
        }
    }
}
