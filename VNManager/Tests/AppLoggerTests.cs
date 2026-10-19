using Launcher.Logging;

namespace Launcher.Tests;

public sealed class AppLoggerTests
{
    [Fact]
    public void Write_EscapesMessageAndDataValues()
    {
        using var files = new TemporaryFiles();
        var logPath = Path.Combine(files.DirectoryPath, "launcher.log");

        new AppLogger("exec-1", logPath).Write(
            LogLevel.Info,
            "Component",
            "line1\nline2",
            new Dictionary<string, object?> { ["Path"] = "a;b\\c\nd", ["Missing"] = null });

        var line = Assert.Single(File.ReadAllLines(logPath));
        Assert.Contains("[INFO] [exec-1] [Component] line1\\nline2 | ", line);
        Assert.EndsWith(@"Path=a\;b\\c\nd; Missing=null", line);
    }

    [Fact]
    public void Write_FallsBackToBaseDirectory_When_LogPathHasNoDirectory()
    {
        var fallbackPath = Path.Combine(AppContext.BaseDirectory, $".{Environment.ProcessId}");
        File.Delete(fallbackPath);

        try
        {
            new AppLogger("exec-1", Path.GetPathRoot(AppContext.BaseDirectory)!).Write(
                LogLevel.Warning, "Component", "fallback message");

            Assert.Contains("fallback message", File.ReadAllText(fallbackPath));
        }
        finally
        {
            File.Delete(fallbackPath);
        }
    }
}
