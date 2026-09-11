namespace Launcher.Abstractions;

public sealed record LaunchResult(
    bool Success,
    IReadOnlyDictionary<string, object?>? Data = null,
    LaunchError? Error = null)
{
    public static LaunchResult Succeeded(IReadOnlyDictionary<string, object?>? data = null) =>
        new(true, data);

    public static LaunchResult Failed(string code, string message) =>
        new(false, Error: new LaunchError(code, message));
}
