using System;
using UnityEngine;

namespace noio.MultiBuild
{
public class BuildStepValidationResult
{
    public BuildStepValidationResult(
        Severity severity = Severity.Error,
        string message = null,
        Action fixAction = default,
        string fixActionName = "Fix"
    )
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

    public void Log(string prefix = "")
    {
        switch (Severity)
        {
            case Severity.Error:
                Debug.LogError(prefix + Message);
                break;
            case Severity.Warning:
                Debug.LogWarning(prefix + Message);
                break;
            default:
            case Severity.Info:
                Debug.Log(prefix + Message);
                break;
        }
    }
}

public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2
}
}