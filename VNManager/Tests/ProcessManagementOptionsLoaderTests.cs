using Launcher.ProcessManagement;

namespace Launcher.Tests;

public sealed class ProcessManagementOptionsLoaderTests
{
    [Fact]
    public void Load_ImportsMonitorGlobalAndExecutableSpecificLists()
    {
        using var files = new TemporaryFiles();
        var configurationPath = files.CreateFile(
            "process-management.json",
            """
            {
              "monitorProcesses": ["rugp.exe", "SiglusEngine.exe", "malie.exe"],
              "preferredProcessDetectionWindowSeconds": 7,
              "globalTimeout": 120,
              "globalCleanupServices": ["UCManSvc"],
              "executables": [
                {
                  "name": "rugp.exe",
                  "timeout": 90,
                  "cleanupServices": ["SdProxyService"]
                }
              ]
            }
            """);

        var options = ProcessManagementOptionsLoader.Load(configurationPath);

        Assert.Equal(["rugp.exe", "SiglusEngine.exe", "malie.exe"], options.MonitorProcesses);
        Assert.Equal(7, options.PreferredProcessDetectionWindowSeconds);
        Assert.Equal(120, options.GlobalTimeout);
        Assert.Equal(["UCManSvc"], options.GlobalCleanupServices);
        var executable = Assert.Single(options.Executables);
        Assert.Equal("rugp.exe", executable.Name);
        Assert.Equal(90, executable.Timeout);
        Assert.Equal(["SdProxyService"], executable.CleanupServices);
    }

    [Fact]
    public void Load_AllowsOmittedOptionalCleanupLists()
    {
        using var files = new TemporaryFiles();
        var configurationPath = files.CreateFile(
            "process-management.json",
            """
            {
              "globalTimeout": 120,
              "executables": [{ "name": "game.exe" }]
            }
            """);

        var options = ProcessManagementOptionsLoader.Load(configurationPath);
        var executable = Assert.Single(options.Executables);

        Assert.Null(executable.CleanupProcesses);
        Assert.Null(executable.CleanupServices);
        Assert.Null(options.GlobalCleanupProcesses);
        Assert.Null(options.GlobalCleanupServices);
    }
}
