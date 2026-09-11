namespace Launcher.Host;

public sealed record HostOptions
{
    public int TemporaryDirectoryMaximumAgeSeconds { get; init; }
}
