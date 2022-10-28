using System;
using UnityEditor;
using UnityEngine;

namespace noio.MultiBuild
{
    [Serializable]
    [BuildStep(BuildStepOrder.PreBuild, false)]
    public class BuildStepDevelopmentBuild : BuildStep
    {
        #region PUBLIC AND SERIALIZED FIELDS

        [SerializeField] bool _scriptDebugging = true;
        [SerializeField] bool _deepProfiling = true;
        [SerializeField] bool _waitForManagedDebugger;

        #endregion

        protected override BuildStepResult Apply(BuildConfig buildConfig, BuildOptionWrapper options)
        {
            Debug.Log($"Applying {this}");

            options.Options |= BuildOptions.Development;
            if (_scriptDebugging)
            {
                options.Options |= BuildOptions.AllowDebugging;
            }

            if (_deepProfiling)
            {
                options.Options |= BuildOptions.EnableDeepProfilingSupport;
            }

            EditorUserBuildSettings.waitForManagedDebugger = _waitForManagedDebugger;

            return default;
        }
    }
}