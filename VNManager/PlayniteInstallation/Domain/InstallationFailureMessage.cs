namespace VnManager.PlayniteInstallation.Domain;

public static class InstallationFailureMessage
{
    public static string Format(string summary, string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? $"{summary}\n\nConsulte o log do Orchestrator para mais detalhes."
            : $"{summary}\n\nMotivo: {reason!.Trim()}";
}
