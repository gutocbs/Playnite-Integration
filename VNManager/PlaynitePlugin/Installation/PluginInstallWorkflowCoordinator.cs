using System;
using System.Collections.Generic;
using System.IO;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using VNInstallation.Protocol.Transport;
using VNInstallation.Protocol.Contracts;
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
    private TerminalOutcomeSynchronization terminalOutcomeSynchronization;
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

    public void Dispatch(Game game, Action<string> completeInstall, Action cancelInstall)
    {
        var request = PlayniteInstallAdapter.CreateRequest(game);
        installCompletions.Register(request.RequestId, game.Id, completeInstall, cancelInstall);
        try
        {
            dispatch.PersistRequest(request);
            uiDispatcher.BeginInvoke(() => MonitorPreparation(request));
        }
        catch (Exception)
        {
            CancelLiveInstall(request, publishCancellation: false);
            api.Notifications.Add("VNOrchestrator", "A solicitação não pôde ser preparada para extração.", NotificationType.Error);
        }
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
        if (terminalOutcomeSynchronization == null)
        {
            terminalOutcomeSynchronization = new TerminalOutcomeSynchronization(TransportEventNames.InstallationTerminalOutcomeNameForCurrentUser(), uiDispatcher, ApplyTerminalOutcomes);
        }
        ScheduleResultSynchronization("startup");
        ScheduleInteractionProcessing("startup");
        ApplyTerminalOutcomes();
    }

    public void ReconcilePendingResults() => resultApplier.ApplyPending();

    private void MonitorPreparation(VNInstallation.Protocol.Contracts.InstallationRequest request)
    {
        PreparationOutcome outcome = null;
        var cancelled = false;
        DispatchResult activation = null;
        var progress = api.Dialogs.ActivateGlobalProgress(args =>
        {
            args.Text = "Extraindo arquivos do jogo...";
            args.IsIndeterminate = true;
            activation = dispatch.ActivateAsync().GetAwaiter().GetResult();
            if (!activation.OrchestratorActivated) return;
            while (!args.CancelToken.IsCancellationRequested)
            {
                outcome = store.ConsumePreparationOutcome(request.RequestId, request.PlayniteGameId);
                if (outcome != null) return;
                System.Threading.Thread.Sleep(100);
            }
            cancelled = true;
        }, new GlobalProgressOptions("Extraindo arquivos do jogo...", true) { IsIndeterminate = true });

        if (activation is null || !activation.OrchestratorActivated)
        {
            CancelLiveInstall(request, publishCancellation: true);
            api.Notifications.Add("VNOrchestrator", "A solicitação foi salva, mas o VN Install Orchestrator não pôde ser iniciado.", NotificationType.Error);
            return;
        }
        if (cancelled || progress.Canceled)
        {
            CancelLiveInstall(request, publishCancellation: true);
            return;
        }
        if (outcome is null || outcome.Outcome == PreparationOutcomeKind.Failed)
        {
            CancelLiveInstall(request, publishCancellation: false);
            api.Notifications.Add("VNOrchestrator", "Não foi possível preparar os arquivos do jogo. Consulte o log do Orchestrator.", NotificationType.Error);
            return;
        }
        if (outcome.Outcome == PreparationOutcomeKind.PreparedWithoutInstallerLaunch)
        {
            _ = dispatch.DispatchPortableConfigurationAsync(outcome).ContinueWith(completed =>
            {
                if (completed.IsFaulted || !completed.Result.OrchestratorActivated)
                {
                    CancelLiveInstall(request, publishCancellation: true);
                    api.Notifications.Add("VNOrchestrator", "A configuração portátil não pôde ser iniciada.", NotificationType.Error);
                }
            });
            return;
        }
        _ = dispatch.DispatchInstallerLaunchAsync(outcome).ContinueWith(completed =>
        {
            if (completed.IsFaulted || !completed.Result.OrchestratorActivated)
            {
                CancelLiveInstall(request, publishCancellation: true);
                api.Notifications.Add("VNOrchestrator", "O instalador não pôde ser iniciado.", NotificationType.Error);
            }
        });
    }

    private void CancelLiveInstall(VNInstallation.Protocol.Contracts.InstallationRequest request, bool publishCancellation)
    {
        installCompletions.TryCancel(request.RequestId, request.PlayniteGameId);
        if (publishCancellation) _ = dispatch.DispatchCancellationAsync(request.RequestId, request.PlayniteGameId);
    }

    private void ApplyTerminalOutcomes()
    {
        foreach (var outcome in store.ReadInstallationTerminalOutcomes())
        {
            if (store.ConsumeInstallationTerminalOutcome(outcome.RequestId, outcome.WorkflowId, outcome.PlayniteGameId) is null) continue;
            if (installCompletions.TryCancel(outcome.RequestId, outcome.PlayniteGameId))
            {
                api.Notifications.Add("VNOrchestrator", "O instalador externo não foi concluído. Consulte o log do Orchestrator.", NotificationType.Error);
            }
        }
    }

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
        if (terminalOutcomeSynchronization != null)
        {
            terminalOutcomeSynchronization.Dispose();
            terminalOutcomeSynchronization = null;
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
