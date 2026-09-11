using Launcher.Abstractions;

namespace Launcher.NoRegionLoader;

public interface INoRegionLoaderProcessStarter
{
    INoRegionLoaderProcess Start(NoRegionLoaderOptions options, LaunchRequest request);
}

public interface INoRegionLoaderProcess : IDisposable
{
    int Id { get; }
}
