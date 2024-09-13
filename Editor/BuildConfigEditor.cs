using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditorInternal;
using UnityEngine;

namespace noio.MultiBuild
{
    [CustomEditor(typeof(BuildConfig))]
    public class BuildConfigEditor : Editor
    {
        static readonly Lazy<GUIStyle> MiniButtonStyle = new(
            () => new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(0, 0, 0, 0)
            });

        static readonly Lazy<GUIContent> ErrorIcon = new(
            () => EditorGUIUtility.IconContent("Error"));

        static readonly Lazy<GUIContent> WarningIcon = new(
            () => EditorGUIUtility.IconContent("Warning"));

        SerializedProperty _script;
        SerializedProperty _outputFolder;
        SerializedProperty _customPath;
        SerializedProperty _targets;
        SerializedProperty _steps;
        ReorderableList _targetsList;
        BuildConfig _buildConfig;
        bool _showHelp;
        GUIContent _fileExistWarningIcon;

        #region MONOBEHAVIOUR METHODS

        void OnEnable()
        {
            _buildConfig = target as BuildConfig;
            _script = serializedObject.FindProperty("m_Script");
            _outputFolder = serializedObject.FindProperty("_outputFolder");
            _customPath = serializedObject.FindProperty("_customPath");
            _targets = serializedObject.FindProperty("_targets");
            _steps = serializedObject.FindProperty("_steps");

            _targetsList = new ReorderableList(serializedObject, _targets, false, true, true, true)
            {
                onAddDropdownCallback = (rect, list) => ShowAddTargetPopup(rect),
                drawElementCallback = (rect,   index, active, focused) => DrawTargetElement(rect, index),
                drawHeaderCallback = rect => GUI.Label(rect, "Targets", EditorStyles.boldLabel),

                // drawElementBackgroundCallback = DrawTargetElementBackground,
                elementHeight = 60
            };

            _fileExistWarningIcon = EditorGUIUtility.IconContent("console.warnicon");
            _fileExistWarningIcon.tooltip = "Build exists at this path and will be overwritten.";

            _buildConfig.Check();
        }

        #endregion
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

        public override void OnInspectorGUI()
        {
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
            GUILayout.Label("Setup", EditorStyles.boldLabel);
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

            //   __  ___  __  __   __            __  ___
            //  (__'  |  |__ |__) (__'    |   | (__'  |
            //  .__)  |  |__ |    .__)    |__ | .__)  |
            //

            using (var changes = new EditorGUI.ChangeCheckScope())
            {
                for (var idx = 0; idx < _steps.arraySize; idx++)
                {
                    var captureIdx = idx;
                    CoreEditorUtils.DrawSplitter();
                    var stepProperty = _steps.GetArrayElementAtIndex(idx);
                    var step = stepProperty.managedReferenceValue as BuildStep;
                    if (stepProperty.managedReferenceValue == null)
                    {
                        CoreEditorUtils.DrawHeaderFoldout(new GUIContent("Not Found"),
                            true,
                            contextAction: v => OnContextClick(v, captureIdx));

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
                        var activeProperty = stepProperty.FindPropertyRelative("_active");
                        var displayContent = CoreEditorUtils.DrawHeaderToggle(
                            step.DisplayName,
                            stepProperty,
                            activeProperty,
                            v => OnContextClick(v, captureIdx));

                        if (step.LastResult.Type != BuildStepResult.ResultType.Success)
                        {
                            var iconRect = GUILayoutUtility.GetLastRect();
                            iconRect.xMin = iconRect.xMax - 40;
                            iconRect.width = 20;
                            EditorGUI.LabelField(iconRect, step.LastResult.Type switch
                            {
                                BuildStepResult.ResultType.Warning => WarningIcon.Value,
                                BuildStepResult.ResultType.Error   => ErrorIcon.Value,
                                _                                  => null
                            });

                            // EditorGUI.DrawRect(iconRect, Color.magenta);
                        }

                        /*
                         * DRAW BUILD STEP CONTENT
                         */
                        if (displayContent)
                        {
                            if (step.LastResult.Type != BuildStepResult.ResultType.Success)
                            {
                                EditorGUILayout.HelpBox($"{step.LastResult.Message}",
                                    step.LastResult.Type switch
                                    {
                                        BuildStepResult.ResultType.Warning => MessageType.Warning,
                                        BuildStepResult.ResultType.Error   => MessageType.Error,
                                        _                                  => MessageType.None
                                    });
                            }

                            DrawBuildStepInspector(stepProperty);
                        }
                    }
                }

                if (changes.changed)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }

            if (_steps.arraySize > 0)
            {
                CoreEditorUtils.DrawSplitter();
            }
            else
            {
                EditorGUILayout.HelpBox("No pre-build steps have been added.", MessageType.Info);
            }

            EditorGUILayout.Space();

            using (var horizontalScope = new EditorGUILayout.HorizontalScope())
            {
                if (EditorGUILayout.DropdownButton(new GUIContent("Add Pre-Build Step"), FocusType.Keyboard,
                        EditorStyles.miniButton))
                {
                    var rect = horizontalScope.rect;
                    ShowAddBuildStepPopup(rect, _steps, BuildStepOrder.PreBuild);
                }
            }

            //   __              __      __       ___ ___  __
            //  |__) |  | | |   |  \    |__) |  |  |   |  /  \ |\ |
            //  |__) \__/ | |__ |__/    |__) \__/  |   |  \__/ | \|
            //
            GUILayout.Space(10);
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
            }

            GUI.color = Color.white;
            EditorGUILayout.Space();
        }

        string GetBuildStepDisplayName(Type type)
        {
            var typeName = type.Name;
            if (typeName.StartsWith("BuildStep"))
            {
                typeName = typeName[9..];
            }

            return ObjectNames.NicifyVariableName(typeName);
        }

        void DrawBuildStepInspector(SerializedProperty step)
        {
            var activeProp = step.FindPropertyRelative("_active");
            using (new EditorGUI.DisabledScope(activeProp.boolValue == false))
            {
                var sibling = step.Copy();
                sibling.NextVisible(false);
                while (step.NextVisible(true))
                {
                    if (step.propertyPath == sibling.propertyPath)
                    {
                        break;
                    }

                    if (step.name != activeProp.name)
                    {
                        EditorGUILayout.PropertyField(step);
                    }
                }
            }

            EditorGUILayout.Space();
        }

        void OnContextClick(Vector2 position, int index)
        {
            // var targetComponent = targetEditor.target;
            var menu = new GenericMenu();

            if (index == 0)
            {
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Up"));
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move to Top"));
            }
            else
            {
                menu.AddItem(EditorGUIUtility.TrTextContent("Move to Top"), false, () => MoveStep(index, 0));
                menu.AddItem(EditorGUIUtility.TrTextContent("Move Up"), false,
                    () => MoveStep(index, index - 1));
            }

            if (index == _steps.arraySize - 1)
            {
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move to Bottom"));
                menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Move Down"));
            }
            else
            {
                menu.AddItem(EditorGUIUtility.TrTextContent("Move to Bottom"), false,
                    () => MoveStep(index, _steps.arraySize - 1));
                menu.AddItem(EditorGUIUtility.TrTextContent("Move Down"), false,
                    () => MoveStep(index, index + 1));
            }

            //
            // menu.AddSeparator(string.Empty);
            // menu.AddItem(EditorGUIUtility.TrTextContent("Collapse All"), false, () => CollapseComponents());
            // menu.AddItem(EditorGUIUtility.TrTextContent("Expand All"), false, () => ExpandComponents());
            // menu.AddSeparator(string.Empty);
            // menu.AddItem(EditorGUIUtility.TrTextContent("Reset"), false, () => ResetComponent(targetComponent.GetType(), index));

            menu.AddItem(EditorGUIUtility.TrTextContent("Remove"), false, () => RemoveStep(index));

            // menu.AddSeparator(string.Empty);
            // if (targetEditor.hasAdditionalProperties)
            //     menu.AddItem(EditorGUIUtility.TrTextContent("Show Additional Properties"), targetEditor.showAdditionalProperties, () => targetEditor.showAdditionalProperties ^= true);
            // else
            //     menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Show Additional Properties"));
            // menu.AddItem(EditorGUIUtility.TrTextContent("Show All Additional Properties..."), false, () => CoreRenderPipelinePreferences.Open());
            //
            // menu.AddSeparator(string.Empty);
            // menu.AddItem(EditorGUIUtility.TrTextContent("Copy Settings"), false, () => CopySettings(targetComponent));
            //
            // if (CanPaste(targetComponent))
            //     menu.AddItem(EditorGUIUtility.TrTextContent("Paste Settings"), false, () => PasteSettings(targetComponent));
            // else
            //     menu.AddDisabledItem(EditorGUIUtility.TrTextContent("Paste Settings"));
            //
            // menu.AddSeparator(string.Empty);
            // menu.AddItem(EditorGUIUtility.TrTextContent("Toggle All"), false, () => m_Editors[index].SetAllOverridesTo(true));
            // menu.AddItem(EditorGUIUtility.TrTextContent("Toggle None"), false, () => m_Editors[index].SetAllOverridesTo(false));

            menu.DropDown(new Rect(position, Vector2.zero));
        }

        void MoveStep(int index, int destinationIndex)
        {
            serializedObject.Update();
            _steps.MoveArrayElement(index, destinationIndex);
            serializedObject.ApplyModifiedProperties();
        }

        void RemoveStep(int index)
        {
            serializedObject.Update();
            _steps.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
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

        void ShowAddBuildStepPopup(Rect rect, SerializedProperty stepsList, BuildStepOrder order)
        {
            var menu = new GenericMenu();

            var currentSteps = new List<BuildStep>();
            for (var i = 0; i < stepsList.arraySize; i++)
            {
                currentSteps.Add(stepsList.GetArrayElementAtIndex(i).managedReferenceValue as BuildStep);
            }

            var buildSteps = TypeCache.GetTypesWithAttribute<BuildStepAttribute>();
            foreach (var buildStepType in buildSteps)
            {
                foreach (BuildStepAttribute attribute in
                         buildStepType.GetCustomAttributes(typeof(BuildStepAttribute), true))
                {
                    if ((attribute.Order | order) == attribute.Order &&
                        (attribute.AllowMultiple ||
                         currentSteps.Any(step => step.GetType() == buildStepType) == false))
                    {
                        menu.AddItem(new GUIContent(GetBuildStepDisplayName(buildStepType)), false, () =>
                        {
                            var idx = stepsList.arraySize;
                            _steps.InsertArrayElementAtIndex(idx);
                            var step = _steps.GetArrayElementAtIndex(idx);
                            step.managedReferenceValue = Activator.CreateInstance(buildStepType);
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
                GUI.Label(warnIconRect, _fileExistWarningIcon);
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

        #endregion
    }
}