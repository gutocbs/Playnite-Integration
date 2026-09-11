namespace Launcher.Abstractions;

public sealed record DispatcherRequest(
    string ExecutionId,
    string Executable,
    IReadOnlyList<string> Arguments,
    string Launcher);
