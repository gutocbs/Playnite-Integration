namespace VnManager.PlayniteInstallation.Transport;

public sealed class TransportPaths(string root)
{
    public string InboxDirectory => Path.Combine(root, "inbox");
    public string OutboxDirectory => Path.Combine(root, "outbox");
    public string AcknowledgementsDirectory => Path.Combine(root, "acknowledgements");
    public string RequestPath(Guid requestId) => Path.Combine(InboxDirectory, requestId + ".json");
    public string ResultPath(Guid requestId) => Path.Combine(OutboxDirectory, requestId + ".result.json");
    public string AcknowledgementPath(Guid requestId) => Path.Combine(AcknowledgementsDirectory, requestId + ".ack.json");
}
