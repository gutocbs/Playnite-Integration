using System;

namespace VnManager.PlayniteInstallation.Transport;

public interface IResultReadyUiDispatcher
{
    void BeginInvoke(Action action);
}

public sealed class ResultReadySynchronization : IDisposable
{
    private readonly ResultReadyListener listener;

    public ResultReadySynchronization(string eventName, IResultReadyUiDispatcher dispatcher, Action reconcile)
    {
        if (dispatcher is null) throw new ArgumentNullException(nameof(dispatcher));
        if (reconcile is null) throw new ArgumentNullException(nameof(reconcile));
        listener = new ResultReadyListener(eventName, () => dispatcher.BeginInvoke(reconcile));
    }

    public void Dispose() => listener.Dispose();
}
