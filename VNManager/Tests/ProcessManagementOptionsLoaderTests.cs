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
              "globalTimeout": 120,
              "globalCleanupProcesses": ["UCManSvc.exe"],
              "executables": [
                {
                  "name": "rugp.exe",
                  "timeout": 90,
                  "cleanupProcesses": ["SdProxy.exe"]
                }
              ]
            }
            """);

        var options = ProcessManagementOptionsLoader.Load(configurationPath);

        Assert.Equal(["rugp.exe", "SiglusEngine.exe", "malie.exe"], options.MonitorProcesses);
        Assert.Equal(120, options.GlobalTimeout);
        Assert.Equal(["UCManSvc.exe"], options.GlobalCleanupProcesses);
        var executable = Assert.Single(options.Executables);
        Assert.Equal("rugp.exe", executable.Name);
        Assert.Equal(90, executable.Timeout);
        Assert.Equal(["SdProxy.exe"], executable.CleanupProcesses);
    }
}
