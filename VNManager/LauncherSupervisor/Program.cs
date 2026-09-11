using Launcher.Abstractions;
using Launcher.Host;

var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var gameArguments = new List<string>();
var separator = false;
for (var i = 0; i < args.Length; i++)
{
    if (separator) { gameArguments.Add(args[i]); continue; }
    if (args[i] == "--") { separator = true; continue; }
    if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
        return Fail("Argumentos inválidos. Use --executable <caminho> [--launcher <nome>] [-- ...argumentos].");
    values[args[i]] = args[++i];
}
if (!values.TryGetValue("--executable", out var executable) || string.IsNullOrWhiteSpace(executable))
    return Fail("O argumento --executable é obrigatório.");
var launcherName = values.GetValueOrDefault("--launcher", "Locale Emulator");
var configurationDirectory = Path.GetFullPath(values.GetValueOrDefault("--configuration-directory", Path.Combine(AppContext.BaseDirectory, "Configuration")));
var logPath = Path.GetFullPath(values.GetValueOrDefault("--log", Path.Combine(AppContext.BaseDirectory, "launcher.log")));
try
{
    var executionId = Guid.NewGuid().ToString("D");
    var launcher = HostComposition.CreateRegistry(configurationDirectory, logPath).Resolve(launcherName, executionId);
    var result = await launcher.LaunchAsync(new LaunchRequest(executionId, Path.GetFullPath(executable), gameArguments));
    return result.Success ? 0 : Fail(result.Error?.Message ?? "Falha ao iniciar o jogo.");
}
catch (Exception exception) { return Fail(exception.Message); }
static int Fail(string message) { Console.Error.WriteLine($"LauncherSupervisor: {message}"); return 1; }
