using VnManager.PlayniteInstallation.Transport;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class InteractionReadySynchronizationTests
{
    [Fact]
    public void Dispatches_to_the_UI_before_processing_a_signal()
    {
        var eventName = "Local\\VNOrchestrator.InteractionSyncTests." + Guid.NewGuid().ToString("N");
        var dispatcher = new RecordingDispatcher();
        using var processed = new ManualResetEventSlim();
        using var synchronization = new InteractionReadySynchronization(eventName, dispatcher, () =>
        {
            Assert.True(dispatcher.IsDispatching);
            processed.Set();
        });
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

        signal.Set();

        Assert.True(processed.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, dispatcher.CallCount);
    }

    private sealed class RecordingDispatcher : IResultReadyUiDispatcher
    {
        public int CallCount { get; private set; }
        public bool IsDispatching { get; private set; }

        public void BeginInvoke(Action action)
        {
            CallCount++;
            IsDispatching = true;
            try { action(); }
            finally { IsDispatching = false; }
        }
    }
}
