using System.ComponentModel;
using System.Diagnostics;

namespace Launcher.ProcessManagement;

public sealed class ElevatedServiceStopper : IServiceStopper
{
    public async Task<ServiceStopResult> StopAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        if (!OperatingSystem.IsWindows())
        {
            return new ServiceStopResult(false, "Stopping Windows services is only supported on Windows.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("stop");
            startInfo.ArgumentList.Add(serviceName);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ServiceStopResult(false, "Could not start sc.exe.");
            }

            await process.WaitForExitAsync(cancellationToken);

            // 1062 means the service was already stopped, which satisfies cleanup.
            return process.ExitCode is 0 or 1062
                ? new ServiceStopResult(true)
                : new ServiceStopResult(false, $"sc.exe exited with code {process.ExitCode}.");
        }
        catch (Win32Exception exception)
        {
            return new ServiceStopResult(false, exception.Message);
        }
    }
}
