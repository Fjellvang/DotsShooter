// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Metaplay.Cloud.RuntimeOptions
{
    /// <summary>
    /// The environment variables represented as a list of RuntimeOptions definitions.
    /// </summary>
    public class EnvironmentRuntimeOptionsSource
    {
        public class AmbiguousEnvironmentError : Exception
        {
            public AmbiguousEnvironmentError(string message) : base(message)
            {
            }
        }

        readonly MetaDictionary<string, string> _fields;
        public IReadOnlyDictionary<string, string> Definitions => _fields;
        public IConfigurationSource ConfigurationSource => new FixedFieldsConfigurationSource(_fields);

        EnvironmentRuntimeOptionsSource(MetaDictionary<string, string> fields)
        {
            _fields = fields;
        }

        /// <summary>
        /// Parse the environment and returns EnvironmentRuntimeOptionsSource form the contents.
        /// </summary>
        /// <param name="environmentAliases">Extra mappings from an environment variable to a config key. Should be sourced with [EnvironmentVariable]</param>
        public static EnvironmentRuntimeOptionsSource Parse(string prefix, List<(string, string)> environmentAliases)
        {
            return ParseFromEnvironment(prefix, environmentAliases, GetEnvironmentVariables());
        }

        /// <summary>
        /// Parse the given environment and returns EnvironmentRuntimeOptionsSource form the contents.
        /// </summary>
        public static EnvironmentRuntimeOptionsSource ParseFromEnvironment(string prefix, List<(string, string)> environmentAliasesList, MetaDictionary<string, string> environmentVariables)
        {
            MetaDictionary<string, string>       fields      = new MetaDictionary<string, string>();
            MetaDictionary<string, string>       fieldSource = new MetaDictionary<string, string>();
            MetaDictionary<string, List<string>> aliases     = GetAliasLookup(environmentAliasesList);

            foreach ((string environmentVariable, string value) in environmentVariables)
            {
                // Aliases work regardles of default-ignored names and prefixes.
                if (aliases.TryGetValue(environmentVariable.ToLowerInvariant(), out List<string> aliasTargets))
                {
                    foreach (string aliasTarget in aliasTargets)
                    {
                        if (fields.TryAdd(aliasTarget, value))
                        {
                            fieldSource[aliasTarget] = $"{environmentVariable} (explicit [EnvironmentVariable])";
                        }
                        else
                        {
                            throw new AmbiguousEnvironmentError($"Cannot resolve value for {aliasTarget}. Value defined both via {environmentVariable} (explicit [EnvironmentVariable]) and {fieldSource[aliasTarget]}.");
                        }
                    }
                }

                // Normal name resolution
                if (IsIgnoredEnvironmentVariable(environmentVariable))
                    continue;
                if (!environmentVariable.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string configKey = environmentVariable.Substring(prefix.Length).Replace("__", ":");
                if (fields.TryAdd(configKey, value))
                {
                    fieldSource[configKey] = $"{environmentVariable}";
                }
                else
                {
                    throw new AmbiguousEnvironmentError($"Cannot resolve value for {configKey}. Value defined both via {environmentVariable} and {fieldSource[configKey]}.");
                }
            }

            return new EnvironmentRuntimeOptionsSource(fields);
        }

        static MetaDictionary<string, string> GetEnvironmentVariables()
        {
            MetaDictionary<string, string> values = new MetaDictionary<string, string>();
            foreach (string environmentVariable in Environment.GetEnvironmentVariables().Keys)
            {
                string value = Environment.GetEnvironmentVariable(environmentVariable) ?? "";
                values.Add(environmentVariable, value);
            }
            return values;
        }

        static bool IsIgnoredEnvironmentVariable(string sourceEnvVar)
        {
            // Ignore certain well-known environment variables here. They look like options, so the parser is confused, but are harmless
            // \note: refer to env vars with complete name to make Grepping easier.
            string[] ignoredEnvironmentVariables = new string[]
            {
                "METAPLAY_ENVIRONMENT_FAMILY",
                "METAPLAY_OPTIONS",
                "METAPLAY_EXTRA_OPTIONS",

                // legacy options. Avoid warnings in backwards-compatible environments
                "METAPLAY_GRAFANAURL",
                "METAPLAY_KUBERNETESNAMESPACE",
                "METAPLAY_IP",
                "METAPLAY_REMOTINGHOSTSUFFIX",
                "METAPLAY_ENVIRONMENT",
                "METAPLAY_FILELOGPATH",
                "METAPLAY_LOGFORMAT",
                "METAPLAY_LOGFILERETAINCOUNT",
                "METAPLAY_FILELOGSIZELIMIT",
                "METAPLAY_CLUSTERCOOKIE",
                "METAPLAY_CLIENTPORTS",

                // environment variables that look like options. Avoid warnings.
                "METAPLAY_CLUSTERCONFIGJSON",
                "METAPLAY_CLIENT_SVC_*",
            };

            foreach (string ignoredVariable in ignoredEnvironmentVariables)
            {
                // allow minimal wildcard syntax
                if (ignoredVariable.EndsWith('*'))
                {
                    string prefix = ignoredVariable.Substring(0, ignoredVariable.Length - 1);
                    if (sourceEnvVar.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else
                {
                    if (string.Equals(sourceEnvVar, ignoredVariable, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        static MetaDictionary<string, List<string>> GetAliasLookup(List<(string, string)> environmentAliasesList)
        {
            MetaDictionary<string, List<string>> lookup = new MetaDictionary<string, List<string>>();
            foreach ((string envVariable, string configKey) in environmentAliasesList)
            {
                string lookupKey = envVariable.ToLowerInvariant();
                lookup.AddIfAbsent(lookupKey, new List<string>());
                lookup[lookupKey].Add(configKey);
            }
            return lookup;
        }
    }
}
