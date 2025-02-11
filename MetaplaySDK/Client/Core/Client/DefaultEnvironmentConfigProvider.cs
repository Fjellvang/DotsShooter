// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Network;
using Metaplay.Core.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using FileUtil = Metaplay.Core.FileUtil;
#if UNITY_2017_1_OR_NEWER
using UnityEngine;
#endif
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

namespace Metaplay.Core.Client
{
    public class DefaultEnvironmentConfigProvider : IEnvironmentConfigProvider
    {
        protected virtual string ActiveEnvOverrideEditorPrefsKey    => $"Metaplay/{IntegrationRegistry.Get<IMetaplayCoreOptionsProvider>().Options.ProjectName}/Active_Editor_Environment";

        protected virtual string EditorEnvironmentConfigsFileName   => "EnvironmentConfigs";
        protected virtual string EnvironmentConfigsFileExt          => ".json";
        protected virtual string ActiveEnvironmentConfigIdFileExt   => ".txt";
        protected virtual string ResourcesFolder                    => "Assets/Resources";
        protected virtual string ProjectSettingsFolder              => "ProjectSettings";
        protected virtual string EditorEnvironmentConfigsFolderPath => "Metaplay";
        protected virtual string BuildEnvironmentConfigsFileName    => "BuildEnvironmentConfigs";
        protected virtual string BuildEnvironmentConfigsFolderPath  => "Metaplay/BuiltEnvironmentConfigs";
        protected virtual string ActiveEnvironmentConfigIdFileName  => "BuildActiveEnvironmentId";

        /// <summary>
        /// Cache for active environment id, set in <see cref="InitializeSingleton"/> and in <see cref="SetActiveEnvironmentId"/>
        /// </summary>
        protected string _activeEnvironmentId = "";
        protected string _activeEnvironmentIdEditorOverride;
        protected string _activeEnvironmentIdRuntimeOverride;

        protected List<EnvironmentConfig> _environmentConfigs;

        public bool Initialized = false; // Used by editor window

        public EnvironmentConfig GetCurrent() => GetEnvironmentConfig(GetActiveEnvironmentId());

        /// <inheritdoc cref="GetInstance"/>
        public static DefaultEnvironmentConfigProvider Instance => GetInstance();

        /// <summary>
        /// Gets the provider singleton instance. Throws an <see cref="NotDefaultEnvironmentConfigProviderIntegrationException"/>
        /// if the current IEnvironmentConfigProvider implementation is not of type DefaultEnvironmentConfigProvider, meaning another
        /// provider implementation is in use.
        /// </summary>
        /// <exception cref="NotDefaultEnvironmentConfigProviderIntegrationException">If the current Environment Config Provider is not the default</exception>
        private static DefaultEnvironmentConfigProvider GetInstance()
        {
            if (!MetaplayServices.IsInitialized)
                throw new NotDefaultEnvironmentConfigProviderIntegrationException("[Metaplay] MetaplayCore is not initialized. This is most likely due to an error during SDK init. Check logs for details.");

            IEnvironmentConfigProvider provider = IntegrationRegistry.Get<IEnvironmentConfigProvider>();
            if (provider is DefaultEnvironmentConfigProvider defaultProvider)
            {
                return defaultProvider;
            }
            else
            {
                throw new NotDefaultEnvironmentConfigProviderIntegrationException($"[Metaplay] Current IEnvironmentConfigProvider implementation is not of type DefaultEnvironmentConfigProvider. The current provider is {provider.GetType().ToGenericTypeString()}");
            }
        }

        public class NotDefaultEnvironmentConfigProviderIntegrationException : Exception
        {
            public NotDefaultEnvironmentConfigProviderIntegrationException(string message) : base(message) { }
        }

        public class EnvironmentConfigClientBuildOptions
        {
            public string              ActiveEnvironmentId;
            public EnvironmentConfig[] EnvironmentsToIncludeInBuild;

            public EnvironmentConfigClientBuildOptions(string activeEnvironmentId, EnvironmentConfig[] environments)
            {
                ActiveEnvironmentId          = activeEnvironmentId;
                EnvironmentsToIncludeInBuild = environments;
            }
        }

        public DefaultEnvironmentConfigProvider() { }

        public virtual void InitializeSingleton()
        {
            // Load configs
#if NETCOREAPP
            return;
#elif UNITY_EDITOR
            _environmentConfigs = LoadConfigs(EditorEnvironmentConfigsFolderPath, EditorEnvironmentConfigsFileName);
            _activeEnvironmentIdEditorOverride = LoadActiveEnvironmentIdEditorOverride();
#else
            _environmentConfigs = LoadConfigs(BuildEnvironmentConfigsFolderPath, BuildEnvironmentConfigsFileName);
#endif
#pragma warning disable 0162 // Unreachable code detected
            _activeEnvironmentId   = GetActiveEnvironmentId();

            Initialized = true;
#pragma warning restore 0162 // Unreachable code detected
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reloads environment configs from file.
        /// </summary>
        public virtual void ReloadConfigs()
        {
            _environmentConfigs = LoadConfigs(EditorEnvironmentConfigsFolderPath, EditorEnvironmentConfigsFileName);
        }

        /// <summary>
        /// Gets the environment config build options defined in-editor.
        /// </summary>
        public virtual EnvironmentConfigClientBuildOptions GetEditorDefinedBuildOptions()
        {
            string activeEnvironmentId = LoadActiveEnvironmentId();

            List<EnvironmentConfig> environmentsToIncludeInBuild = new List<EnvironmentConfig>()
            {
                GetEnvironmentConfig(activeEnvironmentId),
            };

            return new EnvironmentConfigClientBuildOptions(activeEnvironmentId, environmentsToIncludeInBuild.ToArray());
        }

        /// <summary>
        /// Gets the JSON preview of the filtered environment configs that would be included in a client build. Used in Environment Configs editor window.
        /// </summary>
        public virtual string GetEditorDefinedBuildEnvironmentsPreview()
        {
            EnvironmentConfigClientBuildOptions buildOptions = GetEditorDefinedBuildOptions();
            EnvironmentConfig[] filteredEnvironments = FilterEnvironmentConfigs(buildOptions.EnvironmentsToIncludeInBuild);
            return ConvertToJson(filteredEnvironments);
        }

        /// <summary>
        /// Prepare the provider for client build by copying and creating necessary environment config files in a client accessible location.
        /// Filters out unwanted environments from files included in the build, and filters the included environments to not include unwanted data.
        /// Uses the active environment and build included environments values stored internally and set in the Unity Environment Configs editor window.
        /// </summary>
        public virtual void BuildEnvironmentConfigs()
        {
            try
            {
                ReloadConfigs();

                BuildEnvironmentConfigs(GetEditorDefinedBuildOptions());
            }
            catch (Exception ex)
            {
                throw new BuildFailedException("[Metaplay] Environment configs failed to build: " + ex);
            }
        }

        /// <summary>
        /// Prepare the provider for client build by copying and creating necessary environment config files in a client accessible location.
        /// Filters out unwanted environments from files included in the build, and filters the included environments to not include unwanted data.
        /// Finds the given environments from the environment configs master file.
        /// </summary>
        /// <param name="activeEnvironmentId">The ID of the active environment for the build</param>
        /// <param name="includedEnvironmentsIds">
        /// The set of environments included in the build. Normally this should be <c>new string[]{ activeEnvironmentId } </c>,
        /// but may contain other environments too if the client can change environment at runtime with <see cref="SetActiveEnvironmentId"/>.
        /// </param>
        public virtual void BuildEnvironmentConfigs(string activeEnvironmentId, string[] includedEnvironmentsIds)
        {
            if (!includedEnvironmentsIds.Contains(activeEnvironmentId))
            {
                throw new BuildFailedException("[Metaplay] Given active environment Id is not included in given build included environments.");
            }

            try
            {
                ReloadConfigs();

                List<EnvironmentConfig> environments = new List<EnvironmentConfig>();
                foreach (string envId in includedEnvironmentsIds)
                {
                    environments.Add(GetEnvironmentConfig(envId));
                }

                BuildEnvironmentConfigs(new EnvironmentConfigClientBuildOptions(activeEnvironmentId, environments.ToArray()));
            }
            catch (Exception ex)
            {
                throw new BuildFailedException("[Metaplay] Environment configs failed to build: " + ex);
            }
        }

        /// <summary>
        /// Prepare the provider for client build by copying and creating necessary files in a client accessible location.
        /// Filters out unwanted environments from files included in the build, and filters the included environments to not include unwanted data.
        /// </summary>
        public virtual void BuildEnvironmentConfigs(EnvironmentConfigClientBuildOptions options)
        {
            // Configs are stored in an Editor folder, therefore they will not be included in build by default.
            // So we create a new file outside the Editor folder to hold only the filtered configs that will be included in the build.

            EnvironmentConfig[] filteredEnvironmentsInBuild = FilterEnvironmentConfigs(options.EnvironmentsToIncludeInBuild);

            // Write active environment id to file
            WriteActiveEnvironmentIdToFile(Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath), ActiveEnvironmentConfigIdFileName, options.ActiveEnvironmentId);

            // Write build-included environments to file that will be included in build
            WriteEnvironmentConfigsToFile(Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath), BuildEnvironmentConfigsFileName, filteredEnvironmentsInBuild.ToArray());
        }

        /// <summary>
        /// Creates filtered copies of the given environment configs. <see cref="EnvironmentConfig.CreateClientBuildCopy"/>
        /// </summary>
        public virtual EnvironmentConfig[] FilterEnvironmentConfigs(EnvironmentConfig[] environments)
        {
            List<EnvironmentConfig> environmentsInBuild = new List<EnvironmentConfig>();
            foreach (EnvironmentConfig config in environments)
            {
                environmentsInBuild.Add(config.CreateClientBuildCopy());
            }

            return environmentsInBuild.ToArray();
        }

        /// <summary>
        /// Deletes the build files created in BuildEnvironmentConfigs. Also deletes the directory they were in if it's empty.
        /// </summary>
        public virtual void DeleteBuiltFiles()
        {
#pragma warning disable MP_WGL_00 // Feature is poorly supported in WebGL
            string builtActiveEnvironmentIdFilePath = Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath, ActiveEnvironmentConfigIdFileName) + ActiveEnvironmentConfigIdFileExt;
            File.Delete(builtActiveEnvironmentIdFilePath);
            File.Delete(builtActiveEnvironmentIdFilePath + ".meta");
            string builtEnvironmentsFilePath = Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath, BuildEnvironmentConfigsFileName) + EnvironmentConfigsFileExt;
            File.Delete(builtEnvironmentsFilePath);
            File.Delete(builtEnvironmentsFilePath + ".meta");

            string folderPath = Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath);
            if (Directory.GetFiles(folderPath).Length == 0 && Directory.GetDirectories(folderPath).Length == 0)
            {
                Directory.Delete(folderPath);
                File.Delete(folderPath + ".meta");
            }
#pragma warning restore MP_WGL_00 // Feature is poorly supported in WebGL

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Returns true if both build files exist, false otherwise.
        /// </summary>
        public virtual bool IsBuilt()
        {
#pragma warning disable MP_WGL_00 // Feature is poorly supported in WebGL
            return (File.Exists(Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath, ActiveEnvironmentConfigIdFileName) + ActiveEnvironmentConfigIdFileExt)
                && File.Exists(Path.Join(ResourcesFolder, BuildEnvironmentConfigsFolderPath, BuildEnvironmentConfigsFileName) + EnvironmentConfigsFileExt));
#pragma warning restore MP_WGL_00 // Feature is poorly supported in WebGL
        }

        /// <summary>
        /// Use to set and save to file a modified or new environment
        /// </summary>
        /// <param name="environment"></param>
        public virtual void SetEnvironmentConfig(EnvironmentConfig environment)
        {
            ReloadConfigs();

            // Replace or add environment in cached list
            EnvironmentConfig oldEnv = TryGetEnvironmentConfig(environment.Id);
            if (oldEnv != null)
            {
                int index = _environmentConfigs.IndexOf(oldEnv);
                _environmentConfigs.Remove(oldEnv);
                _environmentConfigs.Insert(index, environment);
            }
            else
            {
                _environmentConfigs.Add(environment);
            }

            WriteEnvironmentConfigsToFile(Path.Join(ProjectSettingsFolder, EditorEnvironmentConfigsFolderPath), EditorEnvironmentConfigsFileName, _environmentConfigs.ToArray());
        }

        /// <summary>
        /// Converts the given environment configs to a JSON string.
        /// </summary>
        public virtual string ConvertToJson(EnvironmentConfig[] environments)
        {
            // Convert environments array to a json string
            // Add a converter to serialize enums as strings
            StringEnumConverter converter = new StringEnumConverter();
            string jsonSerializedObject = JsonConvert.SerializeObject(environments, Formatting.Indented, converter);
            // Use \n on all platforms
            return jsonSerializedObject.Replace("\r\n", "\n");
        }

        /// <summary>
        /// Overwrites the full list of environment configs and writes them to the master file.
        /// </summary>
        public virtual void SetAllEnvironmentConfigs(EnvironmentConfig[] environmentConfigs)
        {
            _environmentConfigs = environmentConfigs.ToList();
            WriteEnvironmentConfigsToFile(Path.Join(ProjectSettingsFolder, EditorEnvironmentConfigsFolderPath), EditorEnvironmentConfigsFileName, _environmentConfigs.ToArray());
        }

        /// <summary>
        /// Serializes the given environments to a file as json.
        /// </summary>
        protected virtual void WriteEnvironmentConfigsToFile(string folderPath, string fileName, EnvironmentConfig[] configs)
        {
            // Convert to a json string
            string jsonSerializedObject = ConvertToJson(configs);

            // Ensure folder exists
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Write to file
#pragma warning disable MP_WGL_00 // Feature is poorly supported in WebGL
            File.WriteAllText(Path.Join(folderPath, fileName) + EnvironmentConfigsFileExt, jsonSerializedObject);
#pragma warning restore MP_WGL_00 // Feature is poorly supported in WebGL

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Writes the given environment Id to the active environment file.
        /// </summary>
        protected virtual void WriteActiveEnvironmentIdToFile(string folderPath, string fileName, string environmentId)
        {
            // Ensure folder exists
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Write active environment Id to file
#pragma warning disable MP_WGL_00 // Feature is poorly supported in WebGL
            File.WriteAllText(Path.Join(folderPath, fileName) + ActiveEnvironmentConfigIdFileExt, environmentId);
#pragma warning restore MP_WGL_00 // Feature is poorly supported in WebGL

            AssetDatabase.Refresh();
        }

        public virtual void SetActiveEnvironmentIdEditorOverride(string editorEnvironmentId)
        {
            _activeEnvironmentIdEditorOverride = editorEnvironmentId;
            EditorPrefs.SetString(ActiveEnvOverrideEditorPrefsKey, editorEnvironmentId);
        }

        public virtual string LoadActiveEnvironmentIdEditorOverride()
        {
            return EditorPrefs.GetString(ActiveEnvOverrideEditorPrefsKey, "");
        }
#endif // if UNITY_EDITOR

        /// <summary>
        /// Gets the active environment ID. If called at runtime, returns the runtime active environment override if it is set.
        /// </summary>
        public virtual string GetActiveEnvironmentId()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying && !string.IsNullOrEmpty(_activeEnvironmentIdRuntimeOverride))
            {
                return _activeEnvironmentIdRuntimeOverride;
            }
#elif UNITY_2017_1_OR_NEWER
            if (!string.IsNullOrEmpty(_activeEnvironmentIdRuntimeOverride))
            {
                return _activeEnvironmentIdRuntimeOverride;
            }
#endif
            if (string.IsNullOrEmpty(_activeEnvironmentId))
            {
                _activeEnvironmentId = LoadActiveEnvironmentId();
            }

            return _activeEnvironmentId;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Gets the active environment ID ignoring the runtime active environment override.
        /// </summary>
        public virtual string GetEditorActiveEnvironmentId()
        {
            if (string.IsNullOrEmpty(_activeEnvironmentId))
            {
                _activeEnvironmentId = LoadActiveEnvironmentId();
            }

            return _activeEnvironmentId;
        }
#endif // if UNITY_EDITOR

        /// <summary>
        /// Loads the stored active environment Id from file at runtime, and loads the editor override if in editor.
        /// </summary>
        protected virtual string LoadActiveEnvironmentId()
        {
#if UNITY_EDITOR
            // Return editor override if it's enabled and set
            if (!string.IsNullOrEmpty(_activeEnvironmentIdEditorOverride))
            {
                return _activeEnvironmentIdEditorOverride;
            }
            else
            {
                return "";
            }
#elif UNITY_2017_1_OR_NEWER
            // Load from file
            return GetActiveEnvironmentIdFromBuiltFile();
#else
            return null;
#endif
        }

        public virtual string[] GetAllEnvironmentIds()
        {
            return _environmentConfigs.Select(x => x.Id).ToArray();
        }

        public virtual string GetActiveEnvironmentIdFromBuiltFile()
        {
            return LoadActiveEnvironmentIdFromFile(Path.Join(BuildEnvironmentConfigsFolderPath, ActiveEnvironmentConfigIdFileName));
        }

        /// <summary>
        /// Sets the active environment ID. If in Editor, sets it as the editor active environment. If called at runtime,
        /// sets the runtime active environment override, which is used overrides the active environment for the current
        /// lifecycle of the process.
        /// </summary>
        public virtual void SetActiveEnvironmentId(string environmentId)
        {
#if UNITY_EDITOR
            // Check that config for given Id exists
            if (!EnvironmentExists(environmentId))
                DebugLog.Debug($"[Metaplay] Setting active environment id to '{environmentId}', but no environment with that id can be found!");

            SetActiveEnvironmentIdEditorOverride(environmentId);
#elif UNITY_2017_1_OR_NEWER
            // Check that config for given Id exists
            if (!EnvironmentExists(environmentId))
            {
                throw new ArgumentException($"[Metaplay] Trying to set active environment id to '{environmentId}', but no environment with that id can be found! Active environment id has not been changed.");
            }

            SetActiveEnvironmentIdRuntimeOverride(environmentId);
#endif
            _activeEnvironmentId = environmentId;
        }

#if UNITY_EDITOR || UNITY_2017_1_OR_NEWER
        /// <summary>
        /// Sets the cached active environment ID, which will apply as an override only for the current lifecycle of the process or until changed.
        /// </summary>
        /// <param name="environmentId">Environment ID that is set as the override.</param>
        public virtual void SetActiveEnvironmentIdRuntimeOverride(string environmentId)
        {
            _activeEnvironmentIdRuntimeOverride = environmentId;
        }
#endif

        /// <summary>
        /// Returns the first environment with a matching id. Throws an ArgumentException if no matching environment is found.
        /// </summary>
        public virtual EnvironmentConfig GetEnvironmentConfig(string environmentId)
        {
            EnvironmentConfig environmentConfig = TryGetEnvironmentConfig(environmentId);
            if (environmentConfig == null)
                throw new ArgumentException($"[Metaplay] Environment Config with Id '{environmentId}' does not exist! " +
                    $"Set the active environment ID in the Environment Configs editor (Metaplay/Environment Configs), " +
                    $"or in another way specified in DefaultEnvironmentConfigBuilder.");

            return environmentConfig;
        }

        /// <summary>
        /// Returns the first environment with a matching id, or null if none are found.
        /// </summary>
        public virtual EnvironmentConfig TryGetEnvironmentConfig(string environmentId)
        {
            EnvironmentConfig environmentConfig = _environmentConfigs.Find(spec => spec.Id == environmentId);

            return environmentConfig;
        }

        public virtual EnvironmentConfig[] GetAllEnvironmentConfigs()
        {
            return _environmentConfigs.ToArray();
        }

        /// <summary>
        /// Loads all environment configs from file.
        /// </summary>
        public virtual List<EnvironmentConfig> LoadConfigs(string folderPath, string fileName)
        {
#if NETCOREAPP
            return null;
#endif
#if UNITY_EDITOR
            string environmentsJson;

            // We're migrating away from the resources folder here
            // Resources are always included in the build even if the files in an Editor folder
            string oldFilePath = Path.Join(ResourcesFolder, "Metaplay/Editor", fileName + EnvironmentConfigsFileExt);
            string filePath    = Path.Join(ProjectSettingsFolder, folderPath, fileName + EnvironmentConfigsFileExt);

#pragma warning disable MP_WGL_00 // Only ran in editor
            if (File.Exists(oldFilePath))
            {
                DebugLog.Warning($"[Metaplay] Environment Configs file found at {oldFilePath}, migrating to {filePath}.");
                // If the target file doesn't exist, just move it over and delete the old files
                if (!File.Exists(filePath))
                {
                    Directory.CreateDirectory(new FileInfo(filePath).Directory.FullName);
                    File.Move(oldFilePath, filePath);
                }
                else
                {
                    // If it does exist, check if the content is the same, if not; throw.
                    // This can happen if switching from an old branch to a branch that already has the migration applied.
                    // However, we don't want to override the target file if they're not the same, as that might indicate
                    // environments being added or changed.

                    // ReadAllLines automatically handles line endings and prevents them from showing up in each individual line.
                    string[] oldContent = File.ReadAllLines(oldFilePath);
                    string[] newContent = File.ReadAllLines(filePath);

                    if (!oldContent.SequenceEqual(newContent))
                        throw new InvalidOperationException($"[Metaplay] Could not automatically migrate Environment configs file, files exist at both paths with different content. Please manually check if '{filePath}' needs to be updated and remove the file at '{oldFilePath}'.");
                }

                File.Delete(oldFilePath);
                File.Delete(oldFilePath + ".meta");

                // Cleanup old folders if they're empty
                string metaplayEditorFolder = Path.Join(ResourcesFolder, "Metaplay/Editor");
                if (Directory.GetFileSystemEntries(metaplayEditorFolder).Length == 0)
                {
                    Directory.Delete(metaplayEditorFolder);
                    File.Delete(metaplayEditorFolder + ".meta");

                    string metaplayFolder = Path.Join(ResourcesFolder, "Metaplay");
                    if (Directory.GetFileSystemEntries(metaplayFolder).Length == 0)
                    {
                        Directory.Delete(metaplayFolder);
                        File.Delete(metaplayFolder + ".meta");
                    }
                }
            }
            // Create default config file if file not found
            if (!File.Exists(filePath))
            {
#pragma warning restore MP_WGL_00 // Feature is poorly supported in WebGL
                DebugLog.Warning($"[Metaplay] Environment Configs file not found, creating new one at: {filePath}. This is expected during first time Metaplay initialization.");
                EnvironmentConfig[] defaultEnvironments = CreateDefaultEnvironments();
                WriteEnvironmentConfigsToFile(Path.Join(ProjectSettingsFolder, folderPath), fileName, defaultEnvironments);
                return defaultEnvironments.ToList();
            }
            else
            {
                // Read from file
                Task<string> readTask = MetaTask.Run(async () => await FileUtil.ReadAllTextAsync(filePath));
                readTask.Wait();
                environmentsJson = readTask.Result;
            }
#elif UNITY_2017_1_OR_NEWER
            // Load environment configs file from Resources folder
            string environmentsJson = Resources.Load<TextAsset>(Path.Join(folderPath, fileName))?.text;
            if (environmentsJson == null)
            {
                throw new FileNotFoundException("[Metaplay] Build Environment Config file not found!");
            }
#endif
#if UNITY_EDITOR || UNITY_2017_1_OR_NEWER
            // Convert json to config objects
            Type                configType      = IntegrationRegistry.GetSingleIntegrationType<EnvironmentConfig>();
            Type                configArrayType = Array.CreateInstance(configType, 0).GetType();
            EnvironmentConfig[] environments    = (EnvironmentConfig[])JsonConvert.DeserializeObject(environmentsJson, configArrayType);
            if (environments == null)
                throw new SerializationException("[Metaplay] Environment Config file is invalid!");

            return environments.ToList();
#endif
        }

        /// <summary>
        /// Loads the active environment's id from file.
        /// </summary>
        public virtual string LoadActiveEnvironmentIdFromFile(string path)
        {
#if UNITY_EDITOR
            // Editor shouldn't need to load the file at any point as the file is created at build-time
            return null;
#elif UNITY_2017_1_OR_NEWER
            string activeEnvId = Resources.Load<TextAsset>(path)?.text;
            if (activeEnvId == null)
            {
                // File should exist, so something has deleted the file? Or Unity has not included it in build?
                throw new FileNotFoundException("[Metaplay] Active Environment Config file not found!");
            }

            // Check that a config for the Id is found
            if (!EnvironmentExists(activeEnvId))
                throw new ArgumentException($"[Metaplay] Active Environment with Id: '{activeEnvId}' does not exist!");

            return activeEnvId;
#else
            // TODO: server should get its own environment info from somewhere
            return null;
#endif
        }

        /// <summary>
        /// Checks whether an environment with the given Id can be found.
        /// </summary>
        public bool EnvironmentExists(string environmentId)
        {
            return TryGetEnvironmentConfig(environmentId) != null;
        }

        /// <summary>
        /// Creates default environments for new projects.
        /// </summary>
        protected virtual EnvironmentConfig[] CreateDefaultEnvironments()
        {
            return new EnvironmentConfig[]
            {
                new EnvironmentConfig
                {
                    Id = "offline",
                    Description = "",
                    DoNotBuildForDevices = false,
                    ConnectionEndpointConfig = new ConnectionEndpointConfig() {
                        ServerHost             = null, // offline (using mock server)
                        ServerPort             = 0,
                        ServerPortForWebSocket = 0,
                        EnableTls              = false,
                        CdnBaseUrl             = null,
                        CommitIdCheckRule      = ClientServerCommitIdCheckRule.Disabled,
                        BackupGateways         = new List<ServerGatewaySpec>(),
                    },
                    ClientLoggingConfig = new ClientLoggingConfig()
                    {
                        LogLevel = LogLevel.Verbose,
                    },
                    ClientGameConfigBuildApiConfig = new ClientGameConfigBuildApiConfig()
                    {
                        // ...
                    },
                },
                new EnvironmentConfig
                {
                    Id = "localhost",
                    Description = "",
                    DoNotBuildForDevices = true,
                    ConnectionEndpointConfig = new ConnectionEndpointConfig() {
                        ServerHost             = "localhost",
                        ServerPort             = 9339,
                        ServerPortForWebSocket = 9380,
                        EnableTls              = false,
                        CdnBaseUrl             = "http://localhost:5552/",
                        CommitIdCheckRule      = ClientServerCommitIdCheckRule.Disabled,
                        BackupGateways         = new List<ServerGatewaySpec>(),
                    },
                    ClientLoggingConfig = new ClientLoggingConfig()
                    {
                        LogLevel = LogLevel.Verbose,
                    },
                    ClientGameConfigBuildApiConfig = new ClientGameConfigBuildApiConfig()
                    {
#if UNITY_EDITOR
                        AdminApiBaseUrl         = "http://localhost:5550/api/",
                        GameConfigPublishPolicy = GameConfigPublishPolicy.Allowed,
#endif
                    },
                },
                // ...
            };
        }
    }
}
