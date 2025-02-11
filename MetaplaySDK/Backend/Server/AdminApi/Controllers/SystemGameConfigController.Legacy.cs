// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Json;
using Metaplay.Server.GameConfig;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    public partial class SystemGameConfigController
    {
        public class StaticGameConfigInfo : GameConfigInfoBase
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public FullGameConfig Contents { get; private init; }

            public static StaticGameConfigInfo FromPersisted(PersistedStaticGameConfig persisted, MetaGuid activeId, Dictionary<MetaGuid, BackgroundTaskStatus> statuses = null)
            {
                FullGameConfig             contents         = null;
                GameConfigImportExceptions importExceptions = default;
                try
                {
                    if (persisted.ArchiveBytes != null)
                        contents = FullGameConfig.CreatePartial(ConfigArchive.FromBytes(persisted.ArchiveBytes), false, null).config;
                    else if (persisted.MetaDataBytes != null)
                        contents = FullGameConfig.MetaDataOnly(GameConfigMetaData.FromBytes(persisted.MetaDataBytes));
                }
                catch (Exception e)
                {
                    importExceptions = new GameConfigImportExceptions(null, new[] {e});
                }

                int blockingGameConfigMessageCount = contents?.MetaData?.BuildSummary == null ? 0 : CalculateBlockingGameConfigMessages(contents.MetaData);
                (GameDataControllerUtility.GameDataStatus status, List<GameConfigErrorBase> errors) = GetPublishableStatusAndErrors(importExceptions, persisted.FailureInfo, persisted, statuses, blockingGameConfigMessageCount);

                MetaGuid id = MetaGuid.Parse(persisted.Id);
                return new StaticGameConfigInfo()
                {
                    Id                             = id,
                    Name                           = persisted.Name,
                    Description                    = persisted.Description,
                    FullConfigVersion              = persisted.VersionHash,
                    CdnVersion                     = GetCdnVersionForActiveConfig(id, activeId),
                    BuildStartedAt                 = MetaTime.FromDateTime(persisted.ArchiveBuiltAt != DateTime.MinValue ? persisted.ArchiveBuiltAt : id.GetDateTime()),
                    LastModifiedAt                 = MetaTime.FromDateTime(persisted.LastModifiedAt),
                    Source                         = persisted.Source,
                    Contents                       = contents,
                    IsActive                       = id == activeId,
                    IsArchived                     = persisted.IsArchived,
                    Status                         = status,
                    BlockingGameConfigMessageCount = blockingGameConfigMessageCount,
                    PublishBlockingErrors          = errors,
                    PublishedAt                    = persisted.PublishedAt != null ? MetaTime.FromDateTime(persisted.PublishedAt.Value) : null,
                    UnpublishedAt                  = persisted.UnpublishedAt != null ? MetaTime.FromDateTime(persisted.UnpublishedAt.Value) : null
                };
            }
        }

        // <summary>
        // API Endpoint to get a specific version of a GameConfig, including contents.
        // Usage:  GET /api/gameConfig/{configVersion}
        // Test:   curl http://localhost:5550/api/gameConfig/696EC7A86DAFE630-281F450A9D56CA1C
        // </summary>
        [HttpGet("gameConfig/{configIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiGameConfigView)]
        public async Task<IActionResult> GetStaticGameConfig(string configIdStr, [FromQuery] bool binary = false)
        {
            GlobalStatusResponse      status    = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
            PersistedStaticGameConfig persisted = await GameDataControllerUtility.GetPersistedGameDataByIdStringOr404Async<PersistedStaticGameConfig>(configIdStr, status.ActiveStaticGameConfigId);
            if (binary)
            {
                HttpContext.Response.Headers["GameConfig-Id"] = persisted.Id;
                MemoryStream stream = new MemoryStream(persisted.ArchiveBytes);
                return new FileStreamResult(stream, "application/octet-stream");
            }
            else
            {
                Dictionary<MetaGuid, BackgroundTaskStatus> taskStatuses = null;
                if (persisted.TaskId != null)
                {
                    BackgroundTaskStatusResponse taskStatusResponse = await EntityAskAsync<BackgroundTaskStatusResponse>(BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(nameof(BuildStaticGameConfigTask)));
                    taskStatuses = taskStatusResponse.Tasks.ToDictionary(t => t.Id, t => t);
                }

                return Json(StaticGameConfigInfo.FromPersisted(persisted, status.ActiveStaticGameConfigId, taskStatuses), AdminApiJsonSerialization.ExcludeReadOnlyPropertiesSerializationSettings);
            }
        }
    }
}
