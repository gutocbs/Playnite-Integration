using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;
using VnManager.PlaynitePlugin.Installation;

namespace VnManager.PlaynitePlugin;

public sealed class VnManagerPlugin : GenericPlugin
{
    private readonly ILogger _logger = LogManager.GetLogger(nameof(VnManagerPlugin));
    private readonly PluginInstallWorkflowCoordinator _installCoordinator;
    public override Guid Id => Guid.Parse("0e1b3f2c-56f4-4eaf-bb7b-7f2c02aa5c5a");

    public VnManagerPlugin(IPlayniteAPI api) : base(api)
    {
        InstallPrivateAssemblyResolver();
        var transportRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VNOrchestrator", "Transport");
        var orchestratorPath = Path.Combine(GetPluginUserDataPath(), "VNInstallOrchestrator.exe");
        _installCoordinator = new PluginInstallWorkflowCoordinator(api, transportRoot, orchestratorPath);
    }

    private static void InstallPrivateAssemblyResolver()
    {
        var extensionDirectory = Path.GetDirectoryName(typeof(VnManagerPlugin).Assembly.Location);
        if (string.IsNullOrWhiteSpace(extensionDirectory))
        {
            return;
        }

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            var requestedAssembly = new AssemblyName(args.Name);
            var candidatePath = Path.Combine(extensionDirectory, requestedAssembly.Name + ".dll");
            if (!File.Exists(candidatePath))
            {
                return null;
            }

            try
            {
                return Assembly.LoadFrom(candidatePath);
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        };
    }

    public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
    {
        if (_installCoordinator.IsEligible(args.Game))
        {
            yield return new OrchestratorInstallController(args.Game, _installCoordinator);
        }
    }

    public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
    {
        _installCoordinator.StartResultSynchronization();
    }

    public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
    {
        _installCoordinator.Dispose();
    }

    public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
    {
        _logger.Debug($"Evaluating dynamic Play Action for game '{args.Game.Name}' ({args.Game.Id}).");
        var launcher = args.Game.Features.FirstOrDefault(feature =>
            feature.Name.StartsWith("[Launcher]", StringComparison.OrdinalIgnoreCase));
        if (launcher is null)
        {
            _logger.Debug("No [Launcher] feature found; no action injected.");
            yield break;
        }

        // The legacy PowerShell action must not be used as the executable source.
        // The plugin wraps the game's real File action with LauncherSupervisor.
        var action = args.Game.GameActions.FirstOrDefault(item =>
            item.Type == GameActionType.File && !string.IsNullOrWhiteSpace(item.Path));
        if (action is null || string.IsNullOrWhiteSpace(action.Path))
        {
            _logger.Warn($"Launcher feature found but no File action exists for '{args.Game.Name}'.");
            yield break;
        }
        action = PlayniteApi.ExpandGameVariables(args.Game, action);

        var supervisor = Path.Combine(GetPluginUserDataPath(), "LauncherSupervisor.exe");
        var executable = action.Path;
        var originalArguments = action.Arguments;

        // During migration the manually configured action may already point to
        // LauncherSupervisor. Unwrap it instead of launching a supervisor that
        // launches another supervisor.
        if (string.Equals(Path.GetFileName(executable), "LauncherSupervisor.exe", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(originalArguments ?? string.Empty, "--executable\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                executable = match.Groups[1].Value;
                var separator = originalArguments.IndexOf(" -- ", StringComparison.Ordinal);
                originalArguments = separator >= 0 ? originalArguments.Substring(separator + 4) : string.Empty;
                _logger.Debug($"Unwrapped existing supervisor action. Executable='{executable}', Arguments='{originalArguments}'.");
            }
        }

        var supervisorArguments = $"--executable \"{executable}\" --launcher \"{launcher.Name.Substring(10).Trim()}\"";
        if (!string.IsNullOrWhiteSpace(originalArguments))
        {
            supervisorArguments += $" -- {originalArguments}";
        }

        if (!File.Exists(supervisor))
        {
            _logger.Error($"LauncherSupervisor não encontrado: '{supervisor}'. Instale o conteúdo completo da pasta de saída do plugin.");
            yield break;
        }

        _logger.Info($"Injecting supervisor action for '{args.Game.Name}'. Supervisor='{supervisor}', Executable='{action.Path}', Arguments='{supervisorArguments}'.");

        var controller = new AutomaticPlayController(args.Game)
        {
            Name = "VNManager",
            Type = AutomaticPlayActionType.File,
            Path = supervisor,
            Arguments = supervisorArguments,
            WorkingDir = Path.GetDirectoryName(supervisor),
            TrackingMode = TrackingMode.Process
        };
        yield return controller;
    }
}

internal sealed class OrchestratorInstallController : InstallController
{
    private readonly PluginInstallWorkflowCoordinator coordinator;

    public OrchestratorInstallController(Game game, PluginInstallWorkflowCoordinator coordinator) : base(game)
    {
        this.coordinator = coordinator;
        Name = "VN Orchestrator";
    }

    public override void Install(InstallActionArgs args)
    {
        coordinator.Dispatch(Game, Complete, Cancel);
    }

    private void Complete(string installationDirectory) => InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData { InstallDirectory = installationDirectory }));

    private void Cancel() => InvokeOnInstallationCancelled(new GameInstallationCancelledEventArgs());
}
