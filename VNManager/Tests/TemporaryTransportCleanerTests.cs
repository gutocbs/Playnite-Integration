using Launcher.Host;

namespace Launcher.Tests;

public sealed class TemporaryTransportCleanerTests
{
    [Fact]
    public async Task CleanupAsync_RemovesOnlyExpiredExecutionDirectories()
    {
        using var files = new TemporaryFiles();
        var currentDirectory = CreateExecutionDirectory(files.DirectoryPath);
        var expiredDirectory = CreateExecutionDirectory(files.DirectoryPath);
        var freshDirectory = CreateExecutionDirectory(files.DirectoryPath);
        var unrelatedDirectory = Path.Combine(files.DirectoryPath, "not-an-execution-id");
        Directory.CreateDirectory(unrelatedDirectory);
        var expiredAt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(2));
        File.SetLastWriteTimeUtc(Path.Combine(expiredDirectory, "request.json"), expiredAt);
        Directory.SetLastWriteTimeUtc(expiredDirectory, expiredAt);

        await TemporaryTransportCleaner.CleanupAsync(
            files.DirectoryPath,
            currentDirectory,
            TimeSpan.FromHours(1),
            TestContext.Current.CancellationToken);

        Assert.True(Directory.Exists(currentDirectory));
        Assert.False(Directory.Exists(expiredDirectory));
        Assert.True(Directory.Exists(freshDirectory));
        Assert.True(Directory.Exists(unrelatedDirectory));
    }

    private static string CreateExecutionDirectory(string root)
    {
        var directory = Path.Combine(root, Guid.NewGuid().ToString("D"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "request.json"), "{}");
        return directory;
    }
}
