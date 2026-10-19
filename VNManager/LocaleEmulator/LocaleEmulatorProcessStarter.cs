using System.Diagnostics;
using Launcher.Abstractions;
using Launcher.Supervision;

namespace Launcher.LocaleEmulator;

public sealed class LocaleEmulatorProcessStarter : ILocaleEmulatorProcessStarter
{
    public ILaunchedProcess Start(LocaleEmulatorOptions options, LaunchRequest request) =>
        ToolProcess.Start(CreateStartInfo(options, request), "Locale Emulator");

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
}
