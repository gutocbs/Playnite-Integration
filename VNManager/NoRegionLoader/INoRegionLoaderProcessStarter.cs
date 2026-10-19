using Launcher.Abstractions;
using Launcher.Supervision;

namespace Launcher.NoRegionLoader;

public interface INoRegionLoaderProcessStarter
{
    ILaunchedProcess Start(NoRegionLoaderOptions options, LaunchRequest request);
}
