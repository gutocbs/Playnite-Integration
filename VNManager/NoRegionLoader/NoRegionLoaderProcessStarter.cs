using System.Diagnostics;
using Launcher.Abstractions;

namespace Launcher.NoRegionLoader;

public sealed class NoRegionLoaderProcessStarter : INoRegionLoaderProcessStarter
{
    public INoRegionLoaderProcess Start(NoRegionLoaderOptions options, LaunchRequest request)
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

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            "NoRegionLoader did not return a process instance.");

        return new LaunchedProcess(process);
    }

    private sealed class LaunchedProcess(Process process) : INoRegionLoaderProcess
    {
        public int Id => process.Id;

        public void Dispose() => process.Dispose();
    }
}
