namespace VnManager.PlayniteInstallation.Transport;

public interface IResultReadyUiDispatcher
{
    void BeginInvoke(Action action);
}
