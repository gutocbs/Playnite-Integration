namespace Launcher.ProcessManagement;

internal static class ExecutableEntries
{
    public static IEnumerable<ExecutableCleanupOptions> For(
        ProcessManagementOptions options,
        string normalizedProcessName) =>
        options.Executables.Where(executable => string.Equals(
            ProcessName.Normalize(executable.Name),
            normalizedProcessName,
            StringComparison.OrdinalIgnoreCase));
}
