// (C)2025 @noio_games
// Thomas van den Berg

namespace noio.MultiBuild
{
public abstract class PreBuildStep : BuildStep
{
    /// <summary>
    ///     Executes/apply this build step. (In the context of a BUILD action)
    ///     There is also a button to ONLY APPLY steps, and not _make_ an actual build.
    /// </summary>
    /// <param name="buildConfig"></param>
    /// <param name="options"></param>
    public abstract void Apply(BuildConfig buildConfig, BuildOptionWrapper options);
}
}