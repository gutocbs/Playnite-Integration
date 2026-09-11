using Launcher.Abstractions;

namespace Launcher.Host;

public sealed class LauncherRegistry
{
    private readonly IReadOnlyList<LauncherRegistration> _registrations;

    public LauncherRegistry(IEnumerable<LauncherRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _registrations = registrations.ToArray();
    }

    public ILauncher Resolve(string launcherName, string executionId)
    {
        if (string.IsNullOrWhiteSpace(launcherName) || LooksLikeExternalResource(launcherName))
        {
            throw new LauncherResolutionException(
                "INVALID_LAUNCHER_NAME",
                $"The launcher name is invalid: {launcherName}");
        }

        var matches = _registrations
            .Where(registration => string.Equals(
                registration.Name,
                launcherName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0].CreateLauncher(executionId),
            0 => throw new LauncherResolutionException(
                "LAUNCHER_NOT_FOUND",
                $"No launcher is registered with the name: {launcherName}"),
            _ => throw new LauncherResolutionException(
                "AMBIGUOUS_LAUNCHER",
                $"More than one launcher is registered with the name: {launcherName}")
        };
    }

    private static bool LooksLikeExternalResource(string launcherName) =>
        launcherName.Contains('/') ||
        launcherName.Contains('\\') ||
        launcherName.Contains("..", StringComparison.Ordinal) ||
        launcherName.Contains(':');
}
