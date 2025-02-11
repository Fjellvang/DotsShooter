// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK route to toggle player session debug mode.
    /// </summary>
    public class PlayerToggleSessionDebugModeController : GameAdminApiController
    {
        /// <summary>
        /// Audit log events
        /// </summary>
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.PlayerToggleSessionDebugMode)]
        public class PlayerToggleSessionDebugModeEvent : PlayerEventPayloadBase
        {
            [MetaMember(1)] public bool Enabled { get; private set; }
            [MetaMember(2)] public int ForNextNumSessions { get; private set; }
            [MetaMember(3)] public PlayerSessionDebugModeParameters Parameters { get; private set; }

            private PlayerToggleSessionDebugModeEvent() { }
            public PlayerToggleSessionDebugModeEvent(bool enabled, int forNextNumSessions, PlayerSessionDebugModeParameters parameters)
            {
                Enabled = enabled;
                ForNextNumSessions = forNextNumSessions;
                Parameters = parameters;
            }

            public override string EventTitle => Enabled ?
                "Player session debug mode enabled" :
                "Player session debug mode disabled";

            public override string EventDescription
            {
                get
                {
                    if (Enabled)
                    {
                        if (Parameters == null)
                        {
                            // Legacy event with only the Enabled member
                            return "Debug mode enabled";
                        }
                        else
                        {
                            return Invariant($"Debug mode enabled for the next {ForNextNumSessions} sessions: {PrettyPrint.Compact(Parameters)}");
                        }
                    }
                    else
                        return "Debug mode disabled";
                }
            }
        }

        /// <summary>
        /// API endpoint to toggle player session debug mode
        /// </summary>
        [HttpPost("players/{playerIdStr}/sessionDebugMode")]
        [RequirePermission(MetaplayPermissions.ApiPlayersToggleDebugMode)]
        public async Task ToggleSessionDebugMode(string playerIdStr, [FromQuery, BindRequired] bool enabled, [FromBody] SessionDebugModeRequestBody body)
        {
            if (!ModelState.IsValid)
                throw new ArgumentException($"Parameter {nameof(enabled)} is required.");

            EntityId playerId = await ParsePlayerIdStrAndCheckForExistenceAsync(playerIdStr);
            _logger.LogInformation("Setting player {0} session debug mode: enabled={1}, forNextNumSessions={2}, parameters={3}", playerId, enabled, body.ForNextNumSessions, PrettyPrint.Compact(body.Parameters));

            await EntityAskAsync(playerId, new InternalPlayerToggleSessionDebugMode(enabled, body.ForNextNumSessions, body.Parameters));
            await WriteAuditLogEventAsync(new PlayerEventBuilder(playerId, new PlayerToggleSessionDebugModeEvent(enabled, body.ForNextNumSessions, body.Parameters)));
        }

        public class SessionDebugModeRequestBody
        {
            [JsonRequired] public int ForNextNumSessions;
            [JsonRequired] public PlayerSessionDebugModeParameters Parameters;
        }
    }
}
