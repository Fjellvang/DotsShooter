// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace Unity.Editor
{
    public static class MetaplayFirebaseDetector
    {
        [InitializeOnLoadMethod]
        public static void InitHook()
        {
            MetaplayUnitySettings settings = MetaplayUnitySettings.instance;

            if (!settings.FirebaseInitialized)
            {
                string[] precompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);
                string   messagingDllPath         = precompiledAssemblyPaths.FirstOrDefault(x => x.Contains("Firebase.Messaging.dll"));
                if (messagingDllPath != null)
                {
                    if (!messagingDllPath.Contains("PackageCache"))
                    {
                        UpdateDefines(NamedBuildTarget.Android);
                        UpdateDefines(NamedBuildTarget.tvOS);
                        UpdateDefines(NamedBuildTarget.iOS);
                        UpdateDefines(NamedBuildTarget.Standalone);

                        EditorUtility.DisplayDialog("Enabled Metaplay Firebase Integration", "Metaplay has detected that Firebase Messaging is included in the project, Metaplay Firebase integration has been automatically enabled. Please submit changes to the project settings folder to your version control.", "Ok");

                        settings.FirebaseInitialized = true;
                        settings.Save();
                    }
                }
            }
        }

        public static void UpdateDefines(NamedBuildTarget platform)
        {
            PlayerSettings.GetScriptingDefineSymbols(platform, out string[] defines);

            if (defines.Contains("METAPLAY_ENABLE_FIREBASE_MESSAGING"))
                return;

            List<string> definesList = defines.ToList();
            definesList.Add("METAPLAY_ENABLE_FIREBASE_MESSAGING");

            PlayerSettings.SetScriptingDefineSymbols(platform, definesList.ToArray());
        }
    }
}
