// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Unity.Editor
{
    [FilePath("ProjectSettings/Metaplay/MetaplaySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class MetaplayUnitySettings : ScriptableSingleton<MetaplayUnitySettings>
    {
        /// <summary>
        /// Scriptable singleton is not really designed to be used with SerializedObjects, so this is a bit of a hacky solution around it.
        /// </summary>
        SerializedObject serializedObject;

        public bool IsProjectInitialized;
        public bool CustomDashboardCreated;

        public bool FirstTimeImportRan;

        /// <summary>
        /// Is Firebase _Messaging_ has been detected and enabled in MetaplaySDK.
        /// </summary>
        public bool FirebaseInitialized;

        void OnEnable()
        {
            serializedObject = new SerializedObject(this);

            if (!FirstTimeImportRan)
            {
                EditorApplication.delayCall += () =>
                {
                    string backendPath   = Path.Combine(Directory.GetCurrentDirectory(), "Backend");
                    string dashboardPath = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "Dashboard");

                    if (Directory.Exists(backendPath))
                        IsProjectInitialized = true;

                    if (Directory.Exists(dashboardPath))
                        CustomDashboardCreated = true;

                    FirstTimeImportRan = true;
                    Save();
                };
            }
        }

        public SerializedObject GetSerializedSettings()
        {
            return serializedObject;
        }

        public void Save()
        {
            Save(true);
        }

        protected override void Save(bool saveAsText)
        {
            // In some cases we access these settings before OnEnable has run, which means that serializedObject is still null.
            // I.e. in InitializeOnLoad, OnEnable has not run yet.
            if (serializedObject?.hasModifiedProperties == true)
                serializedObject?.ApplyModifiedProperties();
            else
                serializedObject?.Update();

            base.Save(saveAsText);
        }
    }

    static class MetaplaySettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateMetaplaySettingsProvider()
        {
            var provider = new SettingsProvider("Project/Metaplay Settings", SettingsScope.Project)
            {
                label = "Metaplay Settings",
                guiHandler = s =>
                {
                    var settings = MetaplayUnitySettings.instance.GetSerializedSettings();
                    using (EditorGUI.ChangeCheckScope changeCheckScope = new EditorGUI.ChangeCheckScope())
                    {
                        EditorGUIUtility.labelWidth = 250;

                        SerializedProperty projectInitialized = settings.FindProperty("IsProjectInitialized");

                        projectInitialized.isExpanded = EditorGUILayout.Foldout(projectInitialized.isExpanded, "Internal Metaplay Settings", true);

                        if (projectInitialized.isExpanded)
                        {
                            using (new EditorGUI.IndentLevelScope())
                            {
                                EditorGUILayout.PropertyField(projectInitialized);
                                EditorGUILayout.PropertyField(settings.FindProperty("CustomDashboardCreated"));
                                EditorGUILayout.PropertyField(settings.FindProperty("FirebaseInitialized"));
                            }
                        }

                        if(changeCheckScope.changed)
                            MetaplayUnitySettings.instance.Save();
                    }
                },

                keywords = new HashSet<string>(new[] { "Metaplay" })
            };

            return provider;
        }
    }
}
