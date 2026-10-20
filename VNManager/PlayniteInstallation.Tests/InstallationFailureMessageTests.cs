using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class InstallationFailureMessageTests
{
    [Fact]
    public void Includes_the_orchestrator_reason_below_the_summary()
    {
        var message = InstallationFailureMessage.Format(
            "A instalação não foi concluída.",
            "  VirtualCloneDrive has 1 virtual drive(s), but this game needs 2.  ");

        Assert.Equal(
            "A instalação não foi concluída.\n\nMotivo: VirtualCloneDrive has 1 virtual drive(s), but this game needs 2.",
            message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Points_to_the_orchestrator_log_when_no_reason_was_sent(string? reason)
    {
        var message = InstallationFailureMessage.Format("A instalação não foi concluída.", reason);

        Assert.Equal("A instalação não foi concluída.\n\nConsulte o log do Orchestrator para mais detalhes.", message);
    }
}
