namespace VnManager.PlayniteInstallation.Transport;

public interface IOrchestratorLauncher
{
    Task ActivateAsync(CancellationToken cancellationToken);
}
