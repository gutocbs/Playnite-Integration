using Launcher.Abstractions;
using Launcher.Host;
using Launcher.Supervisor;

if (!SupervisorArguments.TryParse(args, AppContext.BaseDirectory, out var arguments, out var error))
{
    return Fail(error!);
}

try
{
    var executionId = Guid.NewGuid().ToString("D");
    var launcher = HostComposition
        .CreateRegistry(arguments!.ConfigurationDirectory, arguments.LogPath)
        .Resolve(arguments.LauncherName, executionId);
    var result = await launcher.LaunchAsync(new LaunchRequest(
        executionId,
        Path.GetFullPath(arguments.Executable),
        arguments.GameArguments));

    return result.Success ? 0 : Fail(result.Error?.Message ?? "Falha ao iniciar o jogo.");
}
catch (Exception exception)
{
    return Fail(exception.Message);
}

static int Fail(string message)
{
    Console.Error.WriteLine($"LauncherSupervisor: {message}");
    return 1;
}
