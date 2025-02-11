// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Serialization;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Metaplay.Unity
{
    public class GenerateClientBuildData : IPreprocessBuildWithReport
    {
        public int callbackOrder => 102;

        public void OnPreprocessBuild(BuildReport report)
        {
            Type clientBuildDataType = IntegrationRegistry.GetSingleIntegrationType<ClientBuildData>();
            // The generated file has leaked into the project, error out
            if (clientBuildDataType.Name == "GeneratedClientBuildData")
                throw new BuildFailedException($"Project contains class 'GeneratedClientBuildData' which should be generated during build. Please remove the file");

            string path = $"{GeneratedAssetBuildHelper.GeneratedAssetBuildDirectory}/GeneratedClientBuildData.cs";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Generate(clientBuildDataType));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        static string TryReadFromEnvironment(string property)
        {
            return Environment.GetEnvironmentVariable("METAPLAY_" + property.ToUpperInvariant());
        }

        static string TryReadFromCommandLine(string property)
        {
            string[] cmdLineArgs = Environment.GetCommandLineArgs();
            string   argToMatch  = "Metaplay" + property;
            for (int ndx = 0; ndx < cmdLineArgs.Length; ++ndx)
            {
                string arg = cmdLineArgs[ndx];
                if (arg.Equals("-" + argToMatch, StringComparison.OrdinalIgnoreCase) || arg.Equals("--" + argToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    if (ndx + 1 >= cmdLineArgs.Length)
                        throw new InvalidOperationException($"-Metaplay{property} command line command is missing the value");

                    return cmdLineArgs[ndx + 1];
                }

                if (arg.StartsWith($"-{argToMatch}=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring($"-{argToMatch}=".Length);
                }

                if (arg.StartsWith($"--{argToMatch}=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring($"--{argToMatch}=".Length);
                }
            }

            return null;
        }

        static string Generate(Type clientDataType)
        {
            IndentedStringBuilder sb = new IndentedStringBuilder(outputDebugCode: false);
            sb.AppendLine("using Metaplay.Unity;");
            sb.AppendLine($"public class GeneratedClientBuildData : {clientDataType.FullName}");
            sb.Indent("{");

            foreach (MemberInfo member in clientDataType.EnumerateInstanceDataMembersInUnspecifiedOrder())
            {
                if (member.GetDataMemberType() != typeof(string))
                    throw new InvalidOperationException("ClientBuildData members must be of type string");
                if (!member.DataMemberIsOverridable())
                    throw new InvalidOperationException("ClientBuildData members must be overrideable");

                string propertyName  = member.Name;
                string propertyValue = TryReadFromCommandLine(propertyName);
                if (propertyValue != null)
                    Debug.Log($"Using {clientDataType.Name}.{propertyName} = \"{propertyValue}\" from command line");
                else
                {
                    propertyValue = TryReadFromEnvironment(propertyName);
                    if (propertyValue != null)
                        Debug.Log($"Using {clientDataType.Name}.{propertyName} = \"{propertyValue}\" from environment");
                }

                if (propertyValue != null)
                    sb.AppendLine($"public override string {propertyName} => \"{propertyValue}\";");
            }

            sb.Unindent("}");
            return sb.ToString();
        }
    }
}
