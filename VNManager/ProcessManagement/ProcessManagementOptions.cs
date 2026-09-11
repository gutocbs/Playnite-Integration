namespace Launcher.ProcessManagement;

public sealed record ProcessManagementOptions
{
    public IReadOnlyList<string> MonitorProcesses { get; init; } = [];

    public int GlobalTimeout { get; init; }

    public IReadOnlyList<string> GlobalCleanupProcesses { get; init; } = [];

    public IReadOnlyList<ExecutableCleanupOptions> Executables { get; init; } = [];
}

public sealed record ExecutableCleanupOptions
{
    public string Name { get; init; } = string.Empty;

    public int? Timeout { get; init; }

    public IReadOnlyList<string> CleanupProcesses { get; init; } = [];
}
