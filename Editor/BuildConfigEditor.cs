// (C)2026 @noio_games
// Thomas van den Berg

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace noio.MultiBuild
{
[CustomEditor(typeof(BuildConfig))]
public class BuildConfigEditor : Editor
{
    static readonly Lazy<GUIStyle> MiniButtonStyle = new(() => new GUIStyle(GUI.skin.button)
    {
        padding = new RectOffset(0, 0, 0, 0)
    });

    static readonly Lazy<GUIContent> ErrorIcon = new(() => EditorGUIUtility.IconContent("Error"));
    static readonly Lazy<GUIContent> WarningIcon = new(() => EditorGUIUtility.IconContent("Warning"));

    static readonly Lazy<GUIContent> FileExistsWarningIcon = new(() =>
    {
        var icon = EditorGUIUtility.IconContent("console.warnicon");
        icon.tooltip = "Build exists at this path and will be overwritten.";
        return icon;
    });

    static readonly Lazy<GUIStyle> MessageStyle = new(() => new GUIStyle("label")
    {
        alignment = TextAnchor.MiddleLeft,
        padding = new RectOffset(2, 0, 2, 0),
        fontSize = 11,
        wordWrap = true
    });

    static readonly Lazy<GUIStyle> HeaderStyle = new(() => new GUIStyle(EditorStyles.boldLabel)
    {
        fontSize = 16
    });

    readonly List<StepEntry> _preBuildStepEntries = new();
    readonly List<StepEntry> _postBuildStepEntries = new();
    SerializedProperty _script;
    SerializedProperty _outputFolder;
    SerializedProperty _customPath;
    SerializedProperty _targets;
    SerializedProperty _preBuildSteps;
    SerializedProperty _postBuildSteps;
    ReorderableList _targetsList;
    BuildConfig _buildConfig;
    bool _showHelp;
    bool _queueValidate;

    #region MONOBEHAVIOUR METHODS

    void OnEnable()
    {
        _buildConfig = target as BuildConfig;
        _script = serializedObject.FindProperty("m_Script");
        _outputFolder = serializedObject.FindProperty("_outputFolder");
        _customPath = serializedObject.FindProperty("_customPath");
        _targets = serializedObject.FindProperty("_targets");
        _preBuildSteps = serializedObject.FindProperty("_preBuildSteps");
        _postBuildSteps = serializedObject.FindProperty("_postBuildSteps");

        _targetsList = new ReorderableList(serializedObject, _targets, false, true, true, true)
        {
            onAddDropdownCallback = (rect, list) => ShowAddTargetPopup(rect),
            drawElementCallback = (rect, index, active, focused) => DrawTargetElement(rect, index),
            drawHeaderCallback = rect => GUI.Label(rect, "Targets", EditorStyles.boldLabel),

            // drawElementBackgroundCallback = DrawTargetElementBackground,
            elementHeight = 60
        };

        RebuildPreBuildStepsList();
        RebuildPostBuildStepsList();
        ValidateAll();
    }

    #endregion

    public override void OnInspectorGUI()
    {
        if (_queueValidate)
        {
            ValidateAll();
        }

        serializedObject.UpdateIfRequiredOrScript();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_script);
            GUILayout.Space(4);
        }

        //   __   __ ___       __
        //  (__' |__  |  |  | |__)
        //  .__) |__  |  \__/ |
        //
        GUILayout.Label("Setup", HeaderStyle.Value);
        using (var changes = new EditorGUI.ChangeCheckScope())
        {
            DrawOutputFolderSelect();

            if (_showHelp)
            {
                DrawCustomPathHelp();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_customPath);
                if (GUILayout.Button(EditorGUIUtility.IconContent("d__Help"),
                        MiniButtonStyle.Value,
                        GUILayout.Width(20), GUILayout.Height(20)))
                {
                    _showHelp = !_showHelp;
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button($"Increment Version (Current: {Application.version})", GUILayout.Height(30)))
            {
                IncrementVersion();
            }

            if (changes.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        //  ___   _    __   __   __ ___  __            __  ___
        //   |   /_\  |__) / __ |__  |  (__'    |   | (__'  |
        //   |  /   \ |  \ \__| |__  |  .__)    |__ | .__)  |
        //
        using (var changes = new EditorGUI.ChangeCheckScope())
        {
            GUILayout.Space(10);
            _targetsList.DoLayoutList();
            if (changes.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUILayout.Space();

        GUILayout.Label("Before Build", HeaderStyle.Value);
        DrawStepsList(_preBuildStepEntries, _preBuildSteps, "Pre-Build");
        DrawAddStepButton<PreBuildStep>(_preBuildStepEntries, _preBuildSteps, "Pre-Build");

        GUILayout.Space(20);
        GUILayout.Label("After Build", HeaderStyle.Value);
        DrawStepsList(_postBuildStepEntries, _postBuildSteps, "Post-Build");
        DrawAddStepButton<PostBuildStep>(_postBuildStepEntries, _postBuildSteps, "Post-Build");

        GUILayout.Space(40);
        var hasAnyErrors = _preBuildStepEntries.Any(entry =>
                               entry.IsValid &&
                               entry.BuildStep.ValidationResults.Any(msg =>
                                   msg.Severity == Severity.Error)) ||
                           _postBuildStepEntries.Any(entry =>
                               entry.IsValid &&
                               entry.BuildStep.ValidationResults.Any(msg => msg.Severity == Severity.Error));

        using (new EditorGUI.DisabledScope(hasAnyErrors))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Steps Only", GUILayout.Width(120), GUILayout.Height(50)))
                {
                    _buildConfig.ApplyBuildSteps();
                }

                GUI.color = new Color(0.57f, 1f, 0.56f);
                if (GUILayout.Button("Build", GUILayout.Height(50)))
                {
                    _buildConfig.Build();
                }

                var currentTarget = EditorUserBuildSettings.activeBuildTarget;
                var isCurrentTargetIncluded = _buildConfig.Targets.Contains(currentTarget);

                using (new EditorGUI.DisabledScope(!isCurrentTargetIncluded))
                {
                    if (GUILayout.Button("Build & Run", GUILayout.Width(120), GUILayout.Height(50)))
                    {
                        var success = _buildConfig.Build();

                        if (success)
                        {
                            var buildPath = _buildConfig.GetPathForTarget(currentTarget);
                            var execPath = GetExecutablePath(buildPath, currentTarget);
                            if (string.IsNullOrEmpty(execPath) == false &&
                                (File.Exists(execPath) || Directory.Exists(execPath)))
                            {
                                Debug.Log($"Running build at: {execPath}");
                                Process.Start(execPath);
                            }
                            else
                            {
                                Debug.LogError($"Build not found at: {execPath}");
                            }
                        }
                    }
                }
            }
        }

        GUI.color = Color.white;
        EditorGUILayout.Space();
    }

    void ValidateAll()
    {
        foreach (var entry in _preBuildStepEntries)
        {
            if (entry.IsValid)
            {
                entry.BuildStep.RunValidation(_buildConfig);
            }
        }

        foreach (var entry in _postBuildStepEntries)
        {
            if (entry.IsValid)
            {
                entry.BuildStep.RunValidation(_buildConfig);
            }
        }

        _queueValidate = false;
    }

    void DrawStep(StepEntry stepEntry, Action<Vector2> contextAction)
    {
        CoreEditorUtils.DrawSplitter();

        if (stepEntry.IsValid == false)
        {
            CoreEditorUtils.DrawHeaderFoldout(new GUIContent("Not Found"),
                true,
                contextAction: contextAction);

            EditorGUILayout.HelpBox(
                "Build Step was NULL. This can be caused by renaming a BuildStep class. " +
                "Use the [MovedFrom] attribute to preserve serialization.",
                MessageType.Error);
            EditorGUILayout.Space();
        }
        else
        {
            /*
             * DRAW BUILD STEP FOLDOUT HEADER
             */
            var step = stepEntry.BuildStep;
            var stepSO = stepEntry.SerializedObject;
            var activeProp = stepSO.FindProperty("_active");

            var isExpanded = CoreEditorUtils.DrawHeaderToggle(
                step.DisplayName, stepEntry.SerializedProperty, activeProp,
                contextAction);

            var iconRect = GUILayoutUtility.GetLastRect();
            iconRect.xMin = iconRect.xMax - 40;
            iconRect.width = 20;
            if (step.ValidationResults.Any(msg => msg.Severity == Severity.Error))
            {
                EditorGUI.LabelField(iconRect, ErrorIcon.Value);
            }
            else if (step.ValidationResults.Any(msg => msg.Severity == Severity.Warning))
            {
                EditorGUI.LabelField(iconRect, WarningIcon.Value);
            }

            /*
             * DRAW BUILD STEP CONTENT
             */
            if (isExpanded)
            {
                EditorGUILayout.Space();

                foreach (var msg in step.ValidationResults)
                {
                    var didFix = DrawMessage(msg);
                    if (didFix)
                    {
                        _queueValidate = true;
                    }
                }

                stepSO.Update();

                using (var changes = new EditorGUI.ChangeCheckScope())
                {
                    using (new EditorGUI.DisabledScope(activeProp is { boolValue: false }))
                    {
                        /* Iterate child properties of the sub-asset */
                        var iterator = stepSO.GetIterator();
                        var enterChildren = true;
                        while (iterator.NextVisible(enterChildren))
                        {
                            enterChildren = false;

                            /* Hide script field and the _active (drawn in header toggle) */
                            if (iterator.propertyPath == "m_Script" || iterator.propertyPath == "_active")
                            {
                                continue;
                            }

                            EditorGUILayout.PropertyField(iterator, true);
                        }
                    }

                    if (changes.changed)
                    {
                        _queueValidate = true;
                    }
                }

                stepSO.ApplyModifiedProperties();
            }
        }

        EditorGUILayout.Space();
    }

    void DrawStepsList(
        List<StepEntry> stepEntries,
        SerializedProperty stepsProperty,
        string label
    )
    {
        EditorGUILayout.Space(6);
        using (var changes = new EditorGUI.ChangeCheckScope())                           
        {
            for (var index = 0; index < stepEntries.Count; index++)
            {
                var capturedIndex = index;
                DrawStep(stepEntries[index],
                    v => OnStepContextClick(v, capturedIndex, stepEntries, stepsProperty));
            }

            if (changes.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        if (stepsProperty.arraySize > 0)
        {
            CoreEditorUtils.DrawSplitter();
        }
        else
        {
            EditorGUILayout.HelpBox($"No {label.ToLower()} steps have been added.", MessageType.Info);
        }

        EditorGUILayout.Space();
    }

    void DrawAddStepButton<T>(
        List<StepEntry> stepEntries,
        SerializedProperty stepsProperty,
        string label
    ) where T : BuildStep
    {
        void ShowAddStepPopup(Rect rect)
        {
            var menu = new GenericMenu();

            var stepTypes = TypeCache.GetTypesDerivedFrom<T>()
                                     .Where(t => t.IsAbstract == false && t.IsGenericTypeDefinition == false)
                                     .OrderBy(t => GetBuildStepDisplayName(t));

            foreach (var type in stepTypes)
            {
                var displayName = GetBuildStepDisplayName(type);
                menu.AddItem(new GUIContent(displayName), false, () =>
                {
                    var config = (BuildConfig)target;
                    var path = AssetDatabase.GetAssetPath(config);

                    Undo.RecordObject(config, $"Add {label} Step");

                    var step = (BuildStep)CreateInstance(type);
                    step.name = type.Name;
                    step.hideFlags = HideFlags.HideInHierarchy;

                    AssetDatabase.AddObjectToAsset(step, config);
                    AssetDatabase.ImportAsset(path);
                    EditorUtility.SetDirty(step);
                    EditorUtility.SetDirty(config);

                    var idx = stepsProperty.arraySize;
                    stepsProperty.InsertArrayElementAtIndex(idx);
                    stepsProperty.GetArrayElementAtIndex(idx).objectReferenceValue = step;

                    serializedObject.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();

                    RebuildStepsList(stepEntries, stepsProperty);
                });
            }

            menu.DropDown(rect);
        }

        using (var horizontalScope = new EditorGUILayout.HorizontalScope())
        {
            if (EditorGUILayout.DropdownButton(new GUIContent($"Add {label} Step"), FocusType.Keyboard,
                    EditorStyles.miniButton))
            {
                var rect = horizontalScope.rect;
                ShowAddStepPopup(rect);
            }
        }
    }

    static void IncrementVersion()
    {
        var currentVersion = Application.version;

        var lastDot = currentVersion.LastIndexOf('.');
        if (lastDot != -1 && int.TryParse(currentVersion.Substring(lastDot + 1), out var patchNumber))
        {
            patchNumber++;
            var newVersion = currentVersion.Substring(0, lastDot + 1) + patchNumber;
            PlayerSettings.bundleVersion = newVersion;
            Debug.Log($"Version updated to: {newVersion}");
        }
        else
        {
            Debug.LogWarning($"Can't increment version \"{currentVersion}\".");
        }
    }

    void RebuildPreBuildStepsList()
    {
        RebuildStepsList(_preBuildStepEntries, _preBuildSteps);
    }

    void RebuildPostBuildStepsList()
    {
        RebuildStepsList(_postBuildStepEntries, _postBuildSteps);
    }

    void RebuildStepsList(List<StepEntry> stepEntries, SerializedProperty stepsProperty)
    {
        stepEntries.Clear();
        for (var i = 0; i < stepsProperty.arraySize; i++)
        {
            var entry = new StepEntry(stepsProperty.GetArrayElementAtIndex(i));
            stepEntries.Add(entry);
            if (entry.IsValid)
            {
                entry.BuildStep.hideFlags = HideFlags.HideInHierarchy;
            }
        }
    }

    static string GetBuildStepDisplayName(Type type)
    {
        var typeName = type.Name;
        if (typeName.StartsWith("BuildStep"))
        {
            typeName = typeName[9..];
        }

        return ObjectNames.NicifyVariableName(typeName);
    }

    void OnStepContextClick(
        Vector2 position,
        int index,
        List<StepEntry> stepEntries,
        SerializedProperty stepsProperty
    )
    {
        var menu = new GenericMenu();

        if (index == 0)
        {
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move to Top"));
        }
        else
        {
            menu.AddItem(EditorGUIUtility.TrTextContent("Move to Top"), false,
                () => MoveStep(index, 0, stepEntries, stepsProperty));
            menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false,
                () => MoveStep(index, index - 1, stepEntries, stepsProperty));
        }

        if (index == stepsProperty.arraySize - 1)
        {
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move to Bottom"));
            menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
        }
        else
        {
            menu.AddItem(EditorGUIUtility.TrTextContent("Move to Bottom"), false,
                () => MoveStep(index, stepsProperty.arraySize - 1, stepEntries, stepsProperty));
            menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false,
                () => MoveStep(index, index + 1, stepEntries, stepsProperty));
        }

        menu.AddItem(EditorGUIUtility.TrTextContent("Remove"), false,
            () => RemoveStep(index, stepEntries, stepsProperty));

        menu.DropDown(new Rect(position, Vector2.zero));
    }

    void MoveStep(
        int index,
        int destinationIndex,
        List<StepEntry> stepEntries,
        SerializedProperty stepsProperty
    )
    {
        serializedObject.Update();
        stepsProperty.MoveArrayElement(index, destinationIndex);
        serializedObject.ApplyModifiedProperties();

        RebuildStepsList(stepEntries, stepsProperty);
    }

    void RemoveStep(int index, List<StepEntry> stepEntries, SerializedProperty stepsProperty)
    {
        serializedObject.Update();

        var prop = stepsProperty.GetArrayElementAtIndex(index);
        var obj = prop.objectReferenceValue as BuildStep;

        /* First null the reference in the list */
        stepsProperty.DeleteArrayElementAtIndex(index);
        serializedObject.ApplyModifiedProperties();

        /* Then destroy the sub-asset so it doesn't orphan */
        if (obj != null)
        {
            Undo.RecordObject(target, "Remove Build Step");
            var cfg = (BuildConfig)target;
            var path = AssetDatabase.GetAssetPath(cfg);

            DestroyImmediate(obj, true);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.SaveAssets();
        }

        RebuildStepsList(stepEntries, stepsProperty);
    }

    void ShowAddTargetPopup(Rect rect)
    {
        var menu = new GenericMenu();

        foreach (BuildTarget buildTarget in Enum.GetValues(typeof(BuildTarget)))
        {
            if (buildTarget.GetAttributeOfType<ObsoleteAttribute>() == null)
            {
                if (_buildConfig.Targets.Contains(buildTarget) == false)
                {
                    menu.AddItem(new GUIContent(buildTarget.ToString()), false, () =>
                    {
                        Undo.RecordObject(this, "Add Build Target");
                        var end = _targets.arraySize;
                        _targets.InsertArrayElementAtIndex(end);
                        _targets.GetArrayElementAtIndex(end).intValue = (int)buildTarget;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
            }
        }

        menu.DropDown(rect);
    }

    static bool OpenFolderPanelRelativeToProject(ref string folder)
    {
        var path = Path.Combine(Application.dataPath, folder);
        path = EditorUtility.OpenFolderPanel("Select Folder", path, "");
        if (string.IsNullOrEmpty(path) == false)
        {
            path = Path.GetRelativePath(Application.dataPath, path);
            folder = path;
            return true;
        }

        return false;
    }

    #region INSPECTOR DRAWING ELEMENTS

    static bool DrawMessage(BuildStepValidationResult validationResult)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            var iconContent = validationResult.Severity switch
            {
                Severity.Info    => WarningIcon.Value,
                Severity.Warning => WarningIcon.Value,
                Severity.Error   => ErrorIcon.Value,
                _                => ErrorIcon.Value
            };

            GUILayout.Label(iconContent);

            GUILayout.Label(validationResult.Message, MessageStyle.Value);
            GUILayout.FlexibleSpace();

            if (validationResult.FixAction != null)
            {
                if (GUILayout.Button(validationResult.FixActionName, GUILayout.MinWidth(120)))
                {
                    validationResult.FixAction();
                    return true;
                }
            }
        }

        return false;
    }

    void DrawOutputFolderSelect()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var folder = _buildConfig.GetOutputFolder();
            var hasOverride = _buildConfig.HasOutputFolderOverride;

            if (DrawFolderSelectField(ref folder, new GUIContent("Output Folder")))
            {
                if (hasOverride)
                {
                    _buildConfig.OutputFolderOverride = folder;
                }
                else
                {
                    _outputFolder.stringValue = folder;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            var content = new GUIContent("Local", "Enable to override the output path on this PC only.");

            var newOverride = EditorGUILayout.ToggleLeft(content, hasOverride, GUILayout.Width(60));
            if (newOverride && hasOverride == false)
            {
                if (OpenFolderPanelRelativeToProject(ref folder))
                {
                    _buildConfig.HasOutputFolderOverride = true;
                    _buildConfig.OutputFolderOverride = folder;
                }
            }
            else if (newOverride == false && hasOverride)
            {
                _buildConfig.HasOutputFolderOverride = false;
            }
        }
    }

    void DrawTargetElement(Rect rect, int i)
    {
        rect.yMin += 3;
        rect.yMax -= 3;
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        rect.xMin += 5;

        var t = _targets.GetArrayElementAtIndex(i);
        var buildTarget = (BuildTarget)t.intValue;

        var targetEnumVal = t.enumValueIndex;
        var targetName =
            targetEnumVal >= 0 ? t.enumDisplayNames[targetEnumVal] : t.ToString();
        var path = _buildConfig.GetPathForTarget(buildTarget);

        var labelRect = rect;
        labelRect.yMin += 2;
        labelRect.height = 16;

        var pathRect = rect;
        pathRect.yMin += 18;
        pathRect.height = 45;

        GUI.Label(labelRect, targetName, EditorStyles.boldLabel);

        if (BuildConfig.BuildExists(path))
        {
            var warnIconRect = pathRect;
            warnIconRect.yMin += 4;
            warnIconRect.width = 20;
            warnIconRect.height = 20;
            pathRect.xMin += 22;
            GUI.Label(warnIconRect, FileExistsWarningIcon.Value);
        }

        GUI.Label(pathRect, path, EditorStyles.wordWrappedMiniLabel);
    }

    void DrawCustomPathHelp()
    {
        // GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Use the following placeholders:", EditorStyles.boldLabel);
                if (GUILayout.Button(EditorGUIUtility.IconContent("CrossIcon"),
                        MiniButtonStyle.Value,
                        GUILayout.Width(20), GUILayout.Height(20)))
                {
                    _showHelp = !_showHelp;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("{date}");
                    GUILayout.Label("{name}");
                    GUILayout.Label("{version}");
                    GUILayout.Label("{buildnum}");
                    GUILayout.Label("{target}");
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("The current date");
                    GUILayout.Label("Product Name (from Project Settings)");
                    GUILayout.Label("Version (from project Settings)");
                    GUILayout.Label("The (iOS) build number");
                    GUILayout.Label("Built target for each executable");
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(DateTime.Now.ToString("yyyy-MM-dd"));
                    GUILayout.Label(Application.productName);
                    GUILayout.Label(Application.version);
                    GUILayout.Label(PlayerSettings.iOS.buildNumber);
                    GUILayout.Label("e.g. \"StandaloneWindows64\"");
                }
            }

            GUILayout.Space(4);
        }
    }

    static bool DrawFolderSelectField(ref string folder, GUIContent label = null)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var newFolder = EditorGUILayout.TextField(label, folder);
            if (newFolder != folder)
            {
                folder = newFolder;
                return true;
            }

            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                return OpenFolderPanelRelativeToProject(ref folder);
            }
        }

        return false;
    }

    static string GetExecutablePath(string buildPath, BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return buildPath.EndsWith(".exe") ? buildPath : buildPath + ".exe";

            case BuildTarget.StandaloneOSX:
                return buildPath.EndsWith(".app") ? buildPath : buildPath + ".app";

            case BuildTarget.StandaloneLinux64:
                return buildPath;

            default:
                return buildPath;
        }
    }

    #endregion
}

internal class StepEntry
{
    public StepEntry(SerializedProperty stepProperty)
    {
        SerializedProperty = stepProperty;
        BuildStep = stepProperty.objectReferenceValue as BuildStep;

        if (BuildStep != null)
        {
            SerializedObject = new SerializedObject(BuildStep);
        }
    }

    #region PROPERTIES

    public SerializedProperty SerializedProperty { get; }
    public bool IsValid => BuildStep != null;
    public BuildStep BuildStep { get; set; }
    public SerializedObject SerializedObject { get; set; }

    #endregion
}
}