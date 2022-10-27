using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Utils.Editor
{
    [CreateAssetMenu(menuName = "Utils/Build Config")]
    public class BuildConfig : ScriptableObject
    {
        const string NotLoaded = ":NOT_LOADED:";

        #region PUBLIC AND SERIALIZED FIELDS

        [SerializeField] string _outputFolder;
        [SerializeField] string _customPath;
        [SerializeField] List<BuildTarget> _targets;
        [SerializeField] [SerializeReference] List<BuildStep> _steps;

        #endregion

        string _outputFolderOverride = NotLoaded;

        #region PROPERTIES

        public IReadOnlyList<BuildTarget> Targets => _targets;

        /// <summary>
        ///     Returns a folder path if it was set on this local machine.
        ///     Returns null if no override was set.
        /// </summary>
        public string OutputFolderOverride
        {
            get
            {
                if (_outputFolderOverride == NotLoaded)
                {
                    _outputFolderOverride = EditorPrefs.GetString(OutputFolderOverridePrefsKey(), "");
                }

                return _outputFolderOverride;
            }
            set
            {
                _outputFolderOverride = value;
                if (string.IsNullOrEmpty(value))
                {
                    EditorPrefs.DeleteKey(OutputFolderOverridePrefsKey());
                }
                else
                {
                    EditorPrefs.SetString(OutputFolderOverridePrefsKey(), value);
                }
            }
        }

        #endregion

        /// <summary>
        ///     Return the target output folder, which is possibly the local override value
        /// </summary>
        /// <param name="hasLocalOverride"></param>
        /// <returns></returns>
        public string GetOutputFolder(out bool hasLocalOverride)
        {
            if (OutputFolderOverride != null)
            {
                hasLocalOverride = true;
                return OutputFolderOverride;
            }

            hasLocalOverride = false;
            return _outputFolder;
        }

        public string GetPathForTarget(BuildTarget target)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, GetOutputFolder(out _)));

            var data = new Hashtable
            {
                { "date", DateTime.Now.ToString("yyyy-MM-dd") },
                { "name", Application.productName },
                { "version", Application.version },
                { "target", target.ToString() },
                { "buildnum", PlayerSettings.iOS.buildNumber }
            };

            var customPath = _customPath.Inject(data);

            path = Path.Combine(path, customPath);
            path = Path.Combine(path, Application.productName);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    path += ".exe";
                    break;
                case BuildTarget.StandaloneOSX:
                case BuildTarget.iOS:
                case BuildTarget.GameCoreXboxSeries:
                case BuildTarget.GameCoreXboxOne:
                    break;
            }

            return path;
        }

        public void Check()
        {
            foreach (var buildStep in _steps)
            {
                if (buildStep.Active)
                {
                    buildStep.CheckStep(this);
                }
            }
        }

        public void Build()
        {
            /*
             * Check if builds will be overwritten, and confirm with user
             */
            var overwrittenBuilds = _targets.Select(GetPathForTarget).Where(BuildExists).ToList();
            var confirmBuild = true;
            if (overwrittenBuilds.Any())
            {
                var message = "The following builds will be overwritten\n" +
                              string.Join("\n", overwrittenBuilds);
                confirmBuild = EditorUtility.DisplayDialog("Confirm Overwriting Build?", message, "Overwrite",
                    "Cancel");
            }

            if (confirmBuild == false)
            {
                Debug.LogWarning("Building Cancelled");
                return;
            }

            if (int.TryParse(PlayerSettings.iOS.buildNumber, out var buildNum))
            {
                PlayerSettings.iOS.buildNumber = (buildNum + 1).ToString();
            }

            //    _    __   __              __              __      __  ___  __  __   __
            //   /_\  |__) |__) |   \_/    |__) |  | | |   |  \    (__'  |  |__ |__) (__'
            //  /   \ |    |    |__  |     |__) \__/ | |__ |__/    .__)  |  |__ |    .__)
            //
            var options = ApplyBuildSteps();

            /*
             * Re-order the build targets to start with the current target,
             * and avoid a SwitchActiveBuildTarget call (saves time);
             */
            var originalBuildTargetSetting = EditorUserBuildSettings.activeBuildTarget;
            var targetsInOrder = _targets.ToList();
            var currentTargetIdx = targetsInOrder.IndexOf(originalBuildTargetSetting);
            if (currentTargetIdx > -1)
            {
                (targetsInOrder[0], targetsInOrder[currentTargetIdx]) =
                    (targetsInOrder[currentTargetIdx], targetsInOrder[0]);
            }

            /*
             * Do the actual builds
             */
            foreach (var target in targetsInOrder)
            {
                if (target != EditorUserBuildSettings.activeBuildTarget)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target),
                        target);
                }

                AddressableAssetSettings.BuildPlayerContent();

                var path = GetPathForTarget(target);

                Debug.Log($"Building {target} to {path}");

                if (path.Contains("Assets"))
                {
                    throw new Exception($"Refusing to build into any folder named 'Assets': {path}");
                }

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    target = target,
                    locationPathName = path,
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                                                .Select(s => s.path).ToArray(),
                    options = options.Options
                };
                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    Debug.LogError($"Aborting builds because {target} had error");
                    Debug.Log(report.summary.ToString());
                    break;
                }

                var buildTime = report.summary.totalTime.TotalSeconds;
                var buildSize = report.summary.totalSize / 1024 / 1024;
                var timeStamp = DateTime.Now.ToString("HH:mm");
                var color = ColorUtility.ToHtmlStringRGB(new Color(0.6f, 1f, 0.58f));
                ;
                Debug.Log(
                    $"[{timeStamp}] <b><color=#{color}>Build {target} v{Application.version} successful!</color></b> ({buildTime:.0}s) {buildSize}MB. At {path}");
                EditorUtility.ClearProgressBar();
            }

            /*
             * Switch back to previous build target if necessary
             */
            if (originalBuildTargetSetting != EditorUserBuildSettings.activeBuildTarget)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildPipeline.GetBuildTargetGroup(originalBuildTargetSetting),
                    originalBuildTargetSetting);
            }
        }

        public BuildOptionWrapper ApplyBuildSteps()
        {
            var options = new BuildOptionWrapper();

            foreach (var buildStep in _steps)
            {
                if (buildStep.Active)
                {
                    buildStep.ApplyStep(this, options);
                }
            }

            return options;
        }

        /// <summary>
        ///     Does a build exist at the specified path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool BuildExists(string path)
        {
            return File.Exists(path) || Directory.Exists(path + ".app") || Directory.Exists(path);
        }

        string OutputFolderOverridePrefsKey()
        {
            var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(this));
            return $"{nameof(BuildConfig)}.OverrideOutputFolder.{guid}";
        }
    }

    public class BuildOptionWrapper
    {
        #region PROPERTIES

        public BuildOptions Options { get; set; } = BuildOptions.None;

        #endregion
    }

}

//   __              __      __  ___  __  __      __        _    __   __
//  |__) |  | | |   |  \    (__'  |  |__ |__)    /  ` |    /_\  (__' (__'
//  |__) \__/ | |__ |__/    .__)  |  |__ |       \__, |__ /   \ .__) .__)
//

public static class EnumAttributeExtension
{
    public static T GetAttributeOfType<T>(this Enum enumVal) where T : Attribute
    {
        var type = enumVal.GetType();
        var memInfo = type.GetMember(enumVal.ToString());
        var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
        return attributes.Length > 0 ? (T)attributes[0] : null;
    }
}
