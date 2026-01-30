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

    public List<BuildStepValidationResult> ValidationResults { get; } = new();

    #endregion

    /// <summary>
    ///     Runs validation by clearing ValidationResults and calling Validate.
    ///     Called by the editor to check prerequisites before building.
    /// </summary>
    /// <param name="buildConfig"></param>
    public void RunValidation(BuildConfig buildConfig)
    {
        ValidationResults.Clear();
        Validate(buildConfig);
    }

    /// <summary>
    ///     Validate the prerequisites for this build step.
    ///     Add messages to ValidationResults. If any messages have ERROR severity,
    ///     the build button will be disabled.
    /// </summary>
    /// <param name="buildConfig"></param>
    protected abstract void Validate(BuildConfig buildConfig);
}
}
