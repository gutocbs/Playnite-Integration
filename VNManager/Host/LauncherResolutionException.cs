namespace Launcher.Host;

public sealed class LauncherResolutionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
