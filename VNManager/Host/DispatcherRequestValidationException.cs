namespace Launcher.Host;

public sealed class DispatcherRequestValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
