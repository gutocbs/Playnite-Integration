namespace Launcher.NoRegionLoader;

public sealed record NoRegionLoaderOptions
{
    public string NoRegionLoaderExecutable { get; init; } = string.Empty;

    public TimeSpan PollingInterval { get; init; }
}
