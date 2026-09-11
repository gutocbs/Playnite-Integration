using Launcher.Abstractions;

namespace Launcher.Host;

public sealed record LauncherRegistration(
    string Name,
    Func<string, ILauncher> CreateLauncher);
