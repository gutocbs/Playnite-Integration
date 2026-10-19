using Launcher.Abstractions;
using Launcher.Logging;
using Launcher.ProcessManagement;
using Launcher.Supervision;

namespace Launcher.NoRegionLoader;

public sealed class NoRegionLoaderLauncher(
    NoRegionLoaderOptions options,
    INoRegionLoaderProcessStarter processStarter,
    IProcessMonitor processMonitor,
    IProcessCleanup processCleanup,
    IAppLogger logger) : ILauncher
{
    private static readonly SupervisedTool Tool = new(
        "NoRegionLoader",
        "NoRegionLoader",
        "NO_REGION_LOADER_LAUNCH_FAILED",
        "NoRegionLoaderProcessId");

    public async Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = LaunchValidation.ValidateGameExecutable(request)
            ?? LaunchValidation.ValidateToolExecutable(
                options.NoRegionLoaderExecutable,
                "NO_REGION_LOADER_NOT_FOUND",
                Tool.DisplayName)
            ?? LaunchValidation.ValidatePollingInterval(options.PollingInterval);
        if (validationError is not null)
        {
            return validationError;
        }

        return await SupervisedLaunch.RunAsync(
            Tool,
            request,
            options.PollingInterval,
            () => processStarter.Start(options, request),
            processMonitor,
            processCleanup,
            logger,
            cancellationToken);
    }
}
