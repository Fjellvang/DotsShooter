// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.EventLog;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Server.EventLog;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.Tests
{
    #region EntityEventLogUtil boilerplate

    using TestEventLogUtil = EntityEventLogUtil<EventLogTests.TestEventLogEntry, TestEventLogSegmentPayload, PersistedTestEventLogSegment, TestEventLogScanResponse>;

    [MetaSerializable]
    class TestEventLogSegmentPayload : MetaEventLogSegmentPayload<EventLogTests.TestEventLogEntry>{ }
    // [Table("TestEventLogSegments")] \note Commented out, because these tests do not use the actual database.
    class PersistedTestEventLogSegment : PersistedEntityEventLogSegment{ }

    [MetaMessage(12345, MessageDirection.ServerInternal)]
    class TestEventLogScanResponse : EntityEventLogScanResponse<EventLogTests.TestEventLogEntry>{ }

    [EntityEventLogOptions("Test")]
    public class TestEventLogOptions : EntityEventLogOptionsBase
    {
        // Test helper setters for these otherwise protected-setter properties.
        public new int MinPersistedSegmentsToRetain { get => base.MinPersistedSegmentsToRetain; set { base.MinPersistedSegmentsToRetain = value; } }
        public new int MaxPersistedSegmentsToRetain { get => base.MaxPersistedSegmentsToRetain; set { base.MaxPersistedSegmentsToRetain = value; } }
        public new int NumEntriesPerPersistedSegment { get => base.NumEntriesPerPersistedSegment; set { base.NumEntriesPerPersistedSegment = value; } }
        public new int MaxPersistedSegmentsToRemoveAtOnce { get => base.MaxPersistedSegmentsToRemoveAtOnce; set { base.MaxPersistedSegmentsToRemoveAtOnce = value; } }
        public new TimeSpan RetentionDuration { get => base.RetentionDuration; set { base.RetentionDuration = value; } }

        public TestEventLogOptions()
        {
            NumEntriesPerPersistedSegment = 5;
        }
    }

    #endregion

    [TestFixture(TestFlags.Default)]
    [TestFixture(TestFlags.ForceNoEntryUniqueIdTrackingBetweenSegments)]
    class EventLogTests
    {
        public enum TestFlags
        {
            Default = 0,

            /// <summary>
            /// Forces <see cref="MetaEventLog{TEntry}.PreviousSegmentLastEntryUniqueId"/>
            /// to be set to zero, which causes cursor desync detection between separate segments
            /// to be disabled. Note that desync detection within segments is still enabled.
            /// <para>
            /// This is meant to mimic old persisted data from before
            /// <see cref="MetaEventLog{TEntry}.PreviousSegmentLastEntryUniqueId"/> was added.
            /// </para>
            /// </summary>
            ForceNoEntryUniqueIdTrackingBetweenSegments = 1 << 0,
        }

        TestFlags _testFlags;

        public EventLogTests(TestFlags testFlags)
        {
            _testFlags = testFlags;
        }

        [MetaSerializable]
        public class TestEventLogEntry : MetaEventLogEntry
        {
            [MetaMember(1)] public string Payload;
            /// <summary>
            /// <see cref="IncarnationNumber"/> and <see cref="EntryNumberWithinIncarnation"/>
            /// are used in RandomTests for asserting the consistency of log entry sequences.
            /// </summary>
            [MetaMember(2)] public int IncarnationNumber;
            /// <inheritdoc cref="IncarnationNumber"/>
            [MetaMember(3)] public int EntryNumberWithinIncarnation;

            TestEventLogEntry() : base() { }
            public TestEventLogEntry(BaseParams baseParams, string payload, int incarnationNumber, int entryNumberWithinIncarnation)
                : base(baseParams)
            {
                Payload = payload;
                IncarnationNumber = incarnationNumber;
                EntryNumberWithinIncarnation = entryNumberWithinIncarnation;
            }

            public override string ToString() => Invariant($"({Payload}, incarnation={IncarnationNumber}, entry={EntryNumberWithinIncarnation})");
        }

        [AnalyticsEventCategory("Test")]
        public abstract class TestEventBase : EntityEventBase
        {
        }

        [MetaSerializable]
        public class TestEventLog : MetaEventLog<TestEventLogEntry>
        {
        }

        class TestLogger : MetaLoggerBase
        {
            public override bool IsLevelEnabled(LogLevel level)
            {
                return true;
            }

            public override void LogEvent(LogLevel level, Exception ex, string format, params object[] args)
            {
                Assert.AreNotEqual(LogLevel.Warning, level);
                Assert.AreNotEqual(LogLevel.Error, level);

                if (ex == null)
                    Console.WriteLine("{0}: message: {1}", level, LogTemplateFormatter.ToFlatString(format, args));
                else
                    Console.WriteLine("{0}: message: {1}, exception: {2}", level, LogTemplateFormatter.ToFlatString(format, args), ex);
            }
        }

        class EventLogStorageInMemoryForTesting : IEventLogStorage
        {
            MetaDictionary<string, PersistedEntityEventLogSegment> _segments = new();

            public Task InsertOrUpdateSegmentAsync<TSegment>(TSegment segment) where TSegment : PersistedEntityEventLogSegment
            {
                _segments[segment.GlobalId] = segment;
                return Task.CompletedTask;
            }

            public Task RemoveAllEventLogSegmentsOfEntityAsync<TSegment>(EntityId ownerId) where TSegment : PersistedEntityEventLogSegment
            {
                string ownerIdStr = ownerId.ToString();
                _segments.RemoveWhere(kv => kv.Value.OwnerId == ownerIdStr);
                return Task.CompletedTask;
            }

            public Task<bool> RemoveSegmentAsync<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment
            {
                // \note No need to use partitionKey, as this testing in-memory storage is not partitioned.
                bool result = _segments.Remove(primaryKey);
                return Task.FromResult(result);
            }

            public Task<DateTime?> TryGetSegmentLastEntryTimestamp<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment
            {
                // \note No need to use partitionKey, as this testing in-memory storage is not partitioned.
                DateTime? result = _segments.GetValueOrDefault(primaryKey)?.LastEntryTimestamp;
                return Task.FromResult(result);
            }

            public Task<TSegment> TryGetSegmentAsync<TSegment>(string primaryKey, string partitionKey) where TSegment : PersistedEntityEventLogSegment
            {
                // \note No need to use partitionKey, as this testing in-memory storage is not partitioned.
                TSegment result = (TSegment)_segments.GetValueOrDefault(primaryKey);
                return Task.FromResult(result);
            }

            public Task<TSegment> TryFindTimeRangeStartSegmentAsync<TSegment>(EntityId ownerId, MetaTime startTime, int segmentIdsStart, int segmentIdsEnd) where TSegment : PersistedEntityEventLogSegment
            {
                throw new NotImplementedException("Not implemented in tests, and not tested currently");
            }
        }

        class TestState
        {
            public EntityId EntityId;
            public TestEventLog EventLog;
            public TestEventLog PersistedEventLog;
            public TestEventLogOptions Options;
            public IMetaLogger Logger;
            public IEventLogStorage Storage;
            public MetaTime? CurrentTimeOverride = null;
        }

        TestState CreateState(TestEventLogOptions options = null)
        {
            options ??= new();

            TestEventLog eventLog = new TestEventLog();

            return new TestState
            {
                EntityId = EntityId.CreateRandom(EntityKindCore.Player),
                EventLog = eventLog,
                PersistedEventLog = Clone(eventLog),
                Options = options ?? new(),
                Logger = new TestLogger(),
                Storage = new EventLogStorageInMemoryForTesting(),
            };
        }

        TestEventLog Clone(TestEventLog eventLog)
        {
            return MetaSerialization.CloneTagged(eventLog, MetaSerializationFlags.Persisted, logicVersion: null, resolver: null);
        }

        void AddEntry(TestState state, string payload, int incarnationNumber = 0, int entryNumberWithinIncarnation = 0)
        {
            MetaTime collectedAt = state.CurrentTimeOverride ?? MetaTime.Now;
            RandomPCG eventIdRandom = RandomPCG.CreateNew();
            MetaUInt128 uniqueId = new MetaUInt128((ulong)collectedAt.MillisecondsSinceEpoch, eventIdRandom.NextULong());

            TestEventLogUtil.AddEntry(state.EventLog, state.Options, collectedAt, uniqueId, baseParams => new TestEventLogEntry(baseParams, payload, incarnationNumber, entryNumberWithinIncarnation));

            if (_testFlags.HasFlag(TestFlags.ForceNoEntryUniqueIdTrackingBetweenSegments))
            {
                foreach (MetaEventLog<TestEventLogEntry>.PendingSegment pendingSegment in state.EventLog.PendingSegments)
                    pendingSegment.PreviousSegmentLastEntryUniqueId = MetaUInt128.Zero;
                state.EventLog.PreviousSegmentLastEntryUniqueId = MetaUInt128.Zero;
            }
        }

        Task FlushAsync(TestState state)
        {
            if (TestEventLogUtil.CanFlush(state.EventLog, state.Options))
                return TestEventLogUtil.TryFlushAndCullSegmentsAsync(state.EventLog, state.Options, state.EntityId, logicVersion: 0, state.Logger, () => { PersistModel(state); return Task.CompletedTask; }, state.Storage, state.CurrentTimeOverride);
            else
                return Task.CompletedTask;
        }

        async Task<ScanResult> ScanAsync(TestState state, EntityEventLogCursor startCursor, int numEntries, EntityEventLogScanDirection direction)
        {
            TestEventLogScanResponse response = await TestEventLogUtil.ScanEntriesAsync(
                state.EventLog, state.EntityId, gameConfigResolver: null, logicVersion: 0,
                new EntityEventLogScanRequest(startCursor, numEntries, direction, startTime: null, endTime: null),
                state.Storage);

            return new ScanResult
            {
                Entries = response.Entries.IsEmpty ? null : response.Entries.Deserialize(resolver: null, logicVersion: 0),
                StartCursor = response.StartCursor,
                ContinuationCursor = response.ContinuationCursor,
                FailedWithDesync = response.FailedWithDesync,
                DesyncDescription = response.DesyncDescription,
            };
        }

        void PersistModel(TestState state)
        {
            state.PersistedEventLog = Clone(state.EventLog);
        }

        void CrashModel(TestState state)
        {
            state.EventLog = Clone(state.PersistedEventLog);
        }

        // Helper type holding Entries in non-serialized form (in TestEventLogScanResponse it's MetaSerialized)
        struct ScanResult
        {
            public required List<TestEventLogEntry>     Entries;
            public required EntityEventLogCursorDirect  ContinuationCursor;
            public required EntityEventLogCursorDirect  StartCursor;
            public required bool                        FailedWithDesync;
            public required string                      DesyncDescription;

            public void Deconstruct(out List<TestEventLogEntry> entries, out EntityEventLogCursor continuationCursor)
            {
                if (FailedWithDesync)
                    throw new InvalidOperationException($"Scan failed with desync: {DesyncDescription}");

                entries = Entries;
                continuationCursor = ContinuationCursor;
            }
        }

        [Test]
        public async Task Basic()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");
            AddEntry(state, "test 5");
            AddEntry(state, "test 6");
            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            await FlushAsync(state);

            List<TestEventLogEntry> entries;

            (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 100, EntityEventLogScanDirection.TowardsNewer);

            Assert.AreEqual(10, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 9", entries.Last().Payload);

            (entries, _) = await ScanAsync(state, new EntityEventLogCursorOnePastNewest(), 100, EntityEventLogScanDirection.TowardsOlder);

            Assert.AreEqual(10, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 9", entries.Last().Payload);
        }

        [TestCase(5, 10)]
        [TestCase(5, 5)]
        [TestCase(5, 3)]
        [TestCase(5, 1)]
        [TestCase(3, 10)]
        [TestCase(3, 5)]
        [TestCase(3, 3)]
        [TestCase(3, 1)]
        public async Task ScanInPieces(int numEntriesPerSegment, int numEntriesPerScan)
        {
            TestState state = CreateState(options: new TestEventLogOptions{ NumEntriesPerPersistedSegment = numEntriesPerSegment });

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");
            AddEntry(state, "test 5");
            AddEntry(state, "test 6");
            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            await FlushAsync(state);

            {
                List<TestEventLogEntry> entries;
                EntityEventLogCursor cursor = new EntityEventLogCursorOldest();

                for (int entryNdx = 0; entryNdx < 10; entryNdx += numEntriesPerScan)
                {
                    (entries, cursor) = await ScanAsync(state, cursor, numEntriesPerScan, EntityEventLogScanDirection.TowardsNewer);

                    int expectedActualNumEntries = Math.Min(numEntriesPerScan, 10 - entryNdx);
                    Assert.AreEqual(expectedActualNumEntries, entries.Count);
                    for (int i = 0; i < expectedActualNumEntries; i++)
                        Assert.AreEqual(Invariant($"test {entryNdx + i}"), entries[i].Payload);
                }
            }

            {
                List<TestEventLogEntry> entries;
                EntityEventLogCursor cursor = new EntityEventLogCursorOnePastNewest();

                for (int entryNdx = 9; entryNdx >= 0; entryNdx -= numEntriesPerScan)
                {
                    (entries, cursor) = await ScanAsync(state, cursor, numEntriesPerScan, EntityEventLogScanDirection.TowardsOlder);

                    int expectedActualNumEntries = Math.Min(numEntriesPerScan, entryNdx + 1);
                    Assert.AreEqual(expectedActualNumEntries, entries.Count);
                    for (int i = 0; i < expectedActualNumEntries; i++)
                        Assert.AreEqual(Invariant($"test {entryNdx + 1 - expectedActualNumEntries + i}"), entries[i].Payload);
                }
            }
        }

        [Test]
        public async Task ScanEmpty()
        {
            TestState state = CreateState();

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor;
            (entries, cursor) = await ScanAsync(state, new EntityEventLogCursorOldest(), 100, EntityEventLogScanDirection.TowardsNewer);

            Assert.AreEqual(0, entries.Count);
            Assert.IsInstanceOf<EntityEventLogCursorDirect>(cursor);
            Assert.AreEqual(0, ((EntityEventLogCursorDirect)cursor).SegmentId);
            Assert.AreEqual(0, ((EntityEventLogCursorDirect)cursor).EntryIndexWithinSegment);
        }

        [Test]
        public async Task AddAfterScanningAndScanAgain()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");
            AddEntry(state, "test 5");

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 100, EntityEventLogScanDirection.TowardsNewer);

            Assert.AreEqual(6, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 5", entries.Last().Payload);

            AddEntry(state, "test 6");
            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            (entries, cursor) = await ScanAsync(state, cursor, 100, EntityEventLogScanDirection.TowardsNewer);

            Assert.AreEqual(4, entries.Count);
            Assert.AreEqual("test 6", entries.First().Payload);
            Assert.AreEqual("test 9", entries.Last().Payload);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RemovalDueToCountLimit(bool flushAfterEveryAddition)
        {
            TestState state = CreateState(
                options: new TestEventLogOptions
                {
                    MinPersistedSegmentsToRetain = 6,
                    MaxPersistedSegmentsToRetain = 6,
                    NumEntriesPerPersistedSegment = 5,
                });

            // Add enough entries to exceed MaxPersistedSegmentsToRetain.
            for (int i = 0; i < (6+1)*5; i++)
            {
                AddEntry(state, Invariant($"test {i}"));
                if (flushAfterEveryAddition)
                    await FlushAsync(state);
            }

            if (!flushAfterEveryAddition)
                await FlushAsync(state);

            List<TestEventLogEntry> entries;
            (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(6*5, entries.Count);
            Assert.AreEqual("test 5", entries.First().Payload);
            Assert.AreEqual("test 34", entries.Last().Payload);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RemovalDueToTimeLimit(bool flushAfterEveryAddition)
        {
            TestState state = CreateState(
                options: new TestEventLogOptions
                {
                    MinPersistedSegmentsToRetain = 6,
                    MaxPersistedSegmentsToRetain = 1_000_000, // Won't be exceeded
                    NumEntriesPerPersistedSegment = 5,
                    MaxPersistedSegmentsToRemoveAtOnce = 1000,
                    RetentionDuration = TimeSpan.FromDays(10),
                });

            MetaTime initialTime = MetaTime.FromDateTime(new DateTime(2024, 10, 30, 15, 0, 0, DateTimeKind.Utc));

            state.CurrentTimeOverride = initialTime;

            // Add enough entries to exceed MinPersistedSegmentsToRetain but not MaxPersistedSegmentsToRetain.
            for (int i = 0; i < (6+10)*5; i++)
            {
                AddEntry(state, Invariant($"test {i}"));
                if (flushAfterEveryAddition)
                    await FlushAsync(state);
            }

            if (!flushAfterEveryAddition)
                await FlushAsync(state);

            {
                List<TestEventLogEntry> entries;
                (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 1000, EntityEventLogScanDirection.TowardsNewer);
                Assert.AreEqual((6+10)*5, entries.Count);
                Assert.AreEqual("test 0", entries.First().Payload);
                Assert.AreEqual(Invariant($"test {(6+10)*5-1}"), entries.Last().Payload);
            }

            state.CurrentTimeOverride = initialTime + MetaDuration.FromDays(10) + MetaDuration.FromMilliseconds(1);

            // Need to add more events in order to trigger time-based removal, because FlushAsync
            // only removes segments if TestEventLogUtil.CanFlush returns true, i.e. there are
            // also new segments to persist.
            // (This is so in order to match actual behavior in PlayerActor etc.)
            for (int i = (6+10)*5; i < (6+10+1)*5; i++)
            {
                AddEntry(state, Invariant($"test {i}"));
                if (flushAfterEveryAddition)
                    await FlushAsync(state);
            }

            if (!flushAfterEveryAddition)
                await FlushAsync(state);

            {
                List<TestEventLogEntry> entries;
                (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 1000, EntityEventLogScanDirection.TowardsNewer);
                Assert.AreEqual(6*5, entries.Count);
                Assert.AreEqual(Invariant($"test {(10+1)*5}"), entries.First().Payload);
                Assert.AreEqual(Invariant($"test {(6+10+1)*5-1}"), entries.Last().Payload);
            }
        }

        [Test]
        public async Task RemovalBetweenScans()
        {
            TestState state = CreateState(
                options: new TestEventLogOptions
                {
                    MinPersistedSegmentsToRetain = 6,
                    MaxPersistedSegmentsToRetain = 6,
                    NumEntriesPerPersistedSegment = 5,
                });

            for (int i = 0; i < 3*5; i++)
            {
                AddEntry(state, Invariant($"test {i}"));
                await FlushAsync(state);
            }

            await FlushAsync(state);

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(3*5, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 14", entries.Last().Payload);

            for (int i = 3*5; i < 8*5; i++)
            {
                AddEntry(state, Invariant($"test {i}"));
                await FlushAsync(state);
            }

            EntityEventLogCursor cursor1 = cursor;

            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(5*5, entries.Count);
            Assert.AreEqual("test 15", entries.First().Payload);
            Assert.AreEqual("test 39", entries.Last().Payload);

            {
                (List<TestEventLogEntry> backwardsEntries, _) = await ScanAsync(state, cursor1, 1000, EntityEventLogScanDirection.TowardsOlder);
                Assert.AreEqual(5, backwardsEntries.Count);
                Assert.AreEqual("test 10", backwardsEntries.First().Payload);
                Assert.AreEqual("test 14", backwardsEntries.Last().Payload);
            }

            for (int i = 8*5; i < 100*5; i++)
            {
                AddEntry(state, Invariant($"test {i}"));
                await FlushAsync(state);
            }

            (entries, _) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(6*5, entries.Count);
            Assert.AreEqual("test 470", entries.First().Payload);
            Assert.AreEqual("test 499", entries.Last().Payload);

            {
                (List<TestEventLogEntry> backwardsEntries, _) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsOlder);
                Assert.AreEqual(0, backwardsEntries.Count);
            }
        }

        [Test]
        public async Task CrashBeforePersisting()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");

            List<TestEventLogEntry> entries;
            (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);

            await FlushAsync(state);

            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");

            CrashModel(state);

            (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(0, entries.Count);
        }

        [Test]
        public async Task CrashAfterPersisting()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");

            await FlushAsync(state);
            PersistModel(state);

            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");

            await FlushAsync(state);

            CrashModel(state);

            List<TestEventLogEntry> entries;
            (entries, _) = await ScanAsync(state, new EntityEventLogCursorOldest(), 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);
        }

        [Test]
        public async Task CrashBetweenScansBasic()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");

            await FlushAsync(state);
            PersistModel(state);

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);

            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");

            await FlushAsync(state);
            CrashModel(state);

            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(0, entries.Count);

            AddEntry(state, "test 7 b");
            AddEntry(state, "test 8 b");
            AddEntry(state, "test 9 b");

            AddEntry(state, "test 10 b");
            AddEntry(state, "test 11 b");

            (entries, _) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(5, entries.Count);
            Assert.AreEqual("test 7 b", entries.First().Payload);
            Assert.AreEqual("test 11 b", entries.Last().Payload);

            (entries, _) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsOlder);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);
        }

        [Test]
        public async Task CrashBetweenScansDesyncTooAdvancedSameSegment()
        {
            TestState state = CreateState(new TestEventLogOptions
            {
                NumEntriesPerPersistedSegment = 20,
            });

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");

            await FlushAsync(state);
            PersistModel(state);

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);

            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");

            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(5, entries.Count);
            Assert.AreEqual("test 7", entries.First().Payload);
            Assert.AreEqual("test 11", entries.Last().Payload);

            List<TestEventLogEntry> backwardsEntries;
            EntityEventLogCursor backwardsCursor = cursor;
            (backwardsEntries, backwardsCursor) = await ScanAsync(state, backwardsCursor, 3, EntityEventLogScanDirection.TowardsOlder);
            Assert.AreEqual(3, backwardsEntries.Count);
            Assert.AreEqual("test 9", backwardsEntries.First().Payload);
            Assert.AreEqual("test 11", backwardsEntries.Last().Payload);

            CrashModel(state);

            ScanResult scanResult = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.IsNull(scanResult.Entries);
            Assert.IsTrue(scanResult.FailedWithDesync);
            StringAssert.Contains("Cursor too advanced", scanResult.DesyncDescription);

            ScanResult backwardsScanResult = await ScanAsync(state, backwardsCursor, 10, EntityEventLogScanDirection.TowardsOlder);
            Assert.IsNull(backwardsScanResult.Entries);
            Assert.IsTrue(backwardsScanResult.FailedWithDesync);
            StringAssert.Contains("Cursor too advanced", backwardsScanResult.DesyncDescription);
        }

        [Test]
        public async Task CrashBetweenScansDesyncTooAdvancedNonexistentSegment()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");

            await FlushAsync(state);
            PersistModel(state);

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);

            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");

            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(5, entries.Count);
            Assert.AreEqual("test 7", entries.First().Payload);
            Assert.AreEqual("test 11", entries.Last().Payload);

            List<TestEventLogEntry> backwardsEntries;
            EntityEventLogCursor backwardsCursor = cursor;
            (backwardsEntries, backwardsCursor) = await ScanAsync(state, backwardsCursor, 3, EntityEventLogScanDirection.TowardsOlder);
            Assert.AreEqual(3, backwardsEntries.Count);
            Assert.AreEqual("test 9", backwardsEntries.First().Payload);
            Assert.AreEqual("test 11", backwardsEntries.Last().Payload);

            CrashModel(state);

            ScanResult scanResult = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.IsNull(scanResult.Entries);
            Assert.IsTrue(scanResult.FailedWithDesync);
            StringAssert.Contains("Cursor too advanced", scanResult.DesyncDescription);

            ScanResult backwardsScanResult = await ScanAsync(state, backwardsCursor, 10, EntityEventLogScanDirection.TowardsOlder);
            Assert.IsNull(backwardsScanResult.Entries);
            Assert.IsTrue(backwardsScanResult.FailedWithDesync);
            StringAssert.Contains("Cursor too advanced", backwardsScanResult.DesyncDescription);
        }

        [Test]
        public async Task CrashBetweenScansDesyncMismatch()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");

            await FlushAsync(state);
            PersistModel(state);

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 6", entries.Last().Payload);

            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");

            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(5, entries.Count);
            Assert.AreEqual("test 7", entries.First().Payload);
            Assert.AreEqual("test 11", entries.Last().Payload);

            List<TestEventLogEntry> backwardsEntries;
            EntityEventLogCursor backwardsCursor = cursor;
            (backwardsEntries, backwardsCursor) = await ScanAsync(state, backwardsCursor, 3, EntityEventLogScanDirection.TowardsOlder);
            Assert.AreEqual(3, backwardsEntries.Count);
            Assert.AreEqual("test 9", backwardsEntries.First().Payload);
            Assert.AreEqual("test 11", backwardsEntries.Last().Payload);

            CrashModel(state);

            AddEntry(state, "test 7 b");
            AddEntry(state, "test 8 b");
            AddEntry(state, "test 9 b");

            AddEntry(state, "test 10 b");
            AddEntry(state, "test 11 b");
            AddEntry(state, "test 12 b");
            AddEntry(state, "test 13 b");
            AddEntry(state, "test 14 b");

            ScanResult scanResult = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.IsNull(scanResult.Entries);
            Assert.IsTrue(scanResult.FailedWithDesync);
            StringAssert.Contains("Mismatched entry at cursor", scanResult.DesyncDescription);

            ScanResult backwardsScanResult = await ScanAsync(state, backwardsCursor, 10, EntityEventLogScanDirection.TowardsOlder);
            Assert.IsNull(backwardsScanResult.Entries);
            Assert.IsTrue(backwardsScanResult.FailedWithDesync);
            StringAssert.Contains("Mismatched entry at cursor", backwardsScanResult.DesyncDescription);
        }

        [Test]
        public async Task CrashBetweenScansDesyncMismatchAtSegmentBoundary()
        {
            TestState state = CreateState();

            AddEntry(state, "test 0");
            AddEntry(state, "test 1");
            AddEntry(state, "test 2");
            AddEntry(state, "test 3");
            AddEntry(state, "test 4");

            await FlushAsync(state);
            PersistModel(state);

            List<TestEventLogEntry> entries;
            EntityEventLogCursor cursor = new EntityEventLogCursorOldest();
            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(5, entries.Count);
            Assert.AreEqual("test 0", entries.First().Payload);
            Assert.AreEqual("test 4", entries.Last().Payload);

            AddEntry(state, "test 5");
            AddEntry(state, "test 6");
            AddEntry(state, "test 7");
            AddEntry(state, "test 8");
            AddEntry(state, "test 9");

            AddEntry(state, "test 10");
            AddEntry(state, "test 11");
            AddEntry(state, "test 12");
            AddEntry(state, "test 13");
            AddEntry(state, "test 14");

            (entries, cursor) = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            Assert.AreEqual(10, entries.Count);
            Assert.AreEqual("test 5", entries.First().Payload);
            Assert.AreEqual("test 14", entries.Last().Payload);

            List<TestEventLogEntry> backwardsEntries;
            EntityEventLogCursor backwardsCursor = cursor;
            (backwardsEntries, backwardsCursor) = await ScanAsync(state, backwardsCursor, 5, EntityEventLogScanDirection.TowardsOlder);
            Assert.AreEqual(5, backwardsEntries.Count);
            Assert.AreEqual("test 10", backwardsEntries.First().Payload);
            Assert.AreEqual("test 14", backwardsEntries.Last().Payload);

            CrashModel(state);

            AddEntry(state, "test 5 b");
            AddEntry(state, "test 6 b");
            AddEntry(state, "test 7 b");
            AddEntry(state, "test 8 b");
            AddEntry(state, "test 9 b");

            AddEntry(state, "test 10 b");
            AddEntry(state, "test 11 b");
            AddEntry(state, "test 12 b");
            AddEntry(state, "test 13 b");
            AddEntry(state, "test 14 b");

            AddEntry(state, "test 15 b");
            AddEntry(state, "test 16 b");
            AddEntry(state, "test 17 b");

            ScanResult scanResult = await ScanAsync(state, cursor, 1000, EntityEventLogScanDirection.TowardsNewer);
            ScanResult backwardsScanResult = await ScanAsync(state, backwardsCursor, 5, EntityEventLogScanDirection.TowardsOlder);

            if (_testFlags.HasFlag(TestFlags.ForceNoEntryUniqueIdTrackingBetweenSegments))
            {
                // Unable to detect desync, so check expected inconsistent result.
                // (ForceNoEntryUniqueIdTrackingBetweenSegments mimics legacy data
                // which lacks desync detection support between separate segments.)

                Assert.IsFalse(scanResult.FailedWithDesync);
                Assert.AreEqual(3, scanResult.Entries.Count);
                Assert.AreEqual("test 15 b", scanResult.Entries.First().Payload);
                Assert.AreEqual("test 17 b", scanResult.Entries.Last().Payload);

                Assert.IsFalse(backwardsScanResult.FailedWithDesync);
                Assert.AreEqual(5, backwardsScanResult.Entries.Count);
                Assert.AreEqual("test 5 b", backwardsScanResult.Entries.First().Payload);
                Assert.AreEqual("test 9 b", backwardsScanResult.Entries.Last().Payload);
            }
            else
            {
                Assert.IsNull(scanResult.Entries);
                Assert.IsTrue(scanResult.FailedWithDesync);
                StringAssert.Contains("Mismatched entry at cursor", scanResult.DesyncDescription);

                Assert.IsNull(backwardsScanResult.Entries);
                Assert.IsTrue(backwardsScanResult.FailedWithDesync);
                StringAssert.Contains("Mismatched entry at cursor", backwardsScanResult.DesyncDescription);
            }
        }

        [Test]
        public async Task RandomTests()
        {
            for (int i = 0; i < 100; i++)
                await RandomTest((ulong)i);
        }

        async Task RandomTest(ulong seed)
        {
            RandomPCG rnd = RandomPCG.CreateFromSeed(seed);

            TestState state = CreateState();
            int entryNumberWithinIncarnation = 0;
            int incarnationNumber = 0;

            List<RandomTestScanState> scans = new();

            for (int step = 0; step < 100; step++)
            {
                RandomTestOperation operation = rnd.Choice(s_randomTestOperations);

                switch (operation)
                {
                    case RandomTestOperation.AddEntries:
                    {
                        int numEntries = rnd.NextIntMinMax(1, 15+1);
                        for (int i = 0; i < numEntries; i++)
                        {
                            AddEntry(state, "test", incarnationNumber, entryNumberWithinIncarnation);
                            entryNumberWithinIncarnation++;
                        }
                        break;
                    }

                    case RandomTestOperation.Flush:
                        await FlushAsync(state);
                        break;

                    case RandomTestOperation.PersistModel:
                        PersistModel(state);
                        break;

                    case RandomTestOperation.CrashModel:
                        CrashModel(state);
                        incarnationNumber++;
                        entryNumberWithinIncarnation = 0;
                        break;

                    case RandomTestOperation.StartScan:
                    {
                        scans.Add(new RandomTestScanState
                        {
                            Cursor = rnd.Choice(s_randomTestScanStartCursors),
                            Direction = rnd.Choice(s_randomTestScanDirections),
                            LatestEntry = null,
                            ModelIncarnationNumber = incarnationNumber,
                        });
                        break;
                    }

                    case RandomTestOperation.AdvanceScan:
                    {
                        if (scans.Count == 0)
                            break;

                        RandomTestScanState scanState = rnd.Choice(scans);
                        int numEntries = rnd.NextInt(10+1);
                        ScanResult scanResult = await ScanAsync(state, scanState.Cursor, numEntries, scanState.Direction);
                        if (scanResult.FailedWithDesync)
                        {
                            // Desync should only be possible if incarnation has changed (i.e. there was a "crash")
                            // since the last scan.
                            Assert.AreNotEqual(incarnationNumber, scanState.ModelIncarnationNumber);

                            scans.Remove(scanState);
                            break;
                        }

                        scanState.Cursor = scanResult.ContinuationCursor;
                        scanState.ModelIncarnationNumber = incarnationNumber; // There was no desync, so the current scan cursor must be consistent with the current log incarnation

                        if (scanResult.Entries.Count != 0)
                        {
                            // Check scan result consistency (AssertValidSuccessorSequence).
                            // Except don't do it if ForceNoEntryUniqueIdTrackingBetweenSegments is set, because
                            // in that case we can't properly detect desyncs between separate segments, and therefore
                            // inconsistent results are in fact possible.
                            if (!_testFlags.HasFlag(TestFlags.ForceNoEntryUniqueIdTrackingBetweenSegments))
                            {
                                // Check that returned entries are in a valid successor sequence
                                for (int i = 1; i < scanResult.Entries.Count; i++)
                                    AssertValidSuccessorSequence(scanResult.Entries[i-1], scanResult.Entries[i]);

                                // Check that the latest entry (from the previous scan call)
                                // and the subsequent entry from this new call
                                // form a valid successor sequence.
                                if (scanState.LatestEntry != null)
                                {
                                    if (scanState.Direction == EntityEventLogScanDirection.TowardsNewer)
                                    {
                                        // If scanning towards newer, the latest old entry is one before the first new entry.
                                        AssertValidSuccessorSequence(scanState.LatestEntry, scanResult.Entries.First());
                                    }
                                    else
                                    {
                                        // If scanning towards older, the last new entry is one before the latest old entry.
                                        // (Although we're scanning towards older, each individual scan result still contains the
                                        // entries in the same chronological, non-reversed order as when scanning towards newer.)
                                        AssertValidSuccessorSequence(scanResult.Entries.Last(), scanState.LatestEntry);
                                    }
                                }
                            }

                            // Remember the latest scanned entry so we can compare against it later.
                            if (scanState.Direction == EntityEventLogScanDirection.TowardsNewer)
                                scanState.LatestEntry = scanResult.Entries.Last();
                            else
                                scanState.LatestEntry = scanResult.Entries.First();
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Asserts that <paramref name="second"/> is a valid successor of <paramref name="first"/> in <see cref="RandomTest"/>
        /// based on <see cref="TestEventLogEntry.IncarnationNumber"/> and <see cref="TestEventLogEntry.EntryNumberWithinIncarnation"/>.
        /// </summary>
        void AssertValidSuccessorSequence(TestEventLogEntry first, TestEventLogEntry second)
        {
            if (second.IncarnationNumber == first.IncarnationNumber)
            {
                if (second.EntryNumberWithinIncarnation == first.EntryNumberWithinIncarnation + 1)
                {
                    // Ok, `second` is the next entry after `first` in the same incarnation (i.e. no "crash" inbetween).
                    return;
                }

                Assert.Fail("Inconsistent event log scan: Entry {0} cannot be immediately followed by entry {1}. They belong to the same incarnation, so expected the entry number to increment by 1. A desync should have been detected, but wasn't.", first, second);
            }
            else if (second.IncarnationNumber > first.IncarnationNumber)
            {
                if (second.EntryNumberWithinIncarnation == 0)
                {
                    // Ok, `second` is the first entry in a new incarnation after `first` (i.e. a "crash" happened inbetween).
                    return;
                }

                Assert.Fail("Inconsistent event log scan: Entry {0} cannot be immediately followed by entry {1}. The first entry in a new incarnation must have entry number 0. A desync should have been detected, but wasn't.", first, second);
            }

            Assert.Fail("Inconsistent event log scan: Entry {0} cannot be immediately followed by entry {1}. Incarnation number cannot decrease. A desync should have been detected, but wasn't.", first, second);
        }

        enum RandomTestOperation
        {
            AddEntries,
            Flush,
            PersistModel,
            CrashModel,
            StartScan,
            AdvanceScan,
        }

        static readonly RandomTestOperation[] s_randomTestOperations = EnumUtil.GetValues<RandomTestOperation>().ToArray();
        static readonly EntityEventLogCursor[] s_randomTestScanStartCursors = [ new EntityEventLogCursorOldest(), new EntityEventLogCursorOnePastNewest() ];
        static readonly EntityEventLogScanDirection[] s_randomTestScanDirections = EnumUtil.GetValues<EntityEventLogScanDirection>().ToArray();

        class RandomTestScanState
        {
            public required EntityEventLogCursor Cursor;
            public required EntityEventLogScanDirection Direction;
            public required TestEventLogEntry LatestEntry;
            public required int ModelIncarnationNumber;
        }
    }
}
