using VnManager.PlayniteInstallation.Contracts;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class ResultApplicationPolicyTests
{
    [Fact]
    public void Evaluate_creates_the_managed_action_and_preserves_unrelated_data()
    {
        var current = Current();

        var decision = ResultApplicationPolicy.Evaluate(current, Result());

        Assert.True(decision.IsApplicable);
        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.True(mutation.IsInstalled);
        Assert.Equal(@"D:\\Library\\Game", mutation.InstallDirectory);
        Assert.Contains(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName && action.Path == @"D:\\Library\\Game\\Game.exe");
        Assert.Contains(mutation.GameActions, action => action.Name == "Unrelated action");
        Assert.Equal("value", mutation.Metadata["Unrelated metadata"]);
    }

    [Theory]
    [InlineData(ResultApplicationConflict.MissingGame)]
    [InlineData(ResultApplicationConflict.AlreadyInstalled)]
    [InlineData(ResultApplicationConflict.InstallDirectoryChanged)]
    [InlineData(ResultApplicationConflict.RequestMismatch)]
    [InlineData(ResultApplicationConflict.SourceFingerprintMismatch)]
    [InlineData(ResultApplicationConflict.DuplicateManagedAction)]
    public void Evaluate_refuses_each_unsafe_concurrency_case(ResultApplicationConflict expected)
    {
        var current = Current();
        var result = Result();
        CurrentGameSnapshot? candidate = expected switch
        {
            ResultApplicationConflict.MissingGame => null,
            ResultApplicationConflict.AlreadyInstalled => current with { IsInstalled = true },
            ResultApplicationConflict.InstallDirectoryChanged => current with { InstallDirectory = @"K:\\Changed.rar" },
            ResultApplicationConflict.RequestMismatch => current with { PendingRequestId = Guid.NewGuid() },
            ResultApplicationConflict.SourceFingerprintMismatch => current with { ExpectedSourceFingerprint = "changed" },
            ResultApplicationConflict.DuplicateManagedAction => current with { GameActions = current.GameActions
                .Append(new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, @"D:\\Another.exe", @"D:\\", DirectGameActionType.File))
                .Append(new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, @"D:\\Third.exe", @"D:\\", DirectGameActionType.File))
                .ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(expected))
        };

        var decision = ResultApplicationPolicy.Evaluate(candidate, result);

        Assert.False(decision.IsApplicable);
        Assert.Equal(expected, decision.Conflict);
    }

    private static CurrentGameSnapshot Current() => new(
        Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"),
        false,
        @"K:\\Game.rar",
        Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"),
        "fingerprint",
        new[] { new GameActionSnapshot("Unrelated action", @"C:\\Tool.exe", @"C:\\", DirectGameActionType.File) },
        new Dictionary<string, string> { ["Unrelated metadata"] = "value" });

    private static InstalledGameResult Result() => new(
        ProtocolV1.SchemaVersion,
        Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"),
        Guid.Parse("38e83911-ae9e-4471-90ce-48289a53d3f5"),
        Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"),
        @"K:\\Game.rar",
        "fingerprint",
        @"D:\\Library\\Game",
        @"D:\\Library\\Game\\Game.exe",
        @"D:\\Library\\Game",
        new DateTimeOffset(2026, 10, 13, 0, 0, 0, TimeSpan.Zero));
}
