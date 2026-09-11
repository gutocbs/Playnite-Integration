namespace Launcher.Abstractions;

public interface ILauncher
{
    Task<LaunchResult> LaunchAsync(
        LaunchRequest request,
        CancellationToken cancellationToken = default);
}
