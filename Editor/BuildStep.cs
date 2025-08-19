// (C)2025 @noio_games
// Thomas van den Berg

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace noio.MultiBuild
{
public abstract class BuildStep : ScriptableObject
{
    #region SERIALIZED FIELDS

    [SerializeField] bool _active = true;

    #endregion

    #region PROPERTIES

    public bool Active => _active;

    public virtual string DisplayName
    {
        get
        {
            var typeName = GetType().Name;
            if (typeName.StartsWith("BuildStep"))
            {
                typeName = typeName[9..];
            }

            return ObjectNames.NicifyVariableName(typeName);
        }
    }

    #endregion

    /// <summary>
    ///     Validate the prerequisites for this build step.
    ///     If this returns one or more messages with ERROR state,
    ///     the build button is disabled.
    /// </summary>
    /// <param name="buildConfig"></param>
    /// <returns></returns>
    public abstract void Validate(BuildConfig buildConfig, List<BuildStepMessage> messages);

    /// <summary>
    ///     Executes/apply this build step. (In the context of a BUILD action)
    ///     There is also a button to ONLY APPLY steps, and not _make_ an actual build.
    /// </summary>
    /// <param name="buildConfig"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public abstract void Apply(BuildConfig buildConfig, BuildOptionWrapper options);
}
}