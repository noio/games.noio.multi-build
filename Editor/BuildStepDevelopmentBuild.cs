using System;
using System.Collections.Generic;
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

        public override void Validate(BuildConfig buildConfig, List<BuildStepMessage> messages)
        {
            
        }

        public override void Apply(BuildConfig buildConfig, BuildOptionWrapper options)
        {
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

        }
    }
}