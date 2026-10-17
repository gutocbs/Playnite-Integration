using System;
using System.Threading;

namespace VnManager.PlayniteInstallation.Transport;

public sealed class ResultReadyListener : IDisposable
{
    private readonly Action onSignalled;
    private readonly EventWaitHandle resultReady;
    private readonly RegisteredWaitHandle registeredWait;
    private int disposed;

    public ResultReadyListener(string eventName, Action onSignalled)
    {
        this.onSignalled = onSignalled ?? throw new ArgumentNullException(nameof(onSignalled));
        resultReady = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        registeredWait = ThreadPool.RegisterWaitForSingleObject(resultReady, static (state, _) => ((ResultReadyListener)state!).Notify(), this, Timeout.Infinite, executeOnlyOnce: false);
    }

    private void Notify()
    {
        if (Volatile.Read(ref disposed) == 0) onSignalled();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        using var callbacksComplete = new EventWaitHandle(false, EventResetMode.ManualReset);
        if (registeredWait.Unregister(callbacksComplete)) callbacksComplete.WaitOne();
        resultReady.Dispose();
    }
}
