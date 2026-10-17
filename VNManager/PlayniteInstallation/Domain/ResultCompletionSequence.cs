namespace VnManager.PlayniteInstallation.Domain;

public static class ResultCompletionSequence
{
    public static bool Apply(
        Func<bool> completeInstallController,
        Action updateGame,
        Action writeAcknowledgement)
    {
        var controllerCompleted = completeInstallController();
        updateGame();
        writeAcknowledgement();
        return controllerCompleted;
    }
}
