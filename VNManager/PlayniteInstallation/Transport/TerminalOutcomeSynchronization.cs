namespace VnManager.PlayniteInstallation.Transport;

public sealed class TerminalOutcomeSynchronization : IDisposable
{
    private readonly ResultReadyListener listener;

    public TerminalOutcomeSynchronization(string eventName, IResultReadyUiDispatcher dispatcher, Action reconcile)
    {
        listener = new ResultReadyListener(eventName, () => dispatcher.BeginInvoke(reconcile));
    }

    public void Dispose() => listener.Dispose();
}
