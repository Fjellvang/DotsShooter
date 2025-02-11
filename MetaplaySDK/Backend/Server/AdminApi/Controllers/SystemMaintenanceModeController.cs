// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for stock Metaplay SDK routes to work with maintenance mode.
    /// </summary>
    public class SystemMaintenanceModeController : GameAdminApiController
    {
        /// <summary>
        /// Audit log events
        /// </summary>
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameServerMaintenanceModeScheduled)]
        public class GameServerEventMaintenanceModeScheduled : GameServerEventPayloadBase
        {
            [MetaMember(1)] public MetaTime StartAt { get; private set; }
            [MetaMember(2)] public int EstimatedDurationInMinutes { get; private set; }
            [MetaMember(3)] public bool EstimationIsValid { get; private set; }
            [MetaMember(4)] public List<ClientPlatform> PlatformExclusions { get; private set; }
            public GameServerEventMaintenanceModeScheduled() { }
            public GameServerEventMaintenanceModeScheduled(MetaTime startAt, int estimatedDurationInMinutes, bool estimationIsValid, List<ClientPlatform> platformExclusions)
            {
                StartAt = startAt;
                EstimatedDurationInMinutes = estimatedDurationInMinutes;
                EstimationIsValid = estimationIsValid;
                PlatformExclusions = platformExclusions;
            }
            override public string SubsystemName => "MaintenanceMode";
            override public string EventTitle => "Scheduled";
            override public string EventDescription
            {
                get
                {
                    if (PlatformExclusions == null || PlatformExclusions.Count == 0)
                        return $"Maintenance mode scheduled for {StartAt}.";
                    return $"Maintenance mode scheduled for {StartAt} excluding platforms ({string.Join(',', PlatformExclusions)}).";
                }
            }
        }
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameServerMaintenanceModeUnscheduled)]
        public class GameServerEventMaintenanceModeUnscheduled : GameServerEventPayloadBase
        {
            public GameServerEventMaintenanceModeUnscheduled() { }
            override public string SubsystemName => "MaintenanceMode";
            override public string EventTitle => "Unscheduled";
            override public string EventDescription => $"Maintenance mode unscheduled.";
        }

        /// <summary>
        /// API endpoint to set maintenance mode
        /// Usage:  PUT /api/maintenanceMode
        /// Test:   curl http://localhost:5550/api/maintenanceMode -X put -d '{"StartAt":"2021-01-01T14:30Z", "EstimatedDurationInMinutes":"5", "EstimationIsValid":true}'
        /// </summary>
        [HttpPut("maintenanceMode")]
        [RequirePermission(MetaplayPermissions.ApiSystemEditMaintenance)]
        public async Task PutMaintenanceMode([FromBody] ScheduledMaintenanceMode mode)
        {
            try
            {
                UpdateScheduledMaintenanceModeRequest request = new UpdateScheduledMaintenanceModeRequest(mode);
                _ = await EntityAskAsync(GlobalStateManager.EntityId, request);

                await WriteAuditLogEventAsync(new GameServerEventBuilder(new GameServerEventMaintenanceModeScheduled(mode.StartAt, mode.EstimatedDurationInMinutes, mode.EstimationIsValid, mode.PlatformExclusions)));
            }
            catch (Exception ex)
            {
                throw new MetaplayHttpException(HttpStatusCode.InternalServerError, "Failed to put Maintenance Mode", ex.Message);
            }
        }

        /// <summary>
        /// API endpoint to cancelmaintenance mode
        /// Usage:  DELETE /api/maintenanceMode
        /// Test:   curl http://localhost:5550/api/maintenanceMode -X DELETE
        /// </summary>
        [HttpDelete("maintenanceMode")]
        [RequirePermission(MetaplayPermissions.ApiSystemEditMaintenance)]
        public async Task DeleteMaintenanceMode()
        {
            UpdateScheduledMaintenanceModeRequest request = new UpdateScheduledMaintenanceModeRequest(mode: null);
            _ = await EntityAskAsync(GlobalStateManager.EntityId, request);

            // Audit log event
            await WriteAuditLogEventAsync(new GameServerEventBuilder(new GameServerEventMaintenanceModeUnscheduled()));
        }
    }
}
