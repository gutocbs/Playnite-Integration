using System;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using VnManager.PlayniteInstallation.Domain;
using VnManager.PlayniteInstallation.Transport;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class PluginInstallWorkflowCoordinator
{
    private readonly IPlayniteAPI api;
    private readonly AtomicTransportFileStore store;
    private readonly OrchestratorDispatchService dispatch;
    private readonly PlayniteResultApplier resultApplier;

    public PluginInstallWorkflowCoordinator(IPlayniteAPI api, string transportRoot, string orchestratorPath)
    {
        this.api = api;
        store = new AtomicTransportFileStore(new TransportPaths(transportRoot));
        dispatch = new OrchestratorDispatchService(store, new OrchestratorProcessActivator(orchestratorPath));
        resultApplier = new PlayniteResultApplier(api, store);
    }

    public bool IsEligible(Game game) => InstallEligibility.Evaluate(PlayniteInstallAdapter.ToSnapshot(game), new FileProbe(), new NoActiveWorkflowLookup()).IsEligible;

    public void Dispatch(Game game)
    {
        var task = dispatch.DispatchAsync(PlayniteInstallAdapter.CreateRequest(game));
        task.ContinueWith(completed =>
        {
            if (completed.IsFaulted || !completed.Result.OrchestratorActivated)
            {
                api.Notifications.Add("VNOrchestrator", "A solicitação foi salva, mas o VN Install Orchestrator não pôde ser iniciado.", NotificationType.Error);
            }
        });
    }

    public void ReconcilePendingResults() => resultApplier.ApplyPending();

    private sealed class FileProbe : IFileProbe { public bool Exists(string path) => File.Exists(path); }
    private sealed class NoActiveWorkflowLookup : IActiveWorkflowLookup { public bool HasActiveWorkflow(Guid gameId) => false; }
}
