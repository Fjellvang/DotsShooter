// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Player;
using Metaplay.Server.AdminApi.AuditLog;
using Metaplay.Server.Database;
using Metaplay.Server.GameConfig;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK routes to work with game configs.
    /// </summary>
    public partial class SystemGameConfigController : GameAdminApiController
    {
        // <summary>
        // Get list of StaticGameConfig entries in the database, without contents and stripped of all unnecessary data.
        // Usage:  GET /api/gameConfig
        // Test:   curl http://localhost:5550/api/gameConfig
        // </summary>
        [HttpGet("gameConfig")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigView)]
        public async Task<ActionResult<IEnumerable<MinimalGameConfigInfo>>> GetMinimalStaticGameConfigList([FromQuery] bool showArchived = false)
        {
            GlobalStatusResponse status = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            if (IsCacheValidAndSetLastModified(status.AnyGameConfigLastChangedAt.ToDateTime()))
                return StatusCode(304);

            IEnumerable<StaticGameConfigInfo> persisted = await QueryStaticGameConfigMetaData(status, showArchived);
            return AsResult(persisted.Select(x => new MinimalGameConfigInfo(x)));
        }

        // <summary>
        // API Endpoint to get library and experiment information of a config,
        // Usage:  GET /api/gameConfig/staticGameConfig/{configVersion}/count
        // Test:   curl http://localhost:5550/api/gameConfig/696EC7A86DAFE630-281F450A9D56CA1C
        // </summary>
        [HttpGet("gameConfig/{configIdStr}/count")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigView)]
        public async Task<IActionResult> GetGameConfigLibraryCounts(string configIdStr)
        {
            GlobalStatusResponse       status    = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            PersistedStaticGameConfig  persisted = await GameDataControllerUtility.GetPersistedGameDataByIdStringOr404Async<PersistedStaticGameConfig>(configIdStr, status.ActiveStaticGameConfigId);
            FullGameConfig             config = null;
            GameConfigImportExceptions importErrors;
            try
            {
                if (persisted.FailureInfo != null)
                   (config, importErrors) = (FullGameConfig.MetaDataOnly(GameConfigMetaData.FromArchive(ConfigArchive.FromBytes(persisted.ArchiveBytes))), default);
                else
                   (config, importErrors) = LoadGameConfigWithFiltering(persisted, librariesToInclude: null, includeMetadata: true);
            }
            catch (Exception ex)
            {
                importErrors = new GameConfigImportExceptions(null, new Exception[] {ex});
            }

            Dictionary<MetaGuid, BackgroundTaskStatus> taskStatuses = null;
            if (persisted.TaskId != null)
            {
                BackgroundTaskStatusResponse taskStatusResponse = await EntityAskAsync<BackgroundTaskStatusResponse>(BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(nameof(BuildStaticGameConfigTask)));
                taskStatuses = taskStatusResponse.Tasks.ToDictionary(t => t.Id, t => t);
            }

            return Ok(LibraryCountGameConfigInfo.FromGameConfig(persisted, status.ActiveStaticGameConfigId, config, importErrors, taskStatuses));
        }

        // <summary>
        // API Endpoint to get a content of a set of libraries, including variant patches
        // Usage:  POST /api/gameConfig/{configVersion}/content
        // Body:   {"Libraries": ["HappyHours"], "Experiments": ["EarlyGameFunnel"]}
        // Test:   curl --request POST --url http: //localhost:5551/api/gameConfig/03bf8a7a2dd587c-0-10385eeff6b71ee0/content --header 'Accept-Encoding: gzip' --header 'Content-Type: application/json' --data '{"Libraries": ["HappyHours"], "Experiments": ["EarlyGameFunnel"]}'
        // </summary>
        [HttpPost("gameConfig/{configIdStr}/content")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigView)]
        public async Task<IActionResult> PostStaticGameConfigContent(string configIdStr, [FromBody] ConfigRequestPostArgs requestArgs)
        {
            bool addedExperimentsLibrary = false;
            if (requestArgs.Experiments?.Count > 0 && (!requestArgs.Libraries?.Any(x => string.Equals(x, ServerGameConfigBase.PlayerExperimentsEntryName, StringComparison.Ordinal)) ?? false))
            {
                requestArgs.Libraries.Add(ServerGameConfigBase.PlayerExperimentsEntryName);
                addedExperimentsLibrary = true;
            }
            (FullGameConfig config, GameConfigImportExceptions importErrors) configAndExceptions = await LoadGameConfigWithFiltering(configIdStr, requestArgs.Libraries);

            // Error checking
            if (!CheckLibraryAndExperimentExists(requestArgs, configAndExceptions.config, out IActionResult actionResult))
                return actionResult;

            Dictionary<string, GameConfigLibraryJsonConversionUtility.ConfigKey> config = GameConfigLibraryJsonConversionUtility.ConvertGameConfigToConfigKeys(configAndExceptions.config, requestArgs.Experiments, _logger);

            if (addedExperimentsLibrary)
                config.Remove(ServerGameConfigBase.PlayerExperimentsEntryName);

            return Ok(
                new
                {
                    config               = config,
                    libraryImportErrors = configAndExceptions.importErrors?.LibraryImportExceptions.ToDictionary(x => x.Key, x => new ExceptionGameConfigError(x.Value, GameConfigPhaseType.Import))
                });
        }

        // <summary>
        // API Endpoint to get a diff between 2 configs
        // Usage:  POST /api/gameConfig/diff/{baselineConfigId}/{newConfigId}
        // Body:   {"Libraries": [ "Producers" ]}
        // Test:   curl --request POST --url http://localhost:5551/api/gameConfig/diff/03bfbc45bb8c57d-0-8cbdb5ae78e38f29/03bfc6b9d7d929c-0-74f7f2a4ee00083e --header 'Accept: application/json' --header 'Content-Type: application/json' --data '{"Libraries": [ "Producers" ]}'
        // </summary>
        [HttpPost("gameConfig/diff/{baselineConfigId}/{newConfigId}")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigView)]
        public async Task<IActionResult> PostStaticGameConfigDiff(string baselineConfigId, string newConfigId, [FromBody] ConfigRequestPostArgs args)
        {
            (FullGameConfig config, GameConfigImportExceptions importErrors) baseline  = await LoadGameConfigWithFiltering(baselineConfigId, args.Libraries);
            (FullGameConfig config, GameConfigImportExceptions importErrors) newConfig = await LoadGameConfigWithFiltering(newConfigId, args.Libraries);

            // Error checking
            if (!CheckLibraryAndExperimentExists(args, baseline.config, out IActionResult actionResult))
                return actionResult;
            if (!CheckLibraryAndExperimentExists(args, newConfig.config, out actionResult))
                return actionResult;

            Dictionary<string, GameConfigLibraryJsonConversionUtility.ConfigKey> config = GameConfigLibraryJsonConversionUtility.DiffPartialGameConfig(baselineConfigId, newConfigId, baseline.config, newConfig.config, _logger);

            return Ok(new {
                config                       = config,
                baselineLibraryImportErrors  = baseline.importErrors?.LibraryImportExceptions.ToDictionary(x => x.Key, x => new ExceptionGameConfigError(x.Value, GameConfigPhaseType.Import)),
                newConfigLibraryImportErrors = newConfig.importErrors?.LibraryImportExceptions.ToDictionary(x => x.Key, x => new ExceptionGameConfigError(x.Value, GameConfigPhaseType.Import))
            });
        }

        /// <summary>
        /// API Endpoint to get active game config id
        /// Usage:  GET /api/activeGameConfigId
        /// Test:   curl http://localhost:5550/api/activeGameConfigId
        /// </summary>
        [HttpGet("activeGameConfigId")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigView)]
        public async Task<IActionResult> GetActiveGameConfigId()
        {
            // Fetch active config version from global state
            // \note: Integration tests depend on 404 for no-active-gameconfig.
            GlobalStatusResponse globalStatus = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            if (!globalStatus.ActiveStaticGameConfigId.IsValid)
                return NotFound();

            return Ok(globalStatus.ActiveStaticGameConfigId.ToString());
        }

        // <summary>
        // API Endpoint to archive a set of game configs
        // Usage:  POST /api/gameConfig/archive
        // Body:   [ "03bfbc45bb8c57d-0-8cbdb5ae78e38f29", "03bfc6b9d7d929c-0-74f7f2a4ee00083e"]
        // Test:   curl --request POST --url http://localhost:5551/api/gameConfig/archive --header 'Accept: application/json' --header 'Content-Type: application/json' --data '[ "03bfbc45bb8c57d-0-8cbdb5ae78e38f29", "03bfc6b9d7d929c-0-74f7f2a4ee00083e"]'
        // </summary>
        [HttpPost("gameConfig/archive")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigEdit)]
        public async Task<IActionResult> BatchArchiveStaticGameConfig()
        {
            string[] input = await ParseBodyAsync<string[]>();

            foreach (string guid in input)
                await ArchiveGameConfig(ParseMetaGuidStr(guid));

            return Ok();
        }

        async Task ArchiveGameConfig(MetaGuid guid)
        {
            var input = new GameDataControllerUtility.GameDataEditableProperties() {IsArchived = true};

            await UpdateMetadata(input, guid);
        }

        // <summary>
        // API Endpoint to edit the properties of a StaticGameConfig.
        // Usage:  POST /api/gameConfig/{configId}
        // Test:   curl -X POST http://localhost:5550/api/gameConfig/696EC7A86DAFE630-281F450A9D56CA1C
        // </summary>
        [HttpPost("gameConfig/{configIdStr}")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigEdit)]
        public async Task<IActionResult> UpdateStaticGameConfig(string configIdStr)
        {
            MetaGuid                                             configId = ParseMetaGuidStr(configIdStr);
            GameDataControllerUtility.GameDataEditableProperties input    = await ParseBodyAsync<GameDataControllerUtility.GameDataEditableProperties>();

            await UpdateMetadata(input, configId);
            return Ok();
        }

        async Task UpdateMetadata(GameDataControllerUtility.GameDataEditableProperties input, MetaGuid configId)
        {
            MetaDatabase              db        = MetaDatabase.Get(QueryPriority.Normal);
            PersistedStaticGameConfig persisted = await db.TryGetAsync<PersistedStaticGameConfig>(configId.ToString());
            if (persisted == null)
                throw new MetaplayHttpException(404, "Static Game Config not found.", $"Cannot find static game config with id {configId}.");

            if (!string.IsNullOrEmpty(input.Name) || !string.IsNullOrEmpty(input.Description) || input.IsArchived.HasValue)
            {
                CreateOrUpdateGameDataResponse response = await EntityAskAsync(
                    GlobalStateManager.EntityId,
                    new CreateOrUpdateGameConfigRequest() {
                        Id          = configId,
                        Name        = input.Name,
                        Description = input.Description,
                        IsArchived  = input.IsArchived
                    });

                GameDataControllerUtility.GameDataEditableProperties oldValues = new GameDataControllerUtility.GameDataEditableProperties()
                {
                    Name        = response.OldName,
                    Description = response.OldDescription,
                    IsArchived  = response.OldIsArchived
                };
                GameDataControllerUtility.GameDataEditableProperties newValues = input.FillEmpty(oldValues);
                await WriteAuditLogEventAsync(new GameConfigEventBuilder(configId, new GameConfigEventStaticGameConfigEdited(oldValues, newValues)));
            }
            else
            {
                throw new MetaplayHttpException(400, "No valid input.", "Must pass an editable property in the body request.");
            }
        }

        /// <summary>
        /// API Endpoint to delete all archived game configs
        /// Usage:  DELETE /api/gameConfig/696EC7A86DAFE630-281F450A9D56CA1C
        /// Test:   curl --request DELETE --url http://localhost:5551/api/gameConfig/696EC7A86DAFE630-281F450A9D56CA1C
        /// </summary>
        /// <returns>The TaskId of the background task in charge of deleting the archived gameconfigs</returns>
        [HttpDelete("gameConfig/deleteArchived")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigDelete)]
        public async Task<IActionResult> DeleteAllArchived()
        {
            MetaGuid taskId = MetaGuid.New();

            DeleteAllArchivedGameConfigsBackgroundTask deleteAllArchivedBackgroundTask = new DeleteAllArchivedGameConfigsBackgroundTask();
            StartBackgroundTaskResponse response = await EntityAskAsync<StartBackgroundTaskResponse>(BackgroundTaskActor.EntityId, new StartBackgroundTaskRequest(taskId, deleteAllArchivedBackgroundTask));

            return Ok(response.Id);
        }

        // <summary>
        // API Endpoint to delete a game config
        // Usage:  POST /api/gameConfig/deleteArchived
        // Test:   curl --request POST --url http://localhost:5551/api/gameConfig/deleteArchived
        // </summary>
        [HttpDelete("gameConfig/{configIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigDelete)]
        public async Task<IActionResult> Delete(string configIdStr)
        {
            MetaGuid configId = ParseMetaGuidStr(configIdStr);
            await EntityAskAsync(GlobalStateManager.EntityId, new RemoveGameConfigRequest() {Id = configId});

            return Ok();
        }

        [HttpPost("gameConfig")]
        [Consumes("application/octet-stream")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigEdit)]
        public async Task<IActionResult> UploadStaticGameConfig(
            [FromQuery] bool setAsActive = true,
            [FromQuery] bool parentMustMatchActive = true,
            [FromQuery] bool kickConnectedClients = false,
            [FromQuery] string name = null,
            [FromQuery] string description = null)
        {
            // Parse ConfigArchive
            byte[] bytes = await ReadBodyBytesAsync();

            // Update via GlobalStateManager
            CreateOrUpdateGameDataResponse uploadResponse = await EntityAskAsync(
                GlobalStateManager.EntityId,
                new CreateOrUpdateGameConfigRequest()
                {
                    Name = name,
                    Description = description,
                    Content = bytes,
                    Source = GetUserId()
                });

            MetaGuid configId = uploadResponse.Id;

            List<EventBuilder> auditLogEvents = new List<EventBuilder>()
            {
                new GameConfigEventBuilder(configId, new GameConfigEventStaticGameConfigUploaded())
            };

            if (setAsActive)
            {
                PublishGameConfigResponse response = await EntityAskAsync(GlobalStateManager.EntityId, new PublishGameConfigRequest(configId, parentMustMatchActive, kickConnectedClients));

                if (response.Result is not PublishGameConfigResponse.SuccessResult successResponse)
                {
                    // if publish doesn't succeed, roll back
                    await EntityAskAsync(GlobalStateManager.EntityId, new RemoveGameConfigRequest() { Id = configId });

                    throw ConvertPublishGameConfigResponseToMetaplayHttpException(response);
                }

                // Success path
                MetaGuid oldActiveGameConfig = successResponse.PreviouslyActiveGameConfigId;
                if (oldActiveGameConfig.IsValid)
                    auditLogEvents.Add(new GameConfigEventBuilder(oldActiveGameConfig, new GameConfigEventGameConfigUnpublished()));
                auditLogEvents.Add(new GameConfigEventBuilder(configId, new GameConfigEventGameConfigPublished(kickConnectedClients)));
            }

            await WriteRelatedAuditLogEventsAsync(auditLogEvents);

            return Ok(new { uploadResponse.Id });
        }

        // <summary>
        // API Endpoint to publish a game config build
        // Usage:  POST /api/gameConfig/publish
        // Test:   curl -X POST http://localhost:5550/api/gameConfig/publish
        // </summary>
        [HttpPost("gameConfig/publish")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigEdit)]
        public async Task<IActionResult> PublishGameConfig([FromQuery] bool parentMustMatchActive = true, [FromQuery] bool kickConnectedClients = false)
        {
            GameDataControllerUtility.GameDataIdInput input    = await ParseBodyAsync<GameDataControllerUtility.GameDataIdInput>();
            PublishGameConfigResponse                 response = await EntityAskAsync(GlobalStateManager.EntityId, new PublishGameConfigRequest(input.Id, parentMustMatchActive, kickConnectedClients));

            if (response.Result is not PublishGameConfigResponse.SuccessResult successResponse)
                throw ConvertPublishGameConfigResponseToMetaplayHttpException(response);

            MetaGuid oldActiveGameConfig = successResponse.PreviouslyActiveGameConfigId;

            await WriteRelatedAuditLogEventsAsync(new List<EventBuilder>
            {
                new GameConfigEventBuilder(oldActiveGameConfig, new GameConfigEventGameConfigUnpublished()),
                new GameConfigEventBuilder(input.Id, new GameConfigEventGameConfigPublished(kickConnectedClients)),
            });

            return Ok();
        }

        MetaplayHttpException ConvertPublishGameConfigResponseToMetaplayHttpException(PublishGameConfigResponse response)
        {
            switch (response.Result)
            {
                case PublishGameConfigResponse.RefusedResult refused:
                    return new MetaplayHttpException(400, "Game config archive refused", refused.Reason);

                case PublishGameConfigResponse.ParentConfigIdPreconditionFailedResult preconditionFailed:
                    return new MetaplayHttpException(
                        400,
                        "Partial game config is not built on currently active game config",
                        $"The given partial game config archive is built on game config {preconditionFailed.RequiredGameConfigId}, but the currently active game config is {preconditionFailed.ActiveGameConfigId}. "
                        + "To publish the partial game config, either build it on the current game config (try again), or specify 'parentMustMatchActive=false' query parameter to this HTTP call. "
                        + "Note that if 'parentMustMatchActive=false', publish may change game configs that were not included in the partial build.");

                case PublishGameConfigResponse.UpdatePublishInfoFailedResult updatePublishInfoFailedResult:
                    return new MetaplayHttpException(500, "Game config updating publishing info in database failed", updatePublishInfoFailedResult.ErrorMessage);

                case PublishGameConfigResponse.PublishFailedResult publishFailed:
                    return new MetaplayHttpException(500, "Game config publish failed", publishFailed.ErrorMessage);

                default:
                    return new MetaplayHttpException(400, "Cannot publish config", $"Publish failed with unknown error result: {response.Result?.GetType().ToGenericTypeString()}");
            }
        }

        // <summary>
        // API Endpoint to start a new game config build
        // Usage:  POST /api/gameConfig/build
        // Test:   curl -X POST http://localhost:5550/api/gameConfig/build
        // </summary>
        [HttpPost("gameConfig/build")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigEdit)]
        public async Task<IActionResult> StartStaticGameConfigBuild()
        {
            StaticGameConfigBuildInput input = await ParseBodyAsync<StaticGameConfigBuildInput>();

            // Generate an id for build task
            MetaGuid taskId = MetaGuid.New();

            // Create an empty StaticGameConfig entry for this build
            MetaGuid configId = (await EntityAskAsync(
                GlobalStateManager.EntityId,
                new CreateOrUpdateGameConfigRequest() {
                    Source = GetUserId(),
                    Name = input.Properties.Name,
                    Description = input.Properties.Description,
                    IsArchived = input.Properties.IsArchived,
                    TaskId = taskId
                })).Id;

            // Start the build task
            BuildStaticGameConfigTask buildTask = new BuildStaticGameConfigTask(configId, input.ParentConfigId, input.BuildParams);
            _ = await EntityAskAsync(BackgroundTaskActor.EntityId, new StartBackgroundTaskRequest(taskId, buildTask));

            await WriteAuditLogEventAsync(new GameConfigEventBuilder(configId, new GameConfigEventStaticGameBuildStarted(input)));

            return Ok(new { Id = configId });
        }

        static string GetCdnVersionForActiveConfig(MetaGuid id, MetaGuid activeId)
        {
            if (id != activeId)
                return "";

            ActiveGameConfig activeGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get();
            return activeGameConfig.ClientSharedGameConfigContentHash.ToString();
        }

        async Task<IEnumerable<StaticGameConfigInfo>> QueryStaticGameConfigMetaData(GlobalStatusResponse status, bool showArchived)
        {
            MetaDatabase                           db      = MetaDatabase.Get(QueryPriority.Normal);
            IEnumerable<PersistedStaticGameConfig> configs = await db.QueryAllStaticGameConfigs(showArchived);

            // Retrieve build task statuses if needed
            Dictionary<MetaGuid, BackgroundTaskStatus> taskStatuses = null;
            if (configs.Any(config => config.TaskId != null))
            {
                BackgroundTaskStatusResponse taskStatusResponse = await EntityAskAsync<BackgroundTaskStatusResponse>(BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(nameof(BuildStaticGameConfigTask)));
                taskStatuses = taskStatusResponse.Tasks.ToDictionary(t => t.Id, t => t);
            }

            IEnumerable<StaticGameConfigInfo> persisted = configs.Select(x => StaticGameConfigInfo.FromPersisted(x, status.ActiveStaticGameConfigId,  taskStatuses));
            return persisted;
        }

        /// <summary>
        /// Utility to figure out whether this config can be published and a relevant error message, all parameters are optional but at least one is required.
        /// </summary>
        static (GameDataControllerUtility.GameDataStatus status, List<GameConfigErrorBase> errors) GetPublishableStatusAndErrors(
            GameConfigImportExceptions importErrors = default,
            string failureInfo = null,
            PersistedGameData persistedGameData = null,
            Dictionary<MetaGuid, BackgroundTaskStatus> statuses = null,
            int blockingMessageCount = 0)
        {
            List<GameConfigErrorBase> errors = new List<GameConfigErrorBase>();
            if (persistedGameData != null && statuses != null)
            {
                if (persistedGameData.TaskId != null && persistedGameData.TaskId != MetaGuid.None.ToString())
                {
                    if (statuses.TryGetValue(MetaGuid.Parse(persistedGameData.TaskId), out BackgroundTaskStatus taskStatus))
                    {
                        // Early exit if we're still building, no point in reporting errors if we expect to have an invalid state.
                        if (!taskStatus.Completed)
                            return (GameDataControllerUtility.GameDataStatus.Building, errors);

                        // Don't think this ever triggers because we have a try catch in the BackgroundTask.Run?
                        if (taskStatus.Failure != null)
                            errors.Add(new StringExceptionGameConfigError(taskStatus.Failure,  GameConfigPhaseType.Build));
                    }
                    else
                        errors.Add(new GameConfigErrorBase(GameConfigErrorType.TaskDisappeared, GameConfigPhaseType.Build));
                }
            }

            if (!string.IsNullOrWhiteSpace(failureInfo) && (failureInfo != "Game Config build failed, see log for errors" || blockingMessageCount == 0 ))
                errors.Add(new StringExceptionGameConfigError(failureInfo, GameConfigPhaseType.Build));

            if (blockingMessageCount > 0)
                errors.Add(new GameConfigErrorBase(GameConfigErrorType.BlockingMessages, GameConfigPhaseType.Build));

            if (importErrors?.LibraryImportExceptions?.Count > 0)
                errors.Add(new GameConfigErrorBase(GameConfigErrorType.LibraryImport, GameConfigPhaseType.Import));

            if (importErrors?.GlobalExceptions?.Count > 0)
                errors.Add(new ExceptionGameConfigError(importErrors.GetGlobalExceptionOrAggregate(), GameConfigPhaseType.Import));

            return (errors.Count > 0 ? GameDataControllerUtility.GameDataStatus.Failed : GameDataControllerUtility.GameDataStatus.Success, errors: errors);
        }

        bool CheckLibraryAndExperimentExists(ConfigRequestPostArgs requestArgs, FullGameConfig fullGameConfig, out IActionResult actionResult)
        {
            if (requestArgs.Libraries != null)
            {
                Dictionary<string, IGameConfigEntry> sharedConfigEntries = fullGameConfig.SharedConfig.GetConfigEntries().ToDictionary(x => x.EntryInfo.Name, x => x.Entry);
                Dictionary<string, IGameConfigEntry> serverConfigEntries = fullGameConfig.ServerConfig.GetConfigEntries().ToDictionary(x => x.EntryInfo.Name, x => x.Entry);
                foreach (string lib in requestArgs.Libraries)
                {
                    sharedConfigEntries.TryGetValue(lib, out IGameConfigEntry sharedConfigEntry);
                    serverConfigEntries.TryGetValue(lib, out IGameConfigEntry serverConfigEntry);
                    if (serverConfigEntry == null && sharedConfigEntry == null)
                    {
                        actionResult = BadRequest($"Library {lib} not found.");
                        return false;
                    }
                }
            }

            if (requestArgs.Experiments?.Count > 0 &&
                fullGameConfig.ServerConfig.PlayerExperiments != null )
            {
                foreach (string experiment in requestArgs.Experiments)
                {
                    if (fullGameConfig.ServerConfig.PlayerExperiments.GetValueOrDefault(PlayerExperimentId.FromString(experiment)) == null)
                    {
                        actionResult = BadRequest($"Player experiment {experiment} not found.");
                        return false;
                    }
                }
            }

            actionResult = Ok();
            return true;
        }

        (FullGameConfig config, GameConfigImportExceptions importErrors) LoadGameConfigWithFiltering(
            PersistedStaticGameConfig persisted,
            List<string> librariesToInclude = null,
            bool includeMetadata = false,
            bool omitPatchesInServerConfigExperiments = false)
        {
            (FullGameConfig config, GameConfigImportExceptions importErrors) configWithErrors;

            if (persisted.ArchiveBytes != null)
            {
                ConfigArchive configArchive = ConfigArchive.FromBytes(persisted.ArchiveBytes);

                configWithErrors = FullGameConfig.CreatePartial(
                    configArchive,
                    includeMetadata: includeMetadata,
                    filters: librariesToInclude?.ToHashSet(),
                    omitPatchesInServerConfigExperiments: omitPatchesInServerConfigExperiments);
            }
            else
                throw new InvalidOperationException("Unable to parse libraries from PersistedStaticGameConfig, PersistedStaticGameConfig does not contain archive bytes.");

            return configWithErrors;
        }

        async Task<(FullGameConfig config, GameConfigImportExceptions importErrors)> LoadGameConfigWithFiltering(string configIdStr, List<string> libraries, bool includeMetadata = false)
        {
            GlobalStatusResponse      status    = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            PersistedStaticGameConfig persisted = await GameDataControllerUtility.GetPersistedGameDataByIdStringOr404Async<PersistedStaticGameConfig>(configIdStr, status.ActiveStaticGameConfigId);
            return LoadGameConfigWithFiltering(persisted, libraries, includeMetadata);
        }
    }
}
