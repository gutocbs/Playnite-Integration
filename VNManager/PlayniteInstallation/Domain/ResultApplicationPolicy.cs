using VNInstallation.Protocol.Contracts;

namespace VnManager.PlayniteInstallation.Domain;

public enum DirectGameActionType
{
    File
}

public sealed record GameActionSnapshot(string Name, string Path, string WorkingDirectory, DirectGameActionType Type);

public sealed record CurrentGameSnapshot(
    Guid GameId,
    bool IsInstalled,
    string InstallDirectory,
    Guid PendingRequestId,
    string ExpectedSourceFingerprint,
    IReadOnlyList<GameActionSnapshot> GameActions,
    IReadOnlyDictionary<string, string> Metadata);

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

        if (current.IsInstalled)
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.AlreadyInstalled);
        }

        if (!string.Equals(current.InstallDirectory, result.SourcePath, StringComparison.OrdinalIgnoreCase))
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

        if (current.GameActions.Count(action => string.Equals(action.Name, ManagedActionName, StringComparison.Ordinal)) > 1)
        {
            return ResultApplicationDecision.Reject(ResultApplicationConflict.DuplicateManagedAction);
        }

        var actions = current.GameActions
            .Where(action => !string.Equals(action.Name, ManagedActionName, StringComparison.Ordinal))
            .Append(new GameActionSnapshot(ManagedActionName, result.ExecutablePath, result.WorkingDirectory, DirectGameActionType.File))
            .ToArray();

        return ResultApplicationDecision.Apply(new GameUpdateMutation(true, result.InstallationDirectory, actions, current.Metadata));
    }
}
