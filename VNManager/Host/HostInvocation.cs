namespace Launcher.Host;

public sealed record HostInvocation(
    string RequestPath,
    string ResultPath,
    string StartedPath,
    string LogPath,
    string ConfigurationDirectory,
    string TransportRoot)
{
    public static HostInvocation Parse(string[] arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || !arguments[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Host arguments must be provided as --name value pairs.");
            }

            if (!values.TryAdd(arguments[index], arguments[index + 1]))
            {
                throw new ArgumentException($"Duplicate Host argument: {arguments[index]}");
            }
        }

        var requestPath = Required(values, "--request");
        var resultPath = Required(values, "--result");
        var startedPath = Required(values, "--started");
        var logPath = Required(values, "--log");
        var transportRoot = Required(values, "--transport-root");
        var configurationDirectory = values.GetValueOrDefault(
            "--configuration-directory",
            Path.Combine(AppContext.BaseDirectory, "Configuration"));

        var knownArguments = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--request",
            "--result",
            "--started",
            "--log",
            "--configuration-directory",
            "--transport-root"
        };

        var unknownArgument = values.Keys.FirstOrDefault(key => !knownArguments.Contains(key));
        if (unknownArgument is not null)
        {
            throw new ArgumentException($"Unknown Host argument: {unknownArgument}");
        }

        return new HostInvocation(
            Path.GetFullPath(requestPath),
            Path.GetFullPath(resultPath),
            Path.GetFullPath(startedPath),
            Path.GetFullPath(logPath),
            Path.GetFullPath(configurationDirectory),
            Path.GetFullPath(transportRoot));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Required Host argument was not provided: {name}");
}
