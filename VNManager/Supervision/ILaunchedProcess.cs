namespace Launcher.Supervision;

public interface ILaunchedProcess : IDisposable
{
    int Id { get; }
}
