// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK routes to change an individual player's logic version.
    /// </summary>
    public class PlayerUpdateLogicVersionController : GameAdminApiController
    {
        /// <summary>
        /// Audit log events
        /// </summary>
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.PlayerLogicVersionUpdated)]
        public class PlayerEventLogicVersionChanged : PlayerEventPayloadBase
        {
            [MetaMember(1)] public int OldLogicVersion { get; private set; }
            [MetaMember(2)] public int NewLogicVersion { get; private set; }

            [MetaDeserializationConstructor]
            public PlayerEventLogicVersionChanged(int oldLogicVersion, int newLogicVersion)
            {
                OldLogicVersion = oldLogicVersion;
                NewLogicVersion = newLogicVersion;
            }

            override public string EventTitle => "Logic Version changed";
            override public string EventDescription => Invariant($"Player's logic version changed from {OldLogicVersion} to {NewLogicVersion}.");
        }

        /// <summary>
        /// HTTP request body to change a player's logic version
        /// </summary>
        public class PlayerUpdateLogicVersionBody
        {
            public int NewLogicVersion { get; private set; }
        }

        /// <summary>
        /// API endpoint to update the player's logic version
        /// Usage:  POST /api/players/{PLAYERID}/updateLogicVersion
        /// Test:   curl http://localhost:5550/api/players/{PLAYERID}/updateLogicVersion -X POST -H "Content-Type:application/json" -d '{"NewLogicVersion":8}'
        /// </summary>
        [HttpPost("players/{playerIdStr}/updateLogicVersion")]
        [Consumes("application/json")]
        [RequirePermission(MetaplayPermissions.ApiPlayersUpdateLogicVersion)]
        public async Task<ActionResult> UpdateLogicVersion(string playerIdStr, [FromBody] PlayerUpdateLogicVersionBody body)
        {
            // Resolve the player Id
            EntityId playerId = await ParsePlayerIdStrAndCheckForExistenceAsync(playerIdStr);

            ActiveClientCompatibilitySettings compatibilitySettings = GlobalStateProxyActor.ActiveClientCompatibilitySettings.Get();

            MetaVersionRange logicVersionRange = compatibilitySettings.ClientCompatibilitySettings.ActiveLogicVersionRange;

            if (logicVersionRange.MinVersion > body.NewLogicVersion || logicVersionRange.MaxVersion < body.NewLogicVersion)
            {
                return new BadRequestResult();
            }

            PlayerUpdateLogicVersionResponse response = await EntityAskAsync<PlayerUpdateLogicVersionResponse>(playerId, new PlayerUpdateLogicVersionRequest(body.NewLogicVersion));
            await WriteAuditLogEventAsync(new PlayerEventBuilder(playerId, new PlayerEventLogicVersionChanged(response.OldLogicVersion, response.NewLogicVersion)));

            return new OkResult();
        }
    }
}
