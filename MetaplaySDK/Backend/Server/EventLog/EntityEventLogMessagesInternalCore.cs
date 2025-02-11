// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.EventLog;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System.Collections.Generic;

namespace Metaplay.Server.EventLog
{
    [MetaMessage(MessageCodesCore.TriggerEntityEventLogFlushing, MessageDirection.ServerInternal)]
    public class TriggerEventLogFlushing : MetaMessage
    {
    }

    /// <summary>
    /// Request the entity to scan a number of entries from its event log.
    /// The response is of (a subclass of) type <see cref="EntityEventLogScanResponse{TEntry}"/>
    /// <para>
    /// This scan API is cursor-based; besides the entries, the response
    /// includes a cursor which can be used to continue the scan from where
    /// the previous scan ended (assuming the relevant entries haven't been
    /// deleted in the meantime).
    /// </para>
    /// <para>
    /// Both backwards and forwards scanning are supported, specified by <see cref="ScanDirection"/>.
    /// When using <see cref="EntityEventLogScanDirection.TowardsNewer"/>,
    /// the start cursor is inclusive; when using <see cref="EntityEventLogScanDirection.TowardsOlder"/>,
    /// the start cursor is exclusive. This way, starting both a backwards and forwards scan
    /// from the same cursor will produce non-overlapping results.
    /// </para>
    /// <para>
    /// In addition to the continuation cursor, the scan response also contains
    /// the concrete start cursor value, useful in case the request's <see cref="StartCursor"/>
    /// was the symbolic <see cref="EntityEventLogCursorOnePastNewest"/>. The purpose is
    /// to support the use case of starting both a backwards and forwards scan from the
    /// same cursor, by first issuing one scan request from the symbolic start cursor,
    /// and then using the returned continuation cursor to start scanning in one direction,
    /// and the returned concrete start cursor to start scanning in the other direction.
    /// </para>
    /// <para>
    /// The scan request can fail due to no fault on the requester's part,
    /// in case an event log rollback happened (due to actor crashing before persisting).
    /// This is detected based on <see cref="EntityEventLogCursorDirect.PreviousEntryUniqueId"/>.
    /// In that case, <see cref="EntityEventLogScanResponse{TEntry}.FailedWithDesync"/> will be true.
    /// </para>
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityEventLogScanRequest, MessageDirection.ServerInternal)]
    public class EntityEventLogScanRequest : MetaMessage
    {
        /// <summary>
        /// The scan starts at this cursor and moves either forwards or
        /// backwards as indicated by <see cref="ScanDirection"/>.
        /// For backwards scanning, the start cursor is exclusive: the first event
        /// returned is the next older event from the start cursor.
        ///
        /// If this cursor indicates an entry older than the oldest still
        /// available entry, the scan starts at the oldest available entry
        /// instead.
        /// </summary>
        public EntityEventLogCursor StartCursor { get; private set; }
        /// <summary>
        /// Maximum number of entries to return. The amount of entries returned
        /// can be lower if the end of the event log is reached.
        /// </summary>
        public int                  NumEntries  { get; private set; }
        /// <summary>
        /// Controls whether to scan forwards or backwards.
        /// </summary>
        public EntityEventLogScanDirection ScanDirection { get; private set; }

        /// <summary>
        /// <see cref="StartTime"/> and <see cref="EndTime"/> can be optionally
        /// used for time range filtering of the results. They are not currently
        /// employed by the stock SDK itself but are implemented to support custom queries.
        /// Their usage has certain restrictions; for details, see remarks on
        /// <see cref="AdminApi.Controllers.EntityEventLogControllerBase.RequestEventLogAsync"/>.
        /// </summary>
        public MetaTime? StartTime { get; private set; }
        /// <inheritdoc cref="StartTime"/>
        public MetaTime? EndTime { get; private set; }

        EntityEventLogScanRequest() { }
        public EntityEventLogScanRequest(EntityEventLogCursor startCursor, int numEntries, EntityEventLogScanDirection scanDirection, MetaTime? startTime, MetaTime? endTime)
        {
            StartCursor = startCursor;
            NumEntries = numEntries;
            ScanDirection = scanDirection;
            StartTime = startTime;
            EndTime = endTime;
        }
    }

    [MetaSerializable]
    public enum EntityEventLogScanDirection
    {
        /// <summary>
        /// Scan "forwards", i.e. from a given start cursor towards newer events.
        /// </summary>
        TowardsNewer,
        /// <summary>
        /// Scan "backwards", i.e. from a given start cursor towards older events.
        /// For backwards scanning, the start cursor is exclusive: the first event
        /// returned is the next older event from the start cursor.
        /// </summary>
        TowardsOlder,
    }

    /// <summary>
    /// Response to <see cref="EntityEventLogScanRequest"/>.
    /// </summary>
    [MetaImplicitMembersRange(100, 200)]
    public abstract class EntityEventLogScanResponse<TEntry> : MetaMessage
        where TEntry : MetaEventLogEntry
    {
        /// <summary>
        /// Event log entries returned by the scan
        /// (or null if <see cref="FailedWithDesync"/> is true).
        /// These can be fewer than requested if the current end of the log was reached.
        ///
        /// \note MetaSerialized, because event log entries can contain arbitrary stuff, including game config references.
        /// </summary>
        public MetaSerialized<List<TEntry>> Entries             { get; set; }

        /// <summary>
        /// A cursor that can be used to perform a scan starting from where this scan ended,
        /// (or null if <see cref="FailedWithDesync"/> is true).
        /// </summary>
        public EntityEventLogCursorDirect   ContinuationCursor  { get; set; }
        /// <summary>
        /// Concrete values of the start cursor that was used for the scan
        /// (or null if <see cref="FailedWithDesync"/> is true).
        /// When the scan request specified a "symbolic" start cursor (in <see cref="EntityEventLogScanRequest.StartCursor"/>),
        /// you can use this to know what the actual concrete cursor was.
        /// </summary>
        public EntityEventLogCursorDirect   StartCursor         { get; set; }

        /// <summary>
        /// Whether the scan failed due to a mismatch between the cursor in the request
        /// and the actual event log state. See <see cref="EntityEventLogCursorDirect.PreviousEntryUniqueId"/>
        /// for more information.
        /// <para>
        /// If this is true, <see cref="DesyncDescription"/> contains a human-readable
        /// description of the reason.
        /// </para>
        /// </summary>
        public bool                         FailedWithDesync    { get; set; }
        public string                       DesyncDescription   { get; set; }
    }
}
