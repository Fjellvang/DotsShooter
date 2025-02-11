// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.League;
using Metaplay.Core.League.Player;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Metaplay.Core.Tasks;
using Metaplay.Server.Database;
using Metaplay.Server.League.InternalMessages;
using Metaplay.Server.League.Player.InternalMessages;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Prometheus;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Server.League
{
    /// <summary>
    /// ParticipantId to DivisionId association record.
    /// This table is read by participants to find out which division they belong to,
    /// and only written to by the league manager actor. This allows for easy lookup of division placement
    /// and migration between seasons without having to wake up all participant actors.
    /// </summary>
    [LeaguesEnabledCondition]
    [Table("LeagueParticipantDivisionAssociations")]
    [Index(nameof(DivisionId))]
    [Index(nameof(LeagueStateRevision))]
    [PrimaryKey(nameof(LeagueId), nameof(ParticipantId))]
    [NoPrimaryKey] // \todo: Actually has one, but it's a composite key. We don't really support that yet.
    public sealed class PersistedParticipantDivisionAssociation : IPersistedItem
    {
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string ParticipantId { get; set; }

        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string LeagueId { get; set; }

        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string DivisionId { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// The league state revision at the time of the latest update.
        /// This is used by the league manager to sync up division changes in case of an actor crash
        /// that would cause the league manager's state and the division associations getting out of sync.
        /// </summary>
        [Required]
        public int LeagueStateRevision { get; set; }

        PersistedParticipantDivisionAssociation() { }

        public PersistedParticipantDivisionAssociation(string participantId, string leagueId, string divisionId, DateTime updatedAt, int leagueStateRevision)
        {
            ParticipantId       = participantId;
            DivisionId          = divisionId;
            UpdatedAt           = updatedAt;
            LeagueId            = leagueId;
            LeagueStateRevision = leagueStateRevision;
        }
    }

    [LeaguesEnabledCondition]
    [Table("LeagueManagers")]
    public class PersistedLeagueManager : IPersistedEntity
    {
        [Key]
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string EntityId { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime PersistedAt { get; set; }

        public byte[] Payload { get; set; }

        [Required]
        public int SchemaVersion { get; set; }

        [Required]
        public bool IsFinal { get; set; }

        public PersistedLeagueManager() { }
    }

    public abstract class LeagueManagerConfigBase : PersistedEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindCloudCore.LeagueManager;
        public override Type              EntityActorType      => typeof(LeagueManagerActorBase<,,>);
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateMultiService(LeagueManagerRegistry.Instance.LeagueInfos.Count);
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(10);

        protected LeagueManagerConfigBase()
        {
            if (LeagueManagerRegistry.Instance.LeagueInfos.Count == 0)
                throw new InvalidOperationException("No leagues found in the league manager registry. Please add at least one.");
        }

        public override Type GetActorTypeForEntity(EntityId entityId)
        {
            if (LeagueManagerRegistry.Instance.LeagueManagerActorTypeCache.TryGetValue(entityId, out Type actorType))
                return actorType;

            throw new ArgumentOutOfRangeException(nameof(entityId), $"No league manager actor type found for entity id {entityId}");
        }

        public override Type GetPersistedPayloadType(EntityId entityId)
        {
            if (LeagueManagerRegistry.Instance.LeagueManagerPayloadTypeCache.TryGetValue(entityId, out Type payloadType))
                return payloadType;

            throw new ArgumentOutOfRangeException(nameof(entityId), $"No league manager persisted payload type found for entity id {entityId}");
        }
    }

    /// <summary>
    /// A rank-specific state object for the current season.
    /// </summary>
    [MetaSerializable]
    public class LeagueManagerCurrentSeasonRankState
    {
        [MetaMember(1)] public int NumDivisions    { get; set; }
        [MetaMember(2)] public int NumParticipants { get; set; }
        /// <summary>
        /// Number of reassigned participants.
        /// </summary>
        [MetaMember(3)] public int NumReassigned   { get; set; }
    }

    /// <summary>
    /// League state for the current season.
    /// </summary>
    [MetaSerializable]
    public class LeagueManagerCurrentSeasonState
    {
        [MetaMember(1)] public MetaTime                                  StartTime          { get; set; }
        [MetaMember(2)] public MetaTime                                  EndTime            { get; set; }
        [MetaMember(3)] public MetaTime                                  EndingSoonStartsAt { get; set; }
        [MetaMember(4)] public int                                       SeasonId           { get; set; }
        [MetaMember(5)] public List<LeagueManagerCurrentSeasonRankState> Ranks              { get; set; }
        [MetaMember(6)] public bool                                      MigrationComplete  { get; set; }
        [MetaMember(7)] public int                                       NewParticipants    { get; set; }
        /// <summary>
        /// True if the season was forcibly started early by an administrator.
        /// </summary>
        [MetaMember(8)] public bool                                      StartedEarly       { get; set; }
        /// <summary>
        /// True if the season was forcibly ended early by an administrator.
        /// </summary>
        [MetaMember(9)] public bool                                      EndedEarly         { get; set; }
    }

    /// <summary>
    /// A rank-specific state for a historic season.
    /// </summary>
    [MetaSerializable]
    public class LeagueManagerHistoricSeasonRankState
    {
        [MetaMember(1)] public int NumDivisions    { get; set; }
        [MetaMember(2)] public int NumParticipants { get; set; }
        [MetaMember(3)] public int NumPromotions   { get; set; }
        [MetaMember(4)] public int NumDemotions    { get; set; }
        [MetaMember(5)] public int NumDropped      { get; set; }
        [MetaMember(7)] public int NumReassigned   { get; set; }

        [MetaMember(6)] public LeagueRankDetails RankDetails  { get; set; }

    }

    /// <summary>
    /// League state for a historic season.
    /// </summary>
    [MetaSerializable]
    public class LeagueManagerHistoricSeasonState
    {
        [MetaMember(1)] public MetaTime                                   StartTime           { get; set; }
        [MetaMember(2)] public MetaTime                                   EndTime             { get; set; }
        [MetaMember(3)] public int                                        SeasonId            { get; set; }
        [MetaMember(4)] public List<LeagueManagerHistoricSeasonRankState> Ranks               { get; set; }
        [MetaMember(5)] public int                                        TotalParticipants   { get; set; }
        [MetaMember(6)] public int                                        NewParticipants     { get; set; }
        [MetaMember(7)] public int                                        DroppedParticipants { get; set; }
        [MetaMember(8)] public LeagueSeasonDetails                        SeasonDetails       { get; set; }
        /// <summary>
        /// True if the season was forcibly started early by an administrator.
        /// </summary>
        [MetaMember(9)]  public bool                                      StartedEarly        { get; set; }
        /// <summary>
        /// True if the season was forcibly ended early by an administrator.
        /// </summary>
        [MetaMember(10)] public bool                                      EndedEarly          { get; set; }
    }

    /// <summary>
    /// A state object for the league manager.
    /// </summary>
    [MetaSerializable]
    [MetaReservedMembers(1, 100)]
    public abstract class LeagueManagerActorStateBase : ISchemaMigratable
    {
        [MetaMember(1)] public LeagueManagerCurrentSeasonState        CurrentSeason   { get; set; }
        [MetaMember(2)] public List<LeagueManagerHistoricSeasonState> HistoricSeasons { get; set; }

        /// <summary>
        /// Starts at 0 and is incremented every time the league manager state is persisted. This is used to ensure that
        /// the league manager's state is kept up-to-date with the division associations even if the league manager crashes.
        /// </summary>
        [MetaMember(3)] public int StateRevision   { get; set; } = -1; // Set to -1 to force an update on existing leagues.

        /// <summary>
        /// Division participant counts for the current season by rank. This is used to determine which divisions are full.
        /// The counts are compressed into a byte array to reduce the amount of data that needs to be persisted,
        /// and to avoid the persisted list size limit in the serializer (16k).
        /// </summary>
        [JsonIgnore]
        [MetaMember(4)] public List<PersistedDivisionCounts> CurrentSeasonDivisionParticipantCounts { get; set; }
    }

    /// <summary>
    /// The league schedule. This is turned into a <see cref="MetaRecurringCalendarSchedule"/> that will be used to track the
    /// season timings.
    /// </summary>
    [MetaSerializable]
    public struct LeagueSeasonCycleSchedule
    {
        [MetaMember(1)] public MetaCalendarDateTime StartDate  { get; private set; }
        [MetaMember(2)] public MetaCalendarPeriod   Recurrence { get; private set; }
        [MetaMember(3)] public MetaCalendarPeriod   RestPeriod { get; private set; }
        [MetaMember(4)] public MetaCalendarPeriod   EndingSoon { get; private set; }

        public LeagueSeasonCycleSchedule(MetaCalendarDateTime startDate, MetaCalendarPeriod recurrence, MetaCalendarPeriod restPeriod, MetaCalendarPeriod endingSoon)
        {
            StartDate  = startDate;
            Recurrence = recurrence;
            RestPeriod = restPeriod;
            EndingSoon = endingSoon;
        }

        public readonly MetaRecurringCalendarSchedule ToCalendarSchedule()
        {
            MetaRecurringCalendarSchedule schedule = new MetaRecurringCalendarSchedule(
                timeMode: MetaScheduleTimeMode.Utc,
                start: StartDate,
                duration: Recurrence - RestPeriod,
                endingSoon: EndingSoon,
                preview: RestPeriod,
                review: new MetaCalendarPeriod(),
                recurrence: Recurrence,
                numRepeats: null);

            return schedule;
        }

        public readonly bool IsValid()
        {
            if (StartDate.Year == 0)
                return false;
            if (Recurrence.IsNone)
                return false;
            if (RestPeriod >= Recurrence)
                return false;
            if (EndingSoon >= Recurrence - RestPeriod)
                return false;

            return true;
        }
    }

    [LeaguesEnabledCondition]
    public abstract class LeagueManagerOptionsBase : RuntimeOptionsBase
    {
        [MetaDescription("Whether the leagues are enabled or not.")]
        public bool Enabled { get; protected set; } = true;
        [MetaDescription("The batch size for season migration. Used when fetching players and writing new placements.")]
        public int SeasonMigrationBatchSize { get; protected set; } = 1024;
        [MetaDescription("The start date of the first season. Leagues will not run before this date.")]
        public DateTime SeasonCycleStartDate { get; protected set; } = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        [MetaDescription("Season length that includes the rest period. Real start and end times are calculated for every season separately taking into account the rest period and different month lengths and such.")]
        public MetaCalendarPeriod SeasonCycleRecurrence { get; protected set; } = new MetaCalendarPeriod(0, 1, 0, 0, 0, 0);
        [MetaDescription("The rest period between seasons. The season migration job will run during this time.")]
        public MetaCalendarPeriod SeasonCycleRestPeriod { get; protected set; } = new MetaCalendarPeriod(0, 0, 1, 0, 0, 0);
        [MetaDescription("A warning period before the season ends, that shows that it will be ending soon.")]
        public MetaCalendarPeriod SeasonCycleEndingSoonPeriod { get; protected set; } = new MetaCalendarPeriod(0, 0, 2, 0, 0, 0);
        [MetaDescription("If this value is set, the leagues will use a custom league schedule instead of the recurring schedule defined in RuntimeOptions.")]
        public bool UseCustomLeagueSchedule { get; protected set; } = false;
        [MetaDescription("A random delay in milliseconds to use when concluding divisions.")]
        public int ConcludeSeasonMaxDelayMilliseconds { get; protected set; } = 30000; // 30 seconds by default
        [MetaDescription("If this value is set, the league manager can refill existing divisions where some participants have left.")]
        public bool AllowDivisionBackFill {get; protected set; } = true;
    }

    [MetaSerializable]
    public enum LeagueRankUpMethod
    {
        /// <summary>
        /// Use when promotions and demotions can be calculated locally from the data provided in <see cref="IDivisionParticipantConclusionResult"/>.
        /// For example, if top 3 participants are promoted and bottom 3 demoted, the local strategy could be used as the system does not look into other divisions on the same rank.
        /// </summary>
        Local,
        /// <summary>
        /// Use when promotion and demotion logic requires looking at all players from one or more ranks.
        /// All participants in ranks with the Global rank up method will be handled in a single pass.
        /// This method is much more expensive, so use sparingly.
        /// </summary>
        Global,
    }

    [MetaSerializable]
    public struct LeagueRankDetails
    {
        /// <summary>
        /// The display name of the rank.
        /// </summary>
        [MetaMember(1)]
        public string DisplayName { get; private set; }

        /// <summary>
        /// The description of the rank.
        /// </summary>
        [MetaMember(2)]
        public string Description { get; private set; }

        /// <summary>
        /// The desired participant count for divisions in this rank.
        /// </summary>
        [MetaMember(3)]
        public int DivisionParticipantCount { get; private set; }

        public LeagueRankDetails(string displayName, string description, int participantCount)
        {
            DisplayName              = displayName;
            Description              = description;
            DivisionParticipantCount = participantCount;
        }
    }

    /// <summary>
    /// A struct defining a rank-up strategy for a rank in a league.
    /// This is used by the league manager to change the behaviour of the default rank-up algorithm when
    /// migrating participants between seasons.
    /// </summary>
    public struct LeagueRankUpStrategy
    {
        /// <summary>
        /// This bool prevents the league manager from creating more than one division for this rank.
        /// This may result in a huge division to be created if you're not careful.
        /// </summary>
        public bool IsSingleDivision { get; private set; }

        // \todo [nomi] Make this work.
        /// <summary>
        /// This bool makes the league manager prefer filling divisions over their desired participant count rather than creating a half-filled division for the last one.
        /// <para>TODO. This feature is not implemented yet.</para>
        /// </summary>
        public bool PreferNonEmptyDivisions { get; private set; }

        /// <summary>
        /// The rank-up algorithm to use for this rank.
        /// </summary>
        public LeagueRankUpMethod RankUpMethod { get; private set; }

        public LeagueRankUpStrategy(bool isSingleDivision = false, bool preferNonEmptyDivisions = false, LeagueRankUpMethod rankUpMethod = LeagueRankUpMethod.Local)
        {
            IsSingleDivision        = isSingleDivision;
            PreferNonEmptyDivisions = preferNonEmptyDivisions;
            RankUpMethod            = rankUpMethod;
        }
    }

    [MetaSerializable]
    public struct LeagueDetails
    {
        /// <summary>
        /// A display name for the league to show in the dashboard.
        /// </summary>
        [MetaMember(1)] public string LeagueDisplayName { get; private set; }
        /// <summary>
        /// A description for the league to show in the dashboard.
        /// </summary>
        [MetaMember(2)] public string LeagueDescription { get; private set; }

        public LeagueDetails(string leagueDisplayName, string leagueDescription)
        {
            LeagueDisplayName = leagueDisplayName;
            LeagueDescription = leagueDescription;
        }
    }

    [MetaSerializable]
    public struct LeagueSeasonDetails
    {
        /// <summary>
        /// A name for a single season within a league.
        /// </summary>
        [MetaMember(1)] public string SeasonDisplayName { get; private set; }
        /// <summary>
        /// A description for a season within a league.
        /// </summary>
        [MetaMember(2)] public string SeasonDescription { get; private set; }
        /// <summary>
        /// The number of ranks in this season.
        /// </summary>
        [MetaMember(3)] public int NumRanks{ get; private set; }

        public LeagueSeasonDetails(string seasonDisplayName, string seasonDescription, int numRanks)
        {
            SeasonDisplayName = seasonDisplayName;
            SeasonDescription = seasonDescription;
            NumRanks          = numRanks;
        }
    }

    public struct GlobalRankUpParticipantData
    {
        public int                                  CurrentRank      { get; private set; }
        public IDivisionParticipantConclusionResult ConclusionResult { get; private set; }

        public GlobalRankUpParticipantData(int currentRank, IDivisionParticipantConclusionResult conclusionResult) : this()
        {
            CurrentRank      = currentRank;
            ConclusionResult = conclusionResult;
        }
    }

    [LeaguesEnabledCondition]
    public abstract class LeagueManagerRegistry : IMetaIntegrationSingleton<LeagueManagerRegistry>
    {
        public class LeagueInfo
        {
            /// <summary>
            /// The entityId of the league manager.
            /// </summary>
            public EntityId LeagueManagerId { get; private set; }
            /// <summary>
            /// The client slot used for this league.
            /// </summary>
            public ClientSlot ClientSlot { get; private set; }
            /// <summary>
            /// The entity kind of the participant. Eg. Player or Guild.
            /// </summary>
            public EntityKind ParticipantKind { get; private set; }
            /// <summary>
            /// The type of the league manager actor deriving from <see cref="LeagueManagerActorBase{TState, TPersistedDivision, TOptions}"/>.
            /// </summary>
            public Type ManagerActorType { get; private set; }
            /// <summary>
            /// The type of the options object deriving from <see cref="LeagueManagerOptionsBase"/>.
            /// </summary>
            public Type OptionsType { get; private set; }
            /// <summary>
            /// The type of the division actor deriving from <see cref="DivisionActorBase{TModel, TServerModel, TPersisted, TOptions>"/>.
            /// </summary>
            public Type DivisionActorType { get; private set; }

            public LeagueInfo(EntityId leagueManagerId, ClientSlot clientSlot, EntityKind participantKind, Type managerActorType,
                Type optionsType,
                Type divisionActorType)
            {
                LeagueManagerId   = leagueManagerId;
                ClientSlot        = clientSlot;
                ParticipantKind   = participantKind;
                ManagerActorType  = managerActorType;
                OptionsType       = optionsType;
                DivisionActorType = divisionActorType;
            }

            LeagueInfo() { }
        }

        public abstract IReadOnlyList<LeagueInfo> LeagueInfos { get; }

        public IReadOnlyList<EntityId> LeagueManagerIds => LeagueInfos.Select(x => x.LeagueManagerId).ToList();

        public static LeagueManagerRegistry Instance => IntegrationRegistry.Get<LeagueManagerRegistry>();


        internal readonly ReadOnlyDictionary<EntityId, Type> LeagueManagerActorTypeCache;
        internal readonly ReadOnlyDictionary<EntityId, Type> LeagueManagerPayloadTypeCache;

        internal readonly ReadOnlyDictionary<int, Type> DivisionActorTypeCache;
        internal readonly ReadOnlyDictionary<int, Type> DivisionActorPayloadTypeCache;

        protected LeagueManagerRegistry()
        {
            Dictionary<EntityId, Type> leagueActorTypeCache   = new Dictionary<EntityId, Type>();
            Dictionary<EntityId, Type> leaguePayloadTypeCache = new Dictionary<EntityId, Type>();

            Dictionary<int, Type> divisionActorTypeCache   = new Dictionary<int, Type>();
            Dictionary<int, Type> divisionPayloadTypeCache = new Dictionary<int, Type>();

            foreach (LeagueInfo leagueInfo in LeagueInfos)
            {
                leagueActorTypeCache[leagueInfo.LeagueManagerId]   = leagueInfo.ManagerActorType;
                leaguePayloadTypeCache[leagueInfo.LeagueManagerId] = leagueInfo.ManagerActorType.GetGenericAncestorTypeArguments(typeof(LeagueManagerActorBase<,,>))[0];

                divisionActorTypeCache[(int)leagueInfo.LeagueManagerId.Value]   = leagueInfo.DivisionActorType;
                divisionPayloadTypeCache[(int)leagueInfo.LeagueManagerId.Value] = leagueInfo.DivisionActorType.GetGenericAncestorTypeArguments(typeof(DivisionActorBase<,,,>))[0];
            }

            LeagueManagerActorTypeCache   = new ReadOnlyDictionary<EntityId, Type>(leagueActorTypeCache);
            LeagueManagerPayloadTypeCache = new ReadOnlyDictionary<EntityId, Type>(leaguePayloadTypeCache);
            DivisionActorTypeCache        = new ReadOnlyDictionary<int, Type>(divisionActorTypeCache);
            DivisionActorPayloadTypeCache  = new ReadOnlyDictionary<int, Type>(divisionPayloadTypeCache);
        }

        public LeagueInfo TryGetLeagueInfo(EntityId leagueManagerId)
        {
            return LeagueInfos.FirstOrDefault(x => x.LeagueManagerId == leagueManagerId);
        }
    }

    internal class MissingLeagueManagerRegistry : LeagueManagerRegistry
    {
        /// <inheritdoc />
        public override IReadOnlyList<LeagueInfo> LeagueInfos => new List<LeagueInfo>();
    }

    public abstract class LeagueManagerActorBase<TState, TPersistedDivision, TOptions> : PersistedEntityActor<PersistedLeagueManager, TState>
        where TPersistedDivision : PersistedDivisionBase, new()
        where TState : LeagueManagerActorStateBase
        where TOptions : LeagueManagerOptionsBase
    {
        static readonly Histogram c_LeagueJoinRequestLatencies = Prometheus.Metrics.CreateHistogram("metaplay_leagues_join_request_duration", "The latency of replying to a league join request.", new Prometheus.HistogramConfiguration
        {
            Buckets    = Metaplay.Cloud.Metrics.Defaults.LatencyDurationBuckets,
            LabelNames = new[] { "league", "newdivision" },
        });

        class CheckSeasonChangeCommand
        {
            public static CheckSeasonChangeCommand Instance { get; } = new CheckSeasonChangeCommand();
            CheckSeasonChangeCommand() { }
        }

        protected internal struct ParticipantJoinRequestResult
        {
            public bool CanJoin      { get; set; }
            public int  StartingRank { get; set; }

            public ParticipantJoinRequestResult(bool canJoin, int startingRank)
            {
                CanJoin      = canJoin;
                StartingRank = startingRank;
            }
        }

        protected struct ParticipantSeasonPlacementResult
        {
            public bool RemoveFromNextSeason { get; set; }
            public int  NextSeasonRank       { get; set; }

            public ParticipantSeasonPlacementResult(bool removeFromNextSeason, int nextSeasonRank)
            {
                RemoveFromNextSeason = removeFromNextSeason;
                NextSeasonRank       = nextSeasonRank;
            }

            public static ParticipantSeasonPlacementResult ForRemoval()
            {
                return new ParticipantSeasonPlacementResult(true, 0);
            }

            public static ParticipantSeasonPlacementResult ForRank(int rank)
            {
                return new ParticipantSeasonPlacementResult(false, rank);
            }
        }

        protected struct ParticipantDivisionPair
        {
            public EntityId      ParticipantId;
            public DivisionIndex Division;

            public ParticipantDivisionPair(EntityId participantId, DivisionIndex division)
            {
                this.ParticipantId = participantId;
                this.Division      = division;
            }
        }

        protected class SeasonMigrationResult
        {
            public class SeasonMigrationRankResult
            {
                public int NumParticipants;
                public int NumDropped;
                public int NumPromoted;
                public int NumDemoted;
            }

            public SeasonMigrationRankResult[]   LastSeasonRankResults;
            public int                           LastSeasonTotalParticipants;
            public int                           ParticipantsMigrated;
            public int                           ParticipantsDropped;
            public List<ParticipantDivisionPair> ParticipantAssignments;

            public SeasonMigrationResult() { }

            public SeasonMigrationResult(int numRanks)
            {
                LastSeasonRankResults = new SeasonMigrationRankResult[numRanks];
                for (int i = 0; i < numRanks; i++)
                    LastSeasonRankResults[i] = new SeasonMigrationRankResult();
            }
        }

        protected class SeasonMigrationDivisionsResult
        {
            /// <summary>
            /// The list of new, created divisions.
            /// </summary>
            public List<DivisionIndex> CreatedDivisions;

            /// <summary>
            /// The result list of participant to division mapping.
            /// </summary>
            public List<ParticipantDivisionPair> DivisionPairs;

            /// <summary>
            /// A dictionary of divisionId to a list of participants. Used in <see cref="InitializeDivisionsAndTransferAvatars"/>.
            /// </summary>
            public Dictionary<DivisionIndex, List<EntityId>> ParticipantsPerDivision;

            public SeasonMigrationDivisionsResult()
            {
                CreatedDivisions        = new List<DivisionIndex>();
                DivisionPairs           = new List<ParticipantDivisionPair>();
                ParticipantsPerDivision = new Dictionary<DivisionIndex, List<EntityId>>();
            }
        }

        /// <inheritdoc />
        protected LeagueManagerActorBase(EntityId entityId) : base(entityId)
        {
            LeagueId = (int)entityId.Value;
        }

        protected override AutoShutdownPolicy ShutdownPolicy   => AutoShutdownPolicy.ShutdownNever();
        protected override TimeSpan           SnapshotInterval => TimeSpan.FromMinutes(5);

        /// <summary>
        /// Needs to be unique from all other league managers.
        /// </summary>
        protected int LeagueId { get; set; } = 0;

        protected abstract EntityKind ParticipantEntityKind { get; }

        protected MetaScheduleBase LeagueSchedule                    { get; private set; }
        protected bool             SeasonMigrationInProgress         { get; private set; }
        protected float            SeasonMigrationProgressEstimate   { get; private set; }
        protected string           SeasonMigrationProgressPhase      { get; private set; }
        protected string           SeasonMigrationError              { get; private set; }

        protected TOptions Options { get; private set; }
        protected TState   State   { get; private set; }

        protected int[] CurrentSeasonRankParticipantCounts => State.CurrentSeason != null ?
            Enumerable.Range(0, State.CurrentSeason.Ranks.Count).Select(r => GetRankDetails(r, State.CurrentSeason.SeasonId).DivisionParticipantCount).ToArray() :
            Enumerable.Range(0, GetSeasonDetails(0).NumRanks).Select(r => GetRankDetails(r, 0).DivisionParticipantCount).ToArray();

        /// <summary>
        /// The effective participant counts used for assigning new participants to divisions.
        /// These may not be 100% accurate if division backfilling option is turned off, or
        /// some unexpected server event causes data loss.
        /// </summary>
        protected LeagueDivisionParticipantCountState _participantCountState;

        readonly RandomPCG _random = RandomPCG.CreateNew();

        /// <inheritdoc />
        protected override async Task Initialize()
        {
            // Try to fetch from database & restore from it (if exists)
            PersistedLeagueManager persisted = await MetaDatabase.Get().TryGetAsync<PersistedLeagueManager>(_entityId.ToString());

            await InitializePersisted(persisted);
        }

        protected override Task<TState> RestoreFromPersisted(PersistedLeagueManager persisted)
        {
            // Deserialize actual state
            TState state = DeserializePersistedPayload<TState>(persisted.Payload, resolver: null, logicVersion: null);

            return Task.FromResult(state);
        }

        protected override async Task PostLoad(TState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            // Set actor state to payload
            State = payload;

            Options = RuntimeOptionsRegistry.Instance.GetCurrent<TOptions>();

            State.HistoricSeasons ??= new List<LeagueManagerHistoricSeasonState>();

            if (Options.SeasonCycleStartDate.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("Season cycle start time only supports UTC DateTime.");

            if (!Options.Enabled)
                _log.Warning("League {LeagueId} is not enabled.", _entityId);

            if (State.CurrentSeason != null)
            {
                if (State.CurrentSeasonDivisionParticipantCounts != null)
                    _participantCountState = new LeagueDivisionParticipantCountState(CurrentSeasonRankParticipantCounts, State.CurrentSeasonDivisionParticipantCounts);
                else
                    _participantCountState = new LeagueDivisionParticipantCountState(CurrentSeasonRankParticipantCounts);

                if (State.StateRevision == -1) // Migrate old divisions
                    await MigrateDivisionState();
                else
                    await SynchronizeDivisionState();
            }
            else
                _participantCountState = new LeagueDivisionParticipantCountState(CurrentSeasonRankParticipantCounts);

            _participantCountState.Validate();

            State.StateRevision++;

            if (Options.UseCustomLeagueSchedule)
            {
                _log.Info("Using custom league schedule. Overriding season cycle.");
                LeagueSchedule = GetCustomSchedule();

                if (LeagueSchedule != null && LeagueSchedule.TimeMode != MetaScheduleTimeMode.Utc)
                    throw new ArgumentException("Custom league schedule must use UTC time mode. Leagues will not run!");
            }
            else
            {
                LeagueSeasonCycleSchedule cycle = new LeagueSeasonCycleSchedule(
                    MetaCalendarDateTime.FromDateTime(Options.SeasonCycleStartDate),
                    Options.SeasonCycleRecurrence,
                    Options.SeasonCycleRestPeriod,
                    Options.SeasonCycleEndingSoonPeriod);

                if (cycle.IsValid())
                    LeagueSchedule = cycle.ToCalendarSchedule();
                else
                    throw new ArgumentException("Configured season cycle is not valid. Leagues will not run.");
            }

            StartPeriodicTimer(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), CheckSeasonChangeCommand.Instance);
        }

        /// <inheritdoc />
        protected override async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            MetaTime now = MetaTime.Now;
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, CurrentSchemaVersion);

            // Update persisted participant counts.
            State.CurrentSeasonDivisionParticipantCounts = _participantCountState.ToPersisted();

            // Serialize and compress the state
            byte[] persistedPayload = SerializeToPersistedPayload(State, resolver: null, logicVersion: null);

            // Persist in database
            PersistedLeagueManager persisted = new PersistedLeagueManager();
            persisted.EntityId               = _entityId.ToString();
            persisted.PersistedAt            = now.ToDateTime();
            persisted.Payload                = persistedPayload;
            persisted.SchemaVersion          = CurrentSchemaVersion;
            persisted.IsFinal                = isFinal;

            if (isInitial)
                await MetaDatabase.Get(QueryPriority.Normal).InsertAsync(persisted).ConfigureAwait(false);
            else
                await MetaDatabase.Get(QueryPriority.Normal).UpdateAsync(persisted).ConfigureAwait(false);

            // Update state revision after persisting.
            State.StateRevision++;
        }

        [CommandHandler]
        async Task CheckSeasonChange(CheckSeasonChangeCommand _)
        {
            if (!Options.Enabled)
                return;

            if (Options.UseCustomLeagueSchedule)
            {
                // Update schedule.
                LeagueSchedule = GetCustomSchedule();

                if (LeagueSchedule != null && LeagueSchedule.TimeMode != MetaScheduleTimeMode.Utc)
                    throw new ArgumentException("Custom league schedule must use UTC time mode. Leagues will not run!");
            }

            MetaTime now = MetaTime.Now;

            if ((State.CurrentSeason == null || now > State.CurrentSeason.EndTime) && LeagueSchedule != null)
            {
                MetaScheduleOccasion? nextOccasion = LeagueSchedule.TryGetCurrentOrNextEnabledOccasion(
                    new PlayerLocalTime(now, MetaDuration.Zero));

                bool currentMigrationFinished = (State.CurrentSeason == null || State.CurrentSeason.MigrationComplete)
                    && !SeasonMigrationInProgress;

                bool overlapsWithCurrentSeason = State.CurrentSeason != null && nextOccasion.HasValue &&
                                             nextOccasion.Value.EnabledRange.Start < State.CurrentSeason.EndTime;

                if (nextOccasion.HasValue && nextOccasion.Value.IsVisibleAt(now) &&
                    currentMigrationFinished && !overlapsWithCurrentSeason)
                {
                    await ChangeSeason(nextOccasion.Value);
                }
            }

            if (State.CurrentSeason != null && !State.CurrentSeason.MigrationComplete && !SeasonMigrationInProgress)
            {
                StartSeasonMigration();
            }
        }

        protected virtual async Task ChangeSeason(MetaScheduleOccasion newSeasonOccasion)
        {
            int nextSeasonId = 0;

            if (State.CurrentSeason != null)
            {
                nextSeasonId = State.CurrentSeason.SeasonId + 1;
                LeagueManagerHistoricSeasonState historicState = new LeagueManagerHistoricSeasonState();
                historicState.Ranks = new List<LeagueManagerHistoricSeasonRankState>(State.CurrentSeason.Ranks.Count);

                int rankI = 0;
                foreach (LeagueManagerCurrentSeasonRankState rankState in State.CurrentSeason.Ranks)
                {
                    historicState.Ranks.Add(
                        new LeagueManagerHistoricSeasonRankState()
                        {
                            NumDivisions    = rankState.NumDivisions,
                            NumParticipants = 0,
                            RankDetails     = GetRankDetails(rankI, State.CurrentSeason.SeasonId),
                            NumReassigned   = rankState.NumReassigned,
                        });
                    rankI++;
                }

                historicState.StartTime       = State.CurrentSeason.StartTime;
                historicState.EndTime         = State.CurrentSeason.EndTime;
                historicState.SeasonId        = State.CurrentSeason.SeasonId;
                historicState.NewParticipants = State.CurrentSeason.NewParticipants;
                historicState.SeasonDetails   = GetSeasonDetails(State.CurrentSeason.SeasonId);
                historicState.StartedEarly    = State.CurrentSeason.StartedEarly;
                historicState.EndedEarly      = State.CurrentSeason.EndedEarly;

                State.HistoricSeasons.Add(historicState);
            }

            int newNumRanks = GetSeasonDetails(nextSeasonId).NumRanks;

            LeagueManagerCurrentSeasonState seasonState = new LeagueManagerCurrentSeasonState();

            seasonState.SeasonId           = nextSeasonId;
            seasonState.StartTime          = newSeasonOccasion.EnabledRange.Start;
            seasonState.EndTime            = newSeasonOccasion.EnabledRange.End;
            seasonState.EndingSoonStartsAt = newSeasonOccasion.EndingSoonStartsAt;
            seasonState.Ranks              = new List<LeagueManagerCurrentSeasonRankState>(newNumRanks);
            seasonState.MigrationComplete  = false;

            for (int i = 0; i < newNumRanks; i++)
                seasonState.Ranks.Add(new LeagueManagerCurrentSeasonRankState());

            State.CurrentSeason = seasonState;

            _participantCountState = new LeagueDivisionParticipantCountState(CurrentSeasonRankParticipantCounts);

            await PersistStateIntermediate();

            StartSeasonMigration();
        }

        void StartSeasonMigration()
        {
            if (!Options.Enabled)
                return;
            if (State.CurrentSeason == null || State.CurrentSeason.MigrationComplete || SeasonMigrationInProgress)
                return;

            SeasonMigrationInProgress       = true;
            SeasonMigrationProgressEstimate = 0;

            _log.Debug("Starting season migration for season {Season}...", State.CurrentSeason.SeasonId);

            // Clear current season divisions before starting.
            foreach (LeagueManagerCurrentSeasonRankState currentSeasonRank in State.CurrentSeason.Ranks)
            {
                currentSeasonRank.NumParticipants = 0;
                currentSeasonRank.NumDivisions    = 0;
            }

            ContinueTaskOnActorContext(
                Task.Run(MigrateParticipantsToCurrentSeason),
                async (result) =>
                {
                    _log.Info("Season migration complete for {Result} participants. Dropped {Dropped} participants.", result.ParticipantsMigrated, result.ParticipantsDropped);

                    SeasonMigrationProgressEstimate = 1f;
                    SeasonMigrationProgressPhase    = null;

                    if (State.HistoricSeasons.Count > 0)
                    {
                        // Update historic season
                        LeagueManagerHistoricSeasonState historyState = State.HistoricSeasons[^1];
                        historyState.DroppedParticipants = result.ParticipantsDropped;
                        historyState.TotalParticipants   = result.LastSeasonTotalParticipants;
                        for (int i = 0; i < result.LastSeasonRankResults.Length; i++)
                        {
                            historyState.Ranks[i].NumParticipants = result.LastSeasonRankResults[i].NumParticipants;
                            historyState.Ranks[i].NumDropped      = result.LastSeasonRankResults[i].NumDropped;
                            historyState.Ranks[i].NumPromotions   = result.LastSeasonRankResults[i].NumPromoted;
                            historyState.Ranks[i].NumDemotions    = result.LastSeasonRankResults[i].NumDemoted;
                        }
                    }

                    // Update participant counts for current season.
                    if (result.ParticipantAssignments != null && result.ParticipantAssignments.Count > 0)
                    {
                        _participantCountState = new LeagueDivisionParticipantCountState(
                            CurrentSeasonRankParticipantCounts,
                            result.ParticipantAssignments.Select(x => x.Division));
                    }
                    else
                        _participantCountState = new LeagueDivisionParticipantCountState(CurrentSeasonRankParticipantCounts);

                    _participantCountState.Validate();

                    for (int r = 0; r < State.CurrentSeason.Ranks.Count; r++)
                    {
                        State.CurrentSeason.Ranks[r].NumParticipants = _participantCountState.CalculateRankParticipantCount(r);

                        for (int d = 0; d < State.CurrentSeason.Ranks[r].NumDivisions; d++)
                        {
                            // Call OnNewDivisionCreated for each division.
                            DivisionIndex div = new DivisionIndex(LeagueId, State.CurrentSeason.SeasonId, r, d);
                            OnNewDivisionCreated(div, true);
                        }
                    }

                    State.CurrentSeason.MigrationComplete = true;
                    await PersistStateIntermediate();

                    SeasonMigrationInProgress = false;
                    SeasonMigrationError      = null;
                },
                (exception) =>
                {
                    _log.Error(exception, "Season migration failed.");
                    SeasonMigrationInProgress       = false;
                    SeasonMigrationProgressEstimate = 0f;
                    SeasonMigrationProgressPhase    = "Error";
                    SeasonMigrationError            = $"{exception.GetType().Name}: {exception.Message}";
                });
        }

        // Not run in actor context.
        /// <summary>
        /// <para>
        /// The main migration method for handling season-to-season migration of participants.
        /// This method should:
        /// </para>
        /// <list type="number">
        /// <item>Decide where participants are placed for the next season.</item>
        /// <item>Create new divisions and transfer avatars from last season.</item>
        /// <item>Write association entries to the database.</item>
        /// <item>Clear out any old association data from participants who were removed.</item>
        /// <item>Populate the <see cref="SeasonMigrationResult"/> returned to the base actor.</item>
        /// </list>
        /// <para>IMPORTANT: This method is run outside of actor context, so any state mutation should be done by calling
        /// <see cref="EntityActor.ExecuteOnActorContextAsync(System.Action)"/>. Database mutation and entity asks are allowed inside this method.</para>
        /// </summary>
        protected virtual async Task<SeasonMigrationResult> MigrateParticipantsToCurrentSeason()
        {
            int lastSeason    = -1;
            int currentSeason = State.CurrentSeason.SeasonId;

            SeasonMigrationResult migrationResult = new SeasonMigrationResult();

            if (State.HistoricSeasons.Count > 0)
            {
                SeasonMigrationProgressPhase = "Calculating placements";

                LeagueManagerHistoricSeasonState lastSeasonState = State.HistoricSeasons[^1];
                lastSeason = lastSeasonState.SeasonId;
                int lastSeasonRankCount = lastSeasonState.Ranks.Count;
                int totalNumDivisions   = lastSeasonState.Ranks.Sum(s => s.NumDivisions);
                int divisionProgress    = 0;

                migrationResult = new SeasonMigrationResult(lastSeasonRankCount);

                MetaDatabase db = MetaDatabase.Get(QueryPriority.Low);

                List<ParticipantDivisionPair> leagueParticipantNewDivisions = new List<ParticipantDivisionPair>();

                int numRanks = GetSeasonDetails(currentSeason).NumRanks;

                (int division, int participants)[] rankDivisionFillValues = new (int division, int participants)[numRanks];

                // Migrate locally decidable divisions.
                for (int r = 0; r < lastSeasonRankCount; r++)
                {
                    LeagueRankUpMethod method = GetRankUpStrategy(r, lastSeason).RankUpMethod;

                    if (method == LeagueRankUpMethod.Local)
                    {
                        for (int d = 0; d < lastSeasonState.Ranks[r].NumDivisions; d++)
                        {
                            DivisionIndex div = new DivisionIndex(LeagueId, lastSeason, r, d);

                            IEnumerable<ParticipantDivisionPair> results = await MigrateSingleDivisionToNewSeason(div, currentSeason,  rankDivisionFillValues, migrationResult, numRanks);
                            leagueParticipantNewDivisions.AddRange(results);
                            divisionProgress++;
                            SeasonMigrationProgressEstimate = divisionProgress / (float)totalNumDivisions * 0.5f;
                        }
                    }
                }

                SeasonMigrationProgressPhase = "Calculating global placements";

                // Migrate the rest of the divisions, i.e. globally decidable divisions
                IEnumerable<ParticipantDivisionPair> globalResults = await MigrateGlobalMethodRanksToNewSeason(lastSeasonState, currentSeason, rankDivisionFillValues, migrationResult, numRanks);
                leagueParticipantNewDivisions.AddRange(globalResults);

                int totalToWrite  = leagueParticipantNewDivisions.Count;
                int writeProgress = 0;
                SeasonMigrationProgressPhase = "Updating association data";

                // Write out association data in chunks.
                // Note: This is done outside of actor context, so the state revision might be out of sync.
                // If the actor crashes, the migration will restart so this should not be an issue.
                IEnumerable<PersistedParticipantDivisionAssociation[]> chunksToWrite = leagueParticipantNewDivisions.Select(
                    (participant) => new PersistedParticipantDivisionAssociation(
                        participantId: participant.ParticipantId.ToString(),
                        leagueId: _entityId.ToString(),
                        divisionId: participant.Division.ToEntityId().ToString(),
                        MetaTime.Now.ToDateTime(), State.StateRevision)).Chunk(Options.SeasonMigrationBatchSize);

                foreach (PersistedParticipantDivisionAssociation[] divisionAssociations in chunksToWrite)
                {
                    await db.MultiInsertOrReplaceAsync(divisionAssociations);
                    writeProgress                   += divisionAssociations.Length;
                    SeasonMigrationProgressEstimate =  writeProgress / (float)totalToWrite * 0.25f + 0.5f;
                }

                migrationResult.ParticipantsMigrated   = leagueParticipantNewDivisions.Count;
                migrationResult.ParticipantAssignments = leagueParticipantNewDivisions;
            }

            SeasonMigrationProgressPhase = "Cleaning up";

            // Wait for read replication to catch up.
            // \todo: The database modifications should be done in a transaction, so this delay would be unnecessary.
            await Task.Delay(1000);

            migrationResult.ParticipantsDropped = await RemoveOldParticipantDivisionAssociations(currentSeason);

            return migrationResult;
        }

        protected void UpdateSeasonMigrationProgress(float progress, string phase = null)
        {
            SeasonMigrationProgressEstimate = progress;
            if(phase != null)
                SeasonMigrationProgressPhase = phase;
        }

        /// <summary>
        /// Single division migration func. Will run outside of actor context.
        /// </summary>
        /// <param name="oldDivision"></param>
        /// <param name="newSeason"></param>
        /// <param name="rankDivisionFillValues">The largest index unfilled division per rank and its current participant count.</param>
        /// <param name="migrationResult"></param>
        /// <param name="numRanks"></param>
        /// <returns></returns>
        async Task<IEnumerable<ParticipantDivisionPair>> MigrateSingleDivisionToNewSeason(DivisionIndex oldDivision, int newSeason, (int division, int participants)[] rankDivisionFillValues, SeasonMigrationResult migrationResult, int numRanks)
        {
            EntityId     oldDivisionEntity = oldDivision.ToEntityId();
            MetaDatabase db                = MetaDatabase.Get();

            Dictionary<EntityId, IDivisionParticipantConclusionResult> conclusionResults = await TryFetchConclusionResultsForDivisionParticipants(oldDivision.ToEntityId());

            if (conclusionResults == null)
                throw new InvalidOperationException($"Failed to fetch participant conclusion results for division {oldDivision}");

            SeasonMigrationDivisionsResult divisionsResult = new SeasonMigrationDivisionsResult();

            // Handle promotion and demotion for each participant.
            foreach ((EntityId participant, IDivisionParticipantConclusionResult result) in conclusionResults)
            {
                PersistedParticipantDivisionAssociation oldAssociation   = await db.TryGetParticipantDivisionAssociation(_entityId, participant);
                EntityId                                oldDivId         = oldAssociation != null ? EntityId.ParseFromString(oldAssociation.DivisionId) : EntityId.None;
                DivisionIndex                           oldDivisionIndex = oldAssociation != null ? DivisionIndex.FromEntityId(oldDivId) : default;

                // Ignore discrepancies in association data if the actor crashed during migration.
                bool isCrashRecovery = oldAssociation != null && DivisionIndex.FromEntityId(oldDivId).Season == newSeason;
                if (oldDivId != oldDivisionEntity && !isCrashRecovery)
                {
                    _log.Warning("Found a discrepancy in participant {Participant} association and division data. Expected: {OldDivisionIndex}, but found in division: {Division}. Skipping...", participant, oldDivisionIndex, oldDivision);
                    continue;
                }

                migrationResult.LastSeasonRankResults[oldDivision.Rank].NumParticipants++;
                migrationResult.LastSeasonTotalParticipants++;

                ParticipantSeasonPlacementResult nextSeasonPlacement = SolveLocalSeasonPlacement(oldDivision.Season, newSeason, oldDivision.Rank, result);

                HandleSeasonPlacementImpl(
                    participant,
                    nextSeasonPlacement,
                    oldDivision.Rank,
                    newSeason,
                    numRanks,
                    rankDivisionFillValues,
                    migrationResult,
                    divisionsResult);
            }

            await ExecuteOnActorContextAsync(
                () => InitializeDivisionsAndTransferAvatars(divisionsResult, conclusionResults));

            return divisionsResult.DivisionPairs;
        }

        /// <summary>
        /// Migrate all ranks with <see cref="LeagueRankUpMethod.Global"/> to a new season. Will run outside of actor context.
        /// </summary>
        /// <param name="lastSeason"></param>
        /// <param name="newSeason"></param>
        /// <param name="rankDivisionFillValues">The largest unfilled division per rank and its current participant count.</param>
        /// <param name="migrationResult"></param>
        /// <param name="numRanks"></param>
        /// <returns></returns>
        async Task<IEnumerable<ParticipantDivisionPair>> MigrateGlobalMethodRanksToNewSeason(LeagueManagerHistoricSeasonState lastSeason, int newSeason, (int division, int participants)[] rankDivisionFillValues, SeasonMigrationResult migrationResult, int numRanks)
        {
            MetaDatabase db = MetaDatabase.Get();

            Dictionary<EntityId, GlobalRankUpParticipantData>          participantDatas     = new Dictionary<EntityId, GlobalRankUpParticipantData>();
            Dictionary<EntityId, IDivisionParticipantConclusionResult> allConclusionResults = new Dictionary<EntityId, IDivisionParticipantConclusionResult>();

            SeasonMigrationDivisionsResult divisionsResult = new SeasonMigrationDivisionsResult();

            for (int r = 0; r < lastSeason.Ranks.Count; r++)
            {
                if (GetRankUpStrategy(r, lastSeason.SeasonId).RankUpMethod == LeagueRankUpMethod.Global)
                {
                    for (int d = 0; d < lastSeason.Ranks[r].NumDivisions; d++)
                    {
                        DivisionIndex division       = new DivisionIndex(LeagueId, lastSeason.SeasonId, r, d);
                        EntityId      divisionEntity = division.ToEntityId();

                        Dictionary<EntityId, IDivisionParticipantConclusionResult> results = await TryFetchConclusionResultsForDivisionParticipants(divisionEntity);

                        if (results == null)
                            throw new InvalidOperationException($"Failed to fetch participant conclusion results for division {division}");

                        foreach ((EntityId participant, IDivisionParticipantConclusionResult value) in results)
                        {
                            PersistedParticipantDivisionAssociation oldAssociation   = await db.TryGetParticipantDivisionAssociation(_entityId, participant);
                            EntityId                                oldDivId         = oldAssociation != null ? EntityId.ParseFromString(oldAssociation.DivisionId) : EntityId.None;
                            DivisionIndex                           oldDivisionIndex = oldAssociation != null ? DivisionIndex.FromEntityId(oldDivId) : default;

                            // Ignore discrepancies in association data if the actor crashed during migration.
                            bool isCrashRecovery = oldAssociation != null && DivisionIndex.FromEntityId(oldDivId).Season == newSeason;
                            if (oldDivId != divisionEntity && !isCrashRecovery)
                            {
                                _log.Warning("Found a discrepancy in participant {Participant} association and division data. Expected: {OldDivisionIndex}, but found in division: {Division}. Skipping...", participant, oldDivisionIndex, division);
                                continue;
                            }

                            allConclusionResults.Add(participant, value);
                            participantDatas.Add(participant, new GlobalRankUpParticipantData(r, value));

                            migrationResult.LastSeasonRankResults[r].NumParticipants++;
                            migrationResult.LastSeasonTotalParticipants++;
                        }
                    }
                }
            }

            // Skip rest of work.
            if (allConclusionResults.Count == 0)
                return divisionsResult.DivisionPairs;

            Dictionary<EntityId, ParticipantSeasonPlacementResult> placements = SolveGlobalSeasonPlacement(lastSeason.SeasonId, newSeason, participantDatas);

            if (placements.Count != participantDatas.Count)
                throw new InvalidOperationException($"{nameof(SolveGlobalSeasonPlacement)} did not return a result for all given participants!");

            foreach ((EntityId participantId, ParticipantSeasonPlacementResult placement) in placements)
            {
                int oldRank = participantDatas[participantId].CurrentRank;

                HandleSeasonPlacementImpl(
                    participantId,
                    placement,
                    oldRank,
                    newSeason,
                    numRanks,
                    rankDivisionFillValues,
                    migrationResult,
                    divisionsResult);
            }

            await ExecuteOnActorContextAsync(
                () => InitializeDivisionsAndTransferAvatars(
                    divisionsResult,
                    allConclusionResults));

            return divisionsResult.DivisionPairs;
        }

        /// <summary>
        /// Handle <see cref="ParticipantSeasonPlacementResult"/> for a participant and update lists.
        /// Divisions are filled until the desired participant count in <see cref="LeagueManagerOptionsBase"/> is reached.
        /// </summary>
        /// <param name="participant">The participant's id.</param>
        /// <param name="nextSeasonPlacement">The season placement returned from <see cref="SolveLocalSeasonPlacement"/> or <see cref="SolveGlobalSeasonPlacement"/></param>
        /// <param name="oldRank">The old rank of the participant</param>
        /// <param name="newSeason">The seasonId of the new season.</param>
        /// <param name="numRanks">The number of ranks in the new season.</param>
        /// <param name="rankDivisionFillValues">The largest unfilled division per rank and its current participant count.</param>
        /// <param name="migrationResult">The migration result object.</param>
        /// <param name="divisionsResult">The result object to add the participant to.</param>
        /// /// <exception cref="IndexOutOfRangeException"></exception>
        void HandleSeasonPlacementImpl(
            EntityId participant,
            ParticipantSeasonPlacementResult nextSeasonPlacement,
            int oldRank,
            int newSeason,
            int numRanks,
            (int division, int participants)[] rankDivisionFillValues,
            SeasonMigrationResult migrationResult,
            SeasonMigrationDivisionsResult divisionsResult)
        {
            if (nextSeasonPlacement.RemoveFromNextSeason)
            {
                migrationResult.LastSeasonRankResults[oldRank].NumDropped++;
                return;
            }

            if (nextSeasonPlacement.NextSeasonRank < 0)
                throw new IndexOutOfRangeException($"Rank placement from {nameof(SolveLocalSeasonPlacement)} was less than 0: {nextSeasonPlacement.NextSeasonRank}");
            if (nextSeasonPlacement.NextSeasonRank >= numRanks)
                throw new IndexOutOfRangeException($"Rank placement from {nameof(SolveLocalSeasonPlacement)} was more or equal than number of ranks({numRanks}): {nextSeasonPlacement.NextSeasonRank}");

            if (nextSeasonPlacement.NextSeasonRank > oldRank)
                migrationResult.LastSeasonRankResults[oldRank].NumPromoted++;

            if (nextSeasonPlacement.NextSeasonRank < oldRank)
                migrationResult.LastSeasonRankResults[oldRank].NumDemoted++;

            int                  rankParticipantCount = GetRankDetails(nextSeasonPlacement.NextSeasonRank, newSeason).DivisionParticipantCount;
            LeagueRankUpStrategy rankUpStrategy       = GetRankUpStrategy(nextSeasonPlacement.NextSeasonRank, newSeason);
            bool                 isRankFull           = rankDivisionFillValues[nextSeasonPlacement.NextSeasonRank].participants >= rankParticipantCount;

            // Move to fill next division if current is full.
            if (!rankUpStrategy.IsSingleDivision && isRankFull)
            {
                rankDivisionFillValues[nextSeasonPlacement.NextSeasonRank] =
                    (rankDivisionFillValues[nextSeasonPlacement.NextSeasonRank].division + 1, 0);
            }

            DivisionIndex newDivision = new DivisionIndex(LeagueId, newSeason, nextSeasonPlacement.NextSeasonRank, rankDivisionFillValues[nextSeasonPlacement.NextSeasonRank].division);

            // Mark a division as a new division if this is the first participant placed there.
            if (rankDivisionFillValues[nextSeasonPlacement.NextSeasonRank].participants == 0)
                divisionsResult.CreatedDivisions.Add(newDivision);

            rankDivisionFillValues[nextSeasonPlacement.NextSeasonRank].participants++;

            divisionsResult.DivisionPairs.Add(new ParticipantDivisionPair(participant, newDivision));

            if (divisionsResult.ParticipantsPerDivision.TryGetValue(newDivision, out List<EntityId> parts))
                parts.Add(participant);
            else
                divisionsResult.ParticipantsPerDivision.Add(newDivision, new List<EntityId> {participant});
        }

        /// <summary>
        /// Remove any participants' old association data for the current league. Can be used outside of actor context.
        /// </summary>
        protected async Task<int> RemoveOldParticipantDivisionAssociations(int currentSeason)
        {
            MetaDatabase db = MetaDatabase.Get(QueryPriority.Low);
            int participantsRemoved = 0;

            await foreach (IList<PersistedParticipantDivisionAssociation> participants in EnumerateAllParticipants().Buffer(Options.SeasonMigrationBatchSize))
            {
                // Participants where season is less than current season.
                EntityId[] oldParticipants = participants.Where(p => DivisionIndex.FromEntityId(EntityId.ParseFromString(p.DivisionId)).Season < currentSeason)
                    .Select(p => EntityId.ParseFromString(p.ParticipantId)).ToArray();

                if (oldParticipants.Length == 0)
                    continue;

                int countRemoved = await db.RemoveLeagueParticipantDivisionAssociationsAsync(_entityId, oldParticipants);

                if (countRemoved != oldParticipants.Length)
                    _log.Warning("Failed to remove some participant associations for league {LeagueId}. Expected: {Expected}, Removed: {Removed}", _entityId, oldParticipants.Length, countRemoved);

                participantsRemoved += countRemoved;
            }

            return participantsRemoved;
        }

        /// <summary>
        /// Enumerate all participants of this league in an asynchronous manner.
        /// Used during season migration.
        /// </summary>
        /// <param name="queryPriority">The query priority to use.</param>
        protected async IAsyncEnumerable<PersistedParticipantDivisionAssociation> EnumerateAllParticipants(QueryPriority queryPriority = QueryPriority.Low)
        {
            MetaDatabase db = MetaDatabase.Get(queryPriority);

            PagedIterator iterator = PagedIterator.Start;
            do
            {
                PagedQueryResult<PersistedParticipantDivisionAssociation> result = await db.QueryLeagueParticipantsPaged(
                    _entityId,
                    iterator,
                    Options.SeasonMigrationBatchSize);

                iterator = result.Iterator;

                foreach (PersistedParticipantDivisionAssociation association in result.Items)
                {
                    EntityId      divisionId    = EntityId.ParseFromString(association.DivisionId);
                    DivisionIndex divisionIndex = DivisionIndex.FromEntityId(divisionId);

                    if (divisionIndex.League == LeagueId)
                        yield return association;
                }
            } while (!iterator.IsFinished);
        }

        /// <summary>
        /// <para>
        /// Fetch conclusion results from a single division. Optionally, only request results of a list of participants.
        /// The conclusion results are used by the rank up methods to decide the placements for next season.
        /// </para>
        /// <para>
        /// Can be used outside of actor context.
        /// </para>
        /// </summary>
        /// <param name="divisionId">The division to fetch results from.</param>
        /// <param name="participants">The participants to requests results for. Leave null for all participants.</param>
        /// <returns>A dictionary of participant ids and their results. May return null if failed.</returns>
        protected async Task<Dictionary<EntityId, IDivisionParticipantConclusionResult>> TryFetchConclusionResultsForDivisionParticipants(EntityId divisionId, List<EntityId> participants = null)
        {
            try
            {
                InternalDivisionParticipantResultResponse response = await EntityAskAsync(divisionId, new InternalDivisionParticipantResultRequest(participants));

                return response.ParticipantResults;
            }
            catch (Exception askException)
            {
                _log.Error(askException, "Failed to fetch results from old division {DivisionId}.", divisionId);
            }

            return null;
        }

        /// <summary>
        /// <para>
        /// Fetch conclusion results for multiple participants from each division they belong to.
        /// The conclusion results are used by the rank up methods to decide the placements for next season.
        /// May return null if failed.
        /// </para>
        /// <para>
        /// Can be used outside of actor context.
        /// </para>
        /// </summary>
        /// <param name="participants">The list of participants to fetch results for.</param>
        /// <returns>A dictionary of participant ids and their results. May return null if failed.</returns>
        protected async Task<Dictionary<EntityId, IDivisionParticipantConclusionResult>> TryFetchConclusionResultsForDivisionParticipants(List<EntityId> participants)
        {
            Dictionary<EntityId, IDivisionParticipantConclusionResult> conclusionResults = new Dictionary<EntityId, IDivisionParticipantConclusionResult>(participants.Count);

            MetaDatabase db = MetaDatabase.Get();

            try
            {
                Dictionary<EntityId, List<EntityId>> oldDivisionsToFetchResultsFrom = new Dictionary<EntityId, List<EntityId>>();

                foreach (EntityId participant in participants)
                {
                    // \todo: Optimize this to fetch all associations in one query.
                    PersistedParticipantDivisionAssociation oldAssociation = await db.TryGetParticipantDivisionAssociation(_entityId, participant);
                    if (oldAssociation == null)
                        continue;

                    EntityId oldDivId = EntityId.ParseFromString(oldAssociation.DivisionId);
                    if (oldDivisionsToFetchResultsFrom.TryGetValue(oldDivId, out List<EntityId> partsInDivision))
                        partsInDivision.Add(participant);
                    else
                        oldDivisionsToFetchResultsFrom.Add(oldDivId, new List<EntityId> {participant});
                }

                // Fetch conclusion results from older divisions
                foreach ((EntityId divId, List<EntityId> parts) in oldDivisionsToFetchResultsFrom)
                {
                    Dictionary<EntityId, IDivisionParticipantConclusionResult> results = await TryFetchConclusionResultsForDivisionParticipants(divId, parts);
                    if (results != null)
                    {
                        foreach ((EntityId key, IDivisionParticipantConclusionResult conclusion) in results)
                            conclusionResults.Add(key, conclusion);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to fetch conclusion results from divisions");
                return null;
            }

            return conclusionResults;
        }

        /// <summary>
        /// Initialize new divisions for a season, and send the old seasons participants' avatars.
        /// </summary>
        /// <param name="divisionsResult">Contains the list of new divisions to initialize, and the participant to division mapping.</param>
        /// <param name="conclusionResults">Last season conclusion results of the participants.</param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentException"></exception>
        protected async Task InitializeDivisionsAndTransferAvatars(
            SeasonMigrationDivisionsResult divisionsResult,
            Dictionary<EntityId, IDivisionParticipantConclusionResult> conclusionResults)
        {
            // Create new divisions.
            foreach (DivisionIndex idx in divisionsResult.CreatedDivisions)
            {
                EntityId divisionId = idx.ToEntityId();
                await DatabaseEntityUtil.CreateNewEntityAsync<TPersistedDivision>(divisionId);

                State.CurrentSeason.Ranks[idx.Rank].NumDivisions = Math.Max(State.CurrentSeason.Ranks[idx.Rank].NumDivisions, idx.Division + 1);

                DivisionSetupParams setupParams = new DivisionSetupParams(
                    _entityId,
                    idx.League,
                    idx.Season,
                    idx.Rank,
                    idx.Division,
                    State.CurrentSeason.StartTime,
                    State.CurrentSeason.EndTime,
                    State.CurrentSeason.EndingSoonStartsAt,
                    GetRankDetails(idx.Rank, idx.Season).DivisionParticipantCount);

                try
                {
                    InternalEntitySetupResponse _ = await EntityAskAsync(divisionId, new InternalEntitySetupRequest(setupParams));
                }
                catch (InvalidEntityAsk invalidEntityAsk)
                {
                    // Failure
                    _log.Error("Division setup of division {DivisionIndex} failed with message {Message}", idx, invalidEntityAsk.Message);
                    throw new InvalidOperationException($"Division setup of division {idx} failed: {invalidEntityAsk.Message}");
                }
                catch (InternalEntitySetupRefusal refusal)
                {
                    // Refused
                    _log.Warning("Division setup of division {DivisionIndex} refused with message {Message}. Trying again with force.", idx, refusal.Message);

                    InternalDivisionForceSetupDebugResponse response = await EntityAskAsync(divisionId, new InternalDivisionForceSetupDebugRequest(setupParams));

                    if (!response.IsSuccess)
                    {
                        _log.Error("Force division setup of division {DivisionIndex} failed.", idx);
                        throw new InvalidOperationException($"Force division setup of division {idx} failed.");
                    }
                }
            }

            // Transfer avatars
            if (ParticipantEntityKind == EntityKindCore.Player)
            {
                foreach ((DivisionIndex division, List<EntityId> participants) in divisionsResult.ParticipantsPerDivision)
                {
                    EntityId divId = division.ToEntityId();

                    Dictionary<EntityId, PlayerDivisionAvatarBase> divisionAvatars = new Dictionary<EntityId, PlayerDivisionAvatarBase>();

                    foreach (EntityId participant in participants)
                    {
                        if (conclusionResults.TryGetValue(participant, out IDivisionParticipantConclusionResult conclusionResult))
                        {
                            if (conclusionResult is IPlayerDivisionParticipantConclusionResult playerConclusionResult)
                                divisionAvatars.TryAdd(participant, playerConclusionResult.Avatar);
                            else
                                throw new ArgumentException($"Conclusion result of type {conclusionResult.GetType()} does not inherit type {nameof(IPlayerDivisionParticipantConclusionResult)}!");
                        }
                    }

                    await EntityAskAsync(divId, new InternalPlayerDivisionAvatarBatchUpdate(divisionAvatars));
                }
            }
            //else if (ParticipantEntityKind == EntityKindCore.Guild)
            //{
            //    throw new NotImplementedException();
            //}
        }

        /// <summary>
        /// Get division index and setup params for a new division in the current season.
        /// This is called from the actor context before calling <see cref="CreateNewCurrentSeasonDivision"/>.
        /// </summary>
        (DivisionIndex, DivisionSetupParams) GetNewCurrentSeasonDivisionSetup(int rank)
        {
            DivisionIndex newDivision = new DivisionIndex(
                LeagueId,
                State.CurrentSeason.SeasonId,
                rank,
                State.CurrentSeason.Ranks[rank].NumDivisions);

            DivisionSetupParams setupParams = new DivisionSetupParams(
                creatorId:      _entityId,
                league:         newDivision.League,
                season:         newDivision.Season,
                rank:           newDivision.Rank,
                division:       newDivision.Division,
                startTime:      State.CurrentSeason.StartTime,
                endTime:        State.CurrentSeason.EndTime,
                endingSoonTime: State.CurrentSeason.EndingSoonStartsAt,
                GetRankDetails(newDivision.Rank, newDivision.Season).DivisionParticipantCount);

            // Increment divisions
            State.CurrentSeason.Ranks[rank].NumDivisions++;

            return (newDivision, setupParams);
        }

        /// <summary>
        /// Create a new division in the current season. This is run on a separate thread outside the actor context.
        /// Before calling this, <see cref="GetNewCurrentSeasonDivisionSetup"/> is called from the actor context.
        /// </summary>
        protected virtual async Task<DivisionIndex> CreateNewCurrentSeasonDivision(DivisionIndex newDivision, DivisionSetupParams setupParams)
        {
            EntityId divisionId = newDivision.ToEntityId();

            await DatabaseEntityUtil.CreateNewEntityAsync<TPersistedDivision>(divisionId);

            try
            {
                InternalEntitySetupResponse _ = await EntityAskAsync(divisionId, new InternalEntitySetupRequest(setupParams));

                return newDivision; // Success. return here
            }
            catch (InvalidEntityAsk invalidEntityAsk)
            {
                // Failure
                _log.Error("Division setup of division {DivisionIndex} failed with message {Message}", newDivision, invalidEntityAsk.Message);
            }
            catch (InternalEntitySetupRefusal refusal)
            {
                // Refused
                _log.Warning("Division setup of division {DivisionIndex} refused with message {Message}. Division might already be set up.", newDivision, refusal.Message);
            }

            throw new InvalidOperationException("Failed to create a new division.");
        }

        async Task UpdateDivisionForParticipant(EntityId participantId, DivisionIndex division)
        {
            PersistedParticipantDivisionAssociation association = new PersistedParticipantDivisionAssociation(
                participantId: participantId.ToString(),
                leagueId: _entityId.ToString(),
                divisionId: division.ToEntityId().ToString(),
                MetaTime.Now.ToDateTime(),
                State.StateRevision);

            await MetaDatabase.Get().InsertOrReplaceAsync<PersistedParticipantDivisionAssociation>(association);
        }

        /// <summary>
        /// Synchronizes the division state of this league with the division associations in the database.
        /// The state can get out of sync if the league crashes or is restarted unexpectedly.
        /// We can not, as of yet, synchronize removals, so data might still get out of sync.
        /// </summary>
        async Task SynchronizeDivisionState()
        {
            MetaDatabase db = MetaDatabase.Get();

            List<PersistedParticipantDivisionAssociation> newAssociations = await db.QueryLeagueParticipantAssociationsByLatestKnownStateRevision(_entityId, State.StateRevision);

            if (newAssociations.Count == 0)
                return;

            _log.Warning("Synchronizing missed division assignments for {NumAssociations} participants.", newAssociations.Count);

            List<DivisionIndex> newDivisions = new List<DivisionIndex>();

            int revisionErrorCount = 0;
            int rankErrorCount     = 0;
            foreach (PersistedParticipantDivisionAssociation newAssociation in newAssociations)
            {
                if (newAssociation.LeagueStateRevision != State.StateRevision + 1)
                {
                    if (revisionErrorCount < 30)
                        _log.Error("Found division assignment with state revision of 2 or higher than latest persisted! Current revision: {Expected}, Found: {Found}, Participant: {Participant}.",
                            State.StateRevision, newAssociation.LeagueStateRevision, newAssociation.ParticipantId);
                    revisionErrorCount++;
                }

                DivisionIndex division = DivisionIndex.FromEntityId(EntityId.ParseFromString(newAssociation.DivisionId));

                if(division.Season != State.CurrentSeason.SeasonId)
                    continue;

                if (division.Rank >= State.CurrentSeason.Ranks.Count)
                {
                    if (rankErrorCount < 30)
                        _log.Error("Trying to sync division {DivisionIndex}, but rank does not exist in the current state!", division);
                    rankErrorCount++;
                    continue;
                }

                State.CurrentSeason.Ranks[division.Rank].NumDivisions = Math.Max(State.CurrentSeason.Ranks[division.Rank].NumDivisions, division.Division + 1);
                State.CurrentSeason.Ranks[division.Rank].NumParticipants++;

                _participantCountState.AddParticipant(division.Rank, division.Division);

                if (_participantCountState.Ranks[division.Rank].DivisionParticipantCounts[division.Division].Count == 1)
                    newDivisions.Add(division);
            }

            if(revisionErrorCount > 0 || rankErrorCount > 0)
                _log.Error("Finished sync with errors! Total error count: {Count}.", revisionErrorCount + rankErrorCount);

            await OnDivisionsRecoveredAfterCrash(newDivisions);
        }

        /// <summary>
        /// Migrate division count state from before StateRevision was introduced in R25.
        /// All existing division counts are initially set to full.
        /// Then the last divisions of each rank are updated until we hit a streak of full divisions.
        /// This might miss some actual counts but prevents divisions from getting overfilled.
        /// </summary>
        async Task MigrateDivisionState()
        {
            MetaDatabase db = MetaDatabase.Get();
            _log.Info("Migrating division count state from before R25...");

            for (int r = 0; r < State.CurrentSeason.Ranks.Count; ++r)
            {
                int rankParticipantCount = GetRankDetails(r, State.CurrentSeason.SeasonId).DivisionParticipantCount;
                for (int d = 0; d < State.CurrentSeason.Ranks[r].NumDivisions; ++d)
                {
                    // Set participant count to max.
                    _participantCountState.SetParticipantCount(r, d, rankParticipantCount);
                }

                int fullCount = 0;

                // Iterate backwards and get actual counts from db until we hit 10 full divisions in a row.
                // Divisions at the end are more likely to be empty than divisions in the beginning,
                // so chances of missing severely under-filled divisions should be low.
                for (int d = State.CurrentSeason.Ranks[r].NumDivisions - 1; d >= 0; --d)
                {
                    EntityId divisionId = new DivisionIndex(LeagueId, State.CurrentSeason.SeasonId, r, d).ToEntityId();
                    int actualCount = await db.CountLeagueParticipantsByDivision(divisionId);

                    if (actualCount >= rankParticipantCount)
                        fullCount++;
                    else
                    {
                        fullCount = 0;
                        _participantCountState.SetParticipantCount(r, d, actualCount);
                    }
                    if (fullCount >= 10)
                        break;
                }
            }
            _log.Info("Division count migrated!");
        }

        #region EntityAsk Handlers

        [EntityAskHandler]
        async Task HandleInternalLeagueDebugJoinRankRequest(EntityShard.EntityAsk<InternalLeagueJoinResponse> ask, InternalLeagueDebugJoinRankRequest request)
        {
            if (!Options.Enabled)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotEnabled));
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            if (!request.ParticipantId.IsValid)
                throw new InvalidEntityAsk("League join request participant must be a valid EntityId!");

            if (State.CurrentSeason == null)
            {
                _log.Info("Rejecting league join request from {ParticipantId} due to current season being null.", request.ParticipantId);
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotStarted));
                return;
            }

            if (!State.CurrentSeason.MigrationComplete)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.SeasonMigrationInProgress));
                return;
            }

            if (MetaTime.Now >= State.CurrentSeason.EndTime)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotStarted));
                return;
            }

            PersistedParticipantDivisionAssociation oldAssociation = await MetaDatabase.Get().TryGetParticipantDivisionAssociation(_entityId, request.ParticipantId);

            // \TODO: Add check if the user is already in the right division
            if (oldAssociation != null && oldAssociation.LeagueId == _entityId.ToString())
            {
                // Leave old division
                try
                {
                    await EntityAskAsync(EntityId.ParseFromString(oldAssociation.DivisionId), new InternalLeagueLeaveRequest(request.ParticipantId, true));
                }
                catch (EntityAskRefusal e)
                {
                    _log.Warning("Failed to remove participant {ParticipantId} from division {DivisionId}. Reason: {Reason}", request.ParticipantId, oldAssociation.DivisionId, e.Message);
                    ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.AlreadyInLeague));
                    return;
                }
            }

            if (request.StartingRank >= State.CurrentSeason.Ranks.Count || request.StartingRank < 0)
            {
                _log.Error($"Debug join given rank was outside the range of current available ranks");
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.UnknownReason));
                return;
            }

            AddParticipantToDivision(ask, request.ParticipantId, request.StartingRank, sw, isAdminAction: true);
        }

        [EntityAskHandler]
        async Task HandleLeagueJoinRequest(EntityShard.EntityAsk<InternalLeagueJoinResponse> ask, InternalLeagueJoinRequest request)
        {
            if (!Options.Enabled)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotEnabled));
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            if (request.Payload == null)
                throw new InvalidEntityAsk("League join request null payload not allowed!");

            if (!request.ParticipantId.IsValid)
                throw new InvalidEntityAsk("League join request participant must be a valid EntityId!");

            if (State.CurrentSeason == null)
            {
                _log.Info("Rejecting league join request from {ParticipantId} due to current season being null.", request.ParticipantId);
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotStarted));
                return;
            }

            if (!State.CurrentSeason.MigrationComplete)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.SeasonMigrationInProgress));
                return;
            }

            if (MetaTime.Now >= State.CurrentSeason.EndTime)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotStarted));
                return;
            }

            ParticipantJoinRequestResult requestResult = await SolveParticipantInitialPlacement(State.CurrentSeason.SeasonId, request.ParticipantId, request.Payload);

            if (!requestResult.CanJoin)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.RequirementsNotMet));
                return;
            }

            if (requestResult.StartingRank >= State.CurrentSeason.Ranks.Count || requestResult.StartingRank < 0)
            {
                _log.Error($"{nameof(SolveParticipantInitialPlacement)} returned a rank outside the range of current available ranks.");
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.UnknownReason));
                return;
            }

            int startingRank = requestResult.StartingRank;
            AddParticipantToDivision(ask, request.ParticipantId, startingRank, sw, false);
        }

        [EntityAskHandler]
        async Task HandleInternalLeagueReassignRequest(EntityShard.EntityAsk<InternalLeagueJoinResponse> ask, InternalLeagueReassignRequest request)
        {
            if (!Options.Enabled)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotEnabled));
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            if (request.Payload == null)
                throw new InvalidEntityAsk("League join request null payload not allowed!");

            if (!request.ParticipantId.IsValid)
                throw new InvalidEntityAsk("League join request participant must be a valid EntityId!");

            if (State.CurrentSeason == null)
            {
                _log.Info("Rejecting league join request from {ParticipantId} due to current season being null.", request.ParticipantId);
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotStarted));
                return;
            }

            if (!State.CurrentSeason.MigrationComplete)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.SeasonMigrationInProgress));
                return;
            }

            if (MetaTime.Now >= State.CurrentSeason.EndTime)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.LeagueNotStarted));
                return;
            }

            PersistedParticipantDivisionAssociation oldAssociation = await MetaDatabase.Get().TryGetParticipantDivisionAssociation(_entityId, request.ParticipantId);
            EntityId oldParticipatingDivision = oldAssociation != null && oldAssociation.LeagueId == _entityId.ToString() ?
                    EntityId.ParseFromStringWithKind(EntityKindCore.Division, oldAssociation.DivisionId) : EntityId.None;

            bool wasParticipating = oldParticipatingDivision.IsValid && DivisionIndex.FromEntityId(oldParticipatingDivision).Season == State.CurrentSeason.SeasonId;

            // Leave old division if was participating in the current season.
            if (wasParticipating)
            {
                await MetaDatabase.Get().RemoveLeagueParticipantDivisionAssociationsAsync(_entityId, [request.ParticipantId]);
                int oldRank = DivisionIndex.FromEntityId(oldParticipatingDivision).Rank;
                State.CurrentSeason.NewParticipants--;
                State.CurrentSeason.Ranks[oldRank].NumReassigned++;
                State.CurrentSeason.Ranks[oldRank].NumParticipants--;

                if (request.LeaveOldDivision)
                {
                    int seasonBeforeAsk = State.CurrentSeason.SeasonId;
                    ContinueTaskOnActorContext(EntityAskAsync<EntityAskOk>(oldParticipatingDivision, new InternalLeagueLeaveRequest(request.ParticipantId, false)),
                        ok =>
                        {
                            // If season changed between making the request and receiving the response, ignore.
                            if (seasonBeforeAsk != State.CurrentSeason.SeasonId)
                                return;

                            if (Options.AllowDivisionBackFill)
                            {
                                DivisionIndex oldDivisionIndex = DivisionIndex.FromEntityId(oldParticipatingDivision);
                                _participantCountState.RemoveParticipant(oldDivisionIndex.Rank, oldDivisionIndex.Division);
                            }
                        },
                        err => _log.Error(err, "Failed to remove player {Player} from old division {Division}", request.ParticipantId, oldParticipatingDivision));
                }
            }

            ParticipantJoinRequestResult requestResult = await SolveParticipantInitialPlacement(State.CurrentSeason.SeasonId, request.ParticipantId, request.Payload);

            if (!requestResult.CanJoin)
            {
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.RequirementsNotMet));
                return;
            }

            if (requestResult.StartingRank >= State.CurrentSeason.Ranks.Count || requestResult.StartingRank < 0)
            {
                _log.Error($"{nameof(SolveParticipantInitialPlacement)} returned a rank outside the range of current available ranks.");
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.UnknownReason));
                return;
            }

            int startingRank = requestResult.StartingRank;
            AddParticipantToDivision(ask, request.ParticipantId, startingRank, sw, false);
        }

        void AddParticipantToDivision(EntityShard.EntityAsk<InternalLeagueJoinResponse> ask, EntityId participantId, int startingRank, Stopwatch sw, bool isAdminAction)
        {
            OnBeforeAssignParticipantToRank(participantId, startingRank);

            if (!_participantCountState.TryGetNonFullDivisionForRank(startingRank, out int foundDivision))
            {
                int currentSeason = State.CurrentSeason.SeasonId;

                // Start task to create new division
                (DivisionIndex newDivisionToCreate, DivisionSetupParams newDivisionSetupParams) = GetNewCurrentSeasonDivisionSetup(startingRank);
                Task<DivisionIndex> createDivisionTask = Task.Run(async () => await CreateNewCurrentSeasonDivision(newDivisionToCreate, newDivisionSetupParams));

                // Wait for division creation to complete in a background thread.
                ContinueTaskOnActorContext(
                    createDivisionTask,
                    (division) =>
                    {
                        if (State.CurrentSeason.SeasonId != currentSeason) // Season changed.
                        {
                            ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.UnknownReason));
                            return;
                        }

                        _participantCountState.AddParticipant(startingRank, division.Division);

                        _participantCountState.Validate();

                        c_LeagueJoinRequestLatencies.WithLabels(
                            new string[]
                            {
                                _entityId.ToString(),
                                "true"
                            }).Observe(sw.Elapsed.TotalSeconds);

                        _ = UpdateDivisionForParticipant(participantId, division).ContinueWith(
                            task => _log.Error(task.Exception, "Failed to update division for participant {ParticipantId}.", participantId),
                            default,
                            TaskContinuationOptions.OnlyOnFaulted,
                            TaskScheduler.Default);

                        State.CurrentSeason.NewParticipants++;
                        State.CurrentSeason.Ranks[startingRank].NumParticipants++;

                        if (isAdminAction)
                            CastMessage(participantId, new InternalLeagueParticipantDivisionForceUpdated(division.ToEntityId()));

                        OnNewDivisionCreated(division, false);
                        OnAfterAssignParticipantToDivision(participantId, division);

                        ReplyToAsk(ask, InternalLeagueJoinResponse.ForSuccess(division));
                    },
                    (e) =>
                    {
                        _log.Error(e, "Failed to create division.");
                        ReplyToAsk(ask, InternalLeagueJoinResponse.ForFailure(LeagueJoinRefuseReason.UnknownReason));
                    });
            }
            else
            {
                DivisionIndex division = new DivisionIndex(LeagueId, State.CurrentSeason.SeasonId, startingRank, foundDivision);

                _participantCountState.AddParticipant(startingRank, foundDivision);

                _ = UpdateDivisionForParticipant(participantId, division).ContinueWith(
                    task => _log.Error(task.Exception, "Failed to update division for participant {ParticipantId}.", participantId),
                    default,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);

                State.CurrentSeason.NewParticipants++;
                State.CurrentSeason.Ranks[startingRank].NumParticipants++;

                OnAfterAssignParticipantToDivision(participantId, division);

                c_LeagueJoinRequestLatencies.WithLabels(
                    new string[]
                    {
                        _entityId.ToString(),
                        "false"
                    }).Observe(sw.Elapsed.TotalSeconds);

                if (isAdminAction)
                    CastMessage(participantId, new InternalLeagueParticipantDivisionForceUpdated(division.ToEntityId()));
                ReplyToAsk(ask, InternalLeagueJoinResponse.ForSuccess(division));
            }
        }

        [EntityAskHandler]
        async Task<EntityAskOk> HandleLeagueLeaveRequest(InternalLeagueLeaveRequest request)
        {
            if (!Options.Enabled)
                throw new InvalidEntityAsk("League leave request not allowed when league is disabled!");

            if (!request.ParticipantId.IsValid)
                throw new InvalidEntityAsk("League leave request participant must be a valid EntityId!");

            if (State.CurrentSeason == null)
                throw new InvalidEntityAsk("League leave request not allowed when league is not started!");

            if (!State.CurrentSeason.MigrationComplete || SeasonMigrationInProgress)
                throw new InvalidEntityAsk("League leave request not allowed when season migration is ongoing!");

            PersistedParticipantDivisionAssociation oldAssociation = await MetaDatabase.Get().TryGetParticipantDivisionAssociation(_entityId, request.ParticipantId);

            if(oldAssociation == null || oldAssociation.LeagueId != _entityId.ToString())
                return EntityAskOk.Instance; // Already left

            try
            {
                await EntityAskAsync(EntityId.ParseFromString(oldAssociation.DivisionId), request);
            }
            catch (EntityAskRefusal e)
            {
                _log.Warning("Failed to remove participant {ParticipantId} from division {DivisionId}. Reason: {Reason}", request.ParticipantId, oldAssociation.DivisionId, e.Message);
            }

            await MetaDatabase.Get().RemoveLeagueParticipantDivisionAssociationsAsync(_entityId, new EntityId[] { request.ParticipantId });

            DivisionIndex divisionIndex = DivisionIndex.FromEntityId(EntityId.ParseFromString(oldAssociation.DivisionId));
            if (divisionIndex.Season == State.CurrentSeason.SeasonId)
            {
                // Update participant counts
                if (Options.AllowDivisionBackFill)
                    _participantCountState.RemoveParticipant(divisionIndex.Rank, divisionIndex.Division);
                State.CurrentSeason.Ranks[divisionIndex.Rank].NumParticipants--;
            }

            if (request.IsAdminAction)
                CastMessage(request.ParticipantId, new InternalLeagueParticipantDivisionForceUpdated(EntityId.None));

            _participantCountState.Validate();

            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        async Task<InternalLeagueDebugAddResponse> HandleInternalLeagueDebugAddRequest(InternalLeagueDebugAddRequest request)
        {
            if (!Options.Enabled)
                throw new InvalidEntityAsk("League add request not allowed when league is disabled!");

            if (!request.ParticipantId.IsValid)
                throw new InvalidEntityAsk("Participant must be a valid EntityId!");

            if (!request.ParticipantId.IsOfKind(ParticipantEntityKind))
                throw new InvalidEntityAsk("ParticipantId must match the participant type of the league!");

            if (State.CurrentSeason == null)
                throw new InvalidEntityAsk("League add request not allowed when league is not started!");

            if (!State.CurrentSeason.MigrationComplete || SeasonMigrationInProgress || MetaTime.Now >= State.CurrentSeason.EndTime)
                throw new InvalidEntityAsk("League add request not allowed when season migration is ongoing!");

            if (DivisionIndex.FromEntityId(request.DivisionId).Season != State.CurrentSeason.SeasonId)
                throw new InvalidEntityAsk("Cannot add participant to a division of a past season!");

            PersistedParticipantDivisionAssociation oldAssociation = await MetaDatabase.Get().TryGetParticipantDivisionAssociation(_entityId, request.ParticipantId);

            if (oldAssociation != null && oldAssociation.DivisionId == request.DivisionId.ToString())
                throw new InvalidEntityAsk("Participant is already assigned to the requested division!");

            bool wasAlreadyInDivision = oldAssociation != null && oldAssociation.LeagueId == _entityId.ToString();
            if (wasAlreadyInDivision)
            {
                // Leave old division
                try
                {
                    await EntityAskAsync(EntityId.ParseFromString(oldAssociation.DivisionId), new InternalLeagueLeaveRequest(request.ParticipantId, true));
                }
                catch (EntityAskRefusal e)
                {
                    _log.Warning("Failed to remove participant {ParticipantId} from division {DivisionId}. Reason: {Reason}", request.ParticipantId, oldAssociation.DivisionId, e.Message);
                }

                DivisionIndex oldDivisionIndex = DivisionIndex.FromEntityId(EntityId.ParseFromString(oldAssociation.DivisionId));
                if (oldDivisionIndex.Season == State.CurrentSeason.SeasonId)
                {
                    // Update participant counts
                    _participantCountState.RemoveParticipant(oldDivisionIndex.Rank, oldDivisionIndex.Division);
                    State.CurrentSeason.Ranks[oldDivisionIndex.Rank].NumParticipants--;
                }
            }

            DivisionIndex newDivisionIndex = DivisionIndex.FromEntityId(request.DivisionId);

            await UpdateDivisionForParticipant(request.ParticipantId, newDivisionIndex);

            // Update participant counts
            _participantCountState.AddParticipant(newDivisionIndex.Rank, newDivisionIndex.Division);
            State.CurrentSeason.Ranks[newDivisionIndex.Rank].NumParticipants++;

            CastMessage(request.ParticipantId, new InternalLeagueParticipantDivisionForceUpdated(request.DivisionId));

            _participantCountState.Validate();

            return new InternalLeagueDebugAddResponse(wasAlreadyInDivision);
        }

        [EntityAskHandler]
        InternalLeagueStateResponse HandleLeagueStateRequest(InternalLeagueStateRequest _)
        {
            LeagueRankDetails[] rankDetails   = null;
            LeagueSeasonDetails seasonDetails = default;
            LeagueDetails       leagueDetails = GetLeagueDetails();

            if (State.CurrentSeason != null && State.CurrentSeason.Ranks != null)
            {
                seasonDetails = GetSeasonDetails(State.CurrentSeason.SeasonId);
                rankDetails   = new LeagueRankDetails[State.CurrentSeason.Ranks.Count];
                for (int i = 0; i < rankDetails.Length; i++)
                    rankDetails[i] = GetRankDetails(i, State.CurrentSeason.SeasonId);
            }

            LeagueSeasonMigrationProgressState migrationProgressState = new LeagueSeasonMigrationProgressState(
                SeasonMigrationInProgress,
                SeasonMigrationProgressEstimate,
                SeasonMigrationProgressPhase,
                SeasonMigrationError);

            return new InternalLeagueStateResponse(State, LeagueSchedule, Options.Enabled, rankDetails, seasonDetails, leagueDetails, migrationProgressState);
        }

        [EntityAskHandler]
        async Task<EntityAskOk> HandleLeagueAdvanceRequest(InternalLeagueDebugAdvanceSeasonRequest request)
        {
            MetaTime now = MetaTime.Now;

            _log.Info("Received debug advance request for the league.");

            if (!Options.Enabled)
            {
                throw new InvalidEntityAsk("Could not process request due to league not being enabled.");
            }

            void InformCurrentSeasonDivisions(MetaTime newStartTime, MetaTime newEndTime)
            {
                List<DivisionIndex> divisionsToInform = new List<DivisionIndex>();
                for (int rank = 0; rank < State.CurrentSeason.Ranks.Count; rank++)
                {
                    for (int division = 0; division < State.CurrentSeason.Ranks[rank].NumDivisions; division++)
                        divisionsToInform.Add(new DivisionIndex(LeagueId, State.CurrentSeason.SeasonId, rank, division));
                }

                Task.Run(() => InformDivisionsOfSeasonDebugAdvance(divisionsToInform, newStartTime, newEndTime)).
                    ContinueWith(
                        t =>
                        {
                            if (t.IsFaulted)
                            {
                                _log.Error(t.Exception, "Failed to inform divisions of season debug advance.");
                                return;
                            }
                            if(t.IsCompleted)
                                _log.Information("Informed {Count} divisions of season schedule change.", divisionsToInform.Count);
                        },
                        TaskScheduler.Default);
            }

            MetaScheduleOccasion GetCurrentScheduleOccasionWithStartNow(MetaTime time)
            {
                // Default is a week long league.
                MetaTimeRange defaultRange = new MetaTimeRange(time, time + MetaDuration.FromDays(7));
                MetaScheduleOccasion defaultOccasion = new MetaScheduleOccasion(
                    defaultRange,
                    defaultRange.End - MetaDuration.FromDays(1),
                    defaultRange);

                if(LeagueSchedule == null) // No schedule. Return default.
                    return defaultOccasion;

                MetaScheduleOccasion? currentOrNext = LeagueSchedule.TryGetCurrentOrNextEnabledOccasion(new PlayerLocalTime(time, MetaDuration.Zero));

                if (!currentOrNext.HasValue) // No current or next occurrence. Return default.
                    return defaultOccasion;

                MetaScheduleOccasion? nextOccasion = LeagueSchedule.TryGetCurrentOrNextEnabledOccasion(
                    new PlayerLocalTime(currentOrNext.Value.VisibleRange.End + MetaDuration.FromMinutes(1), MetaDuration.Zero));

                if (currentOrNext.Value.EnabledRange.Start <= time && nextOccasion.HasValue && nextOccasion.Value.EnabledRange.Start > time)
                {
                    // Current occurrence is ongoing or ended, and next is available. Return next occurrence with preview time set to now.
                    return new MetaScheduleOccasion(
                        new MetaTimeRange(nextOccasion.Value.EnabledRange.Start, nextOccasion.Value.EnabledRange.End),
                        nextOccasion.Value.EndingSoonStartsAt,
                        new MetaTimeRange(time, nextOccasion.Value.EnabledRange.End));
                }
                else if (currentOrNext.Value.EnabledRange.End < time)
                {
                    // Current occurrence has ended. Return new occurrence starting from now.
                    MetaDuration seasonDuration = currentOrNext.Value.EnabledRange.End - currentOrNext.Value.EnabledRange.Start;
                    MetaDuration endingSoonTime = currentOrNext.Value.EndingSoonStartsAt - currentOrNext.Value.EnabledRange.Start;
                    return new MetaScheduleOccasion(
                        new MetaTimeRange(time, time + seasonDuration),
                        time + endingSoonTime,
                        new MetaTimeRange(time, time + seasonDuration));
                }
                // Current has not started yet. Return current occurrence with preview time set to now.
                return new MetaScheduleOccasion(
                    new MetaTimeRange(currentOrNext.Value.EnabledRange.Start, currentOrNext.Value.EnabledRange.End),
                    currentOrNext.Value.EndingSoonStartsAt,
                    new MetaTimeRange(time, currentOrNext.Value.EnabledRange.End));
            }

            if (request.IsEndSeasonRequest)
            {
                if (State.CurrentSeason == null) // No season to end
                {
                    throw new InvalidEntityAsk("Could not end season due to there being no season to end.");
                }
                if (State.CurrentSeason.EndTime < now) // Season already ended
                {
                    throw new InvalidEntityAsk("Could not end season due to the season being already ended.");
                }
                if (State.CurrentSeason.StartTime > now) // Season not started yet
                {
                    throw new InvalidEntityAsk("Could not end season due to the season not having started yet.");
                }
                if (!State.CurrentSeason.MigrationComplete || SeasonMigrationInProgress) // Season migration not complete
                {
                    throw new InvalidEntityAsk("Could not end season due to season migration not being complete.");
                }

                _log.Info("Ending season prematurely.");

                // Otherwise go ahead with the operation
                State.CurrentSeason.EndTime    = now;
                State.CurrentSeason.EndedEarly = true;
                InformCurrentSeasonDivisions(State.CurrentSeason.StartTime, State.CurrentSeason.EndTime);
            }
            else if(State.CurrentSeason != null) // Season exists. Start from preview or start a new season.
            {
                if(State.CurrentSeason.StartTime <= now && State.CurrentSeason.EndTime > now) // Season already in progress
                {
                    throw new InvalidEntityAsk("Could not start season due to season already being in progress.");
                }
                if (!State.CurrentSeason.MigrationComplete || SeasonMigrationInProgress) // Season migration not complete
                {
                    throw new InvalidEntityAsk("Could not start season due to season migration not being complete.");
                }

                // Start the season from preview phase.
                if (State.CurrentSeason.StartTime > now)
                {
                    _log.Info("Starting season now.");
                    State.CurrentSeason.StartTime    = now;
                    State.CurrentSeason.StartedEarly = true;
                    InformCurrentSeasonDivisions(State.CurrentSeason.StartTime, State.CurrentSeason.EndTime);
                }else if (State.CurrentSeason.EndTime < now) // Season has ended. Start a new season
                {
                    _log.Info("Starting a new season now.");
                    await ChangeSeason(GetCurrentScheduleOccasionWithStartNow(now));
                    State.CurrentSeason.StartedEarly = true;
                }
            }else // No season exists. Start a new season
            {
                _log.Info("Starting first season now.");
                await ChangeSeason(GetCurrentScheduleOccasionWithStartNow(now));
                State.CurrentSeason.StartedEarly = true;
            }

            await PersistStateIntermediate();

            return EntityAskOk.Instance;
        }

        async Task InformDivisionsOfSeasonDebugAdvance(List<DivisionIndex> divisionsToInform, MetaTime newStartTime, MetaTime newEndTime)
        {
            const int maxDivisionsPerSecond = 1000;
            InternalDivisionDebugSeasonScheduleUpdate update = new InternalDivisionDebugSeasonScheduleUpdate(newStartTime, newEndTime);
            foreach (DivisionIndex[] divisions in divisionsToInform.Chunk(maxDivisionsPerSecond))
            {
                foreach (DivisionIndex division in divisions)
                    CastMessage(division.ToEntityId(), update);

                await Task.Delay(1000);
            }
        }

        [MessageHandler]
        async Task HandleInternalLeagueReportInvalidDivisionState(InternalLeagueReportInvalidDivisionState report)
        {
            if (State.CurrentSeason == null)
            {
                _log.Error("Received invalid division state report for division {DivisionId} when current season is null.", report.Division);
                return;
            }

            if (!State.CurrentSeason.MigrationComplete || SeasonMigrationInProgress)
            {
                _log.Error("Received invalid division state report for division {DivisionId} when migration is in progress.", report.Division);
                return;
            }

            MetaDatabase db = MetaDatabase.Get();

            DivisionIndex di = DivisionIndex.FromEntityId(report.Division);

            if (di.League != LeagueId)
            {
                _log.Error("Received invalid division state report for different league division {DivisionId}.", report.Division);
                return;
            }

            if (di.Season != State.CurrentSeason.SeasonId)
            {
                _log.Error("Received invalid division state report for old division {DivisionId}.", report.Division);
                return;
            }

            // Remove all participants from the division
            int numRemoved = await db.RemoveLeagueParticipantsByDivision(report.Division);

            _log.Error("Received invalid division state report for division {DivisionId}. Removed all players from the division ({NumRemoved}).", report.Division, numRemoved);

            // Set participant count to max to prevent division from being re-used.
            _participantCountState.SetParticipantCount(di.Rank, di.Division,
                GetRankDetails(di.Rank, State.CurrentSeason.SeasonId).DivisionParticipantCount);

            _participantCountState.Validate();

            State.CurrentSeason.NewParticipants -= numRemoved;
            State.CurrentSeason.Ranks[di.Rank].NumParticipants -= numRemoved;
        }

        #endregion

        #region Game overrideable methods

        /// <summary>
        /// Called by the league manager when a new participant joins the league.
        /// This method should decide if a participant is allowed to join, and which rank they would start in.
        /// </summary>
        /// <returns></returns>
        protected abstract Task<ParticipantJoinRequestResult> SolveParticipantInitialPlacement(int currentSeason, EntityId participant, LeagueJoinRequestPayloadBase payload);

        /// <summary>
        /// Called by the league manager when a participant is being migrated to the next season using <see cref="LeagueRankUpMethod.Local"/>.
        /// This method decides whether a participant will be removed from the next season, gets promoted/demoted, or stays in the same rank.
        /// </summary>
        protected abstract ParticipantSeasonPlacementResult SolveLocalSeasonPlacement(int lastSeason, int nextSeason, int currentRank, IDivisionParticipantConclusionResult conclusionResult);

        /// <summary>
        /// Called by the league manager when participants are being migrated to the next season using <see cref="LeagueRankUpMethod.Global"/>.
        /// All participants in ranks with the global rank-up method will be given in the same method call.
        /// This method decides whether participants will be removed from the next season, get promoted/demoted, or stay in the same rank.
        /// </summary>
        /// <returns>A dictionary containing each participant's id and their next season placement.</returns>
        protected virtual Dictionary<EntityId, ParticipantSeasonPlacementResult> SolveGlobalSeasonPlacement(int lastSeason, int nextSeason, Dictionary<EntityId, GlobalRankUpParticipantData> allParticipants)
            => throw new NotImplementedException($"Implement {nameof(SolveGlobalSeasonPlacement)} if using {nameof(LeagueRankUpMethod.Global)} rank up method.");

        /// <summary>
        /// Called by the league manager to fetch a rank's info for a season.
        /// </summary>
        protected abstract LeagueRankDetails GetRankDetails(int rank, int season);

        /// <summary>
        /// Called by the league manager to fetch a rank's rank-up strategy for a season.
        /// Override this to change how the rank-up algorithm works for a certain rank.
        /// </summary>
        protected virtual LeagueRankUpStrategy GetRankUpStrategy(int rank, int season) => new LeagueRankUpStrategy();

        /// <summary>
        /// Called by the league manager to fetch a season's info.
        /// </summary>
        protected abstract LeagueSeasonDetails GetSeasonDetails(int season);

        /// <summary>
        /// Called by the league manager to fetch the league's info.
        /// </summary>
        protected abstract LeagueDetails GetLeagueDetails();

        /// <summary>
        /// Called by the league manager to fetch the league's custom schedule.
        /// Override this to define a custom schedule for the league.
        /// If using custom scheduling, also set the <see cref="LeagueManagerOptionsBase.UseCustomLeagueSchedule"/> option to true.
        /// </summary>
        /// <returns></returns>
        protected virtual MetaScheduleBase GetCustomSchedule() => null;

        /// <summary>
        /// Called by the league manager when a new division is created during a season, or during the season migration.
        /// This method is called after the division is created and set up. If the division is created during a season migration,
        /// this method will be called as the last step in the migration process when all other state is valid.
        /// </summary>
        protected virtual void OnNewDivisionCreated(DivisionIndex newDivision, bool isSeasonMigration) { }

        /// <summary>
        /// Called by the league manager when a new division is created during a season, or during the season migration.
        /// This method is called after the division is created and set up. If the division is created during a season migration,
        /// this method will be called as the last step in the migration process when all other state is valid.
        /// </summary>
        protected virtual Task OnDivisionsRecoveredAfterCrash(IEnumerable<DivisionIndex> divisions) => Task.CompletedTask;

        /// <summary>
        /// Called by the league manager when a participant is being assigned to a division.
        /// This is called right before the decision of which division the participant will be assigned to.
        /// Note: this only applies to participants joining the league, not participants being migrated to the next season.
        /// </summary>
        protected virtual void OnBeforeAssignParticipantToRank(EntityId participantId, int startingRank) { }

        /// <summary>
        /// Called by the league manager after a participant has been assigned to a division.
        /// Note: this only applies to participants joining the league, not participants being migrated to the next season.
        /// </summary>
        protected virtual void OnAfterAssignParticipantToDivision(EntityId participantId, DivisionIndex assignedDivision) { }

        #endregion
    }
}
