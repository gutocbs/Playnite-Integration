using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using VnManager.PlayniteInstallation.Domain;
using VnManager.PlayniteInstallation.Transport;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class PlayniteResultApplier(IPlayniteAPI api, AtomicTransportFileStore store)
{
    public void ApplyPending()
    {
        foreach (var result in store.ReadResults())
        {
            var request = store.ReadRequest(result.RequestId);
            var game = api.Database.Games.Get(result.PlayniteGameId);
            var decision = ResultApplicationPolicy.Evaluate(game is null || request is null ? null : Snapshot(game, request), result);
            if (!decision.IsApplicable)
            {
                api.Notifications.Add("VNOrchestrator", "A instalação concluída não pôde ser sincronizada: " + decision.Conflict, NotificationType.Error);
                continue;
            }

            var mutation = decision.Mutation!;
            game.IsInstalled = mutation.IsInstalled;
            game.InstallDirectory = mutation.InstallDirectory;
            var managed = game.GameActions.SingleOrDefault(action => action.Name == ResultApplicationPolicy.ManagedActionName);
            if (managed is null)
            {
                managed = new GameAction { Name = ResultApplicationPolicy.ManagedActionName };
                game.GameActions.Add(managed);
            }
            managed.Type = GameActionType.File;
            managed.Path = result.ExecutablePath;
            managed.WorkingDir = result.WorkingDirectory;
            api.Database.Games.Update(game);
            store.WriteAcknowledgement(new VnManager.PlayniteInstallation.Contracts.PlayniteAcknowledgement(1, result.RequestId, result.WorkflowId, result.PlayniteGameId, System.DateTimeOffset.UtcNow));
        }
    }

    private static CurrentGameSnapshot Snapshot(Game game, VnManager.PlayniteInstallation.Contracts.InstallationRequest request) => new(
        game.Id, game.IsInstalled, game.InstallDirectory, request.RequestId, request.SourceFingerprint,
        game.GameActions.Select(action => new GameActionSnapshot(action.Name, action.Path, action.WorkingDir, DirectGameActionType.File)).ToArray(),
        new Dictionary<string, string>());
}
