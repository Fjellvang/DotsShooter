// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Cloud.Analytics;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.Config;
using Metaplay.Core.EventLog;
using Metaplay.Core.IO;
using Metaplay.Core.Math;
using Metaplay.Core.Memory;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.EventLog
{
    public class EntityEventLogOptionsAttribute : RuntimeOptionsAttribute
    {
        public EntityEventLogOptionsAttribute(string entityTypeName)
            : base(
                  sectionName: $"EventLogFor{entityTypeName}",
                  isStatic: false,
                  sectionDescription: $"Configuration options for the storage of `{entityTypeName}` entity event logs.")
        {
        }
    }

    public abstract class EntityEventLogOptionsBase : RuntimeOptionsBase
    {
        /// <summary>
        /// Desired retention duration for persisted events. A
        /// persisted segment becomes eligible for deletion when all
        /// of its entries are older than this, assuming there are
        /// more than <see cref="MinPersistedSegmentsToRetain"/>
        /// persisted segments for the entity. Also, a persisted
        /// segment may be deleted even if its entries are younger
        /// than this, if there are more than <see cref="MaxPersistedSegmentsToRetain"/>
        /// persisted segments for the entity.
        /// </summary>
        /// <remarks>
        /// A segment is not guaranteed to be deleted immediately as
        /// soon as it reaches this age. Segment deletion is only
        /// considered when performing certain event log operations
        /// (namely, the flushing of new segments to the database).
        /// </remarks>
        [MetaDescription("The minimum amount of time that the event logs are persisted before automatic deletion.")]
        public TimeSpan RetentionDuration { get; protected set; } = TimeSpan.FromDays(2 * 7);

        /// <summary>
        /// Desired number of entries to put in each of the separately-
        /// persisted segments. At least
        /// NumEntriesPerPersistedSegment*MinPersistedSegmentsToRetain
        /// entries will be kept available.
        /// </summary>
        /// <remarks>
        /// Existing segments should not be assumed to have any specific
        /// number of entries, as this configuration can be changed.
        /// </remarks>
        [MetaDescription("The number of event logs to persist in each segment.")]
        public int NumEntriesPerPersistedSegment   { get; protected set; } = 50;

        /// <summary>
        /// Minimum number of persisted segments to retain per entity.
        /// At least this many segments are retained, even if older
        /// than <see cref="RetentionDuration"/>.
        /// </summary>
        [MetaDescription($"The minimum number of segments to retain per entity.")]
        public int MinPersistedSegmentsToRetain { get; protected set; } = 20;

        /// <summary>
        /// Maximum number of persisted segments to retain per entity.
        /// At most this many segments are retained, even if younger
        /// than <see cref="RetentionDuration"/>. Oldest segments are
        /// deleted first.
        /// </summary>
        [MetaDescription($"The maximum number of segments to retain per entity.")]
        public int MaxPersistedSegmentsToRetain { get; protected set; } = 1000;

        /// <summary>
        /// Maximum number of persisted segments to remove in one
        /// removal pass. This limit exists to avoid latency spikes in
        /// the entity actor: for example, if a player returns to the
        /// game after a long time, there might be lots of old segments
        /// to remove, but we don't want to remove all at once. Instead,
        /// at most this many segments will be removed each time a new
        /// segment is flushed to the database.
        /// </summary>
        /// <remarks>
        /// This must be at least 2 to guarantee that segment removal
        /// will eventually catch up. Segment removal is done only when
        /// a new segment is flushed to the database, so if this is 2,
        /// each flush-and-remove step will decrease the number of
        /// persisted segments by at most 1.
        /// </remarks>
        [MetaDescription("The maximum number of segments to remove from the database in one removal pass.")]
        public int MaxPersistedSegmentsToRemoveAtOnce { get; protected set; } = 5;

        [MetaDescription("Whether to include the analytics context values for each event in the event log.")]
        public bool IncludeAnalyticsContext { get; private set; } = false;

        [MetaDescription("Whether to include the analytics label values for each event in the event log.")]
        public bool IncludeAnalyticsLabels { get; private set; } = false;

        public override Task OnLoadedAsync()
        {
            if (RetentionDuration < TimeSpan.Zero)
                throw new InvalidOperationException($"{nameof(RetentionDuration)} must be non-negative (is {RetentionDuration})");

            if (NumEntriesPerPersistedSegment <= 0)
                throw new InvalidOperationException($"{nameof(NumEntriesPerPersistedSegment)} must be positive (is {NumEntriesPerPersistedSegment})");

            if (MinPersistedSegmentsToRetain < 0)
                throw new InvalidOperationException($"{nameof(MinPersistedSegmentsToRetain)} must be non-negative (is {MinPersistedSegmentsToRetain})");
            if (MaxPersistedSegmentsToRetain < 0)
                throw new InvalidOperationException($"{nameof(MaxPersistedSegmentsToRetain)} must be non-negative (is {MaxPersistedSegmentsToRetain})");
            if (MinPersistedSegmentsToRetain > MaxPersistedSegmentsToRetain)
                throw new InvalidOperationException($"{nameof(MinPersistedSegmentsToRetain)} must be less than or equal to {nameof(MaxPersistedSegmentsToRetain)} ({MinPersistedSegmentsToRetain} vs {MaxPersistedSegmentsToRetain})");

            if (MaxPersistedSegmentsToRemoveAtOnce < 2)
                throw new InvalidOperationException($"{nameof(MaxPersistedSegmentsToRemoveAtOnce)} must at least 2 (is {MaxPersistedSegmentsToRemoveAtOnce})");

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Persisted payload for an event log segment.
    /// </summary>
    [MetaSerializable]
    public abstract class MetaEventLogSegmentPayload<TEntry> where TEntry : MetaEventLogEntry
    {
        [MetaMember(1)] public List<TEntry> Entries;
        [MetaMember(2)] public MetaUInt128 PreviousSegmentLastEntryUniqueId;
    }

    public static class PersistedEntityEventLogSegmentUtil
    {
        public static string CreateGlobalId(EntityId ownerId, int segmentSequentialId)  => Invariant($"{ownerId}/{segmentSequentialId}");
        public static string GetPartitionKey(EntityId ownerId)                          => ownerId.ToString();
    }

    /// <summary>
    /// A cursor for addressing an event log entry, used when scanning the event log.
    /// A cursor can be a concrete <see cref="EntityEventLogCursorDirect"/>,
    /// but a special symbolic cursor <see cref="EntityEventLogCursorOnePastNewest"/> exists
    /// for the purpose of starting an event request from whatever is the latest
    /// event at that time. This special cursor exists so that the caller does not
    /// need to beforehand know the concrete segment and entry numbers for the latest
    /// event.
    /// The <see cref="EntityEventLogScanResponse{TEntry}.ContinuationCursor"/> returned
    /// by an event request is always a concrete <see cref="EntityEventLogCursorDirect"/>.
    /// </summary>
    [MetaSerializable]
    public abstract class EntityEventLogCursor
    {
    }

    /// <summary>
    /// A cursor for addressing an event log entry, used when scanning the event log.
    /// This is represented in a manner that allows directly addressing the relevant
    /// database-stored segment, without needing to do persistent external bookkeeping
    /// about which segment contains which entry ids.
    /// </summary>
    [MetaSerializableDerived(1)]
    public class EntityEventLogCursorDirect : EntityEventLogCursor
    {
        /// <summary>
        /// The segment into which this cursor points.
        /// </summary>
        [MetaMember(1)] public int SegmentId                { get; private set; }
        /// <summary>
        /// The position within the segment this cursor points to.
        /// This index can be equal to the number of entries in the segment,
        /// which should be understood as pointing to the end of the segment.
        /// </summary>
        [MetaMember(2)] public int EntryIndexWithinSegment  { get; private set; }
        /// <summary>
        /// The <see cref="MetaEventLogEntry.UniqueId"/> of the entry preceding
        /// (i.e. older than) the entry pointed to by this cursor (or 0 if this
        /// cursor points at the first entry). This is for detecting desyncs
        /// in case the event log rolled back due to the producing actor crashing
        /// without having persisted all of its event log data, and the requester
        /// has a cursor from before the rollback.
        /// </summary>
        /// <remarks>
        /// This can be null, in which case the desync detection based on <see cref="MetaEventLogEntry.UniqueId"/>
        /// is disabled (but "cursor too advanced" detection is still enabled).
        /// Desync detection is currently always enabled in stock SDK usage,
        /// but disabling it may be useful for implementing ad hoc access patterns
        /// other than linear scanning (e.g. binary search).
        /// </remarks>
        [MetaMember(3)] public MetaUInt128? PreviousEntryUniqueId { get; private set; }

        EntityEventLogCursorDirect() { }
        public EntityEventLogCursorDirect(int segmentId, int entryIndexWithinSegment, MetaUInt128? previousEntryUniqueId)
        {
            SegmentId = segmentId;
            EntryIndexWithinSegment = entryIndexWithinSegment;
            PreviousEntryUniqueId = previousEntryUniqueId;
        }
    }

    /// <summary>
    /// A cursor pointing at one past the newest event in the event log.
    /// <para>
    /// It is *one past* rather than exactly the newest, because this way,
    /// starting a backwards (newest-to-oldest) scan from this cursor will return
    /// the N latest events (backwards scanning uses exclusive start cursor).
    /// Starting a forwards scan from this cursor will return 0 events
    /// (and a continuation cursor that can then be used for live-tailing
    /// future events).
    /// </para>
    /// </summary>
    [MetaSerializableDerived(2)]
    public class EntityEventLogCursorOnePastNewest : EntityEventLogCursor
    {
    }

    /// <summary>
    /// A cursor identifying the oldest event of the event log.
    /// </summary>
    [MetaSerializableDerived(3)]
    public class EntityEventLogCursorOldest : EntityEventLogCursor
    {
    }

    [Index(nameof(OwnerId))]
    public abstract class PersistedEntityEventLogSegment : IPersistedItem
    {
        /// <summary> Id that contains both the owner entity id and the segment sequential id. </summary>
        [Key]
        [Required]
        [MaxLength(128)]
        [Column(TypeName = "varchar(128)")]
        public string   GlobalId            { get; set; }

        /// <summary> Id of the entity that owns the event log. </summary>
        /// <remarks> This is also used as the partition key, so segments get stored on the same shard as the entity itself. </remarks>
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string   OwnerId             { get; set; }

        [Required]
        public int      SegmentSequentialId { get; set; }

        /// <summary>
        /// Serialized <see cref="MetaEventLogSegmentPayload{TEntry}"/>-derived object,
        /// serialized via its concrete static type.
        /// Furthermore, the serialized blob is compressed (and header-prefixed)
        /// with <see cref="CompressPayload"/>, except in old non-compressed
        /// legacy items. See also <see cref="UncompressPayload"/>.
        /// </summary>
        [Required]
        public byte[]   Payload             { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime FirstEntryTimestamp { get; set; } // \note based on CollectedAt

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime LastEntryTimestamp  { get; set; } // \note based on CollectedAt

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime CreatedAt           { get; set; }

        const byte PayloadHeaderMagicPrefixByte = 255; // Arbitrary byte different from (byte)WireDataType.NullableStruct

        public static byte[] CompressPayload(byte[] uncompressed)
        {
            // Compress, and add header prefix.

            using (FlatIOBuffer buffer = new FlatIOBuffer())
            {
                using (IOWriter writer = new IOWriter(buffer))
                {
                    writer.WriteByte(PayloadHeaderMagicPrefixByte);

                    writer.WriteVarInt(1); // schema version
                    writer.WriteVarInt((int)CompressionAlgorithm.Deflate);

                    byte[] compressed = CompressUtil.DeflateCompress(uncompressed);
                    writer.WriteBytes(compressed, 0, compressed.Length);
                }

                return buffer.ToArray();
            }
        }

        public byte[] UncompressPayload()
        {
            // Check header and decompress (unless header is missing because it's legacy).

            if (Payload[0] == (byte)WireDataType.NullableStruct)
            {
                // Payload is from before schema version was added. Payload is the plain serialized data.
                return Payload;
            }
            else if (Payload[0] == PayloadHeaderMagicPrefixByte)
            {
                using (IOReader reader = new IOReader(Payload, offset: 1, size: Payload.Length - 1))
                {
                    int schemaVersion = reader.ReadVarInt();
                    if (schemaVersion == 1)
                    {
                        CompressionAlgorithm compressionAlgorithm = (CompressionAlgorithm)reader.ReadVarInt();

                        // \todo [nuutti] Support offset in CompressUtil.Decompress to avoid ad hoc handling here?
                        if (compressionAlgorithm == CompressionAlgorithm.Deflate)
                            return CompressUtil.DeflateDecompress(Payload, offset: reader.Offset);
                        else
                            throw new InvalidOperationException($"Invalid compression algorithm {compressionAlgorithm}");
                    }
                    else
                        throw new InvalidOperationException($"Invalid schema version: {schemaVersion}");
                }
            }
            else
                throw new InvalidOperationException($"Expected {nameof(Payload)} to start with either byte {(byte)WireDataType.NullableStruct} ({nameof(WireDataType)}.{nameof(WireDataType.NullableStruct)}) or {PayloadHeaderMagicPrefixByte}, but it starts with {Payload[0]}");
        }
    }

    /// <summary>
    /// Interface to the persistent storage for event logs;
    /// normally backed by database with <see cref="EventLogStorageMetaDatabase"/>, but in unit tests a fake one is used.
    /// </summary>
    public interface IEventLogStorage
    {
        Task InsertOrUpdateSegmentAsync<TSegment>(TSegment segment) where TSegment : PersistedEntityEventLogSegment;
        Task<DateTime?> TryGetSegmentLastEntryTimestamp<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment;
        Task<bool> RemoveSegmentAsync<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment;
        Task RemoveAllEventLogSegmentsOfEntityAsync<TSegment>(EntityId ownerId) where TSegment : PersistedEntityEventLogSegment;
        Task<TSegment> TryGetSegmentAsync<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment;
        /// <summary>
        /// Returns the earliest segment belonging to <paramref name="ownerId"/> that contains an entry
        /// whose <see cref="MetaEventLogEntry.CollectedAt"/> is greater than or equal to
        /// <paramref name="startTime"/>, or null if no such segment is found.
        /// Additionally, only segments with a segment id between <paramref name="segmentIdsStart"/> (inclusive)
        /// and <paramref name="segmentIdsEnd"/> (exclusive) are considered.
        /// </summary>
        Task<TSegment> TryFindTimeRangeStartSegmentAsync<TSegment>(EntityId ownerId, MetaTime startTime, int segmentIdsStart, int segmentIdsEnd) where TSegment : PersistedEntityEventLogSegment;
    }

    public class EventLogStorageMetaDatabase : IEventLogStorage
    {
        public static readonly EventLogStorageMetaDatabase Instance = new EventLogStorageMetaDatabase();

        public Task InsertOrUpdateSegmentAsync<TSegment>(TSegment segment) where TSegment : PersistedEntityEventLogSegment
            => MetaDatabase.Get().InsertOrUpdateAsync(segment);

        public Task<DateTime?> TryGetSegmentLastEntryTimestamp<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment
            => MetaDatabase.Get().TryGetEventLogSegmentLastEntryTimestamp<TSegment>(primaryKey, partitionKey);

        public Task<bool> RemoveSegmentAsync<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment
            => MetaDatabase.Get().RemoveAsync<TSegment>(primaryKey, partitionKey);

        public Task RemoveAllEventLogSegmentsOfEntityAsync<TSegment>(EntityId ownerId) where TSegment : PersistedEntityEventLogSegment
            => MetaDatabase.Get(QueryPriority.Low).RemoveAllEventLogSegmentsOfEntityAsync<TSegment>(ownerId);

        public Task<TSegment> TryGetSegmentAsync<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment
            => MetaDatabase.Get().TryGetAsync<TSegment>(primaryKey, partitionKey);

        public Task<TSegment> TryFindTimeRangeStartSegmentAsync<TSegment>(EntityId ownerId, MetaTime startTime, int segmentIdsStart, int segmentIdsEnd) where TSegment : PersistedEntityEventLogSegment
            => MetaDatabase.Get().TryFindEventLogTimeRangeStartSegmentAsync<TSegment>(ownerId, startTime, segmentIdsStart: segmentIdsStart, segmentIdsEnd: segmentIdsEnd);
    }

    public static class EntityEventLogUtil<TEntry, TSegmentPayload, TPersistedSegment, TScanResponse>
        where TEntry : MetaEventLogEntry
        where TSegmentPayload : MetaEventLogSegmentPayload<TEntry>, new()
        where TPersistedSegment : PersistedEntityEventLogSegment, new()
        where TScanResponse : EntityEventLogScanResponse<TEntry>, new()
    {
        public static void AddEntry(MetaEventLog<TEntry> eventLog, EntityEventLogOptionsBase config, MetaTime collectedAt, MetaUInt128 uniqueId, Func<MetaEventLogEntry.BaseParams, TEntry> createEntry)
        {
            TEntry entry = createEntry(new MetaEventLogEntry.BaseParams(eventLog.RunningEntryId, collectedAt, uniqueId));

            // Add entry.
            MetaDebug.Assert(entry.SequentialId == eventLog.RunningEntryId, "SequentialId not set properly");
            eventLog.LatestSegmentEntries.Add(entry);
            eventLog.RunningEntryId++;

            // If configured limit is reached, move LatestSegmentEntries to PendingSegments.
            // It'll get flushed to database later.
            //
            // Note that *all* the entries of LatestSegmentEntries are copied into one
            // segment, instead of taking exactly NumEntriesPerPersistedSegment entries
            // (it might mismatch in case the NumEntriesPerPersistedSegment config was
            // changed).
            // This is so that existing `EntityEventLogCursorDirect`s remain valid: an
            // existing cursor might point into what was earlier in LatestSegmentEntries,
            // and in order for the cursor addressing to remain correct, the number of
            // entries per segment must not decrease (which it would if only a part of
            // the entries in LatestSegmentEntries were taken into a segment).
            if (eventLog.LatestSegmentEntries.Count >= config.NumEntriesPerPersistedSegment)
            {
                List<TEntry> entriesCopy = new List<TEntry>(eventLog.LatestSegmentEntries);
                eventLog.PendingSegments.Add(new MetaEventLog<TEntry>.PendingSegment(entriesCopy, eventLog.PreviousSegmentLastEntryUniqueId));
                eventLog.LatestSegmentEntries.Clear();
                eventLog.RunningSegmentId++;
                eventLog.PreviousSegmentLastEntryUniqueId = uniqueId;
            }
        }

        /// <summary>
        /// Whether the log contains segments waiting to be flushed to the database.
        /// </summary>
        public static bool CanFlush(MetaEventLog<TEntry> eventLog, EntityEventLogOptionsBase config)
        {
            return eventLog.PendingSegments.Count > 0;
        }

        /// <summary>
        /// Helper that combines flushing of a segment from the event log,
        /// and removal of old segments according to the retention limits in <paramref name="config"/>.
        /// You'll likely want to call this when <see cref="CanFlush"/> returns true.
        /// </summary>
        public static async Task TryFlushAndCullSegmentsAsync(MetaEventLog<TEntry> eventLog, EntityEventLogOptionsBase config, EntityId ownerId, int logicVersion, IMetaLogger log, Func<Task> persistModelAsync, IEventLogStorage storage = null, MetaTime? currentTime = null)
        {
            await TryFlushSegmentsAsync(eventLog, config, ownerId, logicVersion, log, storage, currentTime);
            await TryRemoveOldSegmentsAsync(eventLog, config, ownerId, log, persistModelAsync, storage, currentTime);
        }

        /// <summary>
        /// Flush pending segments (if any) from the model's eventLog into separate database items.
        /// </summary>
        public static async Task TryFlushSegmentsAsync(MetaEventLog<TEntry> eventLog, EntityEventLogOptionsBase config, EntityId ownerId, int logicVersion, IMetaLogger log, IEventLogStorage storage = null, MetaTime? currentTimeParam = null)
        {
            storage ??= EventLogStorageMetaDatabase.Instance;
            MetaTime currentTime = currentTimeParam ?? MetaTime.Now;

            while (eventLog.PendingSegments.Count > 0)
            {
                // Pop the oldest pending segment, and persist it.

                MetaEventLog<TEntry>.PendingSegment segment = eventLog.PendingSegments[0];
                int             segmentSequentialId = eventLog.RunningSegmentId - eventLog.PendingSegments.Count;
                eventLog.PendingSegments.RemoveAt(0);

                await PersistSegmentAsync(segmentSequentialId, segment.Entries, segment.PreviousSegmentLastEntryUniqueId, ownerId, logicVersion, log, storage, currentTime).ConfigureAwait(false);
            }
        }

        async static Task PersistSegmentAsync(int segmentSequentialId, List<TEntry> segmentEntries, MetaUInt128 previousSegmentLastEntryUniqueId, EntityId ownerId, int logicVersion, IMetaLogger log, IEventLogStorage storage, MetaTime currentTime)
        {
            TSegmentPayload payload = new TSegmentPayload()
            {
                Entries = segmentEntries,
                PreviousSegmentLastEntryUniqueId = previousSegmentLastEntryUniqueId,
            };

            TPersistedSegment persistedSegment = new TPersistedSegment
            {
                GlobalId            = PersistedEntityEventLogSegmentUtil.CreateGlobalId(ownerId, segmentSequentialId),
                OwnerId             = ownerId.ToString(),
                SegmentSequentialId = segmentSequentialId,
                Payload             = PersistedEntityEventLogSegment.CompressPayload(MetaSerialization.SerializeTagged(payload, MetaSerializationFlags.Persisted | MetaSerializationFlags.EntityEventLog, logicVersion)),
                FirstEntryTimestamp = segmentEntries.First().CollectedAt.ToDateTime(),
                LastEntryTimestamp  = segmentEntries.Last().CollectedAt.ToDateTime(),
                CreatedAt           = currentTime.ToDateTime(),
            };

            // Upsert.
            //
            // A segment item with the same id may exist, in case a previous incarnation of the entity crashed before persisting.
            // In that case, the existing segment is irrelevant (as the entity model has authority), and shall be overwritten.
            //
            // Also for this reason, the entity model does not need to be persisted right away.

            await storage.InsertOrUpdateSegmentAsync(persistedSegment).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove old persisted segments if allowed by the retention
        /// limits in <paramref name="config"/>
        /// </summary>
        /// <param name="persistModelAsync">Persist the model that contains eventLog.</param>
        public static async Task TryRemoveOldSegmentsAsync(MetaEventLog<TEntry> eventLog, EntityEventLogOptionsBase config, EntityId ownerId, IMetaLogger log, Func<Task> persistModelAsync, IEventLogStorage storage = null, MetaTime? currentTimeParam = null)
        {
            storage ??= EventLogStorageMetaDatabase.Instance;
            MetaTime currentTime = currentTimeParam ?? MetaTime.Now;

            MetaTime retentionCutoffTime = currentTime - MetaDuration.FromTimeSpan(config.RetentionDuration);

            // Figure out how many segments we can remove while respecting
            // the retention limits set by `config`.
            //
            // This next loop does not modify eventLog, and does not remove
            // the persisted segments; that is done after this loop. This loop
            // only increments numSegmentsToRemove to an appropriate value.

            int numSegmentsToRemove = 0;
            while (true)
            {
                // numSegmentsToRemove currently holds a "safe" value, i.e. a number of
                // segments that we could remove while still respecting the configured
                // retention limits; though not yet necessarily the largest such number.
                // Now, we set canRemoveOneMoreSegment to whether numSegmentsToRemove
                // can be incremented and still have it respect the configured limits.

                bool canRemoveOneMoreSegment;

                if (numSegmentsToRemove >= config.MaxPersistedSegmentsToRemoveAtOnce)
                {
                    // Reached the limit of how many should be removed at once.
                    // Cannot remove more.
                    canRemoveOneMoreSegment = false;
                }
                else if (eventLog.NumAvailablePersistedSegments - numSegmentsToRemove <= config.MinPersistedSegmentsToRetain)
                {
                    // The tentative number of remaining segments after removal
                    // is already at or below minimum.
                    // Cannot remove more.
                    canRemoveOneMoreSegment = false;
                }
                else if (eventLog.NumAvailablePersistedSegments - numSegmentsToRemove > config.MaxPersistedSegmentsToRetain)
                {
                    // The tentative number of remaining segments after removal
                    // is *above* (but not exactly at) maximum.
                    // *Can* remove at least one more.
                    canRemoveOneMoreSegment = true;
                }
                else
                {
                    // Check time-based retention limit based on LastEntryTimestamp of the next segment.

                    // speculatedRemoveSegmentId is the id of the segment that would get removed
                    // if we were to increment numSegmentsToRemove, but would not get removed with
                    // the current value of numSegmentsToRemove.
                    int     speculatedRemoveSegmentId           = eventLog.OldestAvailableSegmentId + numSegmentsToRemove;
                    string  speculatedRemoveSegmentGlobalId     = PersistedEntityEventLogSegmentUtil.CreateGlobalId(ownerId, speculatedRemoveSegmentId);

                    // From database, get just the LastEntryTimestamp of the segment.
                    // In particular, omit the Payload.
                    DateTime? segmentLastEntryTimestamp =
                        await storage.TryGetSegmentLastEntryTimestamp<TPersistedSegment>(
                            primaryKey:     speculatedRemoveSegmentGlobalId,
                            partitionKey:   PersistedEntityEventLogSegmentUtil.GetPartitionKey(ownerId));

                    if (segmentLastEntryTimestamp.HasValue)
                    {
                        // According to time-based retention, allow removing the segment if
                        // its last (i.e. youngest) entry is older than the retention duration.
                        canRemoveOneMoreSegment = MetaTime.FromDateTime(segmentLastEntryTimestamp.Value) < retentionCutoffTime;
                    }
                    else
                    {
                        // Segment is missing from database? Shouldn't happen. But if it does, allow "removing" it.
                        log.Warning($"Segment {{PersistedSegmentId}} not found when trying to get its {nameof(PersistedEntityEventLogSegment.LastEntryTimestamp)}, this shouldn't happen", speculatedRemoveSegmentGlobalId);
                        canRemoveOneMoreSegment = true;
                    }
                }

                // Either we can still keep going,
                // or we've found the maximum appropriate value for numSegmentsToRemove.

                if (canRemoveOneMoreSegment)
                    numSegmentsToRemove++;
                else
                    break;
            }

            // Omit the rest if we're not gonna remove anything.
            // In particular, don't do persistModelAsync().
            if (numSegmentsToRemove == 0)
                return;

            // Perform the segment removal based on the numSegmentsToRemove we just determined.

            int removeSegmentIdsStart   = eventLog.OldestAvailableSegmentId;
            int removeSegmentIdsEnd     = eventLog.OldestAvailableSegmentId + numSegmentsToRemove;

            // Update eventLog's bookkeeping to reflect the removal of the segments.
            eventLog.OldestAvailableSegmentId = removeSegmentIdsEnd;

            // Persist the model.
            // Model is persisted before the actual removal of the persisted segments,
            // in order to ensure that the model's bookkeeping never indicates segments
            // that no longer exist.
            await persistModelAsync();

            // Finally, remove the actual persisted segments.
            for (int segmentSequentialId = removeSegmentIdsStart; segmentSequentialId < removeSegmentIdsEnd; segmentSequentialId++)
            {
                string persistedSegmentId = PersistedEntityEventLogSegmentUtil.CreateGlobalId(ownerId, segmentSequentialId);
                bool removeOk = await storage.RemoveSegmentAsync<TPersistedSegment>(
                    primaryKey:     persistedSegmentId,
                    partitionKey:   PersistedEntityEventLogSegmentUtil.GetPartitionKey(ownerId)
                    ).ConfigureAwait(false);

                // Tolerate failure of removal, even though it shouldn't happen.
                if (!removeOk)
                    log.Warning("Removal of {TPersistedSegment} failed, this shouldn't happen; id={PersistedSegmentId}", typeof(TPersistedSegment).Name, persistedSegmentId);
            }
        }

        /// <summary>
        /// Remove all segments associated with the given entity, regardless of whether
        /// eventLog's bookkeeping reflects their existence.
        /// This is to be used when deleting an entity.
        /// </summary>
        /// <param name="persistModelAsync">Persist the model that contains eventLog.</param>
        public static async Task RemoveAllSegmentsAsync(MetaEventLog<TEntry> eventLog, EntityId ownerId, Func<Task> persistModelAsync, IEventLogStorage storage = null)
        {
            storage ??= EventLogStorageMetaDatabase.Instance;

            if (eventLog.NumAvailablePersistedSegments > 0)
            {
                // Update eventLog's bookkeeping to reflect the removal of the segments.
                eventLog.ForgetAllPersistedSegments();

                // Persist the model.
                // Model is persisted before the actual removal of the persisted segments,
                // in order to ensure that the model's bookkeeping never indicates segments
                // that no longer exist.
                await persistModelAsync();
            }

            // Finally, remove all owned segments from the database.
            // This is done regardless of whether the eventLog's bookkeeping
            // acknowledged the segments; persisted segments can exist without
            // corresponding bookkeeping, since the entity model (which is where
            // eventLog exists) and the segments are not persisted atomically
            // with each other.
            await storage.RemoveAllEventLogSegmentsOfEntityAsync<TPersistedSegment>(ownerId);
        }

        /// <summary>
        /// Scan log entries according to given <see cref="EntityEventLogScanRequest"/>.
        /// </summary>
        public static async Task<TScanResponse> ScanEntriesAsync(MetaEventLog<TEntry> eventLog, EntityId ownerId, IGameConfigDataResolver gameConfigResolver, int logicVersion, EntityEventLogScanRequest request, IEventLogStorage storage = null)
        {
            storage ??= EventLogStorageMetaDatabase.Instance;

            if ((request.StartTime.HasValue || request.EndTime.HasValue) && request.ScanDirection != EntityEventLogScanDirection.TowardsNewer)
                throw new NotImplementedException($"Start and/or end time filtering is currently only implemented for scan direction {EntityEventLogScanDirection.TowardsNewer} (got {request.ScanDirection})");

            // Resolve concrete start cursor, based on request.StartCursor (which can be a symbolic EntityEventLogCursorOnePastNewest or EntityEventLogCursorOldest, or concrete EntityEventLogCursorDirect).

            int startCursorSegmentId;
            int startCursorEntryIndexWithinSegment;

            // The EntityEventLogCursorDirect.PreviousEntryUniqueId from the request, or null if not available.
            // Used for desync detection.
            MetaUInt128? requestPreviousEntryUniqueId;

            if (request.StartCursor is EntityEventLogCursorDirect)
            {
                EntityEventLogCursorDirect startCursor = (EntityEventLogCursorDirect)request.StartCursor;

                if (startCursor.SegmentId > eventLog.RunningSegmentId)
                    return new TScanResponse { FailedWithDesync = true, DesyncDescription = "Cursor too advanced" };
                if (startCursor.EntryIndexWithinSegment < 0)
                    throw new ArgumentOutOfRangeException(nameof(request), $"Start cursor's {nameof(startCursor.EntryIndexWithinSegment)} cannot be negative (is {startCursor.EntryIndexWithinSegment})");

                // Use startCursor's values directly, except clamp it if it
                // points at older than the oldest available segment.
                if (startCursor.SegmentId >= eventLog.OldestAvailableSegmentId)
                {
                    startCursorSegmentId                = startCursor.SegmentId;
                    startCursorEntryIndexWithinSegment  = startCursor.EntryIndexWithinSegment;
                    requestPreviousEntryUniqueId        = startCursor.PreviousEntryUniqueId;
                }
                else
                {
                    startCursorSegmentId                = eventLog.OldestAvailableSegmentId;
                    startCursorEntryIndexWithinSegment  = 0;
                    requestPreviousEntryUniqueId        = null;
                }
            }
            else if (request.StartCursor is EntityEventLogCursorOnePastNewest)
            {
                startCursorSegmentId                = eventLog.RunningSegmentId;
                startCursorEntryIndexWithinSegment  = eventLog.LatestSegmentEntries.Count;
                requestPreviousEntryUniqueId        = null;
            }
            else if (request.StartCursor is EntityEventLogCursorOldest)
            {
                if (request.StartTime.HasValue)
                {
                    // When starting from the "oldest" cursor, start time filtering is supported.
                    // Find segment id and entry index to start from, based on StartTime,
                    // rather than starting from the actual oldest.
                    (startCursorSegmentId, startCursorEntryIndexWithinSegment) = await GetTimeRangeStartSegmentIdAsync(eventLog, ownerId, gameConfigResolver, logicVersion, request.StartTime.Value, storage);
                }
                else
                {
                    startCursorSegmentId                = eventLog.OldestAvailableSegmentId;
                    startCursorEntryIndexWithinSegment  = 0;
                }

                requestPreviousEntryUniqueId        = null;
            }
            else
                throw new ArgumentException($"Invalid type of {nameof(EntityEventLogCursor)}: {request.StartCursor?.GetType().Name ?? "<null>"}", nameof(request));

            if (request.NumEntries < 0)
                throw new ArgumentOutOfRangeException(nameof(request), $"Requested number of entries cannot be negative (is {request.NumEntries})");

            // EntityEventLogCursorDirect.PreviousEntryUniqueId for the StartCursor to return in the response.
            // \note Initialized to `default` only to please the compiler. It will always be set to a
            //       proper value (except in case of throw or early-error-return where we don't use this variable).
            MetaUInt128 startCursorPreviousEntryUniqueId = default;
            // EntityEventLogCursorDirect.PreviousEntryUniqueId for the ContinuationCursor to return in the response.
            MetaUInt128 continuationCursorPreviousEntryUniqueId;

            // Iterators for the segment id and entry index.
            // Start at the start cursor, then updated in the code below.
            int currentSegmentId = startCursorSegmentId;
            int currentEntryIndexWithinSegment = startCursorEntryIndexWithinSegment;
            // Keeps track in the loop below of whether it's the first segment we're working on.
            // Used for checking requestPreviousEntryUniqueId and setting startCursorPreviousEntryUniqueId.
            bool isAtFirstSegmentToScan = true;

            // Loop over the segments, starting from the current value of currentSegmentId,
            // towards newer or older segments as indicated by request.ScanDirection,
            // continuing until either the requested number of entries
            // is reached, or the end of the segments is reached.
            // After this loop:
            // - resultEntries contains all the entries to return
            // - currentSegmentId and currentEntryIndexWithinSegment form the continuation cursor
            // \note The cases for the TowardsNewer and TowardsOlder scan directions
            //       are written separately, for clarity. This causes some duplication
            //       but is hopefully more understandable overall.

            List<TEntry> resultEntries = new List<TEntry>();

            switch (request.ScanDirection)
            {
                case EntityEventLogScanDirection.TowardsNewer:
                {
                    while (true)
                    {
                        SegmentData segment = await GetSegmentDataAsync(eventLog, ownerId, gameConfigResolver, logicVersion, currentSegmentId, storage).ConfigureAwait(false);

                        // From current segment, starting at the current entry index and moving towards newer entries,
                        // get as many entries as are still needed to fulfill the request, or if there's not enough, then get all.
                        //
                        // currentEntryIndexWithinSegment might be equal to segmentEntries.Count
                        // (in case the start cursor was from a previous scan which left the index there) and
                        // that's also ok, we'll just take 0 entries from this segment.

                        if (currentEntryIndexWithinSegment < 0)
                            throw new MetaAssertException($"Entry index cannot be negative (index {currentEntryIndexWithinSegment} for segment {currentSegmentId})");

                        if (currentEntryIndexWithinSegment > segment.Entries.Count)
                            return new TScanResponse { FailedWithDesync = true, DesyncDescription = "Cursor too advanced" };

                        if (isAtFirstSegmentToScan)
                        {
                            isAtFirstSegmentToScan = false;

                            MetaUInt128 previousEntryUniqueId = segment.GetUniqueIdOfPrecedingEntry(currentEntryIndexWithinSegment);
                            startCursorPreviousEntryUniqueId = previousEntryUniqueId;
                            // If the request cursor contains PreviousEntryUniqueId, validate it by comparing to the actual previousEntryUniqueId.
                            // Except omit the validation if previousEntryUniqueId is zero, which is the case for old persisted segments
                            // from before the MetaEventLog<>.PreviousSegmentLastEntryUniqueId field was introduced.
                            if (requestPreviousEntryUniqueId.HasValue && previousEntryUniqueId != MetaUInt128.Zero && requestPreviousEntryUniqueId.Value != previousEntryUniqueId)
                                return new TScanResponse { FailedWithDesync = true, DesyncDescription = "Mismatched entry at cursor" };
                        }

                        bool reachedEndTime = false;
                        while (resultEntries.Count < request.NumEntries && currentEntryIndexWithinSegment < segment.Entries.Count)
                        {
                            TEntry entry = segment.Entries[currentEntryIndexWithinSegment];
                            if (request.EndTime.HasValue && entry.CollectedAt >= request.EndTime.Value)
                            {
                                reachedEndTime = true;
                                break;
                            }

                            resultEntries.Add(entry);
                            currentEntryIndexWithinSegment++;
                        }

                        continuationCursorPreviousEntryUniqueId = segment.GetUniqueIdOfPrecedingEntry(currentEntryIndexWithinSegment);

                        // Either stop the scan if a stopping condition is reached,
                        // or keep going at the next newer segment.

                        if (resultEntries.Count >= request.NumEntries)
                        {
                            MetaDebug.Assert(resultEntries.Count == request.NumEntries, Invariant($"Took more entries than wanted ({resultEntries.Count} vs {request.NumEntries})"));

                            // We've got the requested number of entries. Stop.
                            // \note We might *also* have exhausted the current segment, in
                            //       which case we could increment the cursor to the next newer segment (unless
                            //       we're already at the newest segment) so that a subsequent continuation scan
                            //       would start there. But there's no need, the above logic handles end-
                            //       of-segment entry index anyway.
                            break;
                        }
                        else if (currentSegmentId >= eventLog.RunningSegmentId)
                        {
                            MetaDebug.Assert(currentSegmentId == eventLog.RunningSegmentId, Invariant($"Went past {nameof(eventLog.RunningSegmentId)} ({currentSegmentId} > {eventLog.RunningSegmentId}); shouldn't happen since there's a precondition check for start cursor"));

                            // Exhausted the newest segment without getting the requested number of
                            // entries. Stop the scan, but don't increment currentSegmentId - the newest
                            // segment might still get more entries, so a subsequent continuation scan
                            // might still get entries from there.
                            break;
                        }
                        else if (reachedEndTime)
                        {
                            // request.EndTime was reached before reaching the request count limit or the end of the event log.
                            break;
                        }
                        else
                        {
                            // Exhausted the current segment which is not the newest segment,
                            // and don't yet have the requested number of entries.
                            // Keep going from the oldest entry of the next newer segment.
                            currentSegmentId++;
                            currentEntryIndexWithinSegment = 0;
                        }
                    }
                    break;
                }

                case EntityEventLogScanDirection.TowardsOlder:
                {
                    while (true)
                    {
                        SegmentData segment = await GetSegmentDataAsync(eventLog, ownerId, gameConfigResolver, logicVersion, currentSegmentId, storage).ConfigureAwait(false);

                        // From current segment, starting at the current entry index and moving towards older entries,
                        // get as many entries as are still needed to fulfill the request, or if there's not enough, then get all.
                        // \note Unlike in the EntityEventLogScanDirection.TowardsNewer case, here currentEntryIndexWithinSegment
                        //       actually points to *one past* the current entry. For example:
                        //       currentEntryIndexWithinSegment = 5: means we will take entries 4, 3, 2, ... (unless capped by requested count).
                        //       currentEntryIndexWithinSegment = segmentEntries.Count: means we take all entries in the segment (unless capped by requested count).
                        //       currentEntryIndexWithinSegment = 0: means we take none of the entries in the segment.

                        // Special marker value for currentEntryIndexWithinSegment to represent the total count of entries
                        // in a segment - only used within this loop, not visible to the outside.
                        // This is used because the actual segmentEntries.Count is not yet known at the point where
                        // this is assigned (when we decrement currentSegmentId to go to the older segment).
                        const int fullSegmentCountIndexMarker = -1;

                        if (currentEntryIndexWithinSegment == fullSegmentCountIndexMarker)
                            currentEntryIndexWithinSegment = segment.Entries.Count;

                        if (currentEntryIndexWithinSegment < 0)
                            throw new MetaAssertException($"Entry index cannot be negative (index {currentEntryIndexWithinSegment} for segment {currentSegmentId})");

                        if (currentEntryIndexWithinSegment > segment.Entries.Count)
                            return new TScanResponse { FailedWithDesync = true, DesyncDescription = "Cursor too advanced" };

                        if (isAtFirstSegmentToScan)
                        {
                            isAtFirstSegmentToScan = false;

                            MetaUInt128 previousEntryUniqueId = segment.GetUniqueIdOfPrecedingEntry(currentEntryIndexWithinSegment);
                            startCursorPreviousEntryUniqueId = previousEntryUniqueId;
                            // If the request cursor contains PreviousEntryUniqueId, validate it by comparing to the actual previousEntryUniqueId.
                            // Except omit the validation if previousEntryUniqueId is zero, which is the case for old persisted segments
                            // from before the MetaEventLog<>.PreviousSegmentLastEntryUniqueId field was introduced.
                            if (requestPreviousEntryUniqueId.HasValue && previousEntryUniqueId != MetaUInt128.Zero && requestPreviousEntryUniqueId.Value != previousEntryUniqueId)
                                return new TScanResponse { FailedWithDesync = true, DesyncDescription = "Mismatched entry at cursor" };
                        }

                        while (resultEntries.Count < request.NumEntries && currentEntryIndexWithinSegment > 0)
                        {
                            resultEntries.Add(segment.Entries[currentEntryIndexWithinSegment - 1]);
                            currentEntryIndexWithinSegment--;
                        }

                        continuationCursorPreviousEntryUniqueId = segment.GetUniqueIdOfPrecedingEntry(currentEntryIndexWithinSegment);

                        if (resultEntries.Count >= request.NumEntries)
                        {
                            MetaDebug.Assert(resultEntries.Count == request.NumEntries, Invariant($"Took more entries than wanted ({resultEntries.Count} vs {request.NumEntries})"));

                            // We've got the requested number of entries. Stop.
                            // \note We might *also* have reached index 0 of the current segment, in
                            //       which case we could decrement the cursor to the next older segment (unless
                            //       we're already at the oldest segment) so that the next scan would
                            //       start there. But there's no need, the above logic handles the
                            //       0 entry index anyway.
                            break;
                        }
                        else if (currentSegmentId <= eventLog.OldestAvailableSegmentId)
                        {
                            MetaDebug.Assert(currentSegmentId == eventLog.OldestAvailableSegmentId, Invariant($"Went past {nameof(eventLog.OldestAvailableSegmentId)} ({currentSegmentId} < {eventLog.OldestAvailableSegmentId}); shouldn't happen since there's a clamp for start cursor"));

                            // Exhausted the oldest segment without getting the requested number of
                            // entries. Stop the scan. Further scans continuing from this continuation cursor
                            // will not return any entries (because no older entries can be added).
                            break;
                        }
                        else
                        {
                            // Exhausted the current segment which is not the oldest segment,
                            // and don't yet have the requested number of entries.
                            // Keep going from the newest entry of the next older segment.
                            currentSegmentId--;
                            currentEntryIndexWithinSegment = fullSegmentCountIndexMarker; // \note Using this marker value because we don't yet know the entry Count of the next older segment (see comment above fullSegmentCountIndexMarker).
                        }
                    }

                    // resultEntries were produces in reverse order (i.e. newest first) by backwards iteration.
                    // Reverse them for the return value.
                    resultEntries.Reverse();
                    break;
                }

                default:
                    throw new ArgumentException($"Invalid {nameof(EntityEventLogScanDirection)}: {request.ScanDirection}", nameof(request));
            }

            // Debug check: IDs must be consecutive and increasing
            for (int i = 1; i < resultEntries.Count; i++)
            {
                TEntry current  = resultEntries[i];
                TEntry previous = resultEntries[i-1];

                MetaDebug.Assert(current.SequentialId == previous.SequentialId+1, "Non-sequential event log entry IDs: {0} vs {1}", previous.SequentialId, current.SequentialId);
            }

            return new TScanResponse
            {
                Entries = MetaSerialization.ToMetaSerialized(resultEntries, MetaSerializationFlags.EntityEventLog, logicVersion),

                // ContinuationCursor is where this scan ended.
                ContinuationCursor = new EntityEventLogCursorDirect(
                    segmentId: currentSegmentId,
                    entryIndexWithinSegment: currentEntryIndexWithinSegment,
                    previousEntryUniqueId: continuationCursorPreviousEntryUniqueId),

                // StartCursor is the cursor where the scan started.
                // If request.StartCursor was the "symbolic" EntityEventLogCursorOnePastNewest or EntityEventLogCursorOldest,
                // this is the concrete value of the cursor rather than "symbolic". See comment on EntityEventLogScanRequest for
                // an explanation of the use case.
                StartCursor = new EntityEventLogCursorDirect(
                    segmentId: startCursorSegmentId,
                    entryIndexWithinSegment: startCursorEntryIndexWithinSegment,
                    previousEntryUniqueId: startCursorPreviousEntryUniqueId),

            };
        }

        /// <summary>
        /// Get the segment id and entry-index-within-segment
        /// of the oldest entry whose <see cref="MetaEventLogEntry.CollectedAt"/>
        /// is at least <paramref name="startTime"/>.
        /// If no such entry exists, the returned SegmentId will be
        /// or <see cref="MetaEventLog{TEntry}.RunningSegmentId"/>
        /// and the returned EntryIndexWithinSegment will be
        /// the Count of <see cref="MetaEventLog{TEntry}.LatestSegmentEntries"/>;
        /// i.e. it will point at the end of the ongoing segment.
        /// </summary>
        static async Task<(int SegmentId, int EntryIndexWithinSegment)> GetTimeRangeStartSegmentIdAsync(MetaEventLog<TEntry> eventLog, EntityId ownerId, IGameConfigDataResolver gameConfigResolver, int logicVersion, MetaTime startTime, IEventLogStorage storage)
        {
            // \note persistedSegmentIdsStart and persistedSegmentIdsEnd may be equal,
            //       when there are no persisted segments available.
            int persistedSegmentIdsStart = eventLog.OldestAvailableSegmentId;
            int persistedSegmentIdsEnd = eventLog.RunningSegmentId - eventLog.PendingSegments.Count;

            // First, try to find from among persisted segments (they're older than any segments resident in eventLog).
            {
                TPersistedSegment persistedSegment = await storage.TryFindTimeRangeStartSegmentAsync<TPersistedSegment>(ownerId, startTime, segmentIdsStart: persistedSegmentIdsStart, segmentIdsEnd: persistedSegmentIdsEnd);
                if (persistedSegment != null)
                {
                    TSegmentPayload segment = DeserializePersistedSegmentPayload(persistedSegment, gameConfigResolver, logicVersion);
                    foreach ((TEntry entry, int entryIndex) in segment.Entries.ZipWithIndex())
                    {
                        if (entry.CollectedAt >= startTime)
                            return (persistedSegment.SegmentSequentialId, entryIndex);
                    }
                }
            }

            // Secondly, try to find from PendingSegments.
            foreach ((MetaEventLog<TEntry>.PendingSegment pendingSegment, int pendingSegmentIndex) in eventLog.PendingSegments.ZipWithIndex())
            {
                int pendingSegmentId = persistedSegmentIdsEnd + pendingSegmentIndex;

                if (pendingSegment.Entries.Last().CollectedAt >= startTime)
                {
                    foreach ((TEntry entry, int entryIndex) in pendingSegment.Entries.ZipWithIndex())
                    {
                        if (entry.CollectedAt >= startTime)
                            return (pendingSegmentId, entryIndex);
                    }
                }
            }

            // Last, try to find from the ongoing segment.
            foreach ((TEntry entry, int entryIndex) in eventLog.LatestSegmentEntries.ZipWithIndex())
            {
                if (entry.CollectedAt >= startTime)
                    return (eventLog.RunningSegmentId, entryIndex);
            }

            // No such entry was found - point at the end of the ongoing segment instead.
            return (eventLog.RunningSegmentId, eventLog.LatestSegmentEntries.Count);
        }

        /// <summary>
        /// Get entries of segment <paramref name="segmentId"/>, which may be found either
        /// in the database, or directly in memory in <paramref name="eventLog"/> if it
        /// hasn't yet been flushed to the database.
        /// <paramref name="segmentId"/> must not be less than <see cref="MetaEventLog{TEntry}.OldestAvailableSegmentId"/>.
        /// </summary>
        static async Task<SegmentData> GetSegmentDataAsync(MetaEventLog<TEntry> eventLog, EntityId ownerId, IGameConfigDataResolver gameConfigResolver, int logicVersion, int segmentId, IEventLogStorage storage)
        {
            // persistedSegmentIdsEnd is one past the id of the last persisted segment.
            // Equivalently, the first segment in PendingSegments, or equal to
            // RunningSegmentId if PendingSegments is empty.
            int persistedSegmentIdsEnd = eventLog.RunningSegmentId - eventLog.PendingSegments.Count;

            // Fetch all the entries from the segment identified by segmentId.
            // The segment comes from one of the following places:
            // - database-persisted segment (when eventLog.OldestAvailableSegmentId <= segmentId < persistedSegmentIdsEnd)
            // - eventLog.PendingSegments (when persistedSegmentIdsEnd <= segmentId < eventLog.RunningSegmentId)
            // - the latest, potentially still incomplete segment (when segmentId == eventLog.RunningSegmentId)
            // That is, database-persisted segments come before (i.e. are older) than pending segments,
            // and pending segments come before the latest segment.
            // Note the exclusive-end id ranges for database-persisted and pending segments;
            // it is possible that there are none of either.

            if (segmentId < persistedSegmentIdsEnd)
            {
                // The segment is database-persisted, fetch it.
                TSegmentPayload segment = await ReadPersistedSegmentPayloadAsync(ownerId, gameConfigResolver, logicVersion, segmentId, storage).ConfigureAwait(false);
                return new SegmentData(segment.Entries, segment.PreviousSegmentLastEntryUniqueId);
            }
            else if (segmentId < eventLog.RunningSegmentId)
            {
                // Segment is not database-persisted, but is older than the latest segment. It's a pending segment.
                MetaEventLog<TEntry>.PendingSegment segment = eventLog.PendingSegments[segmentId - persistedSegmentIdsEnd];
                return new SegmentData(segment.Entries, segment.PreviousSegmentLastEntryUniqueId);
            }
            else
            {
                // Segment is neither database-persisted nor a pending segment. It's the latest segment.
                return new SegmentData(eventLog.LatestSegmentEntries, eventLog.PreviousSegmentLastEntryUniqueId);
            }
        }

        static async Task<TSegmentPayload> ReadPersistedSegmentPayloadAsync(EntityId ownerId, IGameConfigDataResolver gameConfigResolver, int logicVersion, int segmentSequentialId, IEventLogStorage storage)
        {
            string              persistedSegmentId  = PersistedEntityEventLogSegmentUtil.CreateGlobalId(ownerId, segmentSequentialId);
            TPersistedSegment   persistedSegment    =
                await storage.TryGetSegmentAsync<TPersistedSegment>(
                    primaryKey:     persistedSegmentId,
                    partitionKey:   PersistedEntityEventLogSegmentUtil.GetPartitionKey(ownerId)
                    ).ConfigureAwait(false);

            if (persistedSegment == null)
                throw new InvalidOperationException($"Getting {typeof(TPersistedSegment).Name} failed, id={persistedSegmentId}");

            return DeserializePersistedSegmentPayload(persistedSegment, gameConfigResolver, logicVersion);
        }

        static TSegmentPayload DeserializePersistedSegmentPayload(TPersistedSegment persistedSegment, IGameConfigDataResolver gameConfigResolver, int logicVersion)
        {
            return MetaSerialization.DeserializeTagged<TSegmentPayload>(persistedSegment.UncompressPayload(), MetaSerializationFlags.Persisted | MetaSerializationFlags.EntityEventLog, gameConfigResolver, logicVersion);
        }

        struct SegmentData
        {
            public List<TEntry> Entries;
            public MetaUInt128 PreviousSegmentLastEntryUniqueId;

            public SegmentData(List<TEntry> entries, MetaUInt128 previousSegmentLastEntryUniqueId)
            {
                Entries = entries;
                PreviousSegmentLastEntryUniqueId = previousSegmentLastEntryUniqueId;
            }

            public MetaUInt128 GetUniqueIdOfPrecedingEntry(int entryIndex)
            {
                if (entryIndex == 0)
                    return PreviousSegmentLastEntryUniqueId;
                else
                    return Entries[entryIndex - 1].UniqueId;
            }
        }

        static List<T> GetAndRemoveNFirst<T>(List<T> list, int count)
        {
            List<T> result = list.GetRange(0, count);
            list.RemoveRange(0, count);
            return result;
        }
    }
}
