using System.Text.Json;
using VnManager.PlayniteInstallation.Contracts;

namespace VnManager.PlayniteInstallation.Transport;

public sealed class AtomicTransportFileStore(TransportPaths paths)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    public TransportPaths Paths { get; } = paths;

    public string WriteRequest(InstallationRequest request) => Write(Paths.RequestPath(request.RequestId), ProtocolV1.Serialize(request));
    public string WriteResult(InstalledGameResult result) => Write(Paths.ResultPath(result.RequestId), ProtocolV1.Serialize(result));
    public string WriteAcknowledgement(PlayniteAcknowledgement acknowledgement) => Write(Paths.AcknowledgementPath(acknowledgement.RequestId), ProtocolV1.Serialize(acknowledgement));

    public IReadOnlyList<InstalledGameResult> ReadResults()
    {
        if (!Directory.Exists(Paths.OutboxDirectory)) return Array.Empty<InstalledGameResult>();
        return Directory.GetFiles(Paths.OutboxDirectory, "*.result.json")
            .Select(path => TryRead<InstalledGameResult>(path))
            .Where(result => result is not null && result.SchemaVersion == ProtocolV1.SchemaVersion)
            .Cast<InstalledGameResult>()
            .ToArray();
    }

    public IReadOnlyList<Guid> GetAcknowledgedCleanupCandidates(DateTimeOffset olderThan)
    {
        if (!Directory.Exists(Paths.AcknowledgementsDirectory)) return Array.Empty<Guid>();
        return Directory.GetFiles(Paths.AcknowledgementsDirectory, "*.ack.json")
            .Select(TryRead<PlayniteAcknowledgement>)
            .Where(ack => ack is not null && ack.SchemaVersion == ProtocolV1.SchemaVersion && File.Exists(Paths.RequestPath(ack.RequestId)) && File.Exists(Paths.ResultPath(ack.RequestId)))
            .Where(ack => File.GetLastWriteTimeUtc(Paths.RequestPath(ack!.RequestId)) <= olderThan.UtcDateTime && File.GetLastWriteTimeUtc(Paths.ResultPath(ack.RequestId)) <= olderThan.UtcDateTime && File.GetLastWriteTimeUtc(Paths.AcknowledgementPath(ack.RequestId)) <= olderThan.UtcDateTime)
            .Select(ack => ack!.RequestId).ToArray();
    }

    private static T? TryRead<T>(string path) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options); }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    private static string Write(string finalPath, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var partialPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".part";
        File.WriteAllText(partialPath, json);
        File.Move(partialPath, finalPath);
        return finalPath;
    }
}
