// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

// This checks the METAPLAY_HAS_GOOGLE_EDM4U is defined in the project when Android build is attempted.
// See MetaplayGoogleEdm4uDetector for details.

#if UNITY_ANDROID && !METAPLAY_HAS_GOOGLE_EDM4U && !METAPLAY_DISABLE_GOOGLE_EDM4U_CHECK

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Metaplay.Unity.Android
{
    /// <summary>
    /// Checks the Unity project configuration contains EDM4U
    /// </summary>
    class MetaplayGoogleEdm4uChecker : IPreprocessBuildWithReport
    {
        const string EDM4UErrorText =
            "EDM4U is required for resolving Android aar packages and their dependencies. "
            + "Please add EDM4U into the project and try again. "
            + "See instructions in https://docs.metaplay.io/feature-cookbooks/platform-plugins/using-external-dependency-manager-for-unity-edm4u.html"
            + " (This check may also be disabled by defining METAPLAY_DISABLE_GOOGLE_EDM4U_CHECK symbol).";

        public int callbackOrder => -10;

        // Detect configuration errors on build.
        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            throw new UnityEditor.Build.BuildFailedException(
                "(Metaplay) Attempted to build Android application without External Dependency Manager for Unity (EDM4U) in the project. "
                + EDM4UErrorText);
        }

        // Detect configuration errors on load
        [InitializeOnLoadMethod]
        private static void InitializeForEditor()
        {
            throw new System.InvalidOperationException(
                "(Metaplay) This Android project is missing External Dependency Manager for Unity (EDM4U) and may not build correctly. "
                + EDM4UErrorText);
        }
    }
}

#endif
