using System.Diagnostics;

namespace Launcher.Supervision;

public static class ToolProcess
{
    public static ILaunchedProcess Start(ProcessStartInfo startInfo, string toolDisplayName)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            $"{toolDisplayName} did not return a process instance.");

        return new LaunchedProcess(process);
    }

    private sealed class LaunchedProcess(Process process) : ILaunchedProcess
    {
        public int Id => process.Id;

        public void Dispose() => process.Dispose();
    }
}
