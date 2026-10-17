namespace VnManager.PlayniteInstallation.Domain;

public sealed record GameInstallSnapshot(Guid GameId, string Name, bool IsInstalled, string? InstallDirectory, IReadOnlyCollection<string> Genres);

public interface IFileProbe
{
    bool Exists(string path);
}

public interface IActiveWorkflowLookup
{
    bool HasActiveWorkflow(Guid gameId);
}

public enum InstallEligibilityRejection
{
    None,
    Installed,
    SourceMissing,
    UnsupportedSource,
    NotVisualNovel,
    WorkflowAlreadyActive
}

public sealed record InstallEligibilityResult(bool IsEligible, InstallEligibilityRejection Rejection)
{
    public static InstallEligibilityResult Eligible() => new(true, InstallEligibilityRejection.None);
    public static InstallEligibilityResult Rejected(InstallEligibilityRejection rejection) => new(false, rejection);
}

public static class InstallEligibility
{
    public static InstallEligibilityResult Evaluate(GameInstallSnapshot game, IFileProbe fileProbe, IActiveWorkflowLookup workflows)
    {
        if (game.IsInstalled)
        {
            return InstallEligibilityResult.Rejected(InstallEligibilityRejection.Installed);
        }

        var sourcePath = game.InstallDirectory ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourcePath) || !fileProbe.Exists(sourcePath))
        {
            return InstallEligibilityResult.Rejected(InstallEligibilityRejection.SourceMissing);
        }

        if (!string.Equals(Path.GetExtension(sourcePath), ".rar", StringComparison.OrdinalIgnoreCase))
        {
            return InstallEligibilityResult.Rejected(InstallEligibilityRejection.UnsupportedSource);
        }

        if (!game.Genres.Any(genre => string.Equals(genre, "Visual Novel", StringComparison.OrdinalIgnoreCase)))
        {
            return InstallEligibilityResult.Rejected(InstallEligibilityRejection.NotVisualNovel);
        }

        return workflows.HasActiveWorkflow(game.GameId)
            ? InstallEligibilityResult.Rejected(InstallEligibilityRejection.WorkflowAlreadyActive)
            : InstallEligibilityResult.Eligible();
    }
}
