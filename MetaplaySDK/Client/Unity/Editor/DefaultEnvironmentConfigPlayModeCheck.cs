// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Client;
using System;
using UnityEditor;
using static Metaplay.Core.Client.DefaultEnvironmentConfigProvider;

public class DefaultEnvironmentConfigPlayModeCheck
{
    [InitializeOnLoadMethod]
    public static void Init()
    {
        EditorApplication.playModeStateChanged += ValidateActiveEnvironmentOnPlayMode;
    }

    static void ValidateActiveEnvironmentOnPlayMode(PlayModeStateChange playModeStateChange)
    {
        if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
        {
            // Check for no environment selected
            try
            {
                string activeEnvironmentId = DefaultEnvironmentConfigProvider.Instance.GetEditorActiveEnvironmentId();
                if (string.IsNullOrEmpty(activeEnvironmentId))
                {
                    if (EditorUtility.DisplayDialog("No active environment selected!", "Select an active environment in the Metaplay environment config editor window under 'Metaplay/Environment Configs'", "Exit play mode"))
                    {
                        EditorApplication.ExitPlaymode();
                    }
                }
            }
            catch (NotDefaultEnvironmentConfigProviderIntegrationException)
            {
                // DefaultEnvironmentConfigProvider is not in use. Doing nothing.
            }

            // Check that current environment id is valid and environment config getting works
            try
            {
                EnvironmentConfig _ = IEnvironmentConfigProvider.Get();
            }
            catch (Exception ex)
            {
                if (EditorUtility.DisplayDialog("Failed to get environment config!", ex.Message, "Exit play mode"))
                {
                    EditorApplication.ExitPlaymode();
                }
            }
        }
    }
}
