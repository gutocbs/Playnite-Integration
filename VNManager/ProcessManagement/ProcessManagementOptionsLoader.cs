using System.Text.Json;

namespace Launcher.ProcessManagement;

public static class ProcessManagementOptionsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ProcessManagementOptions Load(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        var fullPath = Path.GetFullPath(configurationPath);
        using var stream = File.OpenRead(fullPath);

        var options = JsonSerializer.Deserialize<ProcessManagementOptions>(stream, SerializerOptions)
            ?? throw new InvalidDataException(
                $"Process management configuration is empty: {fullPath}");

        if (options.GlobalTimeout <= 0)
        {
            throw new InvalidDataException(
                "Process management globalTimeout must be greater than zero seconds.");
        }

        if (options.PreferredProcessDetectionWindowSeconds < 0)
        {
            throw new InvalidDataException(
                "Process management preferredProcessDetectionWindowSeconds cannot be negative.");
        }

        var invalidExecutable = options.Executables.FirstOrDefault(executable =>
            string.IsNullOrWhiteSpace(executable.Name) || executable.Timeout is <= 0);
        if (invalidExecutable is not null)
        {
            throw new InvalidDataException(
                "Every executable entry must have a name and a timeout greater than zero when specified.");
        }

        return options;
    }
}
