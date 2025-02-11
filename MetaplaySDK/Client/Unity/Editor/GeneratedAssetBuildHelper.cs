// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Metaplay.Unity
{
    /// <summary>
    /// Helper for generating assets for builds.
    /// </summary>
    public static class GeneratedAssetBuildHelper
    {
        /// <summary>
        /// Gets the top level directory for generated assets. See <see cref="GetGenerationFolderForBuildTarget"/> for target-specific assets.
        /// This folder is deleted before and after a build.
        /// </summary>
        public const string GeneratedAssetBuildDirectory = "Assets/MetaplayGenerated_DONOTSAVE";

        /// <summary>
        /// Gets the asset directory that is included only on given platfrom.
        /// This folder is deleted before and after a build.
        /// </summary>
        public static string GetGenerationFolderForBuildTarget(BuildTarget buildTarget)
        {
            // Encode build target into the path. This is then parsed back in GeneratedDirImportPostprocessor
            return $"{GeneratedAssetBuildDirectory}/{buildTarget}";
        }
    }

    /// <summary>
    /// Sets import rules for the <see cref="GeneratedAssetBuildHelper.GeneratedAssetBuildDirectory"/> folder.
    /// </summary>
    class GeneratedDirImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            // The assets in the generated folder are structured as:
            //   Assets / <GeneratedFolder> / <BuildPlatform> / Asset
            //
            // This processor sets each *Asset* to only be included on the corresponding <Plaftorm>
            //
            // \note: This is almost equal to putting content in ../Plugins/{Android|iOS} folders and using default import
            //        rules to set the corresponding build platforms. The difference is that this operates on BuildTargets
            //        which are NOT EQUAL TO the default matching rules of Plugins, nor is there a DOCUMENTED mapping between
            //        BuildTargets and whatever Plugins path accepts.

            if (!assetPath.StartsWith(GeneratedAssetBuildHelper.GeneratedAssetBuildDirectory))
                return;

            // Extract <BuildPlatform> from the path. This gets us [ '', <BuildPlatform>, ... ]
            string[] components = assetPath.Substring(GeneratedAssetBuildHelper.GeneratedAssetBuildDirectory.Length).Split('/');
            if (components.Length < 3)
                return;

            BuildTarget buildPlatform = EnumUtil.Parse<BuildTarget>(components[1]);

            if (assetImporter is PluginImporter pluginImporter)
            {
                // Enable plugins only for the current target
                pluginImporter.ClearSettings();
                pluginImporter.SetCompatibleWithAnyPlatform(false);
                pluginImporter.SetCompatibleWithEditor(false);
                pluginImporter.SetCompatibleWithPlatform(buildPlatform, true);
            }
            else if (assetImporter.assetPath.EndsWith(".pdb") || assetImporter.assetPath.EndsWith(".dll.md5"))
            {
                // dummy assets, we can ignore these
            }
            else
            {
                throw new InvalidOperationException($"Could not set asset platform {buildPlatform} for {assetPath}");
            }
        }
    }

    /// <summary>
    /// Cleans the generated asset dir between the builds.
    /// </summary>
    class GeneratedDirCleaner : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            CleanDirectory();
            EditorPrefs.SetBool("Metaplay_GeneratedDirCleaner_OngoingBuild", true);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            CleanDirectory();
            EditorPrefs.DeleteKey("Metaplay_GeneratedDirCleaner_OngoingBuild");
        }

        static void CleanDirectory()
        {
            try
            {
                AssetDatabase.DeleteAsset(GeneratedAssetBuildHelper.GeneratedAssetBuildDirectory);
            }
            catch (Exception ex)
            {
                throw new BuildFailedException(ex);
            }
        }

        /// <summary>
        /// In case a build was interrupted, wipe the directory.
        /// </summary>
        [InitializeOnLoadMethod]
        static void InitForEditor()
        {
            if (EditorPrefs.GetBool("Metaplay_GeneratedDirCleaner_OngoingBuild"))
            {
                CleanDirectory();
                EditorPrefs.DeleteKey("Metaplay_GeneratedDirCleaner_OngoingBuild");
            }
        }
    }
}
