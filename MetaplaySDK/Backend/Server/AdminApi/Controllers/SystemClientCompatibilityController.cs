// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK route to update client compatibility settings.
    /// </summary>
    public class SystemClientCompatibilityController : GameAdminApiController
    {
        /// <summary>
        /// Audit log events
        /// </summary>
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameServerClientCompatibilitySettingsUpdated)]
        public class GameServerEventClientCompatibilitySettingsUpdated : GameServerEventPayloadBase
        {
            [MetaMember(1)] public ClientCompatibilitySettings ClientCompatibilitySettings { get; private set; }
            public GameServerEventClientCompatibilitySettingsUpdated() { }
            public GameServerEventClientCompatibilitySettingsUpdated(ClientCompatibilitySettings clientCompatibilitySettings)
            {
                ClientCompatibilitySettings = clientCompatibilitySettings;
            }
            override public string SubsystemName => "ClientCompatibility";
            override public string EventTitle => "Updated";
            override public string EventDescription => "Client compatibility settings updated.";
        }


        /// <summary>
        /// API endpoint to update client compatability settings
        /// Usage:  POST /api/clientCompatibilitySettings
        /// Test:   curl http://localhost:5550/api/clientCompatibilitySettings -X POST -d ""
        /// </summary>
        [HttpPost("clientCompatibilitySettings")]
        [RequirePermission(MetaplayPermissions.ApiSystemEditLogicVersioning)]
        public async Task UpdateClientCompatibilitySettings([FromBody] ClientCompatibilitySettings compatibilitySettings)
        {
            UpdateClientCompatibilitySettingsRequest request = new UpdateClientCompatibilitySettingsRequest(compatibilitySettings);
            UpdateClientCompatibilitySettingsResponse response = await EntityAskAsync(GlobalStateManager.EntityId, request);

            if (response.IsSuccess)
                await WriteAuditLogEventAsync(new GameServerEventBuilder(new GameServerEventClientCompatibilitySettingsUpdated(compatibilitySettings)));
            else
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Failed to update ClientCompatibilitySettings", response.ErrorMessage);
        }
    }
}
