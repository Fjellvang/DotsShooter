// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if !METAPLAY_DISABLE_GUILDS

using Metaplay.Core;
using Metaplay.Core.Guild;
using Metaplay.Core.GuildDiscovery;
using Metaplay.Server.GuildDiscovery;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for requesting guild recommendations in the context of a certain player.
    /// </summary>
    [GuildsEnabledCondition]
    public class PlayerGuildRecommendationsController : GameAdminApiController
    {
        /// <summary>
        /// API endpoint to get guild recommendations
        /// Usage:  POST /api/players/{PLAYERID}/guildRecommendations
        /// Test:   curl http://localhost:5550/api/players/{PLAYERID}/guildRecommendations -X POST -d ""
        /// </summary>
        [HttpPost("players/{playerIdStr}/guildRecommendations")]
        [RequirePermission(MetaplayPermissions.ApiPlayersGuildTools)]
        public async Task<ActionResult<IEnumerable<GuildDiscoveryInfoBase>>> GuildRecommendations(string playerIdStr)
        {
            EntityId playerId = await ParsePlayerIdStrAndCheckForExistenceAsync(playerIdStr);

            // Get context from player
            var contextResponse = await EntityAskAsync(playerId, InternalGuildDiscoveryPlayerContextRequest.Instance);
            GuildDiscoveryPlayerContextBase context = contextResponse.Result;

            InternalGuildRecommendationResponse response = await EntityAskAsync(GuildRecommenderActorBase.EntityId, new InternalGuildRecommendationRequest(context));
            List<GuildDiscoveryInfoBase> guildInfos = response.GuildInfos;
            return Ok(guildInfos);
        }
    }
}

#endif
