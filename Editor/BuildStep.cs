using System;
using UnityEditor;
using UnityEngine;
using Utils.Editor;

[Serializable]
public abstract class BuildStep
{
    #region PUBLIC AND SERIALIZED FIELDS

    [SerializeField] bool _active = true;

    #endregion

    #region PROPERTIES

    public bool Active => _active;

    public BuildStepResult LastResult { get; private set; }

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

    public void ApplyStep(BuildConfig buildConfig, BuildOptionWrapper options)
    {
        LastResult = Apply(buildConfig, options);
    }

    protected virtual BuildStepResult Apply(BuildConfig buildConfig, BuildOptionWrapper options)
    {
        return default;
    }

    public void CheckStep(BuildConfig buildConfig)
    {
        LastResult = Check(buildConfig);
    }
    
    protected virtual BuildStepResult Check(BuildConfig buildConfig)
    {
        return default;
    }
}