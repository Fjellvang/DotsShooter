// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Model;
using Metaplay.Server.AdminApi.AuditLog;
using Metaplay.Server.Database;
using Metaplay.Server.GameConfig;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK routes to work with localizations.
    /// </summary>
    [LocalizationsEnabledCondition]
    public class LocalizationController : GameAdminApiController
    {
        public enum LocalizationErrorType
        {
            /// <summary>
            /// An exception occurred
            /// </summary>
            Exception,

            /// <summary>
            /// An exception occurred that is already transformed to a string (likely from building, and stored in the database)
            /// </summary>
            StringException,

            /// <summary>
            /// The build task mysteriously disappeared, likely due to the server stopping or crashing.
            /// </summary>
            TaskDisappeared,

            /// <summary>
            /// The build messages or validation messages contain one or more messages that are treated as errors.
            /// </summary>
            BlockingMessages,
        }


        public class LocalizationErrorBase
        {
            [Required]
            public LocalizationErrorType ErrorType { get; init; }

            public LocalizationErrorBase(LocalizationErrorType errorType)
            {
                ErrorType = errorType;
            }
        }

        public class StringExceptionLocalizationError : LocalizationErrorBase
        {
            public string FullException { get; init; }

            public StringExceptionLocalizationError(string stringException): base(LocalizationErrorType.StringException)
            {
                FullException = stringException;
            }
        }


        /// <summary>
        /// Utility to figure out whether this config can be published and a relevant error message, all parameters are optional but at least one is required.
        /// </summary>
        static (GameDataControllerUtility.GameDataStatus status, List<LocalizationErrorBase> errors) GetPublishableStatusAndErrors(
            PersistedLocalizations persistedLocalizations = null,
            Dictionary<MetaGuid, BackgroundTaskStatus> statuses = null,
            int blockingMessageCount = 0)
        {
            List<LocalizationErrorBase> errors = new List<LocalizationErrorBase>();
            if (persistedLocalizations != null && statuses != null)
            {
                if (persistedLocalizations.TaskId != null && persistedLocalizations.TaskId != MetaGuid.None.ToString())
                {
                    if (statuses.TryGetValue(MetaGuid.Parse(persistedLocalizations.TaskId), out BackgroundTaskStatus taskStatus))
                    {
                        // Early exit if we're still building, no point in reporting errors if we expect to have an invalid state.
                        if (!taskStatus.Completed)
                            return (GameDataControllerUtility.GameDataStatus.Building, errors);

                        // Don't think this ever triggers because we have a try catch in the BackgroundTask.Run?
                        if (taskStatus.Failure != null)
                            errors.Add(new StringExceptionLocalizationError(taskStatus.Failure));
                    }
                    else
                        errors.Add(new LocalizationErrorBase(LocalizationErrorType.TaskDisappeared));
                }
            }

            if (!string.IsNullOrWhiteSpace(persistedLocalizations?.FailureInfo))
                errors.Add(new StringExceptionLocalizationError(persistedLocalizations?.FailureInfo));

            if (blockingMessageCount > 0)
                errors.Add(new LocalizationErrorBase(LocalizationErrorType.BlockingMessages));

            return (errors.Count > 0 ? GameDataControllerUtility.GameDataStatus.Failed : GameDataControllerUtility.GameDataStatus.Success, errors: errors);
        }

        public class MinimalLocalizationInfo
        {
            public MetaGuid Id { get; private set; }

            public string Name { get; private set; }

            public string Description { get; private set; }

            public string VersionHash { get; private set; }

            public DateTime LastModifiedAt { get; private set; }

            public string Source { get; private set; }

            public bool IsArchived { get; private set; } = false;

            public string TaskId { get; private set; }

            public GameDataControllerUtility.GameDataStatus BestEffortStatus { get; set; }

            public List<LocalizationErrorBase> PublishBlockingErrors { get; set; }

            public bool IsActive { get; set; }

            public MetaTime? PublishedAt   { get; protected init; }
            public MetaTime? UnpublishedAt { get; protected init; }

            public MetaTime BuildStartedAt { get; set; }

            public MinimalLocalizationInfo()
            {
            }

            public static MinimalLocalizationInfo FromPersisted(PersistedLocalizations locs, MetaGuid activeId, Dictionary<MetaGuid, BackgroundTaskStatus> statusAndError)
            {
                MetaGuid id = MetaGuid.Parse(locs.Id);
                (GameDataControllerUtility.GameDataStatus status, List<LocalizationErrorBase> errors) = GetPublishableStatusAndErrors(locs, statusAndError);
                return new MinimalLocalizationInfo()
                {
                    Id                    = id,
                    Name                  = locs.Name,
                    Description           = locs.Description,
                    VersionHash           = locs.VersionHash,
                    BuildStartedAt        = MetaTime.FromDateTime(locs.ArchiveBuiltAt != DateTime.MinValue ? locs.ArchiveBuiltAt : id.GetDateTime()),
                    LastModifiedAt        = locs.LastModifiedAt,
                    Source                = locs.Source,
                    IsArchived            = locs.IsArchived,
                    IsActive              = id == activeId,
                    TaskId                = locs.TaskId,
                    BestEffortStatus      = status,
                    PublishBlockingErrors = errors,
                    PublishedAt           = locs.PublishedAt != null ? MetaTime.FromDateTime(locs.PublishedAt.Value) : null,
                    UnpublishedAt         = locs.UnpublishedAt != null ? MetaTime.FromDateTime(locs.UnpublishedAt.Value) : null,
                };
            }
        }

        // <summary>
        // Get list of PersistedLocalizations entries in the database, without contents.
        // Usage:  GET /api/localizations
        // Test:   curl http://localhost:5550/api/localizations
        // </summary>
        [HttpGet("localizations")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationView)]
        public async Task<ActionResult<IEnumerable<MinimalLocalizationInfo>>> GetLocalizationList([FromQuery] bool showArchived = false)
        {
            GlobalStatusResponse                status  = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            MetaDatabase                        db      = MetaDatabase.Get(QueryPriority.Normal);
            IEnumerable<PersistedLocalizations> locs    = await db.QueryAllLocalizations(showArchived);

            // Retrieve build task statuses if needed
            Dictionary<MetaGuid, BackgroundTaskStatus> taskStatuses = null;
            if (locs.Any(config => config.TaskId != null))
            {
                BackgroundTaskStatusResponse taskStatusResponse = await EntityAskAsync<BackgroundTaskStatusResponse>(BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(nameof(BuildLocalizationsTask)));
                taskStatuses = taskStatusResponse.Tasks.ToDictionary(t => t.Id, t => t);
            }

            IEnumerable<MinimalLocalizationInfo> persisted = locs.Select(x => MinimalLocalizationInfo.FromPersisted(x, status.ActiveLocalizationsId, taskStatuses));
            return AsResult(persisted);
        }

        // <summary>
        // Get a PersistedLocalizations entry.
        // Usage:  GET /api/localization/03c44d2ee8f24bd-0-ef35666bc8a69033
        // Test:   curl http://localhost:5550/api/localization/03c44d2ee8f24bd-0-ef35666bc8a69033
        // </summary>
        [HttpGet("localization/{localizationId}")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationView)]
        public async Task<ActionResult<Dictionary<LanguageId, LocalizationLanguage>>> GetLocalization(string localizationId)
        {
            GlobalStatusResponse status = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);

            PersistedLocalizations persisted = await GameDataControllerUtility.GetPersistedGameDataByIdStringOr404Async<PersistedLocalizations>(localizationId, status.ActiveLocalizationsId);
            // Retrieve build task statuses if needed
            Dictionary<MetaGuid, BackgroundTaskStatus> taskStatuses = null;
            if (persisted.TaskId != null)
            {
                BackgroundTaskStatusResponse taskStatusResponse = await EntityAskAsync<BackgroundTaskStatusResponse>(
                    BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(nameof(BuildLocalizationsTask)));
                taskStatuses = taskStatusResponse.Tasks.ToDictionary(t => t.Id, t => t);
            }

            Dictionary<LanguageId, LocalizationLanguage> locs = new Dictionary<LanguageId, LocalizationLanguage>();
            if (persisted.ArchiveBytes != null)
            {
                ConfigArchive archive = ConfigArchive.FromBytes(persisted.ArchiveBytes);
                foreach (ConfigArchiveEntry entry in archive.Entries)
                {
                    LanguageId           languageId    = LanguageId.FromString(Path.GetFileNameWithoutExtension(entry.Name));
                    byte[]               languageBytes = entry.Bytes.ToArray();
                    LocalizationLanguage loc           = LocalizationLanguage.FromBytes(languageId, entry.Hash, languageBytes);
                    locs.Add(languageId, loc);
                }
            }

            return Ok(new {
                info = MinimalLocalizationInfo.FromPersisted(persisted, status.ActiveLocalizationsId, taskStatuses),
                locs = locs,
            });
        }

        // <summary>
        // Get a PersistedLocalizations entry, transposed to a table format with languages as columns.
        // Usage:  GET /api/localization/03c44d2ee8f24bd-0-ef35666bc8a69033
        // Test:   curl http://localhost:5550/api/localization/03c44d2ee8f24bd-0-ef35666bc8a69033
        // </summary>
        [HttpGet("localization/{localizationId}/table")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationView)]
        public async Task<ActionResult<IEnumerable<MinimalLocalizationInfo>>> GetLocalizationTable(string localizationId)
        {
            GlobalStatusResponse status = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);

            PersistedLocalizations persisted = await GameDataControllerUtility.GetPersistedGameDataByIdStringOr404Async<PersistedLocalizations>(localizationId, status.ActiveLocalizationsId);
            // Retrieve build task statuses if needed
            Dictionary<MetaGuid, BackgroundTaskStatus> taskStatuses = null;
            if (persisted.TaskId != null)
            {
                BackgroundTaskStatusResponse taskStatusResponse = await EntityAskAsync<BackgroundTaskStatusResponse>(
                     BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(nameof(BuildLocalizationsTask)));
                taskStatuses = taskStatusResponse.Tasks.ToDictionary(t => t.Id, t => t);
            }

            Dictionary<LanguageId, LocalizationLanguage>           locs        = new Dictionary<LanguageId, LocalizationLanguage>();
            List<GameConfigLibraryJsonConversionUtility.ConfigKey> table       = null;
            List<string>                                           languageIds = new List<string>();
            if (persisted.ArchiveBytes != null)
            {
                ConfigArchive archive     = ConfigArchive.FromBytes(persisted.ArchiveBytes);
                foreach (ConfigArchiveEntry entry in archive.Entries)
                {
                    LanguageId           languageId    = LanguageId.FromString(Path.GetFileNameWithoutExtension(entry.Name));
                    byte[]               languageBytes = entry.Bytes.ToArray();
                    LocalizationLanguage loc           = LocalizationLanguage.FromBytes(languageId, entry.Hash, languageBytes);
                    languageIds.Add(languageId.Value);
                    locs.Add(languageId, loc);
                }

                // Order english to front since it's the most important language for our users.
                int englishIndex = languageIds.FindIndex(
                    x => x.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                        x.Equals("english", StringComparison.OrdinalIgnoreCase) ||
                        x.Equals("eng", StringComparison.OrdinalIgnoreCase));
                if (englishIndex != -1)
                {
                    var englishKey = languageIds[englishIndex];
                    languageIds.RemoveAt(englishIndex);
                    languageIds.Insert(0, englishKey);
                }

                table = new List<GameConfigLibraryJsonConversionUtility.ConfigKey>();
                foreach ((TranslationId key, string _) in locs.FirstOrDefault().Value.Translations)
                {
                    Dictionary<string, object> values = new Dictionary<string, object>(locs.Count);
                    foreach ((LanguageId languageId, LocalizationLanguage localizationLanguage) in locs)
                    {
                        if (localizationLanguage.Translations.TryGetValue(key, out string translation))
                            values.Add(languageId.Value, translation);
                        else
                            values.Add(languageId.Value, null);
                    }

                    GameConfigLibraryJsonConversionUtility.ConfigKey configKey = new GameConfigLibraryJsonConversionUtility.ConfigKey(key.Value, null, values, "string");
                    table.Add(configKey);
                }
            }

            return Ok(new {
                info  = MinimalLocalizationInfo.FromPersisted(persisted, status.ActiveLocalizationsId, taskStatuses),
                table = table,
                languageIds,
            });
        }

        /// <summary>
        /// Audit log events
        /// </summary>
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LocalizationPublished)]
        public class LocalizationEventLocalizationPublished : LocalizationEventPayloadBase
        {
            public LocalizationEventLocalizationPublished() { }
            public override string EventTitle       => "Published";
            public override string EventDescription => $"Localizations published.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LocalizationUnpublished)]
        public class LocalizationEventLocalizationUnpublished : LocalizationEventPayloadBase
        {
            public LocalizationEventLocalizationUnpublished() { }
            public override string EventTitle       => "Unpublished";
            public override string EventDescription => $"Localizations unpublished.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LocalizationUploaded)]
        public class LocalizationEventLocalizationUploaded : LocalizationEventPayloadBase
        {
            public LocalizationEventLocalizationUploaded() { }
            public override string EventTitle       => "Uploaded";
            public override string EventDescription => $"Localizations uploaded.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LocalizationEdited)]
        public class LocalizationEventLocalizationEdited : LocalizationEventPayloadBase
        {
            [MetaMember(2)] public GameDataControllerUtility.GameDataEditableProperties OldValues { get; private set; }
            [MetaMember(3)] public GameDataControllerUtility.GameDataEditableProperties NewValues { get; private set; }

            public LocalizationEventLocalizationEdited() { }
            public LocalizationEventLocalizationEdited(GameDataControllerUtility.GameDataEditableProperties oldValues, GameDataControllerUtility.GameDataEditableProperties newValues)
            {
                OldValues = oldValues;
                NewValues = newValues;
            }
            public override string EventTitle       => "Edited";
            public override string EventDescription => NewValues.GetSummary("Localization", OldValues);
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LocalizationBuildStarted)]
        public class LocalizationEventLocalizationBuildStarted : LocalizationEventPayloadBase
        {
            [MetaMember(1)] public LocalizationsBuildInput Input { get; private set; }

            public LocalizationEventLocalizationBuildStarted() { }
            public LocalizationEventLocalizationBuildStarted(LocalizationsBuildInput input)
            {
                Input = input;
            }
            public override string EventTitle       => "Build started";
            public override string EventDescription => $"Localizations build from source '{Input.BuildParams.DefaultSource?.DisplayName ?? "unknown"}' started.";
        }


        // <summary>
        // API Endpoint to publish a game config build
        // Usage:  POST /api/localizations/publish
        // Test:   curl -X POST http://localhost:5550/api/localizations/publish
        // </summary>
        [HttpPost("localization/publish")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationEdit)]
        public async Task<IActionResult> PublishLocalization()
        {
            GameDataControllerUtility.GameDataIdInput   input       = await ParseBodyAsync<GameDataControllerUtility.GameDataIdInput>();
            List<EventBuilder>                          auditLogs   = await PublishLocalizations(input.Id);

            await WriteRelatedAuditLogEventsAsync(auditLogs);

            return Ok();
        }

        async Task<List<EventBuilder>> PublishLocalizations(MetaGuid id)
        {
            PublishLocalizationResponse response =
                await EntityAskAsync(GlobalStateManager.EntityId,
                    new PublishLocalizationRequest(id));

            if (response.Status == PublishLocalizationResponse.StatusCode.Refused)
                throw new MetaplayHttpException(400, "Cannot publish localization", response.ErrorMessage);

            MetaGuid oldActiveLocalizationId = response.PreviousId;

            List<EventBuilder> auditLogs = new List<EventBuilder>();

            if (oldActiveLocalizationId.IsValid)
                auditLogs.Add(new LocalizationEventBuilder(oldActiveLocalizationId, new LocalizationEventLocalizationUnpublished()));
            auditLogs.Add(new LocalizationEventBuilder(id, new LocalizationEventLocalizationPublished()));

            return auditLogs;
        }

        /// <summary>
        /// API Endpoint to get active localization id
        /// Usage:  GET /api/activeLocalizationId
        /// Test:   curl http://localhost:5550/api/activeLocalizationId
        /// </summary>
        [HttpGet("activeLocalizationId")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationView)]
        public async Task<IActionResult> GetActiveLocalizationId()
        {
            // Fetch active localization version from global state
            GlobalStatusResponse globalStatus = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            if (!globalStatus.ActiveLocalizationsId.IsValid)
                return NotFound();

            return Ok(globalStatus.ActiveLocalizationsId.ToString());
        }

        // <summary>
        // API Endpoint to archive a set of localizations
        // Usage:  POST /api/localization/archive
        // Body:   [ "03bfbc45bb8c57d-0-8cbdb5ae78e38f29", "03bfc6b9d7d929c-0-74f7f2a4ee00083e"]
        // Test:   curl --request POST --url http://localhost:5551/api/localization/archive --header 'Accept: application/json' --header 'Content-Type: application/json' --data '[ "03bfbc45bb8c57d-0-8cbdb5ae78e38f29", "03bfc6b9d7d929c-0-74f7f2a4ee00083e"]'
        // </summary>
        [HttpPost("localization/archive")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationEdit)]
        public async Task<IActionResult> BatchArchiveLocalizations()
        {
            string[] input = await ParseBodyAsync<string[]>();

            foreach (string guid in input)
                await ArchiveLocalization(ParseMetaGuidStr(guid));

            return Ok();
        }

        async Task ArchiveLocalization(MetaGuid guid)
        {
            var input = new GameDataControllerUtility.GameDataEditableProperties() {IsArchived = true};

            await UpdateLocalization(guid, input);
        }

        // <summary>
        // API Endpoint to edit the properties of a Localization.
        // Usage:  POST /api/localization/{configId}
        // Test:   curl -X POST http://localhost:5550/api/localization/696EC7A86DAFE630-281F450A9D56CA1C
        // </summary>
        [HttpPost("localization/{localizationIdStr}")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationEdit)]
        public async Task<IActionResult> UpdateLocalization(string localizationIdStr)
        {
            MetaGuid                                             localizationId = ParseMetaGuidStr(localizationIdStr);
            GameDataControllerUtility.GameDataEditableProperties input          = await ParseBodyAsync<GameDataControllerUtility.GameDataEditableProperties>();

            await UpdateLocalization(localizationId, input);
            return Ok();
        }

        async Task UpdateLocalization(MetaGuid localizationId, GameDataControllerUtility.GameDataEditableProperties input)
        {
            MetaDatabase           db        = MetaDatabase.Get(QueryPriority.Normal);
            PersistedLocalizations persisted = await db.TryGetAsync<PersistedLocalizations>(localizationId.ToString());
            if (persisted == null)
                throw new MetaplayHttpException(404, "Localization not found.", $"Cannot find localization with id {localizationId}.");

            if (!string.IsNullOrEmpty(input.Name) || !string.IsNullOrEmpty(input.Description) || input.IsArchived.HasValue)
            {
                CreateOrUpdateGameDataResponse response = await EntityAskAsync(
                    GlobalStateManager.EntityId,
                    new CreateOrUpdateLocalizationsRequest() {
                        Id          = localizationId,
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
                await WriteAuditLogEventAsync(new LocalizationEventBuilder(localizationId, new LocalizationEventLocalizationEdited(oldValues, newValues)));
            }
            else
            {
                throw new MetaplayHttpException(400, "No valid input.", "Must pass an editable property in the body request.");
            }
        }

        /// <summary>
        /// API endpoint to update localizations
        /// Usage:  POST /api/localizations
        /// Test: curl -H "Content-Type:application/octet-stream" --data-binary {FILENAME} http://localhost:5550/api/localizations
        /// </summary>
        [HttpPost("localizations")]
        [Consumes("application/octet-stream")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationEdit)]
        public async Task<IActionResult> UploadLocalizationsArchive(
            [FromQuery] bool setAsActive = true,
            [FromQuery] string name = null,
            [FromQuery] string description = null)
        {
            // Publish the update via GlobalStateManager
            byte[] bytes = await ReadBodyBytesAsync();

            // Update via GlobalStateManager
            CreateOrUpdateGameDataResponse uploadResponse = await EntityAskAsync(
                GlobalStateManager.EntityId,
                new CreateOrUpdateLocalizationsRequest()
                {
                    Name = name,
                    Description = description,
                    Content = bytes,
                    Source  = GetUserId()
                });

            MetaGuid configId = uploadResponse.Id;

            List<EventBuilder> auditLogs = new List<EventBuilder>()
            {
                new LocalizationEventBuilder(configId, new LocalizationEventLocalizationUploaded())
            };

            if (setAsActive)
            {
                try
                {
                    List<EventBuilder> publishLogs = await PublishLocalizations(configId);
                    auditLogs.AddRange(publishLogs);
                }
                catch
                {
                    // if publish doesn't succeed, roll back
                    await EntityAskAsync(GlobalStateManager.EntityId, new RemoveLocalizationsRequest() { Id = configId });
                    throw;
                }
            }

            await WriteRelatedAuditLogEventsAsync(auditLogs);

            return Ok(configId);
        }

        [MetaSerializable]
        public struct LocalizationsBuildInput
        {
            [MetaMember(1)] public LocalizationsBuildParameters                         BuildParams  { get; private set; }
            [MetaMember(2)] public GameDataControllerUtility.GameDataEditableProperties Properties   { get; private set; }
        }

        // <summary>
        // API Endpoint to delete all archived localizations
        // Usage:  DELETE /api/localization/696EC7A86DAFE630-281F450A9D56CA1C
        // Test:   curl --request DELETE --url http://localhost:5551/api/localization/696EC7A86DAFE630-281F450A9D56CA1C
        // </summary>
        /// <returns>The TaskId of the background task in charge of deleting the archived localizations</returns>
        [HttpDelete("localization/deleteArchived")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationDelete)]
        public async Task<IActionResult> DeleteAllArchived()
        {
            MetaGuid taskId = MetaGuid.New();

            DeleteAllArchivedLocalizationsBackgroundTask deleteAllArchivedBackgroundTask = new DeleteAllArchivedLocalizationsBackgroundTask();
            StartBackgroundTaskResponse response = await EntityAskAsync<StartBackgroundTaskResponse>(BackgroundTaskActor.EntityId, new StartBackgroundTaskRequest(taskId, deleteAllArchivedBackgroundTask));

            return Ok(response.Id);
        }

        // <summary>
        // API Endpoint to delete all archived localizations
        // Usage:  DELETE /api/localization/696EC7A86DAFE630-281F450A9D56CA1C
        // Test:   curl --request DELETE --url http://localhost:5551/api/localization/696EC7A86DAFE630-281F450A9D56CA1C
        // </summary>
        [HttpDelete("localization/{configIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationDelete)]
        public async Task<IActionResult> Delete(string configIdStr)
        {
            MetaGuid configId = ParseMetaGuidStr(configIdStr);
            await EntityAskAsync(GlobalStateManager.EntityId, new RemoveLocalizationsRequest() {Id = configId});

            return Ok();
        }

        // <summary>
        // API Endpoint to start a new localizations build
        // Usage:  POST /api/localization/build
        // Test:   curl -X POST http://localhost:5550/api/localization/build
        // </summary>
        [HttpPost("localization/build")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiLocalizationEdit)]
        public async Task<IActionResult> StartLocalizationsBuild()
        {
            LocalizationsBuildInput input = await ParseBodyAsync<LocalizationsBuildInput>();

            // Generate an id for build task
            MetaGuid taskId = MetaGuid.New();

            // Create an empty Localizations entry for this build
            MetaGuid configId = (await EntityAskAsync(
                GlobalStateManager.EntityId,
                new CreateOrUpdateLocalizationsRequest() {
                    Source      = GetUserId(),
                    Name        = input.Properties.Name,
                    Description = input.Properties.Description,
                    IsArchived  = input.Properties.IsArchived,
                    TaskId      = taskId
                })).Id;

            // Start the build task
            BuildLocalizationsTask buildTask = new BuildLocalizationsTask(configId, input.BuildParams);
            _ = await EntityAskAsync(BackgroundTaskActor.EntityId, new StartBackgroundTaskRequest(taskId, buildTask));

            await WriteAuditLogEventAsync(new LocalizationEventBuilder(configId, new LocalizationEventLocalizationBuildStarted(input)));

            return Ok(new { Id = configId });
        }
    }
}
