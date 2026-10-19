using Launcher.Abstractions;
using Launcher.Logging;
using Launcher.ProcessManagement;
using Launcher.Supervision;

namespace Launcher.LocaleEmulator;

public sealed class LocaleEmulatorLauncher(
    LocaleEmulatorOptions options,
    ILocaleEmulatorProcessStarter processStarter,
    IProcessMonitor processMonitor,
    IProcessCleanup processCleanup,
    IAppLogger logger) : ILauncher
{
    private static readonly SupervisedTool Tool = new(
        "Locale Emulator",
        "LocaleEmulator",
        "LOCALE_EMULATOR_LAUNCH_FAILED",
        "LocaleEmulatorProcessId");

    public async Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return validationError;
        }

        return await SupervisedLaunch.RunAsync(
            Tool,
            request,
            options.PollingInterval,
            () => processStarter.Start(options, request),
            processMonitor,
            processCleanup,
            logger,
            cancellationToken);
    }

    private LaunchResult? Validate(LaunchRequest request)
    {
        var error = LaunchValidation.ValidateGameExecutable(request)
            ?? LaunchValidation.ValidateToolExecutable(
                options.LocaleEmulatorExecutable,
                "LOCALE_EMULATOR_NOT_FOUND",
                Tool.DisplayName);
        if (error is not null)
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(options.ProfileId))
        {
            return LaunchResult.Failed(
                "INVALID_LOCALE_EMULATOR_PROFILE",
                "The Locale Emulator profile identifier is required.");
        }

        return LaunchValidation.ValidatePollingInterval(options.PollingInterval);
    }
}
