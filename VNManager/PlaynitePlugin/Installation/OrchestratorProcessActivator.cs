using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VnManager.PlayniteInstallation.Transport;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class OrchestratorProcessActivator(string executablePath) : IOrchestratorLauncher
{
    public Task ActivateAsync(CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(executablePath))
        {
            throw new System.IO.FileNotFoundException("VN Install Orchestrator was not found.", executablePath);
        }

        Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
