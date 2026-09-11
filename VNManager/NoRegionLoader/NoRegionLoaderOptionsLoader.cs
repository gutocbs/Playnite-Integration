using System.Text.Json;

namespace Launcher.NoRegionLoader;

public static class NoRegionLoaderOptionsLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static NoRegionLoaderOptions Load(string configurationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        var fullPath = Path.GetFullPath(configurationPath);
        using var stream = File.OpenRead(fullPath);

        return JsonSerializer.Deserialize<NoRegionLoaderOptions>(stream, SerializerOptions)
            ?? throw new InvalidDataException(
                $"NoRegionLoader configuration is empty: {fullPath}");
    }
}
