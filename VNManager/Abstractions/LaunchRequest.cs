namespace Launcher.Abstractions;

public sealed record LaunchRequest(
    string ExecutionId,
    string Executable,
    IReadOnlyList<string> Arguments,
    Func<string, CancellationToken, Task>? OnStarted = null);
