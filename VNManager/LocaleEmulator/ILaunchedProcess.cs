namespace Launcher.LocaleEmulator;

public interface ILaunchedProcess : IDisposable
{
    int Id { get; }
}
