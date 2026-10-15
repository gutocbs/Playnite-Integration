using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class AtomicTransportFileStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "PlayniteInstallationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void WriteRequest_uses_a_deterministic_final_inbox_path_without_leaving_a_part_file()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var request = Request();

        var finalPath = store.WriteRequest(request);

        Assert.Equal(Path.Combine(root, "inbox", request.RequestId + ".json"), finalPath);
        Assert.True(File.Exists(finalPath));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "inbox"), "*.part"));
    }

    [Fact]
    public void ReadResults_ignores_partial_and_malformed_files()
    {
        var paths = new TransportPaths(root);
        Directory.CreateDirectory(paths.OutboxDirectory);
        File.WriteAllText(Path.Combine(paths.OutboxDirectory, "partial.result.json.part"), "{");
        File.WriteAllText(Path.Combine(paths.OutboxDirectory, "bad.result.json"), "{");
        var expected = Result();
        File.WriteAllText(Path.Combine(paths.OutboxDirectory, expected.RequestId + ".result.json"), ProtocolV1.Serialize(expected));

        var results = new AtomicTransportFileStore(paths).ReadResults();

        Assert.Collection(results, actual => Assert.Equal(expected, actual));
    }

    [Fact]
    public void Superseded_request_is_visible_to_result_consumers()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var requestId = Guid.NewGuid();

        store.MarkSuperseded(requestId);

        Assert.True(store.IsSuperseded(requestId));
    }

    [Fact]
    public void CleanupCandidates_includes_only_a_fully_acknowledged_old_triplet()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var request = Request();
        var result = Result();
        store.WriteRequest(request);
        store.WriteResult(result);
        store.WriteAcknowledgement(new PlayniteAcknowledgement(ProtocolV1.SchemaVersion, request.RequestId, result.WorkflowId, request.PlayniteGameId, DateTimeOffset.UtcNow));
        File.SetLastWriteTimeUtc(store.Paths.RequestPath(request.RequestId), DateTime.UtcNow.AddDays(-91));
        File.SetLastWriteTimeUtc(store.Paths.ResultPath(request.RequestId), DateTime.UtcNow.AddDays(-91));
        File.SetLastWriteTimeUtc(store.Paths.AcknowledgementPath(request.RequestId), DateTime.UtcNow.AddDays(-91));

        Assert.Contains(request.RequestId, store.GetAcknowledgedCleanupCandidates(DateTimeOffset.UtcNow.AddDays(-90)));
    }

    [Fact]
    public void Interaction_store_separates_requests_and_responses_and_never_overwrites()
    {
        var store = new AtomicTransportFileStore(new TransportPaths(root));
        var request = new PlayniteInteractionRequest(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            @"K:\原本.rar", "fingerprint", InteractionKind.SelectInstallationDirectory, "Escolha", "Selecione o caminho",
            @"K:\ゲーム\本編", null, DateTimeOffset.UtcNow);
        var response = new PlayniteInteractionResponse(1, request.InteractionId, request.RequestId, request.WorkflowId,
            request.PlayniteGameId, request.Kind, InteractionOutcome.Selected, @"K:\ゲーム\本編\game.exe", DateTimeOffset.UtcNow);

        store.WriteInteractionRequest(request);
        store.WriteInteractionResponse(response);

        Assert.Single(store.ReadInteractionRequests());
        Assert.Single(store.ReadInteractionResponses());
        Assert.Throws<IOException>(() => store.WriteInteractionRequest(request));
        Assert.Throws<IOException>(() => store.WriteInteractionResponse(response));
        store.DeleteInteractionRequest(request.InteractionId);
        store.DeleteInteractionResponse(request.InteractionId);
        Assert.Empty(store.ReadInteractionRequests());
        Assert.Empty(store.ReadInteractionResponses());
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static InstallationRequest Request() => new(1, Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"), Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"), "Game", @"K:\\Game.rar", "fingerprint", DateTimeOffset.UtcNow);
    private static InstalledGameResult Result() => new(1, Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"), Guid.NewGuid(), Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"), @"K:\\Game.rar", "fingerprint", @"D:\\Game", @"D:\\Game\\Game.exe", @"D:\\Game", DateTimeOffset.UtcNow);
}
