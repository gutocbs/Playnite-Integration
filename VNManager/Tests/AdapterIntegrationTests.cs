using System.Diagnostics;
using System.Text.Json;

namespace Launcher.Tests;

[Collection("Process integration")]
public sealed class AdapterIntegrationTests
{
    [Fact]
    public async Task PlayniteAdapter_ExecutesCompletePowerShellToHostFlow()
    {
        await AssertAdapterFlowAsync(forceLegacyProcessStartInfo: false);
    }

    [Fact]
    public async Task PlayniteAdapter_ExecutesCompleteFlow_WhenArgumentListIsUnavailable()
    {
        await AssertAdapterFlowAsync(forceLegacyProcessStartInfo: true);
    }

    private static async Task AssertAdapterFlowAsync(bool forceLegacyProcessStartInfo)
    {
        using var files = new TemporaryFiles();
        var hostExecutable = TestAsset("Host", "Host.exe");
        var localeEmulatorStub = TestAsset("LocaleEmulatorStub", "VnTestLEProc.exe");
        var gameExecutable = TestAsset("ShortLived", "VnTestShort.exe");
        var adapterPath = TestAsset("Adapter", "PlayniteAdapter.ps1");
        var configurationDirectory = Path.Combine(files.DirectoryPath, "Configuration");
        Directory.CreateDirectory(configurationDirectory);

        File.WriteAllText(
            Path.Combine(configurationDirectory, "host.json"),
            JsonSerializer.Serialize(new
            {
                temporaryDirectoryMaximumAgeSeconds = 86_400
            }));
        File.WriteAllText(
            Path.Combine(configurationDirectory, "locale-emulator.json"),
            JsonSerializer.Serialize(new
            {
                localeEmulatorExecutable = localeEmulatorStub,
                profileId = "adapter-integration-profile",
                pollingInterval = "00:00:00.025"
            }));
        File.WriteAllText(
            Path.Combine(configurationDirectory, "process-management.json"),
            JsonSerializer.Serialize(new
            {
                monitorProcesses = new[] { "VnTestShort.exe" },
                globalTimeout = 10,
                globalCleanupProcesses = Array.Empty<string>(),
                executables = Array.Empty<object>()
            }));

        var adapterConfigurationPath = files.CreateFile(
            "PlayniteAdapter.json",
            JsonSerializer.Serialize(new
            {
                hostExecutable,
                hostConfigurationDirectory = configurationDirectory,
                logPath = Path.Combine(files.DirectoryPath, "launcher.log")
            }));
        var harnessPath = Path.Combine(AppContext.BaseDirectory, "PlayniteAdapterHarness.ps1");
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(harnessPath);
        startInfo.ArgumentList.Add("-AdapterPath");
        startInfo.ArgumentList.Add(adapterPath);
        startInfo.ArgumentList.Add("-AdapterConfigPath");
        startInfo.ArgumentList.Add(adapterConfigurationPath);
        startInfo.ArgumentList.Add("-GameExecutable");
        startInfo.ArgumentList.Add(gameExecutable);
        if (forceLegacyProcessStartInfo)
        {
            startInfo.ArgumentList.Add("-ForceLegacyProcessStartInfo");
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException(
            "Could not start the PowerShell Adapter integration harness.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);

        await process.WaitForExitAsync(timeout.Token);

        var output = await standardOutput;
        var error = await standardError;
        Assert.True(process.ExitCode == 0, $"Exit code: {process.ExitCode}; stderr: {error}; stdout: {output}");
        Assert.Contains("\"Success\":true", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Matched\":true", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Started\":true", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"IsRunning\":false", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(error), error);
        Assert.True(File.Exists(Path.Combine(files.DirectoryPath, "launcher.log")));
    }

    private static string TestAsset(string directory, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", directory, fileName);
}
