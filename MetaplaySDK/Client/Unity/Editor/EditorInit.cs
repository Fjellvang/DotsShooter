using Metaplay.Core;
using Metaplay.Unity;
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity hook initializing Metaplay core in Unity Editor.
/// </summary>
public static class MetaplayCoreEditor
{
    static void Init()
    {
        MetaplayCore.Initialize(
            initializers =>
            {
                initializers.ConfigureMetaSerialization("UnityEditor", MetaplaySDK.UnityTempDirectory, useMemberAccessTrampolines: true);
            });
    }

    /// <summary>
    /// Initializes the Metaplay SDK for Editor use if not already initialized, returns false of initialization was unsuccessful.
    /// This should only be called from `InitializeOnLoadMethod` functions, all other code can rely on initialization to have been
    /// done during Editor load.
    /// </summary>
    public static bool EnsureInitialized()
    {
        try
        {
            Init();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        try
        {
            Init();
        }
        catch (MissingIntegrationException)
        {
            // Getting this error in editor means that game integration is incomplete.
            Debug.LogWarning("Metaplay SDK integration incomplete, editor functionality disabled");
        }
        catch (Exception ex)
        {
            Debug.LogError("Metaplay SDK initialization failed!");
            Debug.LogError(ex);

            // \note: we don't need to unhook this. When the error is fixed (or the code is modified for any reason), the domain is reloaded and the hook is lost.
            EditorApplication.playModeStateChanged += (PlayModeStateChange change) =>
            {
                if (change != PlayModeStateChange.ExitingEditMode)
                    return;

                Debug.LogError("Metaplay SDK initialization failed!");
                Debug.LogError(ex);

                bool userAbortedLaunch = EditorUtility.DisplayDialog("Metaplay SDK initialization error.", "Error during initialization. MetaplaySDK is not initialized. See Console for details.", "Abort", "Run anyway");
                if (userAbortedLaunch)
                    EditorApplication.ExitPlaymode();
            };
        }
    }
}
