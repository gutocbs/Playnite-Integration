using VNInstallation.Protocol.Contracts;

namespace VnManager.PlayniteInstallation.Domain;

public sealed record GameActionSnapshot(string Name, string Path, string WorkingDirectory, bool IsPlayAction = false);

public sealed record CurrentGameSnapshot(
    Guid GameId,
    bool IsInstalled,
    string InstallDirectory,
    Guid PendingRequestId,
    string ExpectedSourceFingerprint,
    IReadOnlyList<GameActionSnapshot> GameActions,
    IReadOnlyDictionary<string, string> Metadata,
    bool HasLauncherFeature = false);

public sealed record GameUpdateMutation(bool IsInstalled, string InstallDirectory, IReadOnlyList<GameActionSnapshot> GameActions, IReadOnlyDictionary<string, string> Metadata);

public enum ResultApplicationConflict
{
    None,
    MissingGame,
    AlreadyInstalled,
    InstallDirectoryChanged,
    RequestMismatch,
    SourceFingerprintMismatch,
    DuplicateManagedAction
}

public sealed record ResultApplicationDecision(bool IsApplicable, GameUpdateMutation? Mutation, ResultApplicationConflict Conflict)
{
    public static ResultApplicationDecision Apply(GameUpdateMutation mutation) => new(true, mutation, ResultApplicationConflict.None);
    public static ResultApplicationDecision Reject(ResultApplicationConflict conflict) => new(false, null, conflict);
}

public static class ResultApplicationPolicy
{
    public const string ManagedActionName = "VN Orchestrator";

    public static ResultApplicationDecision Evaluate(CurrentGameSnapshot? current, InstalledGameResult result)
    {
        if (current is null)
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.MissingGame);
        }

        if (!current.IsInstalled
            && !string.Equals(current.InstallDirectory, result.SourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.InstallDirectoryChanged);
        }

        if (current.PendingRequestId != result.RequestId)
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.RequestMismatch);
        }

        if (!string.Equals(current.ExpectedSourceFingerprint, result.SourceFingerprint, StringComparison.Ordinal))
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.SourceFingerprintMismatch);
        }

        var managedActions = current.GameActions.Where(IsManaged).ToArray();
        if (managedActions.Length > 1)
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.DuplicateManagedAction);
        }

        return current.IsInstalled
            ? EvaluateInstalled(current, result, managedActions)
            : Install(current, result);
    }

    private static ResultApplicationDecision EvaluateInstalled(
        CurrentGameSnapshot current,
        InstalledGameResult result,
        IReadOnlyList<GameActionSnapshot> managedActions)
    {
        // An idempotent retry: the game update was persisted before the acknowledgement.
        if (managedActions.Count == 1
            && string.Equals(current.InstallDirectory, result.InstallationDirectory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(managedActions[0].Path, result.ExecutablePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(managedActions[0].WorkingDirectory, result.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return ResultApplicationDecision.Apply(new GameUpdateMutation(
                true,
                current.InstallDirectory,
                current.GameActions
                    .Select(action => IsManaged(action) ? action with { IsPlayAction = !current.HasLauncherFeature } : action)
                    .ToArray(),
                current.Metadata));
        }

        return ResultApplicationDecision.Reject(ResultApplicationConflict.AlreadyInstalled);
    }

    private static ResultApplicationDecision Install(CurrentGameSnapshot current, InstalledGameResult result)
    {
        var actions = current.GameActions
            .Where(action => !IsManaged(action))
            .Append(new GameActionSnapshot(
                ManagedActionName,
                result.ExecutablePath,
                result.WorkingDirectory,
                !current.HasLauncherFeature))
            .ToArray();

        return ResultApplicationDecision.Apply(new GameUpdateMutation(true, result.InstallationDirectory, actions, current.Metadata));
    }

    private static bool IsManaged(GameActionSnapshot action) =>
        string.Equals(action.Name, ManagedActionName, StringComparison.Ordinal);
}
