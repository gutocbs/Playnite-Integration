using System.Text.Json;
using Launcher.Abstractions;

namespace Launcher.Host;

public static class JsonFileTransport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static DispatcherRequest ReadRequest(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<DispatcherRequest>(stream, SerializerOptions)
            ?? throw new InvalidDataException("The DispatcherRequest JSON is empty.");
    }

    public static void WriteResult(string path, HostResult result)
        => WriteJsonAtomically(path, result);

    public static void WriteStarted(string path, string executionId, string processName)
        => WriteJsonAtomically(path, new { executionId, processName });

    private static void WriteJsonAtomically(string path, object value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.part";

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
