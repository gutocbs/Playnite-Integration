using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace VnManager.PlaynitePlugin;

public sealed class VnManagerPlugin : GenericPlugin
{
    private readonly ILogger _logger = LogManager.GetLogger(nameof(VnManagerPlugin));
    public override Guid Id => Guid.Parse("0e1b3f2c-56f4-4eaf-bb7b-7f2c02aa5c5a");

    public VnManagerPlugin(IPlayniteAPI api) : base(api) { }

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
