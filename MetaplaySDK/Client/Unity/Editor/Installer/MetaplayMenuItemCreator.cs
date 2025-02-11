// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using Unity.Editor;
using UnityEditor;
using UnityEngine;

namespace Metaplay.Unity
{
    public static class MetaplayMenuItemCreator
    {
        const string CreateDefaultGameConfigBuilderMenuPath = "Metaplay/Enable Default Game Config Build Window";
        const string SetupDashboardMenuPath = "Metaplay/Setup Custom Dashboard";
        const string InitializeProjectMenuPath = "Metaplay/Initialize Project";

        [InitializeOnLoadMethod]
        static void UpdateMenuItems()
        {
            #if !METAPLAY_SAMPLE
            EditorApplication.delayCall += CreateNonSampleMenuItems;
            #endif

            EditorApplication.delayCall += CreateMenuItems;
        }

        public static void CreateMenuItems()
        {
        }

#if !METAPLAY_SAMPLE
        public static void CreateNonSampleMenuItems()
        {
            MenuWrapper.RebuildAllMenus();
            MetaplayUnitySettings settings = MetaplayUnitySettings.instance;
            AddConditionalMenuItem(!settings.IsProjectInitialized, InitializeProjectMenuPath, () =>
            {
                try
                {
                    Installer.InitializeProject();

                    settings.IsProjectInitialized = true;
                    settings.Save();

                    UpdateMenuItems();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Metaplay project initialization failed: {ex}");
                    EditorUtility.DisplayDialog("Initialization Error", $"Metaplay project initialization failed: {ex}", "Ok");
                }
            }, 1);
            AddConditionalMenuItem(!settings.CustomDashboardCreated, SetupDashboardMenuPath, () =>
            {
                try
                {
                    Installer.SetupCustomDashboardProject();

                    settings.CustomDashboardCreated = true;
                    settings.Save();

                    UpdateMenuItems();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Metaplay custom dashboard setup failed: {ex}");
                    EditorUtility.DisplayDialog("Initialization Error", $"Metaplay custom dashboard setup failed: {ex}", "Ok");
                }
            }, 2);
            MenuWrapper.UpdateAllMenus();
        }
#endif

        public static void AddConditionalMenuItem(bool condition, string menuPath, Action clickHandler, int priority = 1000)
        {
            var menuExist       = MenuWrapper.MenuItemExists(menuPath);
            var menuShouldExist = condition;

            // Menu is removed but should be added
            if (!menuExist && menuShouldExist)
            {
                MenuWrapper.AddMenuItem(menuPath, "", false, priority,
                    clickHandler,
                    () => true);
            }
            // Menu exist but should be removed
            else if (menuExist && !menuShouldExist)
            {
                MenuWrapper.RemoveMenuItem(menuPath);
            }
        }
    }
}
