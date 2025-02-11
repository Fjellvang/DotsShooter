// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Unity;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

/// <summary>
/// Unity build hooks for generating Metaplay serializer .dll used in builds.
/// </summary>
public class MetaplaySerializerBuilder : IPreprocessBuildWithReport, IUnityLinkerProcessor
{
    public int callbackOrder => 0;

    NamedBuildTarget GetActiveNamedBuildTarget(BuildSummary summary)
    {
        // Server build must be detected separately
        if (summary.platformGroup == BuildTargetGroup.Standalone)
        {
            if (summary.GetSubtarget<StandaloneBuildSubtarget>() == StandaloneBuildSubtarget.Server)
                return NamedBuildTarget.Server;
        }

        return NamedBuildTarget.FromBuildTargetGroup(summary.platformGroup);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
            Debug.Log($"MetaplaySerializerBuilder.OnPreprocessBuild(): platform={report.summary.platform}, outputPath={report.summary.outputPath}");

            if (!IsActivePlatformCompatibleWithBuildTarget(report.summary.platform))
                throw new InvalidOperationException(
                    $"The active editor platform is not compatible with build target {report.summary.platform}, " +
                    "switch active platform before building. If you are using batch mode to invoke the build, invoke Unity with -buildTarget command line switch.");

            ScriptingImplementation scriptingBackend = PlayerSettings.GetScriptingBackend(GetActiveNamedBuildTarget(report.summary));
            if (scriptingBackend != ScriptingImplementation.IL2CPP)
            {
                Debug.LogWarning($"[Metaplay] Build with scripting backend {scriptingBackend} uses slow serialization code, consider switching to IL2CPP");
            }

            // Delete generated DLL from old location
            AssetDatabase.DeleteAsset("Assets/Metaplay/Metaplay.Generated.dll");
            AssetDatabase.DeleteAsset("Assets/Metaplay/Metaplay.Generated.pdb.dll");
            AssetDatabase.DeleteAsset("Assets/Metaplay.Generated.dll");
            AssetDatabase.DeleteAsset("Assets/Metaplay.Generated.pdb.dll");

            BuildDll(report.summary.platform, scriptingBackend);
        }
        catch (Exception ex)
        {
            throw new BuildFailedException(ex);
        }
    }

    bool IsActivePlatformCompatibleWithBuildTarget(BuildTarget summaryPlatform)
    {
        return EditorUserBuildSettings.activeBuildTarget == summaryPlatform;
    }

    public static void BuildDll(BuildTarget buildTarget, ScriptingImplementation scriptingBackend)
    {
        string outputDir   = GeneratedAssetBuildHelper.GetGenerationFolderForBuildTarget(buildTarget);
        string dllFileName = $"Metaplay.Generated.{ClientPlatformUnityUtil.GetBuildTargetPlatform()}.dll";

        // Ensure Metaplay.Generated.<Platform>.dll is up-to-date. Using TypeInfo from the current registry service.
        RoslynSerializerCompileCache.EnsureDllUpToDate(
            outputDir: outputDir,
            dllFileName: dllFileName,
            errorDir: MetaplaySDK.UnityTempDirectory,
            enableCaching: false,
            forceRoslyn: true,
            useMemberAccessTrampolines: scriptingBackend != ScriptingImplementation.IL2CPP,
            generateRuntimeTypeInfo: true,
            MetaplayServices.Get<MetaSerializerTypeRegistry>().TypeInfo);

        // Inform Unity of the generated .dll
        AssetDatabase.ImportAsset($"{outputDir}/{dllFileName}", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
    {
        // Add link.xml from Metaplay SDK package to the build to preserve Metaplay assemblies.
        // This is required because Unity doesn't automatically scan for link.xml files in packages.
        //
        // This also preserves the user project's shared code assembly (usually named SharedCode)
        // by detecting its name and injecting it to the link.xml.

        // The GUID needs to match the value in link.xml.meta.
        const string linkXmlGuid = "b9afe0407b8f8104b8f0523b1ae0ab49";
        var          assetPath   = AssetDatabase.GUIDToAssetPath(linkXmlGuid);
        if (string.IsNullOrEmpty(assetPath))
        {
            throw new FileNotFoundException("Could not locate link.xml file in Metaplay SDK package!");
        }

        string linkXmlContent = File.ReadAllText(Path.GetFullPath(assetPath));

        // Inject the name of user's shared code assembly.
        string       userSharedCodeAssembly = IntegrationRegistry.GetSingleIntegrationType<IPlayerModelBase>().Assembly.GetName().Name;
        const string PredefinedAssemblyName = "Assembly-CSharp";
        if (userSharedCodeAssembly == PredefinedAssemblyName)
        {
            Debug.LogWarning(
                $"Server-client shared game code appears to be in the predefined client assembly \"{PredefinedAssemblyName}\". " +
                "Shared code is normally added to link.xml to preserve it from Unity's code stripping. " +
                $"However, \"{PredefinedAssemblyName}\" will not be added to link.xml, as it tends to contain lots of types unrelated to Metaplay game logic. " +
                "It is recommended to put shared code into its own assembly.");

            // \note Leaving linkXmlContent as is, without replacing or removing the template {{USER_SHARED_CODE_ASSEMBLY_NAME}}.
            //       A bit hacky, but unknown assembly names in link.xml don't seem to cause problems.
        }
        else
            linkXmlContent = linkXmlContent.Replace("{{USER_SHARED_CODE_ASSEMBLY_NAME}}", userSharedCodeAssembly);

        linkXmlContent = linkXmlContent.Replace("{{GENERATED_SERIALIZER_ASSEMBLY_NAME_SUFFIX}}", ClientPlatformUnityUtil.GetBuildTargetPlatform().ToString());

        // Write final content to a temporary file.
        string outputPath = $"{MetaplaySDK.UnityTempDirectory}/metaplay-generated-link.xml";
        File.WriteAllText(outputPath, linkXmlContent);
        return Path.GetFullPath(outputPath);
    }
}
