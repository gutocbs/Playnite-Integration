namespace Launcher.Logging;

public interface IAppLogger
{
    void Write(
        LogLevel level,
        string component,
        string message,
        IReadOnlyDictionary<string, object?>? data = null);
}
