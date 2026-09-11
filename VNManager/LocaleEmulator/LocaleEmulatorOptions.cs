namespace Launcher.LocaleEmulator;

public sealed record LocaleEmulatorOptions
{
    public string LocaleEmulatorExecutable { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public TimeSpan PollingInterval { get; init; }
}
