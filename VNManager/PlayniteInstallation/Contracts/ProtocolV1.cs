using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VnManager.PlayniteInstallation.Contracts;

public static class ProtocolV1
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, JsonOptions);

    public static ProtocolReadResult<InstallationRequest> DeserializeRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<InstallationRequest>(json, JsonOptions);
            if (request is null)
            {
                return ProtocolReadResult<InstallationRequest>.Failure(ProtocolErrorCode.MalformedPayload);
            }

            if (request.SchemaVersion != SchemaVersion)
            {
                return ProtocolReadResult<InstallationRequest>.Failure(ProtocolErrorCode.IncompatibleSchema);
            }

            if (request.RequestId == Guid.Empty || request.PlayniteGameId == Guid.Empty ||
                string.IsNullOrWhiteSpace(request.GameName) || string.IsNullOrWhiteSpace(request.SourcePath) ||
                string.IsNullOrWhiteSpace(request.SourceFingerprint))
            {
                return ProtocolReadResult<InstallationRequest>.Failure(ProtocolErrorCode.MissingRequiredField);
            }

            return ProtocolReadResult<InstallationRequest>.Success(request);
        }
        catch (JsonException)
        {
            return ProtocolReadResult<InstallationRequest>.Failure(ProtocolErrorCode.MalformedPayload);
        }
    }

    public static string CreateSourceFingerprint(string canonicalPath, long length, DateTimeOffset lastWriteUtc)
    {
        var material = string.Concat(canonicalPath, "\0", length.ToString(System.Globalization.CultureInfo.InvariantCulture), "\0", lastWriteUtc.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(material))).Replace("-", string.Empty);
    }
}

public enum ProtocolErrorCode
{
    None,
    IncompatibleSchema,
    MalformedPayload,
    MissingRequiredField,
    SourceFingerprintMismatch
}

public sealed record InstallationRequest(
    int SchemaVersion,
    Guid RequestId,
    Guid PlayniteGameId,
    string GameName,
    string SourcePath,
    string SourceFingerprint,
    DateTimeOffset CreatedAtUtc);

public sealed record InstalledGameResult(
    int SchemaVersion,
    Guid RequestId,
    Guid WorkflowId,
    Guid PlayniteGameId,
    string SourcePath,
    string SourceFingerprint,
    string InstallationDirectory,
    string ExecutablePath,
    string WorkingDirectory,
    DateTimeOffset CompletedAtUtc);

public sealed record ProtocolReadResult<T>(bool IsSuccess, T? Value, ProtocolErrorCode ErrorCode)
{
    public static ProtocolReadResult<T> Success(T value) => new(true, value, ProtocolErrorCode.None);
    public static ProtocolReadResult<T> Failure(ProtocolErrorCode errorCode) => new(false, default, errorCode);
}
