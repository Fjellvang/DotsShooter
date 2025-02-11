// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    public class GameTimeController : GameAdminApiController
    {
        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameServerGameTimeOffsetChanged)]
        public class GameServerGameTimeOffsetChanged : GameServerEventPayloadBase
        {
            [MetaMember(1)] public MetaDuration Offset { get; private set; }

            GameServerGameTimeOffsetChanged() { }
            public GameServerGameTimeOffsetChanged(MetaDuration offset)
            {
                Offset = offset;
            }
            override public string SubsystemName => "GameTime";
            override public string EventTitle => "Offset changed";
            override public string EventDescription => $"Game time offset changed to {Offset}";
        }

        public class GameTimeOffsetSettings
        {
            // \note Using plain milliseconds number instead of MetaDuration or TimeSpan for now,
            //       because dashboard doesn't seem to have a utility readily available for
            //       producing the duration format desired by the server.
            public required long OffsetMilliseconds { get; init; }
        }

        [HttpPut("gameTimeOffset")]
        [RequirePermission(MetaplayPermissions.ApiGameTimeOffsetEdit)]
        public async Task PutGameTimeOffset([FromBody] GameTimeOffsetSettings settings)
        {
            MetaDuration offset = MetaDuration.FromMilliseconds(settings.OffsetMilliseconds);
            SetGameTimeOffsetResponse response = await EntityAskAsync(GlobalStateManager.EntityId, new SetGameTimeOffsetRequest(offset));
            if (!response.IsSuccess)
                throw new MetaplayHttpException((int)HttpStatusCode.BadRequest, "Failed to update game time offset.", response.Error);

            await WriteAuditLogEventAsync(new GameServerEventBuilder(new GameServerGameTimeOffsetChanged(offset)));
        }
    }
}
