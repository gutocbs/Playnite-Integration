namespace VnManager.PlayniteInstallation.Transport;

/// <summary>Runs an action on the UI dispatcher whenever a named transport event is signalled.</summary>
public sealed class UiSignalSubscription : IDisposable
{
    private readonly ResultReadyListener listener;

    public UiSignalSubscription(string eventName, IResultReadyUiDispatcher dispatcher, Action onSignal)
    {
        if (dispatcher is null) throw new ArgumentNullException(nameof(dispatcher));
        if (onSignal is null) throw new ArgumentNullException(nameof(onSignal));
        listener = new ResultReadyListener(eventName, () => dispatcher.BeginInvoke(onSignal));
    }

    public void Dispose() => listener.Dispose();
}
