using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class ManagedPlayActionPolicyTests
{
    [Fact]
    public void Evaluate_marks_a_new_managed_action_as_the_play_action_without_changing_unrelated_actions()
    {
        var unrelated = new GameActionSnapshot("Unrelated action", @"C:\\Tool.exe", @"C:\\", DirectGameActionType.File, true);
        var decision = ResultApplicationPolicy.Evaluate(Current(unrelated), Result());

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.True(Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName).IsPlayAction);
        Assert.Equal(unrelated, Assert.Single(mutation.GameActions, action => action.Name == unrelated.Name));
    }

    [Fact]
    public void Evaluate_does_not_mark_a_new_managed_action_as_play_when_a_launcher_feature_exists()
    {
        var current = Current() with { HasLauncherFeature = true };

        var decision = ResultApplicationPolicy.Evaluate(current, Result());

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.False(Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName).IsPlayAction);
    }

    [Fact]
    public void Evaluate_corrects_an_existing_managed_action_in_place_during_an_idempotent_retry()
    {
        var result = Result();
        var managed = new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, result.ExecutablePath, result.WorkingDirectory, DirectGameActionType.File, false);
        var unrelated = new GameActionSnapshot("Unrelated action", @"C:\\Tool.exe", @"C:\\", DirectGameActionType.File, true);
        var current = Current(unrelated, managed) with
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
        var managed = new GameActionSnapshot(ResultApplicationPolicy.ManagedActionName, result.ExecutablePath, result.WorkingDirectory, DirectGameActionType.File, true);
        var current = Current(managed) with
        {
            IsInstalled = true,
            InstallDirectory = result.InstallationDirectory,
            HasLauncherFeature = true
        };

        var decision = ResultApplicationPolicy.Evaluate(current, result);

        var mutation = Assert.IsType<GameUpdateMutation>(decision.Mutation);
        Assert.False(Assert.Single(mutation.GameActions, action => action.Name == ResultApplicationPolicy.ManagedActionName).IsPlayAction);
    }

    private static CurrentGameSnapshot Current(params GameActionSnapshot[] actions) => new(
        Guid.Parse("7604d65c-cb66-4fba-83c3-b1459f754f53"),
        false,
        @"K:\\Game.rar",
        Guid.Parse("5b1af416-9e33-4c67-8e15-014d9f630b57"),
        "fingerprint",
        actions,
        new Dictionary<string, string>());

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
