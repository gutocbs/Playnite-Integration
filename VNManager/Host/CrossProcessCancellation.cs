using System.Diagnostics;

namespace Launcher.Host;

public sealed class CrossProcessCancellation : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly EventWaitHandle _cancellationEvent;
    private readonly RegisteredWaitHandle _registeredWait;
    private readonly Process _adapterProcess;

    public CrossProcessCancellation(int adapterProcessId, string cancellationEventName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Cross-process cancellation currently requires Windows.");
        }

        _cancellationEvent = EventWaitHandle.OpenExisting(cancellationEventName);
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _cancellationEvent,
            static (state, _) => ((CancellationTokenSource)state!).Cancel(),
            _cancellation,
            Timeout.Infinite,
            executeOnlyOnce: true);

        _adapterProcess = Process.GetProcessById(adapterProcessId);
        _adapterProcess.EnableRaisingEvents = true;
        _adapterProcess.Exited += AdapterProcessExited;

        if (_adapterProcess.HasExited)
        {
            _cancellation.Cancel();
        }
    }

    public CancellationToken Token => _cancellation.Token;

    public void Dispose()
    {
        _adapterProcess.Exited -= AdapterProcessExited;
        _registeredWait.Unregister(null);
        _adapterProcess.Dispose();
        _cancellationEvent.Dispose();
        _cancellation.Dispose();
    }

    private void AdapterProcessExited(object? sender, EventArgs eventArgs) => _cancellation.Cancel();
}
