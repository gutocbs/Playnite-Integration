using System;
using System.Threading;

namespace VnManager.PlayniteInstallation.Transport;

public interface IOrchestratorWakeSignal
{
    void Signal();
}

public static class OrchestratorWakeEvent
{
    public static string NameForCurrentUser() => "Local\\VNOrchestrator." + Environment.UserName + ".wake";
}

public sealed class NamedOrchestratorWakeSignal(string eventName) : IOrchestratorWakeSignal
{
    public void Signal()
    {
        using var wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        wakeEvent.Set();
    }
}
