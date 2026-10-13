using VnManager.PlayniteInstallation.Contracts;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class ProtocolV1Tests
{
    [Fact]
    public void DeserializeRequest_round_trips_v1_payload()
    {
        var request = CreateRequest();

        var result = ProtocolV1.DeserializeRequest(ProtocolV1.Serialize(request));

        Assert.True(result.IsSuccess);
        Assert.Equal(request, result.Value);
    }

    [Fact]
    public void DeserializeRequest_rejects_an_incompatible_major_schema()
    {
        const string json = "{\"schemaVersion\":2,\"requestId\":\"5b1af416-9e33-4c67-8e15-014d9f630b57\",\"playniteGameId\":\"7604d65c-cb66-4fba-83c3-b1459f754f53\",\"gameName\":\"Game\",\"sourcePath\":\"K:\\\\Game.rar\",\"sourceFingerprint\":\"fingerprint\",\"createdAtUtc\":\"2026-10-13T00:00:00+00:00\"}";

        var result = ProtocolV1.DeserializeRequest(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolErrorCode.IncompatibleSchema, result.ErrorCode);
    }

    [Fact]
    public void DeserializeRequest_tolerates_unknown_v1_properties()
    {
        var json = ProtocolV1.Serialize(CreateRequest());
        json = json[..^1] + ",\"futureField\":\"ignored\"}";

        var result = ProtocolV1.DeserializeRequest(json);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CreateSourceFingerprint_is_deterministic_for_same_source_snapshot()
    {
        var first = ProtocolV1.CreateSourceFingerprint(@"K:\\VNs\\Game.rar", 42, new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        var second = ProtocolV1.CreateSourceFingerprint(@"K:\\VNs\\Game.rar", 42, new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(first, second);
    }

    private static InstallationRequest CreateRequest() => new(
        ProtocolV1.SchemaVersion,
        Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"),
        Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"),
        "Game",
        @"K:\\Game.rar",
        "fingerprint",
        new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero));
}
