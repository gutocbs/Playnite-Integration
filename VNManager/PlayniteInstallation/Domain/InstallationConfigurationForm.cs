using System.Security;
using VNInstallation.Protocol.Contracts;

namespace VnManager.PlayniteInstallation.Domain;

public sealed class InstallationConfigurationForm
{
    private InstallationConfigurationForm(string? gameDirectory, string? executablePath)
    {
        GameDirectory = gameDirectory;
        ExecutablePath = executablePath;
    }

    public string? GameDirectory { get; }
    public string? ExecutablePath { get; }

    public static InstallationConfigurationForm Create(InstallationConfigurationPrompt prompt)
    {
        if (prompt is null) throw new ArgumentNullException(nameof(prompt));

        return new InstallationConfigurationForm(prompt.SuggestedGameDirectory, prompt.SuggestedExecutablePath);
    }

    public bool TryConfirm(
        string gameDirectory,
        string executablePath,
        string? extraFilesDirectory,
        out InstallationConfigurationSelection? selection,
        out string? validationError)
    {
        selection = null;
        validationError = null;

        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            validationError = "Selecione a pasta do jogo.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            validationError = "Selecione o executável do jogo.";
            return false;
        }

        if (!TryGetFullPath(gameDirectory, null, out var normalizedGameDirectory))
        {
            validationError = "A pasta do jogo não é válida.";
            return false;
        }

        if (!TryGetFullPath(executablePath, normalizedGameDirectory, out var normalizedExecutablePath))
        {
            validationError = "O caminho do executável não é válido.";
            return false;
        }

        string? normalizedExtraFilesDirectory = null;
        if (!string.IsNullOrWhiteSpace(extraFilesDirectory)
            && !TryGetFullPath(extraFilesDirectory!, null, out normalizedExtraFilesDirectory))
        {
            validationError = "A pasta de arquivos extras não é válida.";
            return false;
        }

        selection = new InstallationConfigurationSelection(
            normalizedGameDirectory!,
            normalizedExecutablePath!,
            normalizedExtraFilesDirectory);
        return true;
    }

    private static bool TryGetFullPath(string path, string? relativeTo, out string? fullPath)
    {
        try
        {
            var trimmed = path.Trim();
            fullPath = Path.GetFullPath(Path.IsPathRooted(trimmed) || relativeTo is null
                ? trimmed
                : Path.Combine(relativeTo, trimmed));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           || exception is NotSupportedException
                                           || exception is PathTooLongException
                                           || exception is SecurityException)
        {
            fullPath = null;
            return false;
        }
    }
}
