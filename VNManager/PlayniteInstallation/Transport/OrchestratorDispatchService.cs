using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Transport;

public sealed record DispatchResult(bool OrchestratorActivated, Exception? ActivationError);

public sealed class OrchestratorDispatchService(AtomicTransportFileStore store, IOrchestratorLauncher launcher)
{
    public void PersistRequest(InstallationRequest request) => store.WriteRequest(request);

    public async Task<DispatchResult> ActivateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await launcher.ActivateAsync(cancellationToken).ConfigureAwait(false);
            return new DispatchResult(true, null);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return new DispatchResult(false, exception);
        }
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

    public Task<DispatchResult> DispatchPortableConfigurationAsync(
        PreparationOutcome prepared,
        CancellationToken cancellationToken = default)
    {
        if (prepared.Outcome != PreparationOutcomeKind.PreparedWithoutInstallerLaunch)
        {
            throw new InvalidOperationException("Only a portable prepared outcome can publish portable configuration.");
        }

        var command = new PortableConfigurationCommand(
            prepared.SchemaVersion, prepared.RequestId, prepared.WorkflowId, prepared.PlayniteGameId, DateTimeOffset.UtcNow);
        return DispatchCommandAsync(() => store.WritePortableConfigurationCommand(command), cancellationToken);
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
        return await ActivateAsync(cancellationToken).ConfigureAwait(false);
    }
}
