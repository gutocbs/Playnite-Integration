using Launcher.LocaleEmulator;
using Launcher.Logging;
using Launcher.NoRegionLoader;
using Launcher.ProcessManagement;

namespace Launcher.Host;

public static class HostComposition
{
    public static LauncherRegistry CreateRegistry(
        string configurationDirectory,
        string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        var configurationPath = Path.Combine(
            configurationDirectory,
            "locale-emulator.json");
        var processManagementConfigurationPath = Path.Combine(
            configurationDirectory,
            "process-management.json");
        var noRegionLoaderConfigurationPath = Path.Combine(
            configurationDirectory,
            "no-region-loader.json");
        var options = LocaleEmulatorOptionsLoader.Load(configurationPath);
        var noRegionLoaderOptions = NoRegionLoaderOptionsLoader.Load(
            noRegionLoaderConfigurationPath);
        var processManagementOptions = ProcessManagementOptionsLoader.Load(
            processManagementConfigurationPath);

        return new LauncherRegistry(
        [
            new LauncherRegistration(
                "Locale Emulator",
                executionId =>
                {
                    IAppLogger logger = new AppLogger(executionId, logPath);

                    return new LocaleEmulatorLauncher(
                        options,
                        new LocaleEmulatorProcessStarter(),
                        new ProcessMonitor(processManagementOptions, logger),
                        new ProcessCleanup(processManagementOptions, logger),
                        logger);
                }),
            new LauncherRegistration(
                "NoRegionLoader",
                executionId =>
                {
                    IAppLogger logger = new AppLogger(executionId, logPath);

                    return new NoRegionLoaderLauncher(
                        noRegionLoaderOptions,
                        new NoRegionLoaderProcessStarter(),
                        new ProcessMonitor(processManagementOptions, logger),
                        new ProcessCleanup(processManagementOptions, logger),
                        logger);
                })
        ]);
    }
}
