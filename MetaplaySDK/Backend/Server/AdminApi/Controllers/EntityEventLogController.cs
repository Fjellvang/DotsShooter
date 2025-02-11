// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.EventLog;
using Metaplay.Core.Math;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Server.EventLog;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Net;
using System;
using static System.FormattableString;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

#if !METAPLAY_DISABLE_GUILDS
using Metaplay.Core.Guild;
#endif

namespace Metaplay.Server.AdminApi.Controllers
{
    public class PlayerEventLogController : EntityEventLogControllerBase
    {
        /// <summary>
        /// API endpoint to scan entries from a player's event log.
        /// See <see cref="EntityEventLogControllerBase.RequestEventLogAsync"/> for more info.
        ///
        /// <![CDATA[
        /// Usage:  GET /api/players/{PLAYERID}/eventLog
        /// Test:   curl http://localhost:5550/api/players/{PLAYERID}/eventLog?startCursor=STARTCURSOR&numEntries=NUMENTRIES&scanDirection=towardsNewer
        /// ]]>
        /// </summary>
        [HttpGet("players/{ownerIdStr}/eventLog")]
        [RequirePermission(MetaplayPermissions.ApiPlayersView)]
        public async Task<ActionResult<EntityEventLogResult<PlayerEventLogEntry>>> GetFromPlayer(string ownerIdStr, [FromQuery] string startCursor, [FromQuery] int numEntries, [FromQuery] EntityEventLogScanDirection scanDirection = EntityEventLogScanDirection.TowardsNewer, [FromQuery(Name = "startTime")] string startTimeStr = null, [FromQuery(Name = "endTime")] string endTimeStr = null)
        {
            PlayerDetails details = await GetPlayerDetailsAsync(ownerIdStr);
            return await RequestEventLogAsync<PlayerEventLogEntry>(details.PlayerId, startCursor, numEntries, scanDirection, startTimeStr, endTimeStr);
        }
    }

    #if !METAPLAY_DISABLE_GUILDS
    [GuildsEnabledCondition]
    public class GuildEventLogController : EntityEventLogControllerBase
    {
        /// <summary>
        /// API endpoint to scan entries from a guild's event log.
        /// See <see cref="EntityEventLogControllerBase.RequestEventLogAsync"/> for more info.
        ///
        /// <![CDATA[
        /// Usage:  GET /api/guilds/{GUILDID}/eventLog
        /// Test:   curl http://localhost:5550/api/guilds/{GUILDID}/eventLog?startCursor=STARTCURSOR&numEntries=NUMENTRIES&scanDirection=towardsNewer
        /// ]]>
        /// </summary>
        [HttpGet("guilds/{ownerIdStr}/eventLog")]
        [RequirePermission(MetaplayPermissions.ApiGuildsView)]
        public async Task<ActionResult<EntityEventLogResult<GuildEventLogEntry>>> GetFromGuild(string ownerIdStr, [FromQuery] string startCursor, [FromQuery] int numEntries, [FromQuery] EntityEventLogScanDirection scanDirection = EntityEventLogScanDirection.TowardsNewer, [FromQuery(Name = "startTime")] string startTimeStr = null, [FromQuery(Name = "endTime")] string endTimeStr = null)
        {
            IGuildModelBase guild = (IGuildModelBase)await GetEntityStateAsync(ParseEntityIdStr(ownerIdStr, EntityKindCore.Guild));
            return await RequestEventLogAsync<GuildEventLogEntry>(guild.GuildId, startCursor, numEntries, scanDirection, startTimeStr, endTimeStr);
        }
    }
    #endif

    /// <summary>
    /// Controller for stock Metaplay SDK routes to work with an entity's event log.
    /// </summary>
    public abstract class EntityEventLogControllerBase : GameAdminApiController
    {
        /// <summary>
        /// Note: this should be at most <see cref="MetaSerializationContext.DefaultMaxCollectionSize"/>,
        /// because <see cref="EntityEventLogScanResponse{TEntry}"/> contains the entries in a single collection.
        /// </summary>
        public const int MaxNumEntriesPerRequest = 10_000;

        public class EntityEventLogResult<TEntry> where TEntry : MetaEventLogEntry
        {
            public JArray       Entries            { get; set; }
            public string       ContinuationCursor { get; set; }
            public string       StartCursor        { get; set; }

            public bool         FailedWithDesync   { get; set; }
            public string       DesyncDescription  { get; set; }
        }

        /// <summary>
        /// Scan entries from an entity's event log.
        /// <para>
        /// The scan is cursor-based, such that each scan returns (besides the
        /// scanned items) a continuation cursor which can be used as the start
        /// cursor of a subsequent scan.
        /// Scanning supports both backwards (towards older) and forwards (towards newer) scan directions.
        /// Backwards scanning uses exclusive start index and forwards scanning uses inclusive,
        /// so that when starting both at the same start cursor, the results are non-overlapping.
        /// </para>
        /// <para>
        /// The request can fail if the event log has been rolled back since the cursor was acquired
        /// and the log's current state no longer matches the cursor. In that case, <see cref="EntityEventLogResult{TEntry}.FailedWithDesync"/>
        /// is true, <see cref="EntityEventLogResult{TEntry}.DesyncDescription"/> is a description
        /// of the error, and the other returned properties should be ignored.
        /// In case of a desync, the simplest course of action is to restart the scan.
        /// </para>
        /// <para>
        /// <paramref name="startCursorStr"/> is a string representation of a
        /// <see cref="EntityEventLogCursor"/>, one of the following: <br/>
        ///  - special value <c>$newest</c>, to start the scan from the most-recent end of the event log. <br/>
        ///  - special value <c>$oldest</c>, to start the scan from the least-recent event. <br/>
        ///  - concrete cursor returned in <see cref="EntityEventLogResult{TEntry}.ContinuationCursor"/>
        ///    or <see cref="EntityEventLogResult{TEntry}.StartCursor"/> by a previous request.
        ///    (Syntax: segment id and entry index separated by underscore, as encoded by <see cref="EventLogCursorToString"/>). <br/>
        /// </para>
        /// <para>
        /// If the start cursor points to a log entry earlier than the earliest
        /// still available entry, the scan starts at the earliest available
        /// entry instead.
        /// </para>
        /// <para>
        /// The query may return fewer entries than requested if the log is exhausted.
        /// </para>
        /// <para>
        /// To start incrementally scanning both backwards and forwards at the same time,
        /// starting from the latest event, you can do as follows: <br/>
        ///  1. Scan once backwards from cursor <c>$newest</c>. Set <c>backwardsCursor := result.ContinuationCursor</c>. Set <c>forwardsCursor := result.StartCursor</c>. <br/>
        ///  2. Start independent backwards and forwards scans: <br/>
        ///    - Start repeatedly scanning backwards from cursor <c>backwardsCursor</c>, each time updating <c>backwardsCursor</c> to the <c>ContinuationCursor</c> returned by the backwards scan. <br/>
        ///    - Start repeatedly scanning forwards from cursor <c>forwardsCursor</c>, each time updating <c>forwardsCursor</c> to the <c>ContinuationCursor</c> returned by the forwards scan. <br/>
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// <paramref name="numEntries"/> can be at most <see cref="MaxNumEntriesPerRequest"/>; providing a higher number results in an error.
        /// To get a larger number of entries, make multiple successive requests, using the cursors.
        /// </para>
        /// <para>
        /// <paramref name="startTimeStr"/> and <paramref name="endTimeStr"/> are optional,
        /// and the SDK's built-in features do not employ them. They are supported in the following
        /// restricted manner, for filtering the results to the specified time range (the
        /// start time is inclusive, end time is exclusive):<br/>
        /// - Make an initial scan request with start cursor $oldest, direction TowardsNewer,
        ///   and the desired time range.
        ///   The SDK will start the scan from the start of the time range, rather than
        ///   from the actual least-recent event.<br/>
        /// - Make any continuation scans (from the returned ContinuationCursor) with the same
        ///   time range.<br/>
        /// Note in particular the following restrictions when using the time filter.<br/>
        /// - Time filtering is not currently supported for the TowardsOlder (i.e. backwards) direction.<br/>
        /// - Doing the initial scan with a start cursor other than $oldest is not supported;
        ///   doing so will have unspecified results.<br/>
        /// - Using a different time range for the continuation scans than the initial scan is not supported;
        ///   doing so will have unspecified results.<br/>
        /// </para>
        /// </remarks>
        protected async Task<ActionResult<EntityEventLogResult<TEntry>>> RequestEventLogAsync<TEntry>(
            EntityId ownerId,
            string startCursorStr,
            int numEntries,
            EntityEventLogScanDirection scanDirection,
            string startTimeStr,
            string endTimeStr
            ) where TEntry : MetaEventLogEntry
        {
            MetaTime? startTime = string.IsNullOrEmpty(startTimeStr) ? null : MetaTime.Parse(startTimeStr);
            MetaTime? endTime = string.IsNullOrEmpty(endTimeStr) ? null : MetaTime.Parse(endTimeStr);

            if (numEntries < 0)
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Requested number of entries cannot be negative.", $"The requested number of entries {numEntries} is negative, which is not allowed..");
            else if (numEntries > MaxNumEntriesPerRequest)
                throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Requested number of entries is too large.", $"The requested number of entries {numEntries} exceeds the maximum {MaxNumEntriesPerRequest}.");

            EntityEventLogCursor               startCursor = ParseEventLogCursor(startCursorStr);
            EntityEventLogScanResponse<TEntry> logResponse = await EntityAskAsync<EntityEventLogScanResponse<TEntry>>(ownerId, new EntityEventLogScanRequest(startCursor, numEntries, scanDirection, startTime, endTime));

            if (logResponse.FailedWithDesync)
            {
                return new EntityEventLogResult<TEntry>
                {
                    FailedWithDesync = true,
                    DesyncDescription = logResponse.DesyncDescription,
                };
            }

            // Need config resolver to deserialize the event log.
            ActiveGameConfig        activeGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get();
            ISharedGameConfig       sharedGameConfig = activeGameConfig.BaselineGameConfig.SharedConfig;
            IGameConfigDataResolver resolver         = sharedGameConfig;

            List<TEntry> entries = logResponse.Entries.Deserialize(resolver, logicVersion: null);

            return new EntityEventLogResult<TEntry>
            {
                Entries            = JArray.FromObject(entries, JsonSerializer.Create(AdminApiJsonSerialization.EventLogSerializationSettings)),
                ContinuationCursor = EventLogCursorToString(logResponse.ContinuationCursor),
                StartCursor        = EventLogCursorToString(logResponse.StartCursor),
            };
        }

        protected static string EventLogCursorToString(EntityEventLogCursorDirect cursor)
        {
            string result = Invariant($"{cursor.SegmentId}_{cursor.EntryIndexWithinSegment}");
            if (cursor.PreviousEntryUniqueId.HasValue)
            {
                MetaUInt128 uniqueId = cursor.PreviousEntryUniqueId.Value;
                result += $"_{uniqueId.High:X16}-{uniqueId.Low:X16}";
            }
            return result;
        }

        protected static EntityEventLogCursor ParseEventLogCursor(string cursorStr)
        {
            if (cursorStr == "$newest")
                return new EntityEventLogCursorOnePastNewest();
            if (cursorStr == "$oldest")
                return new EntityEventLogCursorOldest();

            // Either of the following is accepted (see also EventLogCursorToString):
            //  {SEGMENT}_{ENTRY}_{UNIQUEID}
            //  {SEGMENT}_{ENTRY}
            // That is, the UNIQUEID part is optional.

            string[] parts = cursorStr.Split("_");
            if (parts.Length != 2 && parts.Length != 3)
                throw new FormatException("Expected either '$newest', '$oldest', or two or three underscore-separated parts");

            int segmentId = int.Parse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture);
            int entryIndexWithinSegment = int.Parse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture);

            MetaUInt128? previousEntryUniqueId;

            if (parts.Length == 3)
            {
                string[] entryIdParts = parts[2].Split("-");
                if (entryIdParts.Length != 2)
                    throw new FormatException("Unique-id part must consist of two hyphen-separated parts");

                previousEntryUniqueId = new MetaUInt128(
                    ulong.Parse(entryIdParts[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
                    ulong.Parse(entryIdParts[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
            }
            else
                previousEntryUniqueId = null;

            return new EntityEventLogCursorDirect(
                segmentId: segmentId,
                entryIndexWithinSegment: entryIndexWithinSegment,
                previousEntryUniqueId: previousEntryUniqueId);
        }
    }
}
