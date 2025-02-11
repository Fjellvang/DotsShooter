// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Enforce that the client-side Metaplay SDK is in a separate assembly
/// from the user-side client code, as that is how the SDK is designed to be used.
/// <para>
/// There are certain language features that work differently depending on whether
/// things are in the same or different assemblies, for example involving the
/// `internal` keyword, so it's good to enforce a consistent setup across projects.
/// </para>
/// </summary>
public static class SdkUserAssemblySeparationEnforcement
{
    [InitializeOnLoadMethod]
    public static void InitializeOnLoad()
    {
        if (!MetaplayCoreEditor.EnsureInitialized())
            return;

        // On init: log if error is detected; but also hook to play mode change to make the error more noticeable.

        if (TryGetJointAssemblyError(out string errorText))
        {
            Debug.LogError(errorText);
            EditorApplication.playModeStateChanged += stateChange => ShowErrorOnPlayMode(stateChange, errorText);
        }
    }

    static void ShowErrorOnPlayMode(PlayModeStateChange playModeStateChange, string errorText)
    {
        if (playModeStateChange == PlayModeStateChange.ExitingEditMode)
        {
            if (EditorUtility.DisplayDialog("SDK and user assemblies not separated", errorText, "Exit play mode"))
                EditorApplication.ExitPlaymode();
        }
    }

    static bool TryGetJointAssemblyError(out string errorText)
    {
        // Determine user and SDK assemblies by looking at the declaring assemblies
        // of certain well-known types.

        // Choose an arbitrary SDK type.
        Type sdkType = typeof(IMetaplayCoreOptionsProvider);

        // Choose an arbitrary user type.
        // IMetaplayCoreOptionsProvider is a mandatory integration type whose
        // implementation is known to exist on the user side (there is no SDK implementation of it).
        Type userType = IntegrationRegistry.Get<IMetaplayCoreOptionsProvider>().GetType();

        // Assembly separation is assumed to be properly in place
        // if the user and SDK assemblies are different.
        if (userType.Assembly != sdkType.Assembly)
        {
            errorText = null;
            return false;
        }

        errorText =
            $"Metaplay client SDK and user-side game logic code have the same assembly: {sdkType.Assembly} .\n" +
            "The Metaplay SDK is intended to be in its own assembly separate from user code.\n" +
            "This error is likely caused by an .asmref in your client project referring to Metaplay.\n" +
            "Please find it and replace it with an .asmdef file instead. Test that the project still works and commit the changes to your repository.\n" +
            "Unless you have specific needs, you can use the following content for SharedCode.asmdef:\n" +
            SharedCodeAsmdefContent;
        return true;
    }

    const string SharedCodeAsmdefContent =
@"{
    ""name"": ""SharedCode"",
    ""rootNamespace"": """",
    ""references"": [
        ""Metaplay""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": true
}";
}
