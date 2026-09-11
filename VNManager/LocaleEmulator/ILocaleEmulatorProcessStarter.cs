using Launcher.Abstractions;

namespace Launcher.LocaleEmulator;

public interface ILocaleEmulatorProcessStarter
{
    ILaunchedProcess Start(LocaleEmulatorOptions options, LaunchRequest request);
}
