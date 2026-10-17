namespace VnManager.PlayniteInstallation.Transport;

public sealed class InteractionReadySynchronization : IDisposable
{
    private readonly ResultReadyListener listener;

    public InteractionReadySynchronization(string eventName, IResultReadyUiDispatcher dispatcher, Action processPending)
    {
        if (dispatcher is null) throw new ArgumentNullException(nameof(dispatcher));
        if (processPending is null) throw new ArgumentNullException(nameof(processPending));
        listener = new ResultReadyListener(eventName, () => dispatcher.BeginInvoke(processPending));
    }

    public void Dispose() => listener.Dispose();
}
