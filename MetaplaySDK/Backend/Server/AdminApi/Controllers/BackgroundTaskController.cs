// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers;

public class BackgroundTaskController : GameAdminApiController
{
    /// <summary>
    /// API Endpoint to get the status of a background task
    /// Usage:  GET /api/backgroundTasks?className=BuildLocalizationsTask
    /// Usage:  GET /api/backgroundTasks?taskId=03d832d9cabc5b3-0-062cdc5dbcdd8681
    /// Test:   curl http://localhost:5550/api/backgroundTasks?className=BuildLocalizationsTask
    /// </summary>
    /// <returns>Returns a list of task status if a <paramref name="className"/> was passed, or a single task status in case a <paramref name="taskIdStr"/> was passed.</returns>
    [HttpGet("backgroundTasks")]
    [RequirePermission(MetaplayPermissions.ApiBroadcastView)]
    public async Task<ActionResult> GetBackgroundTaskStatusFromClassName(string className, [FromQuery(Name = "taskId")] string taskIdStr)
    {
        if (!string.IsNullOrEmpty(className))
        {
            BackgroundTaskStatusResponse response = await EntityAskAsync<BackgroundTaskStatusResponse>(BackgroundTaskActor.EntityId, new BackgroundTaskStatusByClassNameRequest(className));
            return Ok(response.Tasks);
        }
        else if (!string.IsNullOrEmpty(taskIdStr))
        {
            MetaGuid                     taskId   = ParseMetaGuidStr(taskIdStr);
            BackgroundTaskStatusResponse response = await EntityAskAsync<BackgroundTaskStatusResponse>(BackgroundTaskActor.EntityId, new BackgroundTaskStatusByIdRequest(taskId));

            return Ok(response.Tasks.FirstOrDefault());
        }

        return BadRequest();
    }
}
