using System.Diagnostics;
using Launcher.Abstractions;

namespace Launcher.LocaleEmulator;

public sealed class LocaleEmulatorProcessStarter : ILocaleEmulatorProcessStarter
{
    public ILaunchedProcess Start(LocaleEmulatorOptions options, LaunchRequest request)
    {
        var process = Process.Start(CreateStartInfo(options, request)) ?? throw new InvalidOperationException(
            "Locale Emulator did not return a process instance.");

        return new LaunchedProcess(process);
    }

    public static ProcessStartInfo CreateStartInfo(LocaleEmulatorOptions options, LaunchRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.LocaleEmulatorExecutable,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-runas");
        startInfo.ArgumentList.Add(options.ProfileId);
        startInfo.ArgumentList.Add(request.Executable);

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private sealed class LaunchedProcess(Process process) : ILaunchedProcess
    {
        public int Id => process.Id;

        public void Dispose() => process.Dispose();
    }
}
