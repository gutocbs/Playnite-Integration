namespace Launcher.Abstractions;

public static class LaunchErrorCodes
{
    public const string Cancelled = "LAUNCH_CANCELLED";
    public const string ProcessStartTimeout = "PROCESS_START_TIMEOUT";
    public const string InvalidGameExecutable = "INVALID_GAME_EXECUTABLE";
    public const string GameExecutableNotFound = "GAME_EXECUTABLE_NOT_FOUND";
    public const string InvalidPollingInterval = "INVALID_POLLING_INTERVAL";
}
