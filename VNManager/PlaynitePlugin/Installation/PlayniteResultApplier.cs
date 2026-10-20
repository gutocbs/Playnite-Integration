using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;
using VnManager.PlayniteInstallation.Domain;
using VnManager.PlayniteInstallation.Transport;
using VnManager.PlaynitePlugin.Launching;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class PlayniteResultApplier(IPlayniteAPI api, AtomicTransportFileStore store, ILogger logger, IOrchestratorWakeSignal orchestratorWakeSignal, Action scheduleRetry, Func<Guid, Guid, string, bool> completeInstall)
{
    public void ApplyPending()
    {
        foreach (var result in store.ReadResults())
        {
            try
            {
                using var gameOperation = GameOperationLock.TryAcquire(result.PlayniteGameId);
                if (gameOperation is null)
                {
                    logger.Info($"VN Orchestrator result deferred because the game operation lock is busy. RequestId='{result.RequestId}', GameId='{result.PlayniteGameId}'.");
                    scheduleRetry();
                    continue;
                }
                if (store.IsSuperseded(result.RequestId))
                {
                    logger.Info($"VN Orchestrator result ignored because it was superseded. RequestId='{result.RequestId}', GameId='{result.PlayniteGameId}'.");
                    continue;
                }
                var acknowledgementStatus = ResultAcknowledgementPolicy.Evaluate(
                    result,
                    store.ReadAcknowledgement(result.RequestId));
                if (acknowledgementStatus == ResultAcknowledgementStatus.AlreadyApplied)
                {
                    logger.Info($"VN Orchestrator result ignored because it was already acknowledged. RequestId='{result.RequestId}', WorkflowId='{result.WorkflowId}', GameId='{result.PlayniteGameId}'.");
                    continue;
                }
                if (acknowledgementStatus == ResultAcknowledgementStatus.Conflict)
                {
                    throw new InvalidOperationException(
                        $"The existing acknowledgement does not match the result correlation. WorkflowId='{result.WorkflowId}', GameId='{result.PlayniteGameId}'.");
                }
                logger.Info($"VN Orchestrator result detected. RequestId='{result.RequestId}', WorkflowId='{result.WorkflowId}', GameId='{result.PlayniteGameId}'.");
                var game = api.Database.Games.Get(result.PlayniteGameId);
                var decision = ResultApplicationPolicy.Evaluate(game is null ? null : Snapshot(game, result), result);
                if (!decision.IsApplicable)
                {
                    logger.Warn($"VN Orchestrator result rejected. RequestId='{result.RequestId}', Conflict='{decision.Conflict}'.");
                    api.Notifications.Add("VNOrchestrator", "A instalação concluída não pôde ser sincronizada: " + decision.Conflict, NotificationType.Error);
                    continue;
                }

                var mutation = decision.Mutation!;
                var controllerCompleted = ResultCompletionSequence.Apply(
                    () => completeInstall(result.RequestId, result.PlayniteGameId, result.InstallationDirectory),
                    () =>
                    {
                        game.IsInstalled = mutation.IsInstalled;
                        game.InstallDirectory = mutation.InstallDirectory;
                        game.GameActions ??= new ObservableCollection<GameAction>();
                        var managedMutation = mutation.GameActions.Single(action =>
                            action.Name == ResultApplicationPolicy.ManagedActionName);
                        var managed = game.GameActions.SingleOrDefault(action => action.Name == ResultApplicationPolicy.ManagedActionName);
                        if (managed is null)
                        {
                            managed = new GameAction { Name = ResultApplicationPolicy.ManagedActionName };
                            game.GameActions.Add(managed);
                        }
                        managed.Type = GameActionType.File;
                        managed.Path = result.ExecutablePath;
                        managed.WorkingDir = result.WorkingDirectory;
                        managed.IsPlayAction = managedMutation.IsPlayAction;
                        api.Database.Games.Update(game);
                    },
                    () => store.WriteAcknowledgement(new PlayniteAcknowledgement(
                        ProtocolV1.SchemaVersion,
                        result.RequestId,
                        result.WorkflowId,
                        result.PlayniteGameId,
                        System.DateTimeOffset.UtcNow)));
                if (!controllerCompleted)
                {
                    logger.Info($"VN Orchestrator result had no live install controller; database synchronization continued. RequestId='{result.RequestId}', GameId='{result.PlayniteGameId}'.");
                }
                try { orchestratorWakeSignal.Signal(); }
                catch (Exception exception) { logger.Warn($"VN Orchestrator acknowledgement wake signal failed. RequestId='{result.RequestId}', Exception='{exception}'."); }
                logger.Info($"VN Orchestrator result applied. RequestId='{result.RequestId}', GameId='{result.PlayniteGameId}'.");
            }
            catch (Exception exception)
            {
                try { logger.Error($"VN Orchestrator result application failed. RequestId='{result.RequestId}', Exception='{exception}'."); }
                catch { }
                try { api.Notifications.Add("VNOrchestrator", "A instalação concluída não pôde ser sincronizada. Consulte o log do Playnite.", NotificationType.Error); }
                catch { }
            }
        }
    }

    private static CurrentGameSnapshot Snapshot(Game game, InstalledGameResult result) => new(
        game.Id, game.IsInstalled, game.InstallDirectory, result.RequestId, result.SourceFingerprint,
        game.GameActions?.Select(action => new GameActionSnapshot(action.Name, action.Path, action.WorkingDir, action.IsPlayAction)).ToArray() ?? Array.Empty<GameActionSnapshot>(),
        new Dictionary<string, string>(),
        game.Features?.Any(feature => feature?.Name?.StartsWith(SupervisorPlayAction.LauncherFeaturePrefix, StringComparison.OrdinalIgnoreCase) == true) == true);
}
