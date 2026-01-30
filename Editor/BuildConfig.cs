// (C)2026 @noio_games
// Thomas van den Berg

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

// using UnityEditor.AddressableAssets.Settings;

namespace noio.MultiBuild
{
[CreateAssetMenu(menuName = "Noio/Multi-Build Config")]
public class BuildConfig : ScriptableObject
{
    #region SERIALIZED FIELDS

    [SerializeField] string _outputFolder;
    [SerializeField] string _customPath = "{date} {name}/{target}";
    [SerializeField] List<BuildTarget> _targets;
    [FormerlySerializedAs("_steps")] [SerializeField] List<PreBuildStep> _preBuildSteps;
    [SerializeField] List<PostBuildStep> _postBuildSteps;

    #endregion

    bool _didLoadEditorPrefs;
    bool _hasOutputFolderOverride;
    string _outputFolderOverride;

    #region PROPERTIES

    public IReadOnlyList<BuildTarget> Targets => _targets;

    public bool HasOutputFolderOverride
    {
        get
        {
            LoadEditorPrefs();
            return _hasOutputFolderOverride;
        }
        set
        {
            _hasOutputFolderOverride = value;
            EditorPrefs.SetBool(HasOutputFolderOverridePrefsKey(), value);
        }
    }

    /// <summary>
    ///     Returns a folder path if it was set on this local machine.
    ///     Returns null if no override was set.
    /// </summary>
    public string OutputFolderOverride
    {
        get
        {
            LoadEditorPrefs();
            return _outputFolderOverride;
        }
        set
        {
            Assert.IsTrue(HasOutputFolderOverride,
                "Set HasOutputFolderOverride to true before setting the path");

            _outputFolderOverride = value;
            EditorPrefs.SetString(OutputFolderOverridePrefsKey(), value);
        }
    }

    #endregion

    /// <summary>
    ///     Return the target output folder, which is possibly the local override value
    /// </summary>
    /// <returns></returns>
    public string GetOutputFolder()
    {
        return HasOutputFolderOverride ? _outputFolderOverride : _outputFolder;
    }

    public string GetPathForTarget(BuildTarget target)
    {
        var path = Path.GetFullPath(Path.Combine(Application.dataPath, GetOutputFolder()));

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

    // public void Check()
    // {
    //     if (_steps != null)
    //     {
    //         foreach (var buildStep in _steps)
    //         {
    //             if (buildStep.Active)
    //             {
    //                 buildStep.CheckStep(this);
    //             }
    //         }
    //     }
    // }

    public bool Build()
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
            return false;
        }

        /*
         * Prompt user to save modified scenes
         */
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
        {
            Debug.LogWarning("Building Cancelled");
            return false;
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

        //  __   __      __              __
        // |  \ /  \    |__) |  | | |   |  \
        // |__/ \__/    |__) \__/ | |__ |__/
        //
        /*
         * Do the actual builds
         */
        var allBuildsSucceeded = true;
        var successLogs = new List<string>();
        var postBuildStepResultsPerTarget = new List<List<BuildStepValidationResult>>();

        foreach (var target in targetsInOrder)
        {
            if (target != EditorUserBuildSettings.activeBuildTarget)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target),
                    target);
            }

            // AddressableAssetSettings.BuildPlayerContent();

            var path = GetPathForTarget(target);

            Debug.Log($"Building {target} to {path}");

            if (path.Contains("Assets"))
            {
                throw new Exception($"Refusing to build into any folder named 'Assets': {path}");
            }

            /*
             * DO THE ACTUAL BUILD:
             */
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
                allBuildsSucceeded = false;
                break;
            }

            /*
             * Execute post-build steps
             */
            var results = new List<BuildStepValidationResult>();
            postBuildStepResultsPerTarget.Add(results);
            foreach (var step in _postBuildSteps)
            {
                if (step != null && step.Active)
                {
                    step.Execute(this, target, report, results);
                }
            }

            var buildTime = report.summary.totalTime.TotalSeconds;
            var buildSize = report.summary.totalSize / 1024 / 1024;
            var timeStamp = DateTime.Now.ToString("HH:mm");

            if (results.Any(r => r.Severity == Severity.Error ))
            {
                Debug.LogError($"Build {target} v{Application.version} post-build checks failed!");
                allBuildsSucceeded = false;
            }
            else
            {
                var successColor = ColorUtility.ToHtmlStringRGB(new Color(0.6f, 1f, 0.58f));
                /*
                 * Log a short message, log full message later
                 */
                Debug.Log($"Build {target} v{Application.version} successful!");
                successLogs.Add(
                    $"[{timeStamp}] <b><color=#{successColor}>Build {target} v{Application.version} successful!</color></b> ({buildTime:.0}s) {buildSize}MB. At {path}");
            }

            EditorUtility.ClearProgressBar();
        }

        /*
         * Increment Build Number AFTERWARDS (so that paths are not messed up)
         */
        if (int.TryParse(PlayerSettings.iOS.buildNumber, out var buildNum))
        {
            PlayerSettings.iOS.buildNumber = (buildNum + 1).ToString();
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

        /*
         * Log the final summary messages
         * Success and failure messages are logged again because they tend to get
         * snowed under by a bunch of unity warnings and other logs after each build.
         */
        foreach (var message in successLogs)
        {
            Debug.Log(message);
        }

        for (int i = 0; i < targetsInOrder.Count; i++)
        {
            var target = targetsInOrder[i];
            var results = postBuildStepResultsPerTarget[i];
            if (results.Count > 0)
            {
                Debug.Log($"Post-build checks for <b>{target} v{Application.version}</b>");
                foreach (var result in results)
                {
                    result.Log();
                }
            }
            
        }

        return allBuildsSucceeded;
    }

    public BuildOptionWrapper ApplyBuildSteps()
    {
        var options = new BuildOptionWrapper();

        foreach (var buildStep in _preBuildSteps)
        {
            if (buildStep.Active)
            {
                buildStep.Apply(this, options);
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

    void LoadEditorPrefs()
    {
        if (_didLoadEditorPrefs == false)
        {
            _hasOutputFolderOverride = EditorPrefs.GetBool(HasOutputFolderOverridePrefsKey(), false);
            _outputFolderOverride = EditorPrefs.GetString(OutputFolderOverridePrefsKey(), "../../builds");
            _didLoadEditorPrefs = true;
        }
    }

    string OutputFolderOverridePrefsKey()
    {
        return GetPrefsKey("OutputFolderOverride");
        var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(this));
        return $"{nameof(BuildConfig)}.OverrideOutputFolder.{guid}";
    }

    string HasOutputFolderOverridePrefsKey()
    {
        return GetPrefsKey("HasOutputFolderOverride");
        var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(this));
        return $"{nameof(BuildConfig)}.HasOutputFolderOverride.{guid}";
    }

    string GetPrefsKey(string key)
    {
        var guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(this));
        return $"{nameof(BuildConfig)}.{key}.{guid}";
    }
}

public class BuildOptionWrapper
{
    #region PROPERTIES

    public BuildOptions Options { get; set; } = BuildOptions.None;

    #endregion
}
}

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