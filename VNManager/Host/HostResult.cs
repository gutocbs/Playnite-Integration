using Launcher.Abstractions;

namespace Launcher.Host;

public sealed record HostResult(
    string? ExecutionId,
    bool Success,
    IReadOnlyDictionary<string, object?>? Data,
    LaunchError? Error)
{
    public static HostResult FromLaunchResult(string executionId, LaunchResult result) =>
        new(executionId, result.Success, result.Data, result.Error);

    public static HostResult Failed(string? executionId, string code, string message) =>
        new(executionId, false, null, new LaunchError(code, message));
}
