namespace Launcher.Host;

public static class HostExitCodes
{
    public const int Success = 0;
    public const int InvalidInvocation = 10;
    public const int InvalidRequest = 20;
    public const int LauncherResolutionFailed = 30;
    public const int LauncherFailed = 40;
    public const int InternalError = 50;
    public const int Cancelled = 60;
}
