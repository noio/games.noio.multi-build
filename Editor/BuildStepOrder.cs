using System;

[Flags]
public enum BuildStepOrder
{
    PreBuild = 1 << 0,
    PostBuild = 1 << 1
}