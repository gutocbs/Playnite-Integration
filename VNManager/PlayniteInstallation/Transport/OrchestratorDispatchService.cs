using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Transport;

public sealed record DispatchResult(bool RequestPersisted, bool OrchestratorActivated, Exception? ActivationError);

public sealed class OrchestratorDispatchService(AtomicTransportFileStore store, IOrchestratorLauncher launcher)
{
    public async Task<DispatchResult> DispatchAsync(InstallationRequest request, CancellationToken cancellationToken = default)
    {
        store.WriteRequest(request);
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
