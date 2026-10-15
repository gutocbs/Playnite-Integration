using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VnManager.PlayniteInstallation.Domain;
using VnManager.PlayniteInstallation.Transport;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class InteractionRequestProcessorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "InteractionRequestProcessorTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void One_request_produces_one_dialog_and_one_response_across_reconciliations()
    {
        var store = Store();
        var request = Request();
        store.WriteInteractionRequest(request);
        var dialog = new RecordingDialog(new InteractionDialogResult(InteractionOutcome.Selected, @"K:\ゲーム\game.exe"));
        var wake = new RecordingWakeSignal();
        var processor = new InteractionRequestProcessor(store, dialog, new PredicateValidator(_ => true), wake);

        processor.ProcessPending();
        processor.ProcessPending();

        Assert.Equal(1, dialog.CallCount);
        var response = Assert.Single(store.ReadInteractionResponses());
        Assert.Equal(request.InteractionId, response.InteractionId);
        Assert.Equal(@"K:\ゲーム\game.exe", response.SelectedPath);
        Assert.Equal(1, wake.CallCount);
    }

    [Fact]
    public void Existing_response_suppresses_the_dialog_and_cancel_is_persisted()
    {
        var store = Store();
        var answered = Request();
        store.WriteInteractionRequest(answered);
        store.WriteInteractionResponse(Response(answered, InteractionOutcome.Selected));
        var cancelled = Request();
        store.WriteInteractionRequest(cancelled);
        var dialog = new RecordingDialog(new InteractionDialogResult(InteractionOutcome.Cancelled, null));

        new InteractionRequestProcessor(store, dialog, new PredicateValidator(_ => true), new RecordingWakeSignal()).ProcessPending();

        Assert.Equal(1, dialog.CallCount);
        Assert.Contains(store.ReadInteractionResponses(), response => response.InteractionId == cancelled.InteractionId && response.Outcome == InteractionOutcome.Cancelled);
    }

    [Fact]
    public void Restart_reprocesses_an_unanswered_request_but_two_startup_passes_show_it_once()
    {
        var store = Store();
        store.WriteInteractionRequest(Request());
        new InteractionRequestProcessor(store, new RecordingDialog(new InteractionDialogResult(InteractionOutcome.Selected, "ignored")), new PredicateValidator(_ => false), new RecordingWakeSignal()).ProcessPending();
        var dialogAfterRestart = new RecordingDialog(new InteractionDialogResult(InteractionOutcome.Selected, @"K:\game.exe"));
        var restarted = new InteractionRequestProcessor(store, dialogAfterRestart, new PredicateValidator(_ => true), new RecordingWakeSignal());

        restarted.ProcessPending();
        restarted.ProcessPending();

        Assert.Equal(1, dialogAfterRestart.CallCount);
        Assert.Single(store.ReadInteractionResponses());
    }

    [Fact]
    public void Invalid_or_superseded_requests_are_ignored_while_a_valid_replacement_is_processed()
    {
        var store = Store();
        var superseded = Request();
        var mismatched = Request();
        var replacement = Request();
        store.WriteInteractionRequest(superseded);
        store.WriteInteractionRequest(mismatched);
        store.WriteInteractionRequest(replacement);
        var dialog = new RecordingDialog(new InteractionDialogResult(InteractionOutcome.Confirmed, null));

        new InteractionRequestProcessor(store, dialog, new PredicateValidator(request => request.InteractionId == replacement.InteractionId), new RecordingWakeSignal()).ProcessPending();

        Assert.Equal(1, dialog.CallCount);
        Assert.Equal(replacement.InteractionId, Assert.Single(store.ReadInteractionResponses()).InteractionId);
    }

    private AtomicTransportFileStore Store() => new(new TransportPaths(root));
    private static PlayniteInteractionRequest Request() => new(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), @"K:\game.rar", "fingerprint", InteractionKind.SelectGameExecutable, "Caption", "Purpose", @"K:\ゲーム", "Executable files|*.exe", DateTimeOffset.UtcNow);
    private static PlayniteInteractionResponse Response(PlayniteInteractionRequest request, InteractionOutcome outcome) => new(1, request.InteractionId, request.RequestId, request.WorkflowId, request.PlayniteGameId, request.Kind, outcome, null, DateTimeOffset.UtcNow);

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private sealed class RecordingDialog(InteractionDialogResult result) : IInteractionDialog
    {
        public int CallCount { get; private set; }
        public InteractionDialogResult Show(PlayniteInteractionRequest request) { CallCount++; return result; }
    }

    private sealed class PredicateValidator(Func<PlayniteInteractionRequest, bool> predicate) : IInteractionRequestValidator
    {
        public bool CanHandle(PlayniteInteractionRequest request) => predicate(request);
    }

    private sealed class RecordingWakeSignal : IOrchestratorWakeSignal
    {
        public int CallCount { get; private set; }
        public void Signal() => CallCount++;
    }
}
