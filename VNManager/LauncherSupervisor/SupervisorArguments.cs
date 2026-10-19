namespace Launcher.Supervisor;

public sealed record SupervisorArguments(
    string Executable,
    string LauncherName,
    string ConfigurationDirectory,
    string LogPath,
    IReadOnlyList<string> GameArguments)
{
    private const string UsageError =
        "Argumentos inválidos. Use --executable <caminho> [--launcher <nome>] [-- ...argumentos].";

    public static bool TryParse(
        IReadOnlyList<string> args,
        string baseDirectory,
        out SupervisorArguments? arguments,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        arguments = null;
        error = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var gameArguments = new List<string>();
        var separatorFound = false;

        for (var index = 0; index < args.Count; index++)
        {
            if (separatorFound)
            {
                gameArguments.Add(args[index]);
                continue;
            }

            if (args[index] == "--")
            {
                separatorFound = true;
                continue;
            }

            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
            {
                error = UsageError;
                return false;
            }

            values[args[index]] = args[++index];
        }

        if (!values.TryGetValue("--executable", out var executable) || string.IsNullOrWhiteSpace(executable))
        {
            error = "O argumento --executable é obrigatório.";
            return false;
        }

        arguments = new SupervisorArguments(
            executable,
            values.GetValueOrDefault("--launcher", "Locale Emulator"),
            Path.GetFullPath(values.GetValueOrDefault(
                "--configuration-directory",
                Path.Combine(baseDirectory, "Configuration"))),
            Path.GetFullPath(values.GetValueOrDefault(
                "--log",
                Path.Combine(baseDirectory, "launcher.log"))),
            gameArguments);
        return true;
    }
}
