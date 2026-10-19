using System.Linq;
using Playnite.SDK.Models;
using VnManager.PlaynitePlugin.Launching;

namespace VnManager.PlaynitePlugin.Tests;

public sealed class SupervisorPlayActionTests
{
    [Fact]
    public void FindLauncherName_ReturnsNull_When_NoLauncherFeature()
    {
        Assert.Null(SupervisorPlayAction.FindLauncherName(new[] { "Action", "Visual Novel" }));
    }

    [Fact]
    public void FindLauncherName_TrimsNameAfterPrefix()
    {
        Assert.Equal(
            "Locale Emulator",
            SupervisorPlayAction.FindLauncherName(new[] { "Other", "[launcher]  Locale Emulator " }));
    }

    [Fact]
    public void FindLauncherName_KeepsEmptyName_When_PrefixHasNoName()
    {
        Assert.Equal(string.Empty, SupervisorPlayAction.FindLauncherName(new[] { "[Launcher]" }));
    }

    [Fact]
    public void FindFileAction_SkipsNonFileAndEmptyPathActions()
    {
        var expected = new GameAction { Type = GameActionType.File, Path = @"C:\G\g.exe" };
        var actions = new[]
        {
            new GameAction { Type = GameActionType.Script, Path = @"C:\s.ps1" },
            new GameAction { Type = GameActionType.File, Path = "" },
            expected
        };

        Assert.Same(expected, SupervisorPlayAction.FindFileAction(actions));
    }

    [Fact]
    public void BuildArguments_AppendsOriginalArgumentsAfterSeparator()
    {
        Assert.Equal(
            "--executable \"C:\\G\\g.exe\" --launcher \"NoRegionLoader\" -- -w 1",
            SupervisorPlayAction.BuildArguments(@"C:\G\g.exe", "NoRegionLoader", "-w 1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildArguments_OmitsSeparator_When_NoOriginalArguments(string? original)
    {
        Assert.Equal(
            "--executable \"C:\\G\\g.exe\" --launcher \"Locale Emulator\"",
            SupervisorPlayAction.BuildArguments(@"C:\G\g.exe", "Locale Emulator", original));
    }
}
