// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Cloud.Application;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Options;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Guild;
using Metaplay.Core.Json;
using Metaplay.Core.League.Player;
using Metaplay.Core.Localization;
using Metaplay.Server.GameConfig;
using Metaplay.Server.LiveOpsEvent;
using Metaplay.Server.Matchmaking;
using Metaplay.Server.TelemetryManager;
using Metaplay.Server.Web3;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.ActivablesUtil;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK routes that return information about the deployment.
    /// Note that all of this data is available to anyone who can access the admin dashboard,
    /// so be careful not to accidentally leak any secure data in here.
    /// </summary>
    public class SystemStatusController : GameAdminApiController
    {
        public class HelloResponse
        {
            public class EnabledFeatureFlags
            {
                public required bool PushNotifications                      { get; init; }
                public required bool Guilds                                 { get; init; }
                public required bool AsyncMatchmaker                        { get; init; }
                public required bool Web3                                   { get; init; }
                public required bool PlayerLeagues                          { get; init; }
                public required bool Localization                           { get; init; }
                // \todo #liveops-event Remove this in the future when liveops events are always enabled.
                //       For now this exists as an easy way to hide the sidebar entry in the dashboard.
                public required bool LiveOpsEvents                          { get; init; }
                public required bool GameTimeSkip                           { get; init; }
                public required bool GooglePlayInAppPurchaseRefunds         { get; init; }
                public required bool RemoveIapSubscriptions                 { get; init; }
            }

            public class AuthenticationConfig
            {
                public required AuthenticationType  Type                    { get; init; }
                public bool                         AllowAssumeRoles        { get; init; }
                public string                       RolePrefix              { get; init; }
                public string                       LogoutUri               { get; init; }
            }

            public class StaticProjectInfo
            {
                public required string ProjectName                          { get; init; }
            }

            public class StaticEnvironmentInfo
            {
                public required string EnvironmentName                      { get; init; }
                public required EnvironmentFamily EnvironmentFamily         { get; init; }
                public required bool IsProductionEnvironment                { get; init; }
                public required string GrafanaUri                           { get; init; }
                public required string KubernetesNamespace                  { get; init; }
            }

            public class StaticGameServerInfo
            {
                public required string BuildNumber                          { get; init; }
                public required string CommitId                             { get; init; }
            }

            public class StaticLiveOpsDashboardInfo
            {
                public required TimeSpan PlayerDeletionDefaultDelay         { get; init; }
                public required AuthenticationConfig AuthConfig             { get; init; }
            }

            public required StaticProjectInfo             ProjectInfo             { get; init; }
            public required StaticEnvironmentInfo         EnvironmentInfo         { get; init; }
            public required StaticGameServerInfo          GameServerInfo          { get; init; }
            public required StaticLiveOpsDashboardInfo    LiveOpsDashboardInfo    { get; init; }
            public required EnabledFeatureFlags     FeatureFlags                  { get; init; }
        }

        /// <summary>
        /// API endpoint to return just enough configuration for the dashboard to initialize itself
        /// Note that this endpoint is available to anyone who has access to the admin dashboard.
        /// Because of this, be very careful not to accidentally leak any sensitive data here!
        /// Usage:  GET /api/hello
        /// Test:   curl http://localhost:5550/api/hello
        /// </summary>
        [HttpGet("hello")]
        [RequirePermission(MetaplayPermissions.Anyone)]
        public ActionResult<HelloResponse> GetHello()
        {
            // Fetch all options
            (EnvironmentOptions envOpts, AdminApiOptions adminApiOpts, DeploymentOptions deployOpts, SystemOptions systemOpts, PushNotificationOptions pushOpts, GooglePlayStoreOptions playStoreOpts, GameTimeOptions gameTimeOpts) =
                RuntimeOptionsRegistry.Instance.GetCurrent<EnvironmentOptions, AdminApiOptions, DeploymentOptions, SystemOptions, PushNotificationOptions, GooglePlayStoreOptions, GameTimeOptions>();

            // Figure out the appropriate auth configuration
            HelloResponse.AuthenticationConfig authConfig;
            switch (adminApiOpts.Type)
            {
                case AuthenticationType.None:
                    authConfig = new HelloResponse.AuthenticationConfig
                    {
                        Type             = AuthenticationType.None,
                        AllowAssumeRoles = adminApiOpts.NoneConfiguration.AllowAssumeRoles,
                    };
                    break;

                case AuthenticationType.JWT:
                    authConfig = new HelloResponse.AuthenticationConfig
                    {
                        Type        = AuthenticationType.JWT,
                        RolePrefix  = adminApiOpts.JwtConfiguration.RolePrefix,
                        LogoutUri   = adminApiOpts.JwtConfiguration.LogoutUri,
                    };
                    break;

                default:
                    throw new InvalidOperationException("Invalid or incomplete auth type chosen.");
            }

            return new HelloResponse
            {
                ProjectInfo = new HelloResponse.StaticProjectInfo
                {
                    ProjectName = MetaplayCore.Options.ProjectName,
                },

                EnvironmentInfo = new HelloResponse.StaticEnvironmentInfo
                {
                    EnvironmentName         = envOpts.Environment,
                    EnvironmentFamily       = RuntimeEnvironmentInfo.Instance.EnvironmentFamily,
                    IsProductionEnvironment = RuntimeEnvironmentInfo.Instance.EnvironmentFamily == EnvironmentFamily.Production,
                    GrafanaUri              = string.IsNullOrEmpty(deployOpts.GrafanaUri) ? null : deployOpts.GrafanaUri.TrimEnd('/'),
                    KubernetesNamespace     = deployOpts.KubernetesNamespace,
                },

                GameServerInfo = new HelloResponse.StaticGameServerInfo
                {
                    BuildNumber = CloudCoreVersion.BuildNumber,
                    CommitId    = string.IsNullOrEmpty(CloudCoreVersion.CommitId) ? "not available" : CloudCoreVersion.CommitId,
                },

                LiveOpsDashboardInfo = new HelloResponse.StaticLiveOpsDashboardInfo
                {
                    PlayerDeletionDefaultDelay = systemOpts.PlayerDeletionDefaultDelay,
                    AuthConfig = authConfig,
                },

                FeatureFlags = new HelloResponse.EnabledFeatureFlags
                {
                    PushNotifications   = pushOpts.Enabled,
                    Guilds              = new GuildsEnabledCondition().IsEnabled,
                    AsyncMatchmaker     = new AsyncMatchmakingEnabledCondition().IsEnabled,
                    Web3                = new Web3EnabledCondition().IsEnabled,
                    PlayerLeagues       = new PlayerLeaguesEnabledCondition().IsEnabled,
                    Localization        = new LocalizationsEnabledCondition().IsEnabled,

                    // \todo #liveops-event Remove this in the future when liveops events are always enabled.
                    //       For now this exists as an easy way to hide the sidebar entry in the dashboard.
                    LiveOpsEvents       = new LiveOpsEventsEnabledCondition().IsEnabled,

                    GameTimeSkip        = gameTimeOpts.EnableTimeSkipping,
                    GooglePlayInAppPurchaseRefunds = playStoreOpts.EnableGooglePlayInAppPurchaseRefunds,
                    RemoveIapSubscriptions = envOpts.EnableDevelopmentFeatures,
                }
            };
        }

        public class StaticConfigResponse
        {
            public required ClusterConfig           ClusterConfig                { get; init; }
            public required LanguageId              DefaultLanguage              { get; init; }
            public required MetaVersionRange        SupportedLogicVersions       { get; init; }
            public required List<int>               SupportedLogicVersionOptions { get; init; }
            public required ActivablesMetadata      ActivablesMetadata           { get; init; }
            public required ServerGameDataBuildInfo GameConfigBuildInfo          { get; init; }
            public required ServerGameDataBuildInfo LocalizationsBuildInfo       { get; init; }
        }

        /// <summary>
        /// API endpoint to return information about the deployment that doesn't change during runtime
        /// Usage:  GET /api/staticConfig
        /// Test:   curl http://localhost:5550/api/staticConfig
        /// </summary>
        [HttpGet("staticConfig")]
        [RequirePermission(MetaplayPermissions.ApiGeneralView)]
        public ActionResult<StaticConfigResponse> GetStaticConfig()
        {
            ClusteringOptions clusterOpts = RuntimeOptionsRegistry.Instance.GetCurrent<ClusteringOptions>();
            return new StaticConfigResponse
            {
                ClusterConfig = clusterOpts.ClusterConfig,
                DefaultLanguage = MetaplayCore.Options.DefaultLanguage,
                SupportedLogicVersions = MetaplayCore.Options.SupportedLogicVersions,
                SupportedLogicVersionOptions = MetaplayCore.Options.SupportedLogicVersionOptions,
                ActivablesMetadata = ActivablesUtil.GetMetadata(),
                GameConfigBuildInfo = ServerGameDataBuildInfo.GameConfigBuildInfo,
                LocalizationsBuildInfo = ServerGameDataBuildInfo.LocalizationsBuildInfo
            };
        }

        public class GameDataResponse
        {
            public required Dictionary<string, IGameConfigMember> GameConfig        { get; init; }
            public required Dictionary<string, IGameConfigMember> ServerGameConfig  { get; init; }
        }

        /// <summary>
        /// API endpoint to return potentially large game configuration
        /// Usage:  GET /api/gamedata
        /// Test:   curl http://localhost:5550/api/gamedata
        /// </summary>
        [HttpGet("gamedata")]
        [RequirePermission(MetaplayPermissions.ApiGeneralView)]
        public ActionResult<GameDataResponse> GetGameData()
        {
            ActiveGameConfig                     activeGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get();
            ISharedGameConfig                    sharedGameConfig = activeGameConfig.BaselineGameConfig.SharedConfig;
            IServerGameConfig                    serverGameConfig = activeGameConfig.BaselineGameConfig.ServerConfig;
            Dictionary<string, IGameConfigMember> sharedEntries   = GetGameConfigEntries(sharedGameConfig);
            Dictionary<string, IGameConfigMember> serverEntries   = GetGameConfigEntries(serverGameConfig);

            return new GameDataResponse
            {
                // \todo Should shared and server configs be separate top-level entries like this, or should they be merged into one?
                //       If separate, then consider renaming GameConfig to SharedGameConfig (including in dashboard).
                GameConfig = sharedEntries,
                ServerGameConfig = serverEntries,
            };
        }

        public static Dictionary<string, IGameConfigMember> GetGameConfigEntries<TGameConfig>(TGameConfig gameConfig)
            where TGameConfig : IGameConfig
        {
            GameConfigTypeInfo gameConfigTypeInfo = GameConfigRepository.Instance.GetGameConfigTypeInfo(gameConfig.GetType());
            Dictionary<string, IGameConfigMember> entries = gameConfigTypeInfo.Entries.Values
                .Where(entryInfo =>
                {
                    // Handle only-in-serialization-mode attribute
                    IncludeOnlyInJsonSerializationModeAttribute onlyInModeAttrib = entryInfo.MemberInfo.GetCustomAttribute<IncludeOnlyInJsonSerializationModeAttribute>(inherit: true);
                    if (onlyInModeAttrib != null && onlyInModeAttrib.Mode != JsonSerializationMode.AdminApi)
                        return false;
                    return true;
                })
                .ToDictionary(
                    entryInfo => entryInfo.MemberInfo.Name, // \todo [nuutti] Use member name, or the name specified in the GameConfigEntryAttribute? #config-entry-name
                    entryInfo => (IGameConfigMember)entryInfo.MemberInfo.GetDataMemberGetValueOnDeclaringType()(gameConfig));

            // Add SDK-declared IGameConfigMember properties even if they don't exist as actual game config entries.
            // This exposes the SDK-provided stub libraries to the dashboard.
            foreach (PropertyInfo interfaceProp in typeof(TGameConfig).EnumerateInstancePropertiesInUnspecifiedOrder())
            {
                if (!interfaceProp.PropertyType.IsGenericTypeOf(typeof(IGameConfigLibrary<,>)))
                    continue;
                if (!entries.ContainsKey(interfaceProp.Name))
                    entries.Add(interfaceProp.Name, (IGameConfigMember)interfaceProp.GetDataMemberGetValueOnDeclaringType()(gameConfig));
            }
            return entries;
        }

        public class StatusResponse
        {
            public class DatabaseConfig
            {
                public required DatabaseBackendType Backend         { get; init; }
                public required int                 ActiveShards    { get; init; }
                public required int                 TotalShards     { get; init; }
            }

            public required ClientCompatibilitySettings         ClientCompatibilitySettings { get; init; }
            public required MaintenanceStatus                   MaintenanceStatus           { get; init; }
            public required MetaDictionary<EntityKind, int>  LiveEntityCounts            { get; init; }
            public required DatabaseConfig                      DatabaseStatus              { get; init; }
            public required int                                 NumConcurrents              { get; init; }
            public required MetaDuration                        GameTimeOffset              { get; init; }
        }

        /// <summary>
        /// API endpoint to return frequently updated information about the deployment
        /// Usage:  GET /api/status
        /// Test:   curl http://localhost:5550/api/status
        /// </summary>
        [HttpGet("status")]
        [RequirePermission(MetaplayPermissions.ApiGeneralView)]
        public async Task<ActionResult<StatusResponse>> GetStatus()
        {
            GlobalStatusResponse                    globalStatus        = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            StatsCollectorLiveEntityCountResponse   liveEntityCounts    = await EntityAskAsync(StatsCollectorManager.EntityId, StatsCollectorLiveEntityCountRequest.Instance);
            StatsCollectorNumConcurrentsResponse    stats               = await EntityAskAsync(StatsCollectorManager.EntityId, StatsCollectorNumConcurrentsRequest.Instance);
            DatabaseOptions                         databaseOptions     = RuntimeOptionsRegistry.Instance.GetCurrent<DatabaseOptions>();

            return new StatusResponse
            {
                ClientCompatibilitySettings = globalStatus.ClientCompatibilitySettings,
                MaintenanceStatus           = globalStatus.MaintenanceStatus,
                LiveEntityCounts            = liveEntityCounts.LiveEntityCounts,
                DatabaseStatus              = new StatusResponse.DatabaseConfig
                {
                    Backend         = databaseOptions.Backend,
                    ActiveShards    = databaseOptions.NumActiveShards,
                    TotalShards     = databaseOptions.Shards.Length,
                },
                NumConcurrents = stats.NumConcurrents,
                GameTimeOffset = globalStatus.GameTimeOffset,
            };
        }

        public class DatabaseItemCountsResponse
        {
            public required MetaDictionary<string, int> TotalItemCounts { get; init; }
        }

        /// <summary>
        /// API endpoint to return total item counts of all item types (Players, Guilds, etc.) across
        /// all database shards.
        /// Usage:  GET /api/databaseItemCounts
        /// Test:   curl http://localhost:5550/api/databaseItemCounts
        /// </summary>
        [HttpGet("databaseItemCounts")]
        [RequirePermission(MetaplayPermissions.ApiGeneralView)]
        public async Task<ActionResult<DatabaseItemCountsResponse>> GetDatabaseItemCounts()
        {
            StatsCollectorStateResponse response    = await EntityAskAsync(StatsCollectorManager.EntityId, StatsCollectorStateRequest.Instance);
            StatsCollectorState         state       = response.State.Deserialize(resolver: null, logicVersion: null);

            // Compute per-table item counts over all shards
            MetaDictionary<string, int> totalItemCounts = new MetaDictionary<string, int>();
            foreach ((string tableName, int[] shardItemCounts) in state.DatabaseShardItemCounts)
                totalItemCounts.Add(tableName, shardItemCounts.Sum());

            // Return result
            return new DatabaseItemCountsResponse
            {
                TotalItemCounts = totalItemCounts
            };
        }

        public class DatabaseStatusResponse
        {
            public required RuntimeOptionsRegistry.OptionsWithSource    Options         { get; init; }
            public required int                                         NumShards       { get; init; }
            public required MetaDictionary<string, int[]>            ShardItemCounts { get; init; }
        }

        [HttpGet("databaseStatus")]
        [RequirePermission(MetaplayPermissions.ApiDatabaseStatus)]
        public async Task<ActionResult<DatabaseStatusResponse>> GetDatabaseStatus()
        {
            StatsCollectorStateResponse response    = await EntityAskAsync(StatsCollectorManager.EntityId, StatsCollectorStateRequest.Instance);
            StatsCollectorState         state       = response.State.Deserialize(resolver: null, logicVersion: null);
            return new DatabaseStatusResponse
            {
                Options         = RuntimeOptionsRegistry.Instance.GetOptions<DatabaseOptions>(),
                NumShards       = state.DatabaseShardItemCounts.First().Value.Count(),
                ShardItemCounts = state.DatabaseShardItemCounts,
            };
        }

        public class RuntimeOptionsResponse
        {
            public required object[]                                    AllSources  { get; init; }
            public required RuntimeOptionsRegistry.OptionsWithSource[]  Options     { get; init; }
        }

        /// <summary>
        /// API endpoint to get all current RuntimeOptions information
        /// Usage:  GET /api/runtimeOptions
        /// Test:   curl http://localhost:5550/api/runtimeOptions
        /// </summary>
        [HttpGet("runtimeOptions")]
        [RequirePermission(MetaplayPermissions.ApiRuntimeOptionsView)]
        public ActionResult<RuntimeOptionsResponse> GetRuntimeOptions()
        {
            RuntimeOptionsRegistry registry = RuntimeOptionsRegistry.Instance;

            return new RuntimeOptionsResponse
            {
                AllSources  = registry.GetAllSources(),
                Options     = registry.GetAllOptions(),
            };
        }

        /// <summary>
        /// API endpoint to get all environment links from the runtime options.
        /// Usage:  GET /api/quickLinks
        /// Test:   curl http://localhost:5550/api/quickLinks
        /// </summary>
        [HttpGet("quickLinks")]
        [RequirePermission(MetaplayPermissions.ApiQuickLinksView)]
        public ActionResult<QuickLink[]> GetQuickLinks()
        {
            EnvironmentOptions environmentOptions = RuntimeOptionsRegistry.Instance.GetCurrent<EnvironmentOptions>();
            return environmentOptions.QuickLinks;
        }

        /// <summary>
        /// API endpoint to get Dashboard specific configuration.
        /// Usage:  GET /api/dashboardOptions
        /// Test:   curl http://localhost:5550/api/dashboardOptions
        /// </summary>
        [HttpGet("dashboardOptions")]
        [RequirePermission(MetaplayPermissions.ApiGeneralView)]
        public ActionResult<LiveOpsDashboardOptions> GetDashboardOptions()
        {
            LiveOpsDashboardOptions liveOpsDashboardOptions = RuntimeOptionsRegistry.Instance.GetCurrent<LiveOpsDashboardOptions>();
            return liveOpsDashboardOptions;
        }

        /// <summary>
        /// API endpoint for testing entity ask failures
        /// <![CDATA[
        /// Usage:  GET /api/testEntityAskFailure?controlled={false/true}&async={false/true}
        /// Test:   curl http://localhost:5550/api/testEntityAskFailure?controlled=false&async=false
        /// ]]>
        /// </summary>
        [HttpGet("testEntityAskFailure")]
        [RequirePermission(MetaplayPermissions.ApiTestsAll)]
        public async Task TestEntityAskFailure([FromQuery] bool controlled = true, [FromQuery] bool async = false)
        {
            EnvironmentOptions envOpts = RuntimeOptionsRegistry.Instance.GetCurrent<EnvironmentOptions>();
            if (!envOpts.EnableDevelopmentFeatures)
                return;

            EntityAskRequest<EntityAskOk> request = async ? new TestAsyncHandlerEntityAskFailureRequest(controlled)
                                                    : new TestEntityAskFailureRequest(controlled);

            await EntityAskAsync(EntityId.Create(EntityKindCloudCore.DiagnosticTool, 0), request);
        }

        public class GetServerErrorsResponse
        {
            public required int                                   ErrorCount                     { get; init; }
            public required bool                                  OverMaxErrorCount              { get; init; }
            public required MetaDuration                          MaxAge                         { get; init; }
            public required bool                                  CollectorRestartedWithinMaxAge { get; init; }
            public required MetaTime                              CollectorRestartTime           { get; init; }
            public required RecentLogEventCounter.LogEventInfo[]  Errors                         { get; init; }
        }

        /// <summary>
        /// API endpoint to return error count and details for server runtime
        /// Usage:  GET /api/serverErrors
        /// Test:   curl http://localhost:5550/api/serverErrors
        /// </summary>
        [HttpGet("serverErrors")]
        [RequirePermission(MetaplayPermissions.ApiSystemViewServerErrorLogs)]
        public async Task<ActionResult<GetServerErrorsResponse>> GetServerErrors()
        {
            RecentLoggedErrorsResponse response = await EntityAskAsync(StatsCollectorManager.EntityId, RecentLoggedErrorsRequest.Instance);
            return new GetServerErrorsResponse
            {
                ErrorCount                      = response.RecentLoggedErrors,
                OverMaxErrorCount               = response.OverMaxErrorCount,
                MaxAge                          = response.MaxAge,
                CollectorRestartedWithinMaxAge  = response.CollectorRestartedWithinMaxAge,
                CollectorRestartTime            = response.CollectorRestartTime,
                Errors                          = response.ErrorsDetails
            };
        }

        public class GetTelemetryMessagesResponse
        {
            public required MetaTime                                    UpdatedAt   { get; init; }
            public required TelemetryManagerActor.TelemetryMessage[]    Messages    { get; init; }
        }

        /// <summary>
        /// Return the most recent telemetry messages.
        /// Usage:  GET /api/telemetryMessages
        /// Test:   curl http://localhost:5550/api/telemetryMessages
        /// </summary>
        [HttpGet("telemetryMessages")]
        [RequirePermission(MetaplayPermissions.ApiSystemViewTelemetryMessages)]
        public async Task<ActionResult<GetTelemetryMessagesResponse>> GetTelemetryMessages()
        {
            TelemetryManagerActor.GetMessagesResponse response = await EntityAskAsync(TelemetryManagerActor.EntityId, TelemetryManagerActor.GetMessagesRequest.Instance);
            return new GetTelemetryMessagesResponse
            {
                UpdatedAt   = response.UpdatedAt,
                Messages    = response.Messages,
            };
        }
    }
}
