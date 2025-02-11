// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.StatisticsEvents
{
    /// <summary>
    /// Persisted statistics event in the database. Consists of
    /// - <see cref="UniqueKey"/> to deduplicate multiple instances of the same event.
    /// - <see cref="Timestamp"/> to mark when the event happened (first instance wins when deduplicating).
    /// - <see cref="Payload"/> is the serialized payload of the event's contents.
    /// </summary>
    [Table("StatisticsEvents")]
    [Index(nameof(Timestamp))]
    public class PersistedStatisticsEvent : IPersistedItem
    {
        [Required]
        [Key]
        [PartitionKey]
        [Column(TypeName = "varchar(128)")]
        public string UniqueKey { get; set; }

        [Required]
        [Column(TypeName = "DateTime(3)")]
        public DateTime Timestamp { get; set; }

        [Required]
        public byte[] Payload { get; set; } // MetaSerialized<StatisticsEventBase>
    }

    /// <summary>
    /// Statistics event base class. Contains the timestamp of the event as well
    /// as ability to get <see cref="UniqueKey"/> for deduplicating events.
    /// </summary>
    [MetaSerializable]
    [MetaReservedMembers(100, 200)]
    public abstract class StatisticsEventBase
    {
        /// <summary> Timestamp of the event. Must be a monotonic time from <see cref="StatisticsEventClock"/>. </summary>
        [MetaMember(100)] public MetaTime Timestamp { get; set; }

        /// <summary>
        /// Unique key of the event. Used for deduplication.
        /// Should be of format {context}/{entity}/{key} where context is the type of the event and key is unique within the context.
        /// Example: "login/{playerId}/{loginDate:O}"
        /// </summary>
        public abstract string UniqueKey { get; }

        /// <summary>
        /// Only for deserialization.
        /// </summary>
        protected StatisticsEventBase() { }
        protected StatisticsEventBase(MetaTime timestamp)
        {
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Player login event. Used to track daily active users.
    /// </summary>
    [MetaSerializableDerived(1)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventPlayerLogin : StatisticsEventBase
    {
        public override string   UniqueKey => Invariant($"login/{PlayerId}/{LoginDate:O}"); // Date format is ISO 8601. (yyyy-MM-dd)
        public          DateOnly LoginDate => DateOnly.FromDateTime(Timestamp.ToDateTime());

        /// <summary> EntityId of the player who logged in. </summary>
        [MetaMember(1)] public EntityId PlayerId            { get; private set; }
        /// <summary> Number of days since the player registered, for tracking daily cohort metrics. </summary>
        [MetaMember(3)] public int      DaysSinceRegistered { get; private set; }

        StatisticsEventPlayerLogin() { }
        public StatisticsEventPlayerLogin(MetaTime timestamp, EntityId playerId, int daysSinceRegistered) : base(timestamp)
        {
            PlayerId = playerId;
            DaysSinceRegistered = daysSinceRegistered;
        }
    }

    /// <summary>
    /// New player created event. Used to track new users.
    /// </summary>
    [MetaSerializableDerived(2)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventPlayerCreated : StatisticsEventBase
    {
        public override string  UniqueKey => Invariant($"created/{PlayerId}/{LoginDate:O}"); // Date format is ISO 8601. (yyyy-MM-dd)
        public DateOnly         LoginDate => DateOnly.FromDateTime(Timestamp.ToDateTime());

        [MetaMember(1)] public EntityId PlayerId { get; private set; }

        StatisticsEventPlayerCreated() { }
        public StatisticsEventPlayerCreated(MetaTime timestamp, EntityId playerId) : base(timestamp)
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// Player purchase event. Used to track approximate gross game revenue by simply
    /// tracking all player purchases in gross USD approximate prices, ignoring
    /// currency conversions, taxes, refunds, etc.
    /// </summary>
    [MetaSerializableDerived(3)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventPlayerPurchase : StatisticsEventBase
    {
        public override string UniqueKey => Invariant($"purhase/{PurchaseId}");

        /// <summary> EntityId of player who made the purchase. </summary>
        [MetaMember(1)] public EntityId PlayerId            { get; private set; }
        /// <summary> Unique id for the purchase. </summary>
        [MetaMember(2)] public string   PurchaseId          { get; private set; }
        /// <summary> Approximate dollar value of the purchase. </summary>
        [MetaMember(3)] public float    DollarValue         { get; private set; }
        /// <summary> Number of days since the player registered, for tracking daily cohort metrics. </summary>
        [MetaMember(4)] public int      DaysSinceRegistered { get; private set; }

        StatisticsEventPlayerPurchase() { }
        public StatisticsEventPlayerPurchase(MetaTime timestamp, EntityId playerId, string purchaseId, float dollarValue, int daysSinceRegistered) : base(timestamp)
        {
            if (dollarValue < 0.0f)
                throw new ArgumentException($"Dollar value must be positive, got {dollarValue}", nameof(dollarValue));
            PlayerId            = playerId;
            PurchaseId          = purchaseId;
            DollarValue         = dollarValue;
            DaysSinceRegistered = daysSinceRegistered;
        }
    }

    /// <summary>
    /// Event for tracking live metrics.
    /// </summary>
    [MetaSerializableDerived(4)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventLiveMetrics : StatisticsEventBase
    {
        public override string UniqueKey => Invariant($"live/{Timestamp.ToISO8601()}");

        /// <summary> Concurrent users at the current timestamp </summary>
        [MetaMember(1)] public int Ccu { get; private set; }

        StatisticsEventLiveMetrics() { }
        public StatisticsEventLiveMetrics(MetaTime timestamp, int ccu) : base(timestamp)
        {
            Ccu = ccu;
        }
    }

    /// <summary>
    /// Player session event. Used to track session amount and length.
    /// </summary>
    [MetaSerializableDerived(5)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventPlayerSession : StatisticsEventBase
    {
        public override string UniqueKey => Invariant($"session/{PlayerId}/{Timestamp.ToISO8601()}");

        [MetaMember(1)] public EntityId       PlayerId             { get; private set; }

        /// <summary>
        /// The approximate session length.
        /// </summary>
        [MetaMember(2)] public MetaDuration   SessionLengthApprox  { get; private set; }

        /// <summary>
        /// The <see cref="ClientPlatform"/> reported by the user.
        /// </summary>
        [MetaMember(3)] public ClientPlatform ClientPlatform       { get; private set; }

        /// <summary>
        /// Number of days since the player registered, for tracking daily cohort metrics.
        /// </summary>
        [MetaMember(4)] public int            DaysSinceRegistered  { get; private set; }

        StatisticsEventPlayerSession()
        {
        }

        public StatisticsEventPlayerSession(MetaTime timestamp, EntityId playerId, MetaDuration sessionLengthApprox, ClientPlatform clientPlatform,
            int daysSinceRegistered) : base(timestamp)
        {
            PlayerId            = playerId;
            SessionLengthApprox = sessionLengthApprox;
            ClientPlatform      = clientPlatform;
            DaysSinceRegistered = daysSinceRegistered;
        }
    }

    /// <summary>
    /// Event for tracking error message count.
    /// </summary>
    [MetaSerializableDerived(6)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventErrorMessageCount : StatisticsEventBase
    {
        public override string UniqueKey => Invariant($"errors/{ProxyId}/{Timestamp.ToISO8601()}");

        /// <summary> Number of errors logged since last report. </summary>
        [MetaMember(1)] public int Errors { get; private set; }

        /// <summary> EntityId of the stats collector proxy the errors were collected from. </summary>
        [MetaMember(2)] public EntityId ProxyId { get; private set; }
        StatisticsEventErrorMessageCount() { }
        public StatisticsEventErrorMessageCount(MetaTime timestamp, EntityId proxyId, int errors) : base(timestamp)
        {
            ProxyId = proxyId;
            Errors  = errors;
        }
    }

    /// <summary>
    /// Event for tracking incident report count.
    /// </summary>
    [MetaSerializableDerived(7)]
    [MetaReservedMembers(0, 100)]
    public class StatisticsEventIncidentReport : StatisticsEventBase
    {
        public override string UniqueKey => Invariant($"incident/{PlayerId}/{Timestamp.ToISO8601()}");

        /// <summary> ID of the player the incident is associated with. </summary>
        [MetaMember(1)] public EntityId PlayerId { get; private set; }
        /// <summary> Type of the incident. </summary>
        [MetaMember(2)] public string IncidentType { get; private set; }

        StatisticsEventIncidentReport() { }
        public StatisticsEventIncidentReport(MetaTime timestamp, EntityId playerId, string incidentType) : base(timestamp)
        {
            PlayerId = playerId;
            IncidentType = incidentType;
        }
    }

    // \todo refactor into non-static class for testability.
    public static class StatisticsEventWriter
    {
        static PersistedStatisticsEvent GeneratePersistedEvent(StatisticsEventBase ev)
        {
            return new PersistedStatisticsEvent
            {
                UniqueKey   = ev.UniqueKey ?? throw new NullReferenceException("UniqueKey must be set"),
                Timestamp   = ev.Timestamp.ToDateTime(),
                Payload     = MetaSerialization.SerializeTagged(ev, MetaSerializationFlags.IncludeAll, logicVersion: null),
            };
        }

        /// <summary>
        /// Write a bunch of events to the database. This method should not be used outside of testing purposes,
        /// due to the possibility of writing timestamps that are not monotonic.
        /// Use <see cref="WriteEventAsync(Func{MetaTime, StatisticsEventBase})"/> instead.
        /// </summary>
        /// <param name="ev"></param>
        public static async Task WriteEventsUnsafeAsync(IEnumerable<StatisticsEventBase> ev)
        {
            IEnumerable<PersistedStatisticsEvent> persisted = ev.Select(GeneratePersistedEvent);
            await MetaDatabase.Get(QueryPriority.Normal).MultiInsertOrIgnoreAsync<PersistedStatisticsEvent>(persisted);
        }

        /// <summary>
        /// Write a single event to the database using the time from <see cref="StatisticsEventClock"/>.
        /// This ensures that we don't write events that have a timestamp older than our read watermark.
        /// </summary>
        public static async Task WriteEventAsync(Func<MetaTime, StatisticsEventBase> createEventFunc)
        {
            // Acquire unique monotonic timestamp for this event (and release after write is complete)
            MetaTime timestamp = StatisticsEventClock.AcquireTimestampLock();
            try
            {
                // Create the event using the timestamp
                StatisticsEventBase ev = createEventFunc(timestamp);
                MetaDebug.Assert(ev.Timestamp == timestamp, "Generated event must use the given timestamp: expected={ExpectedTimestamp} used={UsedTimestamp}", timestamp, ev.Timestamp);

                // Write raw event
                // Persist the event with a wrapper
                PersistedStatisticsEvent persisted = GeneratePersistedEvent(ev);
                await MetaDatabase.Get(QueryPriority.Normal).InsertOrIgnoreAsync<PersistedStatisticsEvent>(persisted).ConfigureAwait(false);

                // Debug log
                // Serilog.Log.Debug("Write statistics event at {Timestamp}: {Event}", ev.Timestamp, PrettyPrint.Compact(ev));
            }
            finally
            {
                StatisticsEventClock.ReleaseTimestampLock(timestamp);
            }
        }
    }

    public static class StatsEventsMockDataGenerator
    {
        static void GeneratePlayerEvents(List<StatisticsEventBase> events, MetaTime startTime, EntityId playerId, int timeWindowDays)
        {
            MetaTime now = MetaTime.Now;
            int millisecondsPerDay = (int)MetaDuration.FromDays(1).Milliseconds;
            RandomPCG rng = RandomPCG.CreateNew();
            int playerCreateDayOffset = rng.NextInt(timeWindowDays);
            int numRemainingDays = timeWindowDays - playerCreateDayOffset;
            MetaTime createDate = startTime + MetaDuration.FromDays(playerCreateDayOffset);
            MetaTime createTime = createDate + MetaDuration.FromMilliseconds(rng.NextInt(millisecondsPerDay));
            int createDayNdx = DateOnly.FromDateTime(createTime.ToDateTime()).DayNumber;
            ClientPlatform playerClient = rng.NextInt(3) == 0 ? ClientPlatform.Android : ClientPlatform.iOS;

            // Generate player created event
            events.Add(new StatisticsEventPlayerCreated(createTime, playerId));

            // Generate player login events
            for (int remainingDay = 0; remainingDay < numRemainingDays; remainingDay++)
            {
                float weight = 5.0f / (float)(1.0f + remainingDay);
                int numLogins = (int)(rng.NextFloat() * weight + rng.NextFloat());
                for (int ndx = 0; ndx < numLogins; ndx++)
                {
                    MetaTime     loginTime           = createTime + MetaDuration.FromDays(remainingDay) + MetaDuration.FromMilliseconds(rng.NextInt(millisecondsPerDay));
                    MetaDuration sessionDuration     = MetaDuration.FromSeconds((long)(rng.NextFloat() * 360) + 30);
                    int          loginDayNdx         = DateOnly.FromDateTime(loginTime.ToDateTime()).DayNumber;
                    int          daysSinceRegistered = loginDayNdx - createDayNdx;
                    events.Add(new StatisticsEventPlayerLogin(loginTime, playerId, daysSinceRegistered));
                    events.Add(new StatisticsEventPlayerSession(loginTime + sessionDuration, playerId, sessionDuration, playerClient, daysSinceRegistered));
                }
            }

            // Generate purchase events (not correlated with logins)
            if (rng.NextInt(100) < 10)
            {
                int numPurchases = 1 + rng.NextInt(3);
                for (int ndx = 0; ndx < numPurchases; ndx++)
                {
                    // Make purchases randomly over the two weeks after registration (discard any that go into future)
                    MetaTime timestamp = createTime + MetaDuration.FromMilliseconds(rng.NextLong(MetaDuration.FromDays(14).Milliseconds));
                    if (timestamp < now)
                    {
                        string purchaseId = Guid.NewGuid().ToString();
                        float dollarValue = rng.NextInt(100) < 80 ? 1.99f : 19.99f;
                        int purchaseDayNdx = DateOnly.FromDateTime(timestamp.ToDateTime()).DayNumber;
                        int daysSinceRegistered = purchaseDayNdx - createDayNdx;
                        events.Add(new StatisticsEventPlayerPurchase(timestamp, playerId, purchaseId, dollarValue, daysSinceRegistered));
                    }
                }
            }
        }

        public static async Task GenerateDebugEventsAsync(int numPlayers, int timeWindowDays)
        {
            int numTasks = Math.Max(16, numPlayers / 1000);
            MetaTime startTime = MetaTime.Now - MetaDuration.FromDays(timeWindowDays);
            SemaphoreSlim semaphore = new SemaphoreSlim(4);
            int remainingTasks = numTasks;

            await Task.WhenAll(
                Enumerable.Range(0, numTasks)
                .Select(taskNdx =>
                {
                    return Task.Run(async () =>
                    {
                        // Generate all events in batch
                        List<StatisticsEventBase> events = new();
                        for (int playerNdx = taskNdx; playerNdx < numPlayers; playerNdx += numTasks)
                            GeneratePlayerEvents(events, startTime, EntityId.CreateRandom(EntityKindCore.Player), timeWindowDays);

                        await semaphore.WaitAsync();
                        // Write batched
                        int remaining = Interlocked.Decrement(ref remainingTasks);
                        Serilog.Log.Information("Writing {NumEvents} mock statistics events for task {TaskNdx}... ({Remaining} Remaining)", events.Count, taskNdx, remaining);
                        await StatisticsEventWriter.WriteEventsUnsafeAsync(events);

                        semaphore.Release();
                    });
                }));
        }
    }
}
