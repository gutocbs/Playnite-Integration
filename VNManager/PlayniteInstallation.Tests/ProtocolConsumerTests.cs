using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Tests;

/// <summary>
/// Consumer-side checks for protocol behavior this plugin relies on that the protocol
/// repository does not test yet.
/// </summary>
public sealed class ProtocolConsumerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PlayniteInstallationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Interaction_store_never_overwrites_requests_or_responses()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var request = CreateInteractionRequest(InteractionKind.SelectInstallationDirectory);
        var response = new PlayniteInteractionResponse(1, request.InteractionId, request.RequestId, request.WorkflowId,
            request.PlayniteGameId, request.Kind, InteractionOutcome.Selected, @"K:\ゲーム\本編\game.exe", DateTimeOffset.UtcNow);

        store.WriteInteractionRequest(request);
        store.WriteInteractionResponse(response);

        Assert.Throws<IOException>(() => store.WriteInteractionRequest(request));
        Assert.Throws<IOException>(() => store.WriteInteractionResponse(response));
    }

    [Fact]
    public void Interaction_contract_rejects_schema_2()
    {
        var request = CreateInteractionRequest(InteractionKind.SelectGameExecutable) with { SchemaVersion = 2 };

        var result = ProtocolV1.DeserializeInteractionRequest(ProtocolV1.Serialize(request));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolErrorCode.IncompatibleSchema, result.ErrorCode);
    }

    [Theory]
    [InlineData(InteractionKind.SelectInstallerExecutable, InteractionOutcome.Selected)]
    [InlineData(InteractionKind.SelectInstallationDirectory, InteractionOutcome.Cancelled)]
    [InlineData(InteractionKind.SelectGameExecutable, InteractionOutcome.Confirmed)]
    [InlineData(InteractionKind.ConfirmDirectInstallerLaunch, InteractionOutcome.Declined)]
    public void Interaction_contract_round_trips_all_kinds_and_outcomes(InteractionKind kind, InteractionOutcome outcome)
    {
        var request = CreateInteractionRequest(kind);
        var response = new PlayniteInteractionResponse(1, request.InteractionId, request.RequestId, request.WorkflowId,
            request.PlayniteGameId, kind, outcome, @"K:\ゲーム\本編\game.exe", DateTimeOffset.UtcNow);
        var requestJson = ProtocolV1.Serialize(request).TrimEnd('}') + ",\"futureField\":true}";
        var responseJson = ProtocolV1.Serialize(response).TrimEnd('}') + ",\"futureField\":true}";

        Assert.Equal(request, ProtocolV1.DeserializeInteractionRequest(requestJson).Value);
        Assert.Equal(response, ProtocolV1.DeserializeInteractionResponse(responseJson).Value);
    }

    [Fact]
    public void ReadResults_ignores_partial_and_malformed_outbox_files()
    {
        var paths = new TransportPaths(root);
        Directory.CreateDirectory(paths.OutboxDirectory);
        File.WriteAllText(Path.Combine(paths.OutboxDirectory, "partial.result.json.part"), "{");
        File.WriteAllText(Path.Combine(paths.OutboxDirectory, "bad.result.json"), "{");
        var expected = new InstalledGameResult(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), @"K:\\Game.rar", "fingerprint",
            @"D:\\Game", @"D:\\Game\\Game.exe", @"D:\\Game", DateTimeOffset.UtcNow);
        File.WriteAllText(Path.Combine(paths.OutboxDirectory, expected.RequestId + ".result.json"), ProtocolV1.Serialize(expected));

        var results = new AtomicTransportFileStore(paths).ReadResults();

        Assert.Collection(results, actual => Assert.Equal(expected, actual));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static PlayniteInteractionRequest CreateInteractionRequest(InteractionKind kind) => new(
        1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), @"K:\原本.rar", "fingerprint", kind,
        "Escolha", "Selecione o caminho", @"K:\ゲーム\本編", "Executáveis|*.exe", DateTimeOffset.UtcNow);
}
