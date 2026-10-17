using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class ResultCompletionSequenceTests
{
    [Fact]
    public void Completes_the_active_install_controller_before_updating_and_acknowledging()
    {
        var calls = new List<string>();

        var controllerCompleted = ResultCompletionSequence.Apply(
            () => { calls.Add("controller"); return true; },
            () => calls.Add("game"),
            () => calls.Add("acknowledgement"));

        Assert.True(controllerCompleted);
        Assert.Equal(["controller", "game", "acknowledgement"], calls);
    }

    [Fact]
    public void Leaves_the_game_and_acknowledgement_untouched_when_controller_completion_fails()
    {
        var gameUpdated = false;
        var acknowledgementWritten = false;

        Assert.Throws<InvalidOperationException>(() => ResultCompletionSequence.Apply(
            () => throw new InvalidOperationException("Playnite rejected completion."),
            () => gameUpdated = true,
            () => acknowledgementWritten = true));

        Assert.False(gameUpdated);
        Assert.False(acknowledgementWritten);
    }

    [Fact]
    public void Applies_and_acknowledges_when_no_live_install_controller_exists()
    {
        var gameUpdated = false;
        var acknowledgementWritten = false;

        var controllerCompleted = ResultCompletionSequence.Apply(
            () => false,
            () => gameUpdated = true,
            () => acknowledgementWritten = true);

        Assert.False(controllerCompleted);
        Assert.True(gameUpdated);
        Assert.True(acknowledgementWritten);
    }
}
