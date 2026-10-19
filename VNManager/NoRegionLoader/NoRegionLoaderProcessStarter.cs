using System.Diagnostics;
using Launcher.Abstractions;
using Launcher.Supervision;

namespace Launcher.NoRegionLoader;

public sealed class NoRegionLoaderProcessStarter : INoRegionLoaderProcessStarter
{
    public ILaunchedProcess Start(NoRegionLoaderOptions options, LaunchRequest request) =>
        ToolProcess.Start(CreateStartInfo(options, request), "NoRegionLoader");

    public static ProcessStartInfo CreateStartInfo(NoRegionLoaderOptions options, LaunchRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.NoRegionLoaderExecutable,
            WorkingDirectory = Path.GetDirectoryName(request.Executable)
                ?? throw new InvalidOperationException("The game executable has no parent directory."),
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add(request.Executable);

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
