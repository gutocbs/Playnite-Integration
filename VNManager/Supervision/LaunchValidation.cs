using Launcher.Abstractions;

namespace Launcher.Supervision;

public static class LaunchValidation
{
    public static LaunchResult? ValidateGameExecutable(LaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Executable) || !Path.IsPathFullyQualified(request.Executable))
        {
            return LaunchResult.Failed(
                LaunchErrorCodes.InvalidGameExecutable,
                "The game executable must be an absolute path.");
        }

        if (!File.Exists(request.Executable))
        {
            return LaunchResult.Failed(
                LaunchErrorCodes.GameExecutableNotFound,
                $"The game executable was not found: {request.Executable}");
        }

        return null;
    }

    public static LaunchResult? ValidateToolExecutable(string? path, string notFoundCode, string toolDisplayName)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) ||
            !File.Exists(path))
        {
            return LaunchResult.Failed(
                notFoundCode,
                $"The {toolDisplayName} executable was not found: {path}");
        }

        return null;
    }

    public static LaunchResult? ValidatePollingInterval(TimeSpan interval) =>
        interval <= TimeSpan.Zero
            ? LaunchResult.Failed(
                LaunchErrorCodes.InvalidPollingInterval,
                "The process polling interval must be greater than zero.")
            : null;
}
