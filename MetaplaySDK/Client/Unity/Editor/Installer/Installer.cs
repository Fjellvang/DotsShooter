// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using FileUtil = Metaplay.Core.FileUtil;

namespace Metaplay
{
    public class Installer
    {
        [Serializable]
        public class TemplateFile
        {
            public string Path;     // Path of file
            public string Text;     // Content of text files
            public string Bytes;    // Content of binary files (base64-encoded bytes)
        }

        [Serializable]
        public class TemplatePayload
        {
            public int              Version;    // File format version
            public TemplateFile[]   Files;      // Files of the project template
        }

        class InstallError : Exception
        {
            public InstallError(string message) : base("MetaplaySDK Installation error! Please contact Metaplay for support. Internal error message: " + message)
            {
            }
        }

#if !METAPLAY_SAMPLE
        // Helper method for testing initializing a project from the command line
        public static void ForceInitializeMetaplayProject()
        {
            Debug.Log("Installer: ForceInitializeMetaplayProject()");
            InitializeProject();
        }

        internal static void InitializeProject()
        {
            // Import files from the project template into the target project
            string metaplayClientPath = UnityBuildUtil.ResolveMetaplayClientPath(debugLog: msg => Debug.Log($"Installer: {msg}"));
            ImportFilesFromTemplate(metaplayClientPath, ".", "project_template.json");

            // Refresh the asset database to reflect the changes files
            AssetDatabase.Refresh();
            Debug.Log("Installer: Metaplay project initialization successful");
        }

        public static void SetupCustomDashboardProject()
        {
            Debug.Log("Installer: SetupCustomDashboardProject()");

            string metaplayClientPath = UnityBuildUtil.ResolveMetaplayClientPath(debugLog: msg => Debug.Log($"Installer: {msg}"));
            string nodeModulesDir     = FileUtil.NormalizePath(Path.Combine(metaplayClientPath, "../Frontend"));

            ImportFilesFromTemplate(metaplayClientPath, "Backend/Dashboard", "dashboard_template.json");

            // Create pnpm workspace file
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("packages:");
            sb.AppendLine($" - {nodeModulesDir}/*");
            sb.AppendLine($" - Backend/Dashboard");
            File.WriteAllText(FileUtil.NormalizePath(Path.Combine(Application.dataPath, "../pnpm-workspace.yaml")), sb.ToString());

            // Create vscode workspace
            File.WriteAllText(FileUtil.NormalizePath(Path.Combine(Application.dataPath, "../Backend/Server and Dash.code-workspace")), @"{
	""folders"": [
		{
			""path"": ""./Dashboard"",
			""name"": ""Dashboard""
		},
		{
			""path"": ""./Server"",
			""name"": ""Game Server""
		},
		{
			""path"": ""../MetaplaySDK/Backend/Server"",
			""name"": ""SDK Game Server""
		},
		{
			""path"": ""../MetaplaySDK/Frontend""
		}
	],
}");
            Debug.Log("Installer: Metaplay custom dashboard setup successful");
        }
#endif

        /// <summary>
        /// Get file name for the imported server .sln file, '{productName}-Server.sln'
        /// </summary>
        /// <returns></returns>
        static string GetBackendSolutionFileName()
        {
            // Use Unity productName as base name, with invalid characters filtered out
            string productName = Application.productName;
            char[] invalidChars = Path.GetInvalidFileNameChars();
            productName = new string(productName.Where(ch => !invalidChars.Contains(ch)).ToArray());

            // Return "<product>-Server.sln" or just "Server.sln" if productName is completely empty
            return productName != "" ? $"{productName}-Server.sln" : "Server.sln";
        }

        /// <summary>
        /// Get name of the project for custom dashboard.
        /// </summary>
        /// <returns></returns>
        static string GetProjectName()
        {
            string productName = Application.productName;
            // Replace whitespace with hyphen
            productName = productName.Replace(' ', '-');
            // Only accept letters, digits and '-'
            productName = new string(productName.Where(ch => char.IsLetterOrDigit(ch) || ch == '-').ToArray()).ToLower();
            return productName != "" ? productName : "game";
        }

        static void ImportFilesFromTemplate(string metaplayClientPackagePath, string dstDir, string templateFile)
        {
            // Resolve path to installer project template
            string templatePath = FileUtil.NormalizePath(Path.Combine(metaplayClientPackagePath, "../Installer", templateFile));
            if (!File.Exists(templatePath))
                throw new InstallError($"Unable to find template file at {templatePath}.");

            // Parse the template
            string templateJson = FileUtil.ReadAllText(templatePath);
            TemplatePayload template = JsonUtility.FromJson<TemplatePayload>(templateJson);

            if (template.Version != 1)
                throw new InstallError($"Unsupported installer project template version {template.Version}");
            if (template.Files == null || template.Files.Length == 0)
                throw new InstallError($"Installer project template does not have any files!");

            string dstRoot                 = FileUtil.NormalizePath(Path.Combine(Application.dataPath, "..", dstDir));
            string backendSolutionFileName = GetBackendSolutionFileName();
            string projectName             = GetProjectName();

            string relativePathToSdk = FileUtil.NormalizePath(Path.Combine(metaplayClientPackagePath, "..")); // path from targetProject to MetaplaySDK
            Debug.Log($"Installer: Relative path to MetaplaySDK/: {relativePathToSdk}");

            // Extract all files from the template definition
            foreach (TemplateFile file in template.Files)
            {
                // Resolve destination path (fill in templates)
                string dstPath = Path.Combine(dstRoot, file.Path);
                dstPath = dstPath.Replace("{{{BACKEND_SOLUTION_FILENAME}}}", backendSolutionFileName);
                if (dstPath.Contains("{{{") || dstPath.Contains("}}}"))
                    throw new InstallError($"Template file path {dstPath} contains unhandled template strings!");

                // Ensure destination directory exists
                string dirName = Path.GetDirectoryName(dstPath);
                if (!Directory.Exists(dirName))
                    Directory.CreateDirectory(dirName);

                // Write the file: with template replacements for text files, base64-decoding for binary files
                if (file.Text != null)
                {
                    Debug.Log($"Write: {dstPath} text");
                    string content = file.Text;
                    content = content.Replace("{{{RELATIVE_PATH_TO_SDK}}}", relativePathToSdk);
                    content = content.Replace("{{{PROJECT_NAME}}}", projectName);
                    if (content.Contains("{{{") || content.Contains("}}}"))
                        throw new InstallError($"Template file {dstPath} contains unhandled template strings!");
                    File.WriteAllText(dstPath, content);
                }
                else
                {
                    Debug.Log($"Write {dstPath} binary");
                    byte[] bytes = Convert.FromBase64String(file.Bytes);
                    File.WriteAllBytes(dstPath, bytes);
                }
            }
        }
    }
}
