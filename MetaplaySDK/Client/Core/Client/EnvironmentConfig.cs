// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Message;
using Metaplay.Core.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
#endif

using static System.FormattableString;

namespace Metaplay.Core.Client
{
#if !UNITY_EDITOR // Dummy tooltip Attribute when outside Unity Editor
    internal class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }
#endif

    /// <summary>
    /// A utility class to parse JSON that may contain either a single MetaplayEnvironmentConfigInfo object,
    /// or an array thereof.
    /// </summary>
    internal sealed class MetaplayEnvironmentConfigInfoConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(MetaplayEnvironmentConfigInfo[]);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<MetaplayEnvironmentConfigInfo[]>();
            }
            if (token.Type == JTokenType.Null)
            {
                return null;
            }
            return new MetaplayEnvironmentConfigInfo[] { token.ToObject<MetaplayEnvironmentConfigInfo>() };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    /// <summary>
    /// Used to relay environment config information when copy-pasting from the dev portal to the environment config editor
    /// </summary>
    public class MetaplayEnvironmentConfigInfo
    {
        // Maps to Metaplay.Cloud.RuntimeOptions.EnvironmentFamily: Local, Development, Staging, Production
        public EnvironmentFamily EnvironmentFamily;

        // Unique identifier for this environment, such as 'development' or 'feature-foo'
        public string EnvironmentId;

        // Hostname of the server, eg, 'localhost' or 'mygame-develop.p1.metaplay.io' (empty for offline mode)
        public string ServerHost;

        // Port numbers to use for connections (when using normal sockets, i.e. not WebGL with WebSockets)
        public int[] ServerPorts;

        // Port numbers to use for connections when using WebGL with WebSockets (only first one is used)
        public int[] ServerPortsForWebSocket;

        // Enable TLS crypto for connection (should be enabled for all cloud deployments, disabled for localhost)
        public bool EnableTls;

        // Base URL to asset CDN (eg, 'http://localhost:5552/' or 'https://mygame-develop-assets.p1.metaplay.io/')
        public string CdnBaseUrl;

        // (Editor only) Base URL to Admin API (eg, 'http://localhost:5550/api/' or 'https://mygame-develop-api.p1.metaplay.io/api/')
        public string AdminApiBaseUrl;

        public bool AdminApiUseOpenIdConnectIdToken;

        public string OAuth2ClientID;

        public string OAuth2ClientSecret;

        public string OAuth2Audience;

        public string OAuth2AuthorizationEndpoint;

        public string OAuth2TokenEndpoint;

        public string OAuth2LocalCallback;

        public bool OAuth2UseStateParameter;

        /// <summary>
        /// Tries to deserialize the JSON data into an array of <see cref="MetaplayEnvironmentConfigInfo"/>.
        /// Throws if unsuccessful or if a necessary value is missing.
        /// </summary>
        /// <param name="json">The JSON data, copied from the developer portal</param>
        /// <returns>Array with one or more MetaplayEnvironmentConfigInfo objects</returns>
        /// <exception cref="ArgumentException"></exception>
        public static MetaplayEnvironmentConfigInfo[] ParseFromJson(string json)
        {
            MetaplayEnvironmentConfigInfo[] configs = null;
            try
            {
                configs = JsonConvert.DeserializeObject<MetaplayEnvironmentConfigInfo[]>(json, new MetaplayEnvironmentConfigInfoConverter());
            }
            catch (JsonSerializationException ex)
            {
                throw new ArgumentException("Invalid JSON data", ex);
            }
            catch (JsonReaderException ex)
            {
                throw new ArgumentException("Invalid JSON data", ex);
            }

            if (configs == null)
                throw new ArgumentException("Empty JSON payload");

            for (int i = 0; i < configs.Length; ++i)
            {
                MetaplayEnvironmentConfigInfo config = configs[i];
                // If the user imports a single environment config, let's not refer to it with "at index 0"
                string indexExpression = configs.Length > 1 ? Invariant($" at index {i}") : "";

                // If we have an ill-formed id and there are multiple configs being imported at once,
                // at least provide the index of the offending config so the user can search for it in the JSON
                if (string.IsNullOrWhiteSpace(config.EnvironmentId))
                    throw new ArgumentException($"Environment config data{indexExpression} does not contain a value for 'EnvironmentId'");

                // Having validated environmentId, from now on let's provide the id in case of errors, instead of index
                string envId = config.EnvironmentId;

                if ((int)config.EnvironmentFamily < 0 || (int)config.EnvironmentFamily > Enum.GetValues(typeof(EnvironmentFamily)).Length)
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a valid value for 'EnvironmentFamily', was {config.EnvironmentFamily}");
                if (string.IsNullOrWhiteSpace(config.ServerHost))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'ServerHost'");
                if (config.ServerPorts == null || config.ServerPorts.Length == 0)
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'ServerPorts'");

                const int serverPortMin = 1;
                const int serverPortMax = 65535;

                foreach (int serverPort in config.ServerPorts)
                {
                    if (serverPort < serverPortMin || serverPort > serverPortMax)
                        throw new ArgumentException($"Environment config data for '{envId}' does not contain a valid value for 'ServerPorts', was: '{serverPort}', should be {serverPortMin} <= value <= {serverPortMax}");
                }

                if (string.IsNullOrWhiteSpace(config.CdnBaseUrl))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'CdnBaseUrl'");
                if (string.IsNullOrWhiteSpace(config.AdminApiBaseUrl))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'AdminApiBaseUrl'");
                if (string.IsNullOrWhiteSpace(config.OAuth2ClientID))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'OAuth2ClientID'");
                if (string.IsNullOrWhiteSpace(config.OAuth2ClientSecret))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'OAuth2ClientSecret'");
                if (string.IsNullOrWhiteSpace(config.OAuth2Audience))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'OAuth2Audience'");
                if (string.IsNullOrWhiteSpace(config.OAuth2AuthorizationEndpoint))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'OAuth2AuthorizationEndpoint'");
                if (string.IsNullOrWhiteSpace(config.OAuth2TokenEndpoint))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'OAuth2TokenEndpoint'");
                if (string.IsNullOrWhiteSpace(config.OAuth2LocalCallback))
                    throw new ArgumentException($"Environment config data for '{envId}' does not contain a value for 'OAuth2LocalCallback'");
            }
            return configs;
        }
    }

    [Serializable]
    public class ServerGatewaySpec
    {
        public string Id;

        [Tooltip("Empty and null default to the top level Server Host.")]
        public string ServerHost;
        public int ServerPort;

        public ServerGatewaySpec() { }

        public ServerGatewaySpec(string id, string serverHost, int serverPort)
        {
            Id = id;
            ServerHost = serverHost;
            ServerPort = serverPort;
        }

        public ServerGateway ToGateway(bool enableTls) => new ServerGateway(ServerHost, ServerPort, enableTls);
    }

    /// <summary>
    /// Configuration for a client-side deployment/environment.
    /// </summary>
    [Serializable]
    public class ConnectionEndpointConfig
    {
        [Tooltip("Hostname of the server, eg, 'localhost' or 'mygame-develop.p1.metaplay.io' (empty for offline mode)")]
        public string ServerHost;

        [Tooltip("Port number to use for connections (when using normal sockets, i.e. not WebGL with WebSockets)")]
        public int ServerPort;

        [Tooltip("Port number to use for connections when using WebGL with WebSockets")]
        public int ServerPortForWebSocket;

        [Tooltip("Enable TLS crypto for connection (should be enabled for all cloud deployments, disabled for localhost)")]
        public bool EnableTls;

        [Tooltip("Base URL to asset CDN (eg, 'http://localhost:5552/' or 'https://mygame-develop-assets.p1.metaplay.io/')")]
        public string CdnBaseUrl;

        [Tooltip(
            "Configure whether the server and client must be built from the same commit id for the connection to work.\n"
            + "For development environments, 'Only If Defined' is recommended to allow safe iteration of client and server without needing "
            + "to bump version numbers for each build (the commit id check prevents accidentally using mismatched client and server versions).\n"
            + "For locally running servers, disabling the checks is recommended.\n"
            + "For production and staging environments, the checks should also be disabled, and instead manual versioning be used.")]
        public ClientServerCommitIdCheckRule CommitIdCheckRule;

        [Tooltip("Alternative gateways to the server. These will be used if above server host cannot be connected to.")]
        public List<ServerGatewaySpec> BackupGateways;

        public bool IsOfflineMode => string.IsNullOrEmpty(ServerHost);

        public ServerEndpoint GetServerEndpoint()
        {
            int serverPort;
#if UNITY_WEBGL && !UNITY_EDITOR
            serverPort = ServerPortForWebSocket;
#else
            serverPort = ServerPort;
#endif

            // \note: EnableTLS value for backup gateways is not used, but for clarity the value from primary config is copied over anyway.
            return new ServerEndpoint(ServerHost, serverPort, EnableTls, CdnBaseUrl, BackupGateways.Select(x => x.ToGateway(enableTls: EnableTls)).ToList());
        }
    }

    [Serializable]
    public class LogLevelOverrideSpec
    {
        public string ChannelName;
        public LogLevel LogLevel = LogLevel.Debug;

        public LogLevelOverrideSpec() { }

        public LogLevelOverrideSpec(LogLevel logLevel, string channelName)
        {
            LogLevel = logLevel;
            ChannelName = channelName;
        }
    }

    [Serializable]
    public class ClientLoggingConfig
    {
        [Tooltip("Default log level to use for in-game logging")]
        public LogLevel LogLevel = LogLevel.Debug;

        [Tooltip("Log level overrides per log channel")]
        public List<LogLevelOverrideSpec> LogLevelOverrides = new List<LogLevelOverrideSpec>();
    }

    /// <summary>
    /// Policy for publishing game configs from editor
    /// </summary>
    [Serializable]
    public enum GameConfigPublishPolicy
    {
        NotAllowed,         // No GameConfig publishing or uploading allowed
        UploadOnly,         // Can upload new GameConfigs but publish must be done in dashboard
        AskForConfirmation, // Show confirm dialog before publishing
        Allowed             // Ok to publish without confirmation
    }

    /// <summary>
    /// Admin API authentication token source
    /// </summary>
    [Serializable]
    public enum AuthenticationTokenProvider
    {
        None,
        Google,
        OAuth2,
    }

#if UNITY_EDITOR
    [Serializable]
    public class OAuth2AuthorizationServerSpec
    {
        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.ClientId"/>
        public string ClientId;

        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.ClientSecret"/>
        public string ClientSecret;

        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.AuthorizationEndpoint"/>
        [Tooltip("(Editor only) OAuth2 Authorization server that is used for authorizing access to the game server.")]
        public string AuthorizationEndpoint;

        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.TokenEndpoint"/>
        public string TokenEndpoint;

        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.Audience"/>
        public string Audience;

        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.LocalCallbackUrls"/>
        [Tooltip("Optional")] public List<string> LocalCallbackUrls;

        /// <inheritdoc cref="MetaplayOAuth2Client.IAuthorizationServerConfigOverride.UseStateParameter"/>
        public bool UseStateParameter;
    }
#endif

    [Serializable]
    public class ClientGameConfigBuildApiConfig
    {
#if UNITY_EDITOR
        [Tooltip("(Editor only) Base URL to Admin API (eg, 'http://localhost:5550/api/' or 'https://mygame-develop-api.p1.metaplay.io/api/')")]
        public string AdminApiBaseUrl;

        [Tooltip("(Editor only) Policy for GameConfig publishing from editor")]
        public GameConfigPublishPolicy GameConfigPublishPolicy;

        [Tooltip("(Editor only) Authentication token provider service for Admin API access")]
        public AuthenticationTokenProvider AdminApiAuthenticationTokenProvider;

        [Tooltip("(Editor only) Path to file containing service account credentials for authorizing access to the game server")]
        public string AdminApiCredentialsPath;

        [Tooltip("(Editor only) If true, expect the the Authorization Server to follow OpenID Connect protocol instead of OAuth2 authentication. In this case, rather than authorizing operations with the OAuth2 access_token, user is authenticated with OIDC id_token (and authorization is performed by Resource Server itself). This is currently needed in certain Metaplay-managed auth setups.")]
        public bool AdminApiUseOpenIdConnectIdToken;

        [Tooltip("(Editor only) OAuth2 Authorization server that is used for authorizing access to the game server.")]
        public OAuth2AuthorizationServerSpec AdminApiOAuth2Settings;
#endif
    }

    /// <summary>
    /// Specifications for an environment identified by <see cref="Id"/>. Contains Metaplay specific environment specs.
    /// Inherit this to extend the contained specs and override relevant members in your custom Environment Config Provider
    /// implementing <see cref="IEnvironmentConfigProvider"/>.
    /// </summary>
    [Serializable]
    public class EnvironmentConfig : IMetaIntegrationConstructible<EnvironmentConfig>
    {
        [Tooltip("Unique identifier for this environment, such as 'development' or 'feature-foo'")]
        public string Id;
        [Tooltip("Optional description of the environment to provide developers with extra information")]
        public string Description;
        [Tooltip("If true, device builds for this environment will not be permitted. This is convenient to set true for localhost environments.")]
        public bool DoNotBuildForDevices;

        public ConnectionEndpointConfig       ConnectionEndpointConfig;
        public ClientLoggingConfig            ClientLoggingConfig;
        public ClientGameConfigBuildApiConfig ClientGameConfigBuildApiConfig;

        /// <summary>
        /// Creates a filtered copy of the EnvironmentConfig. See <see cref="FilterForClientBuild"/>.
        /// </summary>
        /// <returns></returns>
        public virtual EnvironmentConfig CreateClientBuildCopy()
        {
            string            jsonObj               = JsonConvert.SerializeObject(this);
            Type              environmentConfigType = IntegrationRegistry.GetSingleIntegrationType(typeof(EnvironmentConfig));
            EnvironmentConfig clientBuildCopy       = (EnvironmentConfig)JsonConvert.DeserializeObject(jsonObj, environmentConfigType);

            clientBuildCopy.FilterForClientBuild();

            return clientBuildCopy;
        }

        /// <summary>
        /// Filter out any information from the environment config that we don't want to include in client builds.
        /// Override this to filter out custom extended information.
        /// </summary>
        protected virtual void FilterForClientBuild()
        {
            Description = "";
            ClientGameConfigBuildApiConfig = new ClientGameConfigBuildApiConfig();
        }
    }

    /// <summary>
    /// Base API for an Environment Config Provider. Inherit to extend or customize config source.
    /// See <see cref="DefaultEnvironmentConfigProvider"/>.
    /// </summary>
    public interface IEnvironmentConfigProvider : IMetaIntegrationSingleton<IEnvironmentConfigProvider>
    {
        public static EnvironmentConfig Get() => MetaplayServices.Get<IEnvironmentConfigProvider>().GetCurrent();

        /// <summary>
        /// Used to initialize the environment config provider, for example to load configs from files.
        /// </summary>
        public void InitializeSingleton();

        /// <summary>
        /// Returns the currently active environment config.
        /// </summary>
        public EnvironmentConfig GetCurrent();
    }
}
