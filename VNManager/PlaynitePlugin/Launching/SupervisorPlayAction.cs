using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK.Models;

namespace VnManager.PlaynitePlugin.Launching;

public static class SupervisorPlayAction
{
    public const string LauncherFeaturePrefix = "[Launcher]";

    /// <summary>Returns the launcher name of the first [Launcher] feature, or null when there is none.</summary>
    public static string FindLauncherName(IEnumerable<string> featureNames)
    {
        var feature = featureNames.FirstOrDefault(name =>
            name != null && name.StartsWith(LauncherFeaturePrefix, StringComparison.OrdinalIgnoreCase));

        return feature?.Substring(LauncherFeaturePrefix.Length).Trim();
    }

    /// <summary>Returns the first File action with a path; it is the source of the game executable.</summary>
    public static GameAction FindFileAction(IEnumerable<GameAction> actions) =>
        actions.FirstOrDefault(action =>
            action.Type == GameActionType.File && !string.IsNullOrWhiteSpace(action.Path));

    public static string BuildArguments(string executable, string launcherName, string originalArguments)
    {
        var arguments = $"--executable \"{executable}\" --launcher \"{launcherName}\"";
        return string.IsNullOrWhiteSpace(originalArguments)
            ? arguments
            : $"{arguments} -- {originalArguments}";
    }
}
