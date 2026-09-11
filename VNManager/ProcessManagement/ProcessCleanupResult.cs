namespace Launcher.ProcessManagement;

public sealed record ProcessCleanupResult(
    int ProcessesFound,
    int ProcessesTerminated,
    IReadOnlyList<string> Failures);
