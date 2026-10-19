namespace VnManager.PlayniteInstallation.Domain;

public sealed class PendingInstallCompletionRegistry
{
    private readonly Dictionary<Guid, PendingCompletion> pending = new();

    public void Register(Guid requestId, Guid gameId, Action<string> complete, Action? cancel = null) => pending[requestId] = new PendingCompletion(gameId, complete, cancel);

    public void Remove(Guid requestId) => pending.Remove(requestId);

    public bool TryComplete(Guid requestId, Guid gameId, string installationDirectory)
    {
        if (!pending.TryGetValue(requestId, out var pendingCompletion) || pendingCompletion.GameId != gameId) return false;
        pendingCompletion.Complete(installationDirectory);
        pending.Remove(requestId);
        return true;
    }

    public bool TryCancel(Guid requestId, Guid gameId)
    {
        if (!pending.TryGetValue(requestId, out var pendingCompletion) || pendingCompletion.GameId != gameId) return false;
        pendingCompletion.Cancel?.Invoke();
        pending.Remove(requestId);
        return true;
    }

    private sealed record PendingCompletion(Guid GameId, Action<string> Complete, Action? Cancel);
}
