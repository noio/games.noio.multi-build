using System;

namespace noio.MultiBuild
{
public readonly struct BuildStepMessage
{
    public BuildStepMessage(Severity severity = Severity.Error, string message = null, Action fixAction = default , string fixActionName = "Fix")
    {
        Severity = severity;
        Message = message;
        FixAction = fixAction;
        FixActionName = fixActionName;
    }

    public Severity Severity { get; }
    public string Message { get; }
    public Action FixAction { get; }
    public string FixActionName { get; }
}

public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2
}
}