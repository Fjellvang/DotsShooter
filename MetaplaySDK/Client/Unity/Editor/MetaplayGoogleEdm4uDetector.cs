// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

// METAPLAY_HAS_GOOGLE_EDM4U is the symbol for detecting the presence of
// External Dependency Manager for Unity (EDM4U, https://github.com/googlesamples/unity-jar-resolver)
//
// EDM4U can be installed two ways:
//  * Via Unity Package Manager Package
//  * Via .unitypackage
//
// When Unity Package Manager is used, the MetaplaySDK.Unity.Editor.asmdef has a package-detecting
// version define which sets METAPLAY_HAS_GOOGLE_EDM4U.
//
// When .unitypackage is imported, METAPLAY_HAS_GOOGLE_EDM4U define will not be set automatically.
// Instead, we use MetaplayGoogleEdm4uDetector to scan the project on each import and if the EDM4U
// is detected, we update the project settings.
//
// Checker is only run on Android (same as EDM4U) and only when EDM4u hasn't been already detected
// with either aforementioned mechanism.

#if !METAPLAY_HAS_GOOGLE_EDM4U && UNITY_ANDROID

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;

namespace Unity.Editor
{
    public static class MetaplayGoogleEdm4uDetector
    {
        [InitializeOnLoadMethod]
        public static void InitHook()
        {
            string dllPath = CompilationPipeline.GetPrecompiledAssemblyPathFromAssemblyName("Google.VersionHandler.dll");
            if (dllPath == null)
                return;

            // DLL is in the project, but flag is not set. Set one now.
            UpdateDefines();
            EditorUtility.DisplayDialog(
                "Enabled External Dependency Manager for Unity (EDM4U) Integration",
                "Metaplay has detected that External Dependency Manager for Unity (EDM4U) is included in the project, " +
                "Metaplay EDM4U integration has been automatically enabled. " +
                "Please submit changes to the project settings folder to your version control.",
                "Ok");
        }

        public static void UpdateDefines()
        {
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out string[] defines);

            if (defines.Contains("METAPLAY_HAS_GOOGLE_EDM4U"))
                return;

            List<string> definesList = defines.ToList();
            definesList.Add("METAPLAY_HAS_GOOGLE_EDM4U");

            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, definesList.ToArray());
        }
    }
}

#endif
