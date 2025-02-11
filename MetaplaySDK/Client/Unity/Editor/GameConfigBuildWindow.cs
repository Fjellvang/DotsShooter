using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Config;
using Metaplay.Core.Serialization;
using Metaplay.Unity.DefaultIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Metaplay.Unity
{
    /// <summary>
    /// A unity editor window to configure <see cref="GameConfigBuildParameters"/> and trigger game config builds.
    /// </summary>
    [Serializable]
    public class GameConfigBuildWindow : EditorWindow
    {
        public const string SharedGameConfigPath = "Assets/StreamingAssets/SharedGameConfig.mpa";
        public const string StaticGameConfigPath = "Backend/Server/GameConfig/StaticGameConfig.mpa";

        static Action<bool> _taskInProgressChanged;

        GameConfigBuildParameters _currentBuildParameters;

        GameConfigBuildIntegration _gameConfigBuildIntegration;

        SerializedObject _self;

        VisualElement _sdkInitializationHelpBox;
        VisualElement _noGameConfigIntegrationHelpBox;
        VisualElement _incrementalSettings;

        [SerializeField] string targetEnvironment;
        [SerializeField] byte[] configBytes;

        [SerializeField]
        string targetParentGameConfig;

        List<GameConfigBuildUtil.GameConfigInfo> _currentEnvironmentGameConfigs = new List<GameConfigBuildUtil.GameConfigInfo>();

        DropdownField _parentGameConfigSelect;
        Button        _buildButton;

        [MenuItem("Metaplay/Game Config Build Window", false, 3)]
        static void CreateWindow()
        {
            Type                  inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.dll");
            GameConfigBuildWindow window        = GetWindow<GameConfigBuildWindow>("GameConfig Build", desiredDockNextTo: new Type[] {inspectorType});
            window.Show();
        }

        void OnEnable()
        {
            if (!MetaplayCore.IsInitialized)
                return;

            _gameConfigBuildIntegration = IntegrationRegistry.Get<GameConfigBuildIntegration>();
            Type defaultGameConfigBuildParametersType = _gameConfigBuildIntegration.GetDefaultGameConfigBuildParametersType();

            if (configBytes != null && configBytes.Length > 0)
            {
                try
                {
                    _currentBuildParameters = (GameConfigBuildParameters)MetaSerialization.DeserializeTagged(
                        configBytes,
                        typeof(GameConfigBuildParameters),
                        MetaSerializationFlags.Persisted,
                        null,
                        0);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    _currentBuildParameters = null;
                }
            }

            if (_currentBuildParameters == null || _currentBuildParameters.GetType() != defaultGameConfigBuildParametersType)
            {
                _currentBuildParameters = (GameConfigBuildParameters)MetaSerializationUtil.CreateAndPopulate(
                    MetaSerializerTypeRegistry.GetTypeSpec(defaultGameConfigBuildParametersType),
                    new List<KeyValuePair<MetaSerializableMember, object>>());
                configBytes             = MetaSerialization.SerializeTagged(_currentBuildParameters, MetaSerializationFlags.Persisted, 0);
            }

            // For custom environment providers there is no name for the current environment.
            IEnvironmentConfigProvider provider = IntegrationRegistry.Get<IEnvironmentConfigProvider>();
            if (provider is not DefaultEnvironmentConfigProvider defaultProvider)
            {
                return;
            }

            _self = new SerializedObject(this);

            SerializedProperty environmentId = _self.FindProperty("targetEnvironment");
            if (string.IsNullOrEmpty(environmentId.stringValue))
            {
                environmentId.stringValue = defaultProvider.GetEditorActiveEnvironmentId();
                _self.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.undoRedoPerformed += () =>
            {
                _currentBuildParameters = (GameConfigBuildParameters)MetaSerialization.DeserializeTagged(
                    configBytes,
                    typeof(GameConfigBuildParameters),
                    MetaSerializationFlags.Persisted,
                    null,
                    0);
                for (int i = rootVisualElement.childCount - 1; i >= 0; i--)
                    rootVisualElement.RemoveAt(i);

                CreateGUI();
            };
        }

        void CreateGUI()
        {
            _sdkInitializationHelpBox = new HelpBox("MetaplayCore is not initialized! Check the logs for errors.", HelpBoxMessageType.Error);
            rootVisualElement.Add(_sdkInitializationHelpBox);
            _sdkInitializationHelpBox.style.display = MetaplayCore.IsInitialized && _self != null ? DisplayStyle.None : DisplayStyle.Flex;

            if (!MetaplayCore.IsInitialized || _self == null)
                return;

            IEnvironmentConfigProvider provider = IntegrationRegistry.Get<IEnvironmentConfigProvider>();

            if (provider is not DefaultEnvironmentConfigProvider)
            {
                rootVisualElement.Add(new HelpBox($"The Game Config Build Window is currently only supported when using the {nameof(DefaultEnvironmentConfigProvider)}.", HelpBoxMessageType.Error));
                return;
            }

            _buildButton = new Button(Build);
            _buildButton.text = "Build";

            TextField nameField = new TextField("Name");
            nameField.tooltip = "Shown only in Dashboard";
            TextField descriptionField = new TextField("Description");
            descriptionField.tooltip = "Shown only in Dashboard";

            Button publishButton = new Button(() => Publish(nameField.text, descriptionField.text));
            publishButton.text = "Publish";

            SerializedProperty environmentId     = _self.FindProperty("targetEnvironment");
            DropdownField      environmentSelect = new DropdownField();
            environmentSelect.BindProperty(environmentId);
            environmentSelect.choices = DefaultEnvironmentConfigProvider.Instance.GetAllEnvironmentIds().ToList();
            environmentSelect.label   = ObjectNames.NicifyVariableName(environmentId.displayName);

            environmentSelect.RegisterValueChangedCallback(
                x =>
                {
                    EnvironmentConfig environmentConfig = DefaultEnvironmentConfigProvider.Instance.TryGetEnvironmentConfig(x.newValue);

                    bool doPublish = environmentConfig?.ClientGameConfigBuildApiConfig?.GameConfigPublishPolicy != GameConfigPublishPolicy.UploadOnly;

                    if (environmentConfig != null
                        && !environmentConfig.ConnectionEndpointConfig.IsOfflineMode
                        && environmentConfig.ClientGameConfigBuildApiConfig?.GameConfigPublishPolicy != GameConfigPublishPolicy.NotAllowed)
                    {
                        publishButton.tooltip = "";
                        publishButton.SetEnabled(true);
                    }
                    else
                    {
                        publishButton.tooltip = "The offline server uses the latest built game config, publishing is not necessary.";
                        publishButton.SetEnabled(false);
                    }

                    publishButton.text = $"{(doPublish ? "Publish" : "Upload")} to {x.newValue}";
                });

            Type defaultGameConfigBuildParametersType = _gameConfigBuildIntegration.GetDefaultGameConfigBuildParametersType();
            VisualElement buildParameters = IntegrationRegistry.Get<MetaGeneratedEditorUI>().CreateGUIForType(
                defaultGameConfigBuildParametersType,
                _currentBuildParameters,
                x =>
                {
                    _currentBuildParameters = (GameConfigBuildParameters)x;
                    Undo.RegisterCompleteObjectUndo(this, "");
                    configBytes    = MetaSerialization.SerializeTagged(_currentBuildParameters, MetaSerializationFlags.Persisted, 0);
                });

            buildParameters.style.paddingLeft = 16;
            buildParameters.AddToClassList("unity-inspector-element");

            VisualElement buttons = new VisualElement();

            Box publishingBox = new Box();

            publishingBox.style.marginTop = 8;
            publishingBox.Add(
                new Label("Publishing")
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold }
                });

            SerializedProperty gameConfig = _self.FindProperty(nameof(targetParentGameConfig));
            _parentGameConfigSelect = new DropdownField( _currentEnvironmentGameConfigs.Select(x => x.Id).ToList(), 0, FormatGameConfigInfo, FormatGameConfigInfo);

            _parentGameConfigSelect.BindProperty(gameConfig);
            _parentGameConfigSelect.label = ObjectNames.NicifyVariableName("Parent Game Config");
            UpdateGameConfigOptions();

            Box buildBox = new Box();

            buildBox.Add(new Label("Build Parameters")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold }
            });

            buildBox.Add(buildParameters);

            // buildBox.Add(incrementalBox);

            HelpBox termsOfUseHelpBox = new HelpBox("Game config building's use and transfer of information received from Google APIs to any other app adhere to Google API Services User Data Policy, including the Limited Use requirements. Click here for more info.", HelpBoxMessageType.Info);
            termsOfUseHelpBox.RegisterCallback<ClickEvent>(evt => Application.OpenURL("https://developers.google.com/terms/api-services-user-data-policy#additional_requirements_for_specific_api_scopes"));

            if (IntegrationRegistry.Get<IUnityGameConfigBuildIntegration>().CreateFetcherConfig() is GameConfigSourceFetcherConfigCore fetcherConfig
                && fetcherConfig.GoogleCredentialsClientSecret != null)
                buildBox.Add(termsOfUseHelpBox);

            buildBox.Add(_buildButton);

            publishingBox.Add(environmentSelect);
            publishingBox.Add(nameField);
            publishingBox.Add(descriptionField);
            publishingBox.Add(publishButton);

            buttons.Add(buildBox);
            buttons.Add(publishingBox);

            _taskInProgressChanged += b => { buttons.SetEnabled(!b); };

            VisualElement root = new VisualElement();
            root.AddToClassList("unity-inspector-main-container");

            _noGameConfigIntegrationHelpBox = new HelpBox($"No implementation for '{nameof(GameConfigBuildIntegration)}.{nameof(GameConfigBuildIntegration.GetAvailableGameConfigBuildSources)}' found, we recommend providing an implementation to easily share configured sources in the project. Click here for more information!", HelpBoxMessageType.Warning);
            _noGameConfigIntegrationHelpBox.RegisterCallback<ClickEvent>(evt => Application.OpenURL("https://docs.metaplay.io/feature-cookbooks/game-configs/implementing-google-sheets-integration.html#configure-fetching-sheets"));

            _noGameConfigIntegrationHelpBox.style.display = DisplayStyle.None;

            rootVisualElement.Add(_noGameConfigIntegrationHelpBox);

            root.Add(buttons);

            rootVisualElement.Add(root);
        }

        string FormatGameConfigInfo(string s)
        {
            GameConfigBuildUtil.GameConfigInfo gameConfigInfo = _currentEnvironmentGameConfigs.FirstOrDefault(y => y.Id.ToString() == s);

            if (gameConfigInfo == null)
                return string.Empty;

            return (string.IsNullOrWhiteSpace(gameConfigInfo.Name) ? "No name" : gameConfigInfo.Name) + $" ({gameConfigInfo.Id.Substring(gameConfigInfo.Id.Length - 6)})" + (gameConfigInfo.IsActive ? " (Active)" : string.Empty);
        }

        void FetchGameConfigs()
        {
            EnvironmentConfig environmentConfig = DefaultEnvironmentConfigProvider.Instance.TryGetEnvironmentConfig(targetEnvironment);
            _taskInProgressChanged(true);

            EditorTask.Run(
                "Fetch GameConfigs",
                async () =>
                {
                    string authorizationToken = await environmentConfig.FetchAuthorizationTokenAsync();
                    _currentEnvironmentGameConfigs = await GameConfigBuildUtil.FetchGameConfigsFromServerAsync(environmentConfig.ClientGameConfigBuildApiConfig.AdminApiBaseUrl, authorizationToken);
                    UpdateGameConfigOptions();
                },
                finalizer: ClearBuildTask);
        }

        void UpdateGameConfigOptions()
        {
            _parentGameConfigSelect.choices = _currentEnvironmentGameConfigs.OrderByDescending(x=>x.PublishedAtTime).ThenByDescending(x=>x.BuildStartedAtTime).Select(x => x.Id).ToList();
            _parentGameConfigSelect.index   = 0;
            _parentGameConfigSelect.SetEnabled(_parentGameConfigSelect.choices.Count > 0);

            if (_parentGameConfigSelect.choices.Count == 0)
                _parentGameConfigSelect.tooltip = "To select a parent game config, please fetch them from the server first. By default the active game config is used.";
            else
                _parentGameConfigSelect.tooltip = "";
        }

        void Update()
        {
            if (_sdkInitializationHelpBox != null)
                _sdkInitializationHelpBox.style.display = MetaplayCore.IsInitialized ? DisplayStyle.None : DisplayStyle.Flex;
            if (_noGameConfigIntegrationHelpBox != null)
                _noGameConfigIntegrationHelpBox.style.display = _gameConfigBuildIntegration?.GetType() != typeof(GameConfigBuildIntegration) ? DisplayStyle.None : DisplayStyle.Flex;

            if (_currentBuildParameters != null)
            {
                if (_incrementalSettings != null)
                {
                    EnvironmentConfig environmentConfig = DefaultEnvironmentConfigProvider.Instance.TryGetEnvironmentConfig(targetEnvironment);
                    _incrementalSettings.style.display = (_currentBuildParameters.IsIncremental && !environmentConfig.ConnectionEndpointConfig.IsOfflineMode) ? DisplayStyle.Flex : DisplayStyle.None;
                }

                if (_buildButton != null && _currentBuildParameters.IsIncremental && string.IsNullOrEmpty(targetParentGameConfig))
                {
                    _buildButton.tooltip = "Incremental builds will use the current active game config as parent if none is set.";
                }
            }
        }

        void Publish(string name, string description)
        {
            EnvironmentConfig environmentConfig = DefaultEnvironmentConfigProvider.Instance.TryGetEnvironmentConfig(targetEnvironment);

            bool doPublish = environmentConfig.ClientGameConfigBuildApiConfig.GameConfigPublishPolicy != GameConfigPublishPolicy.UploadOnly;
            _taskInProgressChanged(true);

            EditorTask.Run(
                "GameConfig upload",
                async () =>
                {
                    ConfigArchive configArchive      = await ConfigArchive.FromFileAsync(StaticGameConfigPath);
                    string        authorizationToken = await environmentConfig.FetchAuthorizationTokenAsync();
                    await GameConfigBuildUtil.PublishGameConfigArchiveToServerAsync(
                        environmentConfig.ClientGameConfigBuildApiConfig.AdminApiBaseUrl,
                        configArchive,
                        authorizationToken,
                        confirmDialog: environmentConfig.ClientGameConfigBuildApiConfig.GameConfigPublishPolicy == GameConfigPublishPolicy.AskForConfirmation,
                        setAsActive: doPublish,
                        name: name,
                        description: description);
                },
                finalizer: ClearBuildTask);
        }

        void Build()
        {
            _taskInProgressChanged(true);
            EditorTask.Run(
                "GameConfig build",
                async () =>
                {
                    ConfigArchive parent = null;

                    string parentConfigId = null;

                    if (_currentBuildParameters.IsIncremental)
                    {
                        EnvironmentConfig environmentConfig = DefaultEnvironmentConfigProvider.Instance.TryGetEnvironmentConfig(targetEnvironment);
                        if (environmentConfig.ConnectionEndpointConfig.IsOfflineMode)
                            parent = await ConfigArchive.FromFileAsync(StaticGameConfigPath);
                        else
                        {
                            string authorizationToken                            = await environmentConfig.FetchAuthorizationTokenAsync();
                            List<GameConfigBuildUtil.GameConfigInfo> gameConfigs = await GameConfigBuildUtil.FetchGameConfigsFromServerAsync(environmentConfig.ClientGameConfigBuildApiConfig.AdminApiBaseUrl, authorizationToken);

                            GameConfigBuildUtil.GameConfigInfo targetGameConfig = gameConfigs.First(x => x.IsActive);

                            (ConfigArchive configArchive, MetaGuid _) = await GameConfigBuildUtil.DownloadGameConfigAsync(targetGameConfig.Id, environmentConfig.ClientGameConfigBuildApiConfig.AdminApiBaseUrl, authorizationToken);

                            parentConfigId = targetGameConfig.Id;
                            parent         = configArchive;
                        }
                    }

                    await BuildFullGameConfigAsync(_currentBuildParameters, parent, parentConfigId);
                    Debug.Log("StaticGameConfig built successfully!");
                },
                finalizer: ClearBuildTask);
        }

        void OnDestroy()
        {
            _self?.Dispose();
        }

        void ClearBuildTask()
        {
            _taskInProgressChanged(false);
            Repaint();
        }

        public static async Task BuildFullGameConfigAsync(GameConfigBuildParameters buildParameters, ConfigArchive parentArchive = null, string parentConfigId = null)
        {
            IGameConfigSourceFetcherConfig fetcherConfig = IntegrationRegistry.Get<IUnityGameConfigBuildIntegration>().CreateFetcherConfig();

            // Build full config (Shared + Server) with minimal params
            ConfigArchive fullConfigArchive = await StaticFullGameConfigBuilder.BuildArchiveAsync(MetaTime.Now, parentId: parentConfigId != null ? MetaGuid.Parse(parentConfigId) : MetaGuid.None, parent: parentArchive, buildParams: buildParameters, fetcherConfig: fetcherConfig);

            // Extract SharedGameConfig from full config & write to disk
            ConfigArchive sharedConfigArchive = ConfigArchive.FromBytes(fullConfigArchive.GetEntryByName("Shared.mpa").Bytes);
            Debug.Log($"Writing {SharedGameConfigPath} with {sharedConfigArchive.Entries.Count} entries:\n{string.Join("\n", sharedConfigArchive.Entries.Select(entry => $"  {entry.Name} ({entry.Bytes.Length} bytes): {entry.Hash}"))}");
            await ConfigArchiveBuildUtility.WriteToFileAsync(SharedGameConfigPath, sharedConfigArchive);

            // Write full StaticGameConfig to disk
            Debug.Log($"Writing {StaticGameConfigPath} with {fullConfigArchive.Entries.Count} entries:\n{string.Join("\n", fullConfigArchive.Entries.Select(entry => $"  {entry.Name} ({entry.Bytes.Length} bytes): {entry.Hash}"))}");
            await ConfigArchiveBuildUtility.WriteToFileAsync(StaticGameConfigPath, fullConfigArchive);

            // If in editor, refresh AssetDatabase to make sure Unity sees changed files
            AssetDatabase.Refresh();

            // Inform Metaplay that GameConfigs have been built, so it can hot-load the GameConfigs in offline mode
            Debug.Log("invoke MetaplayClient.OnSharedGameConfigUpdated()");
            (MetaplaySDK.SessionContext as MetaplayClientState)?.OnSharedGameConfigUpdated(sharedConfigArchive);
        }
    }

    public static class EnvironmentConfigExtensions
    {
        public class OAuth2AuthorizationServerSpecWrapper : MetaplayOAuth2Client.IAuthorizationServerConfigOverride
        {
            public OAuth2AuthorizationServerSpecWrapper(OAuth2AuthorizationServerSpec authSpec)
            {
                AuthSpec = authSpec;
            }

            public OAuth2AuthorizationServerSpec AuthSpec;

            string MetaplayOAuth2Client.IAuthorizationServerConfigOverride.      ClientId              => AuthSpec.ClientId;
            string MetaplayOAuth2Client.IAuthorizationServerConfigOverride.      ClientSecret          => AuthSpec.ClientSecret;
            string MetaplayOAuth2Client.IAuthorizationServerConfigOverride.      AuthorizationEndpoint => AuthSpec.AuthorizationEndpoint;
            string MetaplayOAuth2Client.IAuthorizationServerConfigOverride.      TokenEndpoint         => AuthSpec.TokenEndpoint;
            string MetaplayOAuth2Client.IAuthorizationServerConfigOverride.      Audience              => AuthSpec.Audience;
            List<string> MetaplayOAuth2Client.IAuthorizationServerConfigOverride.LocalCallbackUrls     => AuthSpec.LocalCallbackUrls;
            bool MetaplayOAuth2Client.IAuthorizationServerConfigOverride.        UseStateParameter     => AuthSpec.UseStateParameter;
        }

        public static bool IsLoggedIn(this EnvironmentConfig environment)
        {
            switch (environment.ClientGameConfigBuildApiConfig.AdminApiAuthenticationTokenProvider)
            {
                case AuthenticationTokenProvider.None:
                    return false;

                case AuthenticationTokenProvider.Google:
                {
                    return true;
                }

                case AuthenticationTokenProvider.OAuth2:
                {
                    OAuth2AuthorizationServerSpecWrapper           authSpecWrapper = new OAuth2AuthorizationServerSpecWrapper(environment.ClientGameConfigBuildApiConfig.AdminApiOAuth2Settings);
                    MetaplayOAuth2Client.AuthorizationServerConfig config          = MetaplayOAuth2Client.AuthorizationServerConfig.CreateForMetaplayManagedGameServers();
                    config.ApplyOverrides(authSpecWrapper);
                    return MetaplayOAuth2Client.HasCachedLogin(config);
                }

                default:
                    throw new InvalidOperationException($"Unknown authorization provider {environment.ClientGameConfigBuildApiConfig.AdminApiAuthenticationTokenProvider}");
            }
        }

        public static async Task<string> FetchAuthorizationTokenAsync(this EnvironmentConfig environment)
        {
            switch (environment.ClientGameConfigBuildApiConfig.AdminApiAuthenticationTokenProvider)
            {
                case AuthenticationTokenProvider.None:
                    return null;

                case AuthenticationTokenProvider.Google:
                {
                    return await AdminApiAuthorizationUtil.FetchGoogleIdTokenAsync(environment.ClientGameConfigBuildApiConfig.AdminApiCredentialsPath);
                }

                case AuthenticationTokenProvider.OAuth2:
                {
                    OAuth2AuthorizationServerSpecWrapper           authSpecWrapper = new OAuth2AuthorizationServerSpecWrapper(environment.ClientGameConfigBuildApiConfig.AdminApiOAuth2Settings);
                    MetaplayOAuth2Client.AuthorizationServerConfig config          = MetaplayOAuth2Client.AuthorizationServerConfig.CreateForMetaplayManagedGameServers();
                    config.ApplyOverrides(authSpecWrapper);
                    MetaplayOAuth2Client.LoginResult loginResult = await MetaplayOAuth2Client.LoginAsync(config);
                    if (environment.ClientGameConfigBuildApiConfig.AdminApiUseOpenIdConnectIdToken)
                        return loginResult.IdToken;
                    else
                        return loginResult.AccessToken;
                }

                default:
                    throw new InvalidOperationException($"Unknown authorization provider {environment.ClientGameConfigBuildApiConfig.AdminApiAuthenticationTokenProvider}");
            }
        }
    }
}
