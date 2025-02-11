// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.Database;
using Metaplay.Server.GameConfig;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    [Table("StaticGameConfigs")]
    [NonPartitioned]
    public class PersistedStaticGameConfig : PersistedGameData
    {
        public byte[] MetaDataBytes { get; set; }
    }

    #region MetaMessages

    [MetaMessage(MessageCodesCore.CreateOrUpdateGameConfigRequest, MessageDirection.ServerInternal)]
    [MetaImplicitMembersRange(1, 100)]
    public class CreateOrUpdateGameConfigRequest : GameConfigManager.CreateOrUpdateRequest { }

    [MetaMessage(MessageCodesCore.RemoveStaticGameConfigRequest, MessageDirection.ServerInternal)]
    [MetaImplicitMembersRange(1, 100)]
    public class RemoveGameConfigRequest : GameConfigManager.RemoveRequest { }

    [MetaMessage(MessageCodesCore.GameConfigDataChangedRequest, MessageDirection.ServerInternal)]
    [MetaImplicitMembersRange(1, 100)]
    public class GameConfigDataChangedRequest : GameConfigManager.GameDataChangedRequest { }

    /// <summary>
    /// Request to set a GameConfig active.
    /// </summary>
    [MetaMessage(MessageCodesCore.PublishGameConfigRequest, MessageDirection.ServerInternal)]
    public class PublishGameConfigRequest : EntityAskRequest<PublishGameConfigResponse>
    {
        public MetaGuid Id                    { get; private set; } // StaticFullGameConfig
        public bool     ParentMustMatchActive { get; private set; }
        public bool     KickConnectedClients  { get; private set; }
        // add dynamic version here

        PublishGameConfigRequest() { }

        public PublishGameConfigRequest(MetaGuid id, bool parentMustMatchActive, bool kickConnectedClients)
        {
            Id                    = id;
            ParentMustMatchActive = parentMustMatchActive;
            KickConnectedClients  = kickConnectedClients;
        }
    }

    [MetaMessage(MessageCodesCore.PublishGameConfigResponse, MessageDirection.ServerInternal)]
    public class PublishGameConfigResponse : EntityAskResponse
    {
        [MetaSerializable(MetaSerializableFlags.ImplicitMembers)]
        [MetaImplicitMembersDefaultRangeForMostDerivedClass(1, 10)]
        public abstract class ResultBase
        {
        }
        [MetaSerializableDerived(1)]
        public class SuccessResult : ResultBase
        {
            /// <summary>
            /// The game config ID that was active before this publish request changed it.
            /// </summary>
            public MetaGuid PreviouslyActiveGameConfigId { get; private set; }

            SuccessResult() { }
            public SuccessResult(MetaGuid previouslyActiveGameConfigId)
            {
                PreviouslyActiveGameConfigId = previouslyActiveGameConfigId;
            }
        }
        [MetaSerializableDerived(2)]
        public class RefusedResult : ResultBase
        {
            /// <summary>
            /// The message describing why the game config publish was refused.
            /// </summary>
            public string Reason { get; private set; }

            RefusedResult() { }
            public RefusedResult(string reason)
            {
                Reason = reason;
            }
        }
        [MetaSerializableDerived(3)]
        public class ParentConfigIdPreconditionFailedResult : ResultBase
        {
            /// <summary>
            /// The ID of the active game config.
            /// </summary>
            public MetaGuid ActiveGameConfigId { get; private set; }

            /// <summary>
            /// The ID of the game config that was required to be active but wasn't.
            /// </summary>
            public MetaGuid RequiredGameConfigId { get; private set; }

            ParentConfigIdPreconditionFailedResult() { }
            public ParentConfigIdPreconditionFailedResult(MetaGuid activeGameConfigId, MetaGuid requiredGameConfigId)
            {
                ActiveGameConfigId = activeGameConfigId;
                RequiredGameConfigId = requiredGameConfigId;
            }
        }
        [MetaSerializableDerived(4)]
        public class PublishFailedResult : ResultBase
        {
            /// <summary>
            /// The message describing why the game config publish failed.
            /// </summary>
            public string ErrorMessage { get; private set; }

            PublishFailedResult() { }
            public PublishFailedResult(string errorMessage)
            {
                ErrorMessage = errorMessage;
            }
        }
        [MetaSerializableDerived(5)]
        public class UpdatePublishInfoFailedResult : ResultBase
        {
            /// <summary>
            /// The message describing why the updating failed.
            /// </summary>
            public string ErrorMessage { get; private set; }

            public UpdatePublishInfoFailedResult() { }
            public UpdatePublishInfoFailedResult(string errorMessage)
            {
                ErrorMessage = errorMessage;
            }
        }

        public ResultBase Result { get; private set; }

        PublishGameConfigResponse() { }
        PublishGameConfigResponse(ResultBase result)
        {
            Result = result;
        }

        public static PublishGameConfigResponse Success(MetaGuid previousId) => new PublishGameConfigResponse(new SuccessResult(previousId));
        public static PublishGameConfigResponse Refused(string errorMessage) => new PublishGameConfigResponse(new RefusedResult(errorMessage));
        public static PublishGameConfigResponse ParentConfigIdPreconditionFailed(MetaGuid activeGameConfigId, MetaGuid requiredGameConfigId) => new PublishGameConfigResponse(new ParentConfigIdPreconditionFailedResult(activeGameConfigId, requiredGameConfigId));
        public static PublishGameConfigResponse PublishFailed(string errorMessage) => new PublishGameConfigResponse(new RefusedResult(errorMessage));
        public static PublishGameConfigResponse UpdatePublishInfoFailed(string errorMessage) => new PublishGameConfigResponse(new UpdatePublishInfoFailedResult(errorMessage));
    }

    #endregion

    public class GameConfigManager : GameDataManager<PersistedStaticGameConfig>
    {
        protected override string GameDataTypeDesc => "GameConfig";
        protected override (string, bool) BuiltinArchivePath => (RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>().StaticGameConfigPath, false);

        protected override void OnAnyGameDataChanged(MetaGuid guid)
        {
            _owner.AnyGameConfigLastChangedAt = DateTime.UtcNow;
        }

        protected override MetaGuid ActiveVersion
        {
            get => _state.StaticGameConfigId;
            set
            {
                _state.StaticGameConfigId     = value;
                _state.LatestGameConfigUpdate = MetaTime.Now;
            }
        }

        protected override MetaGuid LatestAutoUpdateVersion
        {
            get => _state.LatestGameConfigAutoUpdateId;
            set => _state.LatestGameConfigAutoUpdateId = value;
        }

        public GameConfigManager(IGlobalStateManager owner) : base(owner) { }

        /// <summary>
        /// Tries to parse the archive as a static game config archive. Throws on failure.
        /// </summary>
        public static void TestStaticGameConfigArchive(ConfigArchive archive)
        {
            // Parse the content to make sure it's valid
            _ = FullGameConfig.CreateSoloUnpatched(archive);
        }

        protected override void PrepareContentsForPersisting(PersistedStaticGameConfig entry, byte[] contents)
        {
            ConfigArchive archive = ConfigArchive.FromBytes(contents);

            // Sanity check archive
            ConfigArchiveBuildUtility.TestArchiveVersion("StaticGameConfig", archive);
            if (entry.FailureInfo == null)
            {
                TestStaticGameConfigArchive(archive);
                entry.VersionHash    = archive.Version.ToString();
            }

            // Re-encode and compress the config
            entry.ArchiveBuiltAt = archive.CreatedAt.ToDateTime();
            entry.ArchiveBytes   = CompressArchiveForPersisting(archive);

            // Remove build report from the MetaData which is stored in the MetaDataBytes column to ensure *near* constant size (build params is user defined thus can vary), this is important for performance in the dashboard list page.
            byte[]             metadataBytes      = null;
            GameConfigMetaData gameConfigMetaData = GameConfigMetaData.FromArchive(archive);
            if (gameConfigMetaData != null)
                metadataBytes = gameConfigMetaData.StripBuildReportAndCloneForPersisting().ToBytes();

            entry.MetaDataBytes = metadataBytes;
        }

        [EntityAskHandler]
        public Task<CreateOrUpdateGameDataResponse> HandleCreateOrUpdateGameConfigRequest(CreateOrUpdateGameConfigRequest request) => CreateOrUpdate(request);

        [EntityAskHandler]
        public EntityAskOk HandleGameConfigDataChangedRequest(GameConfigDataChangedRequest changedRequest)
        {
            OnAnyGameDataChanged(changedRequest.ChangedGuid);
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public async Task<EntityAskOk> HandleRemoveGameConfigRequest(RemoveGameConfigRequest request)
        {
            if (_state.StaticGameConfigId == request.Id)
                throw new InvalidEntityAsk($"Can't remove current active game config {request.Id}");

            return await Remove(request);
        }

        static async Task<(ConfigArchive, FullGameConfig)> GetGameConfigFromDatabaseAsync(MetaGuid id)
        {
            FullGameConfigImportResources resources = await ServerGameConfigProvider.Instance.GetImportResourcesAsync(id, MetaGuid.None);
            FullGameConfig                config    = ServerGameConfigProvider.Instance.GetBaselineGameConfig(id, MetaGuid.None, resources);
            return (resources.FullArchive, config);
        }

        public async Task Initialize()
        {
            SystemOptions systemOpts = RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>();

            MetaGuid currentVersion = ActiveVersion;

            // Get the current game config.
            (ConfigArchive archive, FullGameConfig config) = await GetOrUpdateInitialDataAsync(GetGameConfigFromDatabaseAsync);

            MetaGuid newVersion = ActiveVersion;

            if (config == null)
            {
                // Require some game config.
                if (systemOpts.MustHaveGameConfigToStart)
                    throw new InvalidOperationException($"A valid StaticGameConfig is needed. There was no valid game config in System:StaticGameConfigPath ({systemOpts.StaticGameConfigPath}), nor is there a valid StaticGameConfig in the server state.");
            }
            else
            {
                ConfigArchiveDeliverables deliverables = await UploadSharedGameConfigToCdnAsync(archive);
                _state.SharedGameConfigDeliverables = deliverables;
                await _owner.OnActiveGameConfigChanged(config, isInitial: true);

                if (currentVersion != newVersion)
                {
                    DateTime changedDateTime = MetaTime.Now.ToDateTime();

                    await UpdatePublishingInfo(currentVersion, newVersion, changedDateTime);
                }
            }
        }

        static async Task UpdatePublishingInfo(MetaGuid currentVersion, MetaGuid newVersion, DateTime changedDateTime)
        {
            MetaDatabase              db             = MetaDatabase.Get(QueryPriority.Normal);
            PersistedStaticGameConfig newConfig      = await db.TryGetAsync<PersistedStaticGameConfig>(newVersion.ToString());
            PersistedStaticGameConfig previousConfig = null;
            if (currentVersion != MetaGuid.None)
                previousConfig = await db.TryGetAsync<PersistedStaticGameConfig>(currentVersion.ToString());

            await UpdatePublishingInfo(db, previousConfig, newConfig, changedDateTime);
        }

        static async Task UpdatePublishingInfo(MetaDatabase db, PersistedStaticGameConfig currentConfig, PersistedStaticGameConfig newConfig, DateTime changedDateTime)
        {
            newConfig.PublishedAt = changedDateTime;

            if (currentConfig != null)
            {
                currentConfig.UnpublishedAt = changedDateTime;
                await db.InsertOrUpdateAsync(currentConfig);
            }

            await db.InsertOrUpdateAsync(newConfig);
        }


        [EntityAskHandler]
        public async Task<PublishGameConfigResponse> HandlePublishGameConfigRequest(PublishGameConfigRequest request)
        {
            _log.Info($"Publish game config request with StaticGameConfig id {request.Id} received, ParentMustMatchActive {request.ParentMustMatchActive}, KickConnectedClients {request.KickConnectedClients}");

            // Get StaticGameConfig from DB
            MetaDatabase              db           = MetaDatabase.Get(QueryPriority.Normal);
            PersistedStaticGameConfig staticConfig = await db.TryGetAsync<PersistedStaticGameConfig>(request.Id.ToString());
            if (staticConfig == null)
                return PublishGameConfigResponse.Refused($"Static game config {request.Id} not found in database");
            if (staticConfig.ArchiveBytes == null || staticConfig.FailureInfo != null)
                return PublishGameConfigResponse.Refused($"Static game config {request.Id} was not successfully built");

            ConfigArchive staticConfigArchive;
            try
            {
                staticConfigArchive = ConfigArchive.FromBytes(staticConfig.ArchiveBytes);
            }
            catch (Exception e)
            {
                return PublishGameConfigResponse.Refused($"Static game config {request.Id} loading failed: {e}");
            }

            GameConfigMetaData metaData = GameConfigMetaData.FromArchive(staticConfigArchive);
            if (metaData != null)
            {
                if (request.ParentMustMatchActive)
                {
                    // When requested, and the gameconfig being published is a partial build, only publish
                    // if the currently active gameconfig is the parent
                    if (metaData.ParentConfigId.IsValid)
                    {
                        if (metaData.ParentConfigId != _state.StaticGameConfigId)
                            return PublishGameConfigResponse.ParentConfigIdPreconditionFailed(_state.StaticGameConfigId, metaData.ParentConfigId);
                    }
                }

                if (ShouldBlockPublishDueToBuildLog(metaData))
                    return PublishGameConfigResponse.Refused("GameConfigs with warnings are not allowed to be published in this environment.");
            }

            MetaGuid previousId = _state.StaticGameConfigId;

            try
            {
                DateTime changedDateTime = MetaTime.Now.ToDateTime();
                staticConfig.PublishedAt = changedDateTime;

                PersistedStaticGameConfig previousConfig = null;
                if (previousId != MetaGuid.None)
                    previousConfig = await db.TryGetAsync<PersistedStaticGameConfig>(previousId.ToString());

                // This happens before persisting the changed game config to ensure that the behaviour is the same as on startup
                await UpdatePublishingInfo(db, previousConfig, staticConfig, changedDateTime);
            }
            catch (Exception e)
            {
                _log.Error("Failed to update publishedAt in game config: {Error}", e);
                return PublishGameConfigResponse.PublishFailed($"Static game config {request.Id} publish failed: {e}");
            }

            try
            {
                await PublishGameConfig(request.Id, staticConfigArchive, request.KickConnectedClients);
                OnAnyGameDataChanged(request.Id);
            }
            catch (Exception e)
            {
                _log.Error("Failed to publish game config: {Error}", e);
                return PublishGameConfigResponse.PublishFailed($"Static game config {request.Id} publish failed: {e}");
            }

            return PublishGameConfigResponse.Success(previousId);
        }

        /// <summary>
        /// Upload the specified SharedGameConfig into CDN under GameConfig/SharedGameConfig/. Throws on failure.
        /// </summary>
        async Task<ConfigArchiveDeliverables> UploadSharedGameConfigToCdnAsync(ConfigArchive staticGameConfigArchive)
        {
            ContentDeliveryOptions deliveryOptions = RuntimeOptionsRegistry.Instance.GetCurrent<ContentDeliveryOptions>();

            // Extract shared archive for client
            (ContentHash version, byte[] compressedArchive) = GameConfigUtil.GetSharedArchiveFromFullArchiveForClient(staticGameConfigArchive,
                deliveryOptions.ArchiveCompressionAlgorithm, deliveryOptions.ArchiveMinimumSizeBeforeCompression);

            // Upload to CDN
            await ServerConfigDataProvider.Instance.PublicBlobStorage.PutAsync($"SharedGameConfig/{version}", compressedArchive);

            return new ConfigArchiveDeliverables(version);
        }

        /// <summary>
        /// Uploads the necessary CDN resources and makes the given GameConfig active. On failure, throws without making the game config active.
        /// </summary>
        async Task PublishGameConfig(MetaGuid staticConfigId, ConfigArchive staticConfig, bool kickConnectedClients)
        {
            // First import the game config to ensure it does not throw.
            FullGameConfig fullGameConfig = FullGameConfig.CreateSoloUnpatched(staticConfig);

            // Publish SharedGameConfig to CDN
            // \note: This may fail by throwing
            // \todo [petri] combine with DynamicGameConfig first
            ConfigArchiveDeliverables deliverables = await UploadSharedGameConfigToCdnAsync(staticConfig);

            // Store id & version and persist
            // \note: patch versions are automatically set in MakeSharedGameConfigPatchesActiveAsync
            _state.StaticGameConfigId           = staticConfigId;
            _state.LatestGameConfigUpdate       = MetaTime.Now;
            _state.SharedGameConfigDeliverables = deliverables;

            // If kicking client requested, set the force update timestamp to match this update. Use of any current configs
            // that were made active prior to this timestamp is disallowed.
            if (kickConnectedClients)
                _state.ClientForceUpdateGameConfigTimestamp = _state.LatestGameConfigUpdate;

            await _owner.OnActiveGameConfigChanged(fullGameConfig, isInitial: false);
        }

        static bool ShouldBlockPublishDueToBuildLog(GameConfigMetaData metaData)
        {
            if (metaData?.BuildReport == null)
                return false;

            GameConfigBuildOptions configBuildOptions = RuntimeOptionsRegistry.Instance.GetCurrent<GameConfigBuildOptions>();
            if (configBuildOptions.TreatWarningsAsErrors)
                return metaData.BuildSummary.HighestMessageLevel >= GameConfigLogLevel.Warning;

            return metaData.BuildSummary.HighestMessageLevel >= GameConfigLogLevel.Error;
        }
    }
}
