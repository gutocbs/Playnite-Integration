using System.Collections.Generic;
using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Transport;

public interface IInteractionDialog
{
    InteractionDialogResult Show(PlayniteInteractionRequest request);
}

public interface IInteractionRequestValidator
{
    bool CanHandle(PlayniteInteractionRequest request);
}

public sealed class InteractionRequestProcessor(
    AtomicTransportFileStore store,
    IInteractionDialog dialog,
    IInteractionRequestValidator validator,
    IOrchestratorWakeSignal orchestratorWakeSignal)
{
    private readonly object processingLock = new();

    public int ProcessPending()
    {
        lock (processingLock)
        {
            var answered = new HashSet<Guid>(store.ReadInteractionResponses().Select(response => response.InteractionId));
            var processed = 0;
            foreach (var request in store.ReadInteractionRequests())
            {
                if (answered.Contains(request.InteractionId) || !validator.CanHandle(request)) continue;

                var result = dialog.Show(request);
                var response = new PlayniteInteractionResponse(
                    ProtocolV1.SchemaVersion,
                    request.InteractionId,
                    request.RequestId,
                    request.WorkflowId,
                    request.PlayniteGameId,
                    request.Kind,
                    result.Outcome,
                    result.SelectedPath,
                    DateTimeOffset.UtcNow);
                store.WriteInteractionResponse(response);
                answered.Add(request.InteractionId);
                processed++;
                orchestratorWakeSignal.Signal();
            }

            return processed;
        }
    }
}
