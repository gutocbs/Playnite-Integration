namespace Launcher.ProcessManagement;

internal static class ProcessName
{
    public static string Normalize(string executableOrProcessName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executableOrProcessName);

        var trimmed = executableOrProcessName.Trim().Trim('"');
        var fileName = Path.GetFileName(trimmed);

        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName)
            : fileName;
    }
}
