using System;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using VNInstallation.Protocol.Transport;
using VnManager.PlayniteInstallation.Domain;
using VnManager.PlayniteInstallation.Transport;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class PluginInstallWorkflowCoordinator : IDisposable
{
    private readonly IPlayniteAPI api;
    private readonly AtomicTransportFileStore store;
    private readonly OrchestratorDispatchService dispatch;
    private readonly PlayniteResultApplier resultApplier;
    private readonly InteractionRequestProcessor interactionProcessor;
    private readonly ILogger logger = LogManager.GetLogger(nameof(PluginInstallWorkflowCoordinator));
    private readonly IResultReadyUiDispatcher uiDispatcher;
    private readonly PendingInstallCompletionRegistry installCompletions = new();
    private readonly System.Threading.CancellationTokenSource retryCancellation = new();
    private ResultReadySynchronization resultReadySynchronization;
    private InteractionReadySynchronization interactionReadySynchronization;
    private int retryQueued;
    private bool disposed;

    public PluginInstallWorkflowCoordinator(IPlayniteAPI api, string transportRoot, string orchestratorPath)
    {
        this.api = api;
        store = new AtomicTransportFileStore(new TransportPaths(transportRoot));
        dispatch = new OrchestratorDispatchService(store, new OrchestratorProcessActivator(orchestratorPath));
        resultApplier = new PlayniteResultApplier(api, store, logger, new NamedOrchestratorWakeSignal(OrchestratorWakeEvent.NameForCurrentUser()), () => ScheduleResultSynchronization("lock-contention", TimeSpan.FromMilliseconds(250)), installCompletions.TryComplete);
        uiDispatcher = new PlayniteUiDispatcher(api);
        interactionProcessor = new InteractionRequestProcessor(
            store,
            new PlayniteInteractionDialog(api),
            new PlayniteInteractionValidator(api, store),
            new NamedOrchestratorWakeSignal(OrchestratorWakeEvent.NameForCurrentUser()));
    }

    public bool IsEligible(Game game) => InstallEligibility.Evaluate(PlayniteInstallAdapter.ToSnapshot(game), new FileProbe(), new NoActiveWorkflowLookup()).IsEligible;

    public void Dispatch(Game game, Action<string> completeInstall)
    {
        var request = PlayniteInstallAdapter.CreateRequest(game);
        installCompletions.Register(request.RequestId, game.Id, completeInstall);
        var task = dispatch.DispatchAsync(request);
        task.ContinueWith(completed =>
        {
            if (completed.IsFaulted || !completed.Result.OrchestratorActivated)
            {
                installCompletions.Remove(request.RequestId);
                api.Notifications.Add("VNOrchestrator", "A solicitação foi salva, mas o VN Install Orchestrator não pôde ser iniciado.", NotificationType.Error);
            }
        });
    }

    public void StartResultSynchronization()
    {
        if (resultReadySynchronization == null)
        {
            resultReadySynchronization = new ResultReadySynchronization(TransportEventNames.ResultReadyNameForCurrentUser(), uiDispatcher, SynchronizeFromSignal);
        }
        if (interactionReadySynchronization == null)
        {
            interactionReadySynchronization = new InteractionReadySynchronization(TransportEventNames.InteractionReadyNameForCurrentUser(), uiDispatcher, ProcessInteractionsFromSignal);
        }
        ScheduleResultSynchronization("startup");
        ScheduleInteractionProcessing("startup");
    }

    public void ReconcilePendingResults() => resultApplier.ApplyPending();

    private void SynchronizeFromSignal()
    {
        try
        {
            logger.Info("VN Orchestrator result synchronization started from signal.");
            ReconcilePendingResults();
        }
        catch (Exception exception)
        {
            logger.Error($"VN Orchestrator result synchronization failed after signal. Exception='{exception}'.");
        }
    }

    private void ProcessInteractionsFromSignal()
    {
        try
        {
            logger.Info("VN Orchestrator interaction processing started from signal.");
            var summary = interactionProcessor.ProcessPending();
            LogRejectedInteractions(summary);
            logger.Info($"VN Orchestrator interaction processing completed from signal. Processed='{summary.Processed}'.");
        }
        catch (Exception exception)
        {
            logger.Error($"VN Orchestrator interaction processing failed after signal. Exception='{exception}'.");
        }
    }

    private void ScheduleInteractionProcessing(string trigger)
    {
        if (disposed) return;
        try { uiDispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                logger.Info($"VN Orchestrator interaction processing started from {trigger}.");
                var summary = interactionProcessor.ProcessPending();
                LogRejectedInteractions(summary);
                logger.Info($"VN Orchestrator interaction processing completed from {trigger}. Processed='{summary.Processed}'.");
            }
            catch (Exception exception)
            {
                logger.Error($"VN Orchestrator interaction processing failed after {trigger}. Exception='{exception}'.");
            }
        })); }
        catch (Exception exception) when (disposed)
        {
            logger.Warn($"VN Orchestrator interaction processing was skipped during shutdown. Exception='{exception}'.");
        }
    }

    private void ScheduleResultSynchronization(string trigger, TimeSpan? delay = null)
    {
        if (disposed) return;
        if (delay is { } retryDelay)
        {
            if (System.Threading.Interlocked.Exchange(ref retryQueued, 1) != 0) return;
            System.Threading.Tasks.Task.Delay(retryDelay, retryCancellation.Token).ContinueWith(completed =>
            {
                if (completed.IsCanceled || disposed) return;
                ScheduleResultSynchronization(trigger);
            }, System.Threading.CancellationToken.None, System.Threading.Tasks.TaskContinuationOptions.OnlyOnRanToCompletion, System.Threading.Tasks.TaskScheduler.Default);
            return;
        }
        System.Threading.Interlocked.Exchange(ref retryQueued, 0);
        try { uiDispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                logger.Info($"VN Orchestrator result synchronization started from {trigger}.");
                ReconcilePendingResults();
            }
            catch (Exception exception)
            {
                logger.Error($"VN Orchestrator result synchronization failed after {trigger}. Exception='{exception}'.");
            }
        })); }
        catch (Exception exception) when (disposed)
        {
            logger.Warn($"VN Orchestrator result synchronization was skipped during shutdown. Exception='{exception}'.");
        }
    }

    private void LogRejectedInteractions(InteractionProcessingSummary summary)
    {
        foreach (var rejection in summary.Rejected)
        {
            logger.Warn($"VN Orchestrator interaction was rejected. InteractionId='{rejection.InteractionId}', RequestId='{rejection.RequestId}', WorkflowId='{rejection.WorkflowId}', GameId='{rejection.PlayniteGameId}', ErrorCode='{rejection.ErrorCode}', Reason='{rejection.Reason}'.");
        }
    }

    public void Dispose()
    {
        disposed = true;
        retryCancellation.Cancel();
        if (resultReadySynchronization != null)
        {
            resultReadySynchronization.Dispose();
            resultReadySynchronization = null;
        }
        if (interactionReadySynchronization != null)
        {
            interactionReadySynchronization.Dispose();
            interactionReadySynchronization = null;
        }
        retryCancellation.Dispose();
    }

    private sealed class FileProbe : IFileProbe { public bool Exists(string path) => File.Exists(path); }
    private sealed class NoActiveWorkflowLookup : IActiveWorkflowLookup { public bool HasActiveWorkflow(Guid gameId) => false; }
    private sealed class PlayniteUiDispatcher(IPlayniteAPI api) : IResultReadyUiDispatcher
    {
        public void BeginInvoke(Action action) => api.MainView.UIDispatcher.BeginInvoke(action);
    }
}
