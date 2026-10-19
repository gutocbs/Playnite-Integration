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

        var localeEmulatorOptions = LocaleEmulatorOptionsLoader.Load(
            Path.Combine(configurationDirectory, "locale-emulator.json"));
        var noRegionLoaderOptions = NoRegionLoaderOptionsLoader.Load(
            Path.Combine(configurationDirectory, "no-region-loader.json"));
        var processManagementOptions = ProcessManagementOptionsLoader.Load(
            Path.Combine(configurationDirectory, "process-management.json"));

        (IAppLogger Logger, ProcessMonitor Monitor, ProcessCleanup Cleanup) CreateSupervision(string executionId)
        {
            IAppLogger logger = new AppLogger(executionId, logPath);
            return (
                logger,
                new ProcessMonitor(processManagementOptions, logger),
                new ProcessCleanup(processManagementOptions, logger));
        }

        return new LauncherRegistry(
        [
            new LauncherRegistration(
                "Locale Emulator",
                executionId =>
                {
                    var supervision = CreateSupervision(executionId);
                    return new LocaleEmulatorLauncher(
                        localeEmulatorOptions,
                        new LocaleEmulatorProcessStarter(),
                        supervision.Monitor,
                        supervision.Cleanup,
                        supervision.Logger);
                }),
            new LauncherRegistration(
                "NoRegionLoader",
                executionId =>
                {
                    var supervision = CreateSupervision(executionId);
                    return new NoRegionLoaderLauncher(
                        noRegionLoaderOptions,
                        new NoRegionLoaderProcessStarter(),
                        supervision.Monitor,
                        supervision.Cleanup,
                        supervision.Logger);
                })
        ]);
    }
}
