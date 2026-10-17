using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class PendingInstallCompletionRegistryTests
{
    [Fact]
    public void Completes_the_controller_registered_for_the_applied_game_once()
    {
        var registry = new PendingInstallCompletionRegistry();
        var gameId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        string? completedDirectory = null;
        registry.Register(requestId, gameId, directory => completedDirectory = directory);

        var first = registry.TryComplete(requestId, gameId, @"D:\\Library\\Game");
        var second = registry.TryComplete(requestId, gameId, @"D:\\Library\\Other");

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(@"D:\\Library\\Game", completedDirectory);
    }
}
