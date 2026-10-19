using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class ResultAcknowledgementPolicyTests
{
    [Fact]
    public void Evaluate_keeps_an_unacknowledged_result_pending()
    {
        Assert.Equal(ResultAcknowledgementStatus.Pending, ResultAcknowledgementPolicy.Evaluate(Result(), null));
    }

    [Fact]
    public void Evaluate_recognizes_a_matching_acknowledgement_as_already_applied()
    {
        var result = Result();
        var acknowledgement = new PlayniteAcknowledgement(
            result.SchemaVersion,
            result.RequestId,
            result.WorkflowId,
            result.PlayniteGameId,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            ResultAcknowledgementStatus.AlreadyApplied,
            ResultAcknowledgementPolicy.Evaluate(result, acknowledgement));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Evaluate_rejects_an_acknowledgement_with_different_correlation(
        bool schemaDiffers,
        bool requestDiffers,
        bool workflowDiffers,
        bool gameDiffers)
    {
        var result = Result();
        var acknowledgement = new PlayniteAcknowledgement(
            schemaDiffers ? result.SchemaVersion + 1 : result.SchemaVersion,
            requestDiffers ? Guid.NewGuid() : result.RequestId,
            workflowDiffers ? Guid.NewGuid() : result.WorkflowId,
            gameDiffers ? Guid.NewGuid() : result.PlayniteGameId,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            ResultAcknowledgementStatus.Conflict,
            ResultAcknowledgementPolicy.Evaluate(result, acknowledgement));
    }

    private static InstalledGameResult Result() => new(
        ProtocolV1.SchemaVersion,
        Guid.Parse("97cae31f-2389-4380-9083-734e9ada8459"),
        Guid.Parse("6f902583-9c48-4459-887e-c44eef3d61d2"),
        Guid.Parse("d86384e5-c9ab-4111-91d6-ef96fbeaec9f"),
        @"K:\Game.rar",
        "fingerprint",
        @"D:\Library\Game",
        @"D:\Library\Game\Game.exe",
        @"D:\Library\Game",
        new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero));
}
