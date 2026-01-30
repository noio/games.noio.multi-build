// (C)2025 @noio_games
// Thomas van den Berg

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace noio.MultiBuild
{
public abstract class PostBuildStep : BuildStep
{

    /// <summary>
    ///     Executes this post-build step after a target has been built.
    ///     Add messages to ExecutionResults with any issues found.
    /// </summary>
    /// <param name="buildConfig"></param>
    /// <param name="target"></param>
    /// <param name="report"></param>
    public abstract void Execute(
        BuildConfig buildConfig,
        BuildTarget target,
        BuildReport report,
        List<BuildStepValidationResult> results
    );
}
}
