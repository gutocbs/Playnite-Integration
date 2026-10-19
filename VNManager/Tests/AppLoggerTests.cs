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
}
