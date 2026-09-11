using System.Text.Json;

namespace Launcher.LocaleEmulator;

public static class LocaleEmulatorOptionsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static LocaleEmulatorOptions Load(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        var fullPath = Path.GetFullPath(configurationPath);
        using var stream = File.OpenRead(fullPath);

        return JsonSerializer.Deserialize<LocaleEmulatorOptions>(stream, SerializerOptions)
            ?? throw new InvalidDataException(
                $"Locale Emulator configuration is empty: {fullPath}");
    }
}
