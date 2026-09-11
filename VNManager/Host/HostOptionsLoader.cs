using System.Text.Json;

namespace Launcher.Host;

public static class HostOptionsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static HostOptions Load(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        using var stream = File.OpenRead(Path.GetFullPath(configurationPath));
        var options = JsonSerializer.Deserialize<HostOptions>(stream, SerializerOptions)
            ?? throw new InvalidDataException("Host configuration is empty.");

        if (options.TemporaryDirectoryMaximumAgeSeconds <= 0)
        {
            throw new InvalidDataException(
                "temporaryDirectoryMaximumAgeSeconds must be greater than zero.");
        }

        return options;
    }
}
