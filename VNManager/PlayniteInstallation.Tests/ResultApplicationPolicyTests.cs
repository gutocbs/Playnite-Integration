using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
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

    [Fact]
    public void Evaluate_accepts_an_idempotent_retry_after_the_game_update_was_persisted_before_acknowledgement()
    {
        var result = Result();
        var current = Current() with
        {
            IsInstalled = true,
            InstallDirectory = result.InstallationDirectory,
            GameActions = Current().GameActions
                .Append(new GameActionSnapshot(
                    ResultApplicationPolicy.ManagedActionName,
                    result.ExecutablePath,
                    result.WorkingDirectory))
                .ToArray()
        };

        var decision = ResultApplicationPolicy.Evaluate(current, result);

        Assert.True(decision.IsApplicable);
        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.True(mutation.IsInstalled);
        Assert.Equal(result.InstallationDirectory, mutation.InstallDirectory);
        Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName);
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
                .Append(new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, @"D:\\Another.exe", @"D:\\"))
                .Append(new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, @"D:\\Third.exe", @"D:\\"))
                .ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(expected))
        };

        var decision = ResultApplicationPolicy.Evaluate(candidate, result);

        Assert.False(decision.IsApplicable);
        Assert.Equal(expected, decision.Conflict);
    }

    [Theory]
    [InlineData(ResultApplicationConflict.RequestMismatch)]
    [InlineData(ResultApplicationConflict.SourceFingerprintMismatch)]
    [InlineData(ResultApplicationConflict.DuplicateManagedAction)]
    public void Evaluate_refuses_each_unsafe_case_for_an_installed_game(ResultApplicationConflict expected)
    {
        var result = Result();
        var installed = Current() with { IsInstalled = true, InstallDirectory = result.InstallationDirectory };
        var candidate = expected switch
        {
            ResultApplicationConflict.RequestMismatch => installed with { PendingRequestId = Guid.NewGuid() },
            ResultApplicationConflict.SourceFingerprintMismatch => installed with { ExpectedSourceFingerprint = "changed" },
            ResultApplicationConflict.DuplicateManagedAction => installed with { GameActions = installed.GameActions
                .Append(new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, result.ExecutablePath, result.WorkingDirectory))
                .Append(new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, @"D:\\Third.exe", @"D:\\"))
                .ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(expected))
        };

        var decision = ResultApplicationPolicy.Evaluate(candidate, result);

        Assert.False(decision.IsApplicable);
        Assert.Equal(expected, decision.Conflict);
    }

    [Fact]
    public void Evaluate_marks_a_new_managed_action_as_the_play_action_without_changing_unrelated_actions()
    {
        var unrelated = new GameActionSnapshot("Unrelated action", @"C:\\Tool.exe", @"C:\\", true);
        var decision = ResultApplicationPolicy.Evaluate(CurrentWith(unrelated), Result());

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.True(Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName).IsPlayAction);
        Assert.Equal(unrelated, Assert.Single(mutation.GameActions, action => action.Name == unrelated.Name));
    }

    [Fact]
    public void Evaluate_does_not_mark_a_new_managed_action_as_play_when_a_launcher_feature_exists()
    {
        var current = CurrentWith() with { HasLauncherFeature = true };

        var decision = ResultApplicationPolicy.Evaluate(current, Result());

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.False(Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName).IsPlayAction);
    }

    [Fact]
    public void Evaluate_corrects_an_existing_managed_action_in_place_during_an_idempotent_retry()
    {
        var result = Result();
        var managed = new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, result.ExecutablePath, result.WorkingDirectory, false);
        var unrelated = new GameActionSnapshot("Unrelated action", @"C:\\Tool.exe", @"C:\\", true);
        var current = CurrentWith(unrelated, managed) with
        {
            IsInstalled = true,
            InstallDirectory = result.InstallationDirectory
        };

        var decision = ResultApplicationPolicy.Evaluate(current, result);

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName && action.IsPlayAction);
        Assert.Equal(unrelated, Assert.Single(mutation.GameActions, action => action.Name == unrelated.Name));
    }

    [Fact]
    public void Evaluate_clears_the_managed_play_action_on_retry_when_a_launcher_feature_exists()
    {
        var result = Result();
        var managed = new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, result.ExecutablePath, result.WorkingDirectory, true);
        var current = CurrentWith(managed) with
        {
            IsInstalled = true,
            InstallDirectory = result.InstallationDirectory,
            HasLauncherFeature = true
        };

        var decision = ResultApplicationPolicy.Evaluate(current, result);

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.False(Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName).IsPlayAction);
    }

    private static CurrentGameSnapshot CurrentWith(params GameActionSnapshot[] actions) =>
        Current() with { GameActions = actions, Metadata = new Dictionary<string, string>() };

    private static CurrentGameSnapshot Current() => new(
        Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"),
        false,
        @"K:\\Game.rar",
        Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"),
        "fingerprint",
        new[] { new GameActionSnapshot("Unrelated action", @"C:\\Tool.exe", @"C:\\") },
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
