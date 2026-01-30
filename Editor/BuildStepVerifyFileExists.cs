// (C)2026 @noio_games
// Thomas van den Berg

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace noio.MultiBuild
{
[Serializable]
public class BuildStepVerifyFileExists : PostBuildStep
{
    #region SERIALIZED FIELDS

    [SerializeField] Location _location;
    [SerializeField] string _relativePath = "";
    [SerializeField] TargetType _targetType;

    #endregion

    #region PROPERTIES

    public override string DisplayName =>
        $"Verify {(_targetType)} Exists: {(_relativePath != "" ? _relativePath : "(not set)")}";

    #endregion

    protected override void Validate(BuildConfig buildConfig)
    {
        if (string.IsNullOrEmpty(_relativePath))
        {
            ValidationResults.Add(new BuildStepValidationResult(Severity.Error,
                "No path specified. Set a relative path to check after build."));
        }
    }

    public override void Execute(
        BuildConfig buildConfig,
        BuildTarget target,
        BuildReport report,
        List<BuildStepValidationResult> results
    )
    {
        if (string.IsNullOrEmpty(_relativePath))
        {
            return;
        }

        var buildOutputPath = report.summary.outputPath;
        var buildDirectory = Path.GetDirectoryName(buildOutputPath);

        if (buildDirectory == null)
        {
            results.Add(new BuildStepValidationResult(Severity.Error,
                $"Unable to get directory from output path: {buildOutputPath}"));
            return;
        }

        string basePath;

        switch (_location)
        {
            case Location.InGameData:
                switch (target)
                {
                    case BuildTarget.StandaloneOSX:
                        basePath = Path.Combine(buildOutputPath + ".app", "Contents", "Resources", "Data");
                        break;
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64:
                        basePath = Path.Combine(buildDirectory, $"{Application.productName}_Data");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                break;
            case Location.InBuildFolder:
                basePath = buildDirectory;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var fullPath = Path.GetFullPath(Path.Combine(basePath, _relativePath));

        var exists = _targetType == TargetType.Folder ? Directory.Exists(fullPath) : File.Exists(fullPath);
        
        if (exists == false)
        {
            results.Add(new BuildStepValidationResult(Severity.Error,
                $"[Verify {(_targetType)} Exists] {_targetType} not found: {fullPath}"));
        }
        else
        {
            results.Add(new BuildStepValidationResult(Severity.Info,
                $"[Verify {(_targetType)} Exists] {_targetType} exists: {fullPath}"));
        }
    }

    enum Location
    {
        InBuildFolder,
        InGameData
    }

    internal enum TargetType
    {
        File,
        Folder
    }
}
}