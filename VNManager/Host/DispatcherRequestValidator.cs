using Launcher.Abstractions;

namespace Launcher.Host;

public static class DispatcherRequestValidator
{
    public static void Validate(DispatcherRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ExecutionId))
        {
            throw new DispatcherRequestValidationException(
                "INVALID_EXECUTION_ID",
                "ExecutionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Executable) ||
            !Path.IsPathFullyQualified(request.Executable))
        {
            throw new DispatcherRequestValidationException(
                "INVALID_GAME_EXECUTABLE",
                "Executable must be an absolute path.");
        }

        if (request.Arguments is null)
        {
            throw new DispatcherRequestValidationException(
                "INVALID_GAME_ARGUMENTS",
                "Arguments must be an array and may be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.Launcher))
        {
            throw new DispatcherRequestValidationException(
                "INVALID_LAUNCHER_NAME",
                "Launcher is required.");
        }
    }
}
