using VNInstallation.Protocol.Contracts;

namespace VnManager.PlayniteInstallation.Domain;

public enum ResultAcknowledgementStatus
{
    Pending,
    AlreadyApplied,
    Conflict
}

public static class ResultAcknowledgementPolicy
{
    public static ResultAcknowledgementStatus Evaluate(
        InstalledGameResult result,
        PlayniteAcknowledgement? acknowledgement)
    {
        if (acknowledgement is null)
        {
            return ResultAcknowledgementStatus.Pending;
        }

        return acknowledgement.SchemaVersion == result.SchemaVersion &&
               acknowledgement.RequestId == result.RequestId &&
               acknowledgement.WorkflowId == result.WorkflowId &&
               acknowledgement.PlayniteGameId == result.PlayniteGameId
            ? ResultAcknowledgementStatus.AlreadyApplied
            : ResultAcknowledgementStatus.Conflict;
    }
}
