namespace Launcher.ProcessManagement;

public sealed record ProcessManagementOptions
{
    public IReadOnlyList<string> MonitorProcesses { get; init; } = [];

    // A launcher executable can be only a short-lived bootstrapper. Give the
    // configured engine processes a chance to appear before selecting it as
    // the process that controls the Playnite session.
    public int PreferredProcessDetectionWindowSeconds { get; init; } = 5;

    public int GlobalTimeout { get; init; }

    public IReadOnlyList<string> GlobalCleanupProcesses { get; init; } = [];

    public IReadOnlyList<string> GlobalCleanupServices { get; init; } = [];

    public IReadOnlyList<ExecutableCleanupOptions> Executables { get; init; } = [];
}

public sealed record ExecutableCleanupOptions
{
    public string Name { get; init; } = string.Empty;

    public int? Timeout { get; init; }

    public IReadOnlyList<string> CleanupProcesses { get; init; } = [];

    public IReadOnlyList<string> CleanupServices { get; init; } = [];
}
