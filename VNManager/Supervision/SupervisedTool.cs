namespace Launcher.Supervision;

public sealed record SupervisedTool(
    string DisplayName,
    string Component,
    string FailureCode,
    string ProcessIdDataKey);
