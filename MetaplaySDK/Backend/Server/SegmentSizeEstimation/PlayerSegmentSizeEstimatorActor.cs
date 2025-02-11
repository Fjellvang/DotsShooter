// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.Authentication;
using Metaplay.Server.Database;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server
{
    // SegmentEstimateState

    [MetaSerializable]
    [SupportedSchemaVersions(1, 1)]
    public class SegmentEstimateState : ISchemaMigratable
    {
        [MetaSerializable]
        public struct SegmentSizesSample
        {
            [MetaMember(1)] public MetaTime                             Timestamp;
            [MetaMember(2)] public int                                  NumSamples;
            [MetaMember(3)] public Dictionary<PlayerSegmentId, float>   Data;
        }

        [MetaMember(1)] public MetaTime                     LastEstimateAt;
        [MetaMember(2)] public Queue<SegmentSizesSample>    History;

        #region Schema migrations

        // Empty

        #endregion
    }

    /// <summary>
    /// Represents the database-persisted portion <see cref="PlayerSegmentSizeEstimatorActor"/>.
    /// </summary>
    [Table("SegmentEstimates")]
    public class PersistedSegmentEstimateState : IPersistedEntity
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

        [Required]
        public byte[] Payload { get; set; }   // TaggedSerialized<SegmentEstimateState>

        [Required]
        public int SchemaVersion { get; set; }   // Schema version for object

        [Required]
        public bool IsFinal { get; set; }
    }

    [MetaMessage(MessageCodesCore.SegmentSizeEstimateRequest, MessageDirection.ServerInternal)]
    public class SegmentSizeEstimateRequest : EntityAskRequest<SegmentSizeEstimateResponse>
    {
        public static readonly SegmentSizeEstimateRequest Instance = new SegmentSizeEstimateRequest();
    }

    [MetaMessage(MessageCodesCore.SegmentSizeEstimateResponse, MessageDirection.ServerInternal)]
    public class SegmentSizeEstimateResponse : EntityAskResponse
    {
        public MetaTime                             LastEstimateAt;
        public Dictionary<PlayerSegmentId, float>   SegmentEstimates;
        public long                                 ScannedPlayersCount;
        public long                                 PlayerScanErrorCount;

        public SegmentSizeEstimateResponse() { }
        public SegmentSizeEstimateResponse(Dictionary<PlayerSegmentId, float> segmentEstimates, MetaTime lastEstimateAt,
            long scannedPlayersCount, long playerScanErrorCount)
        {
            LastEstimateAt = lastEstimateAt;
            SegmentEstimates = segmentEstimates;
            ScannedPlayersCount = scannedPlayersCount;
            PlayerScanErrorCount = playerScanErrorCount;
        }
    }

    // PlayerSegmentSizeEstimatorActor

    [EntityConfig]
    public class SegmentSizeEstimatorConfig : PersistedEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.SegmentSizeEstimator;
        public override Type                EntityActorType         => typeof(PlayerSegmentSizeEstimatorActor);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Service;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateSingletonService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// A singleton <see cref="PersistedEntityActor{TPersisted,TPersistedPayload}"/>
    /// that periodically samples the database for players and tries to estimate how large of a
    /// portion of the playerbase belongs to each segment. Asking for the current estimates can be
    /// done with <see cref="EntityActor.EntityAskAsync{TResult}"/> sending a <see cref="SegmentSizeEstimateRequest"/>.
    /// </summary>
    public class PlayerSegmentSizeEstimatorActor : PersistedEntityActor<PersistedSegmentEstimateState, SegmentEstimateState>
    {
        class UpdateCommand
        {
            public static UpdateCommand Instance { get; } = new UpdateCommand();
            private UpdateCommand() { }
        }

        struct SegmentQueryResult
        {
            public string   PlayerId;
            public bool     HasError;
            public bool[]   InSegments;

            public static readonly SegmentQueryResult Error = new() { HasError = true };
        }

        public static readonly EntityId EntityId = EntityId.Create(EntityKindCloudCore.SegmentSizeEstimator, 0);

        protected override sealed AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();
        protected override sealed TimeSpan SnapshotInterval => TimeSpan.FromDays(1);   // Persist state every 24 hours (state is persisted manually otherwise)

        SegmentEstimateState _state;

        static readonly int BatchSize = 16;

        readonly MetaDatabase _db = MetaDatabase.Get(QueryPriority.Lowest);

        Dictionary<PlayerSegmentId, float>  _segmentEstimates;
        bool                                _includeBots;
        bool                                _segmentSizeEstimateRunning;
        int                                 _maxHistorySize;
        MetaDuration                        _maxSampleSizeAge;
        PlayerScanningErrorCounter          _playerScanningErrorReport;


        public PlayerSegmentSizeEstimatorActor(EntityId entityId) : base(entityId)
        {
        }

        protected override sealed async Task Initialize()
        {
            // Try to fetch from database & restore from it (if exists)
            PersistedSegmentEstimateState persisted = await _db.TryGetAsync<PersistedSegmentEstimateState>(_entityId.ToString());
            await InitializePersisted(persisted);

            SegmentationOptions segmentOpts = RuntimeOptionsRegistry.Instance.GetCurrent<SegmentationOptions>();
            SystemOptions systemOpts = RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>();

            _includeBots = systemOpts.AllowBotAccounts;
            _maxHistorySize = segmentOpts.MaxSampleSetsToStore;
            _maxSampleSizeAge = MetaDuration.FromTimeSpan(segmentOpts.MaxSizeSampleAge);

            TimeSpan timeSinceLastUpdate = DateTime.UtcNow - _state.LastEstimateAt.ToDateTime();

            TimeSpan timeToNextUpdate =
                timeSinceLastUpdate < segmentOpts.SizeSamplingInterval?
                    segmentOpts.SizeSamplingInterval - timeSinceLastUpdate :
                    TimeSpan.FromMinutes(1);

            _playerScanningErrorReport = new PlayerScanningErrorCounter(segmentOpts.MaxSampleSetsToStore);

            if (_log.IsDebugEnabled)
            {
                if (_state.LastEstimateAt != MetaTime.Epoch)
                    _log.Debug(Invariant($"Last update was {timeSinceLastUpdate.TotalMinutes:0.0} minutes ago. Next update scheduled in {timeToNextUpdate.TotalMinutes:0.0} minutes."));
                else
                    _log.Debug(Invariant($"Next update scheduled in {timeToNextUpdate.TotalMinutes:0.0} minutes."));
            }

            StartPeriodicTimer(timeToNextUpdate, segmentOpts.SizeSamplingInterval, UpdateCommand.Instance);
        }

        protected override sealed Task<SegmentEstimateState> InitializeNew()
        {
            // Create new state
            SegmentEstimateState state = new SegmentEstimateState();
            state.History = new Queue<SegmentEstimateState.SegmentSizesSample>();

            return Task.FromResult(state);
        }

        protected override sealed Task<SegmentEstimateState> RestoreFromPersisted(PersistedSegmentEstimateState persisted)
        {
            // Deserialize actual state
            SegmentEstimateState state = DeserializePersistedPayload<SegmentEstimateState>(persisted.Payload, resolver: null, logicVersion: null);

            return Task.FromResult(state);
        }

        protected override sealed Task PostLoad(SegmentEstimateState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _state = payload;
            _segmentEstimates = CalculateRunningAverage(_state.History);

            return Task.CompletedTask;
        }

        protected override sealed async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, CurrentSchemaVersion);

            // Serialize and compress the state
            byte[] persistedPayload = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);

            // Persist in database
            PersistedSegmentEstimateState persisted = new PersistedSegmentEstimateState
            {
                EntityId        = _entityId.ToString(),
                PersistedAt     = MetaTime.Now.ToDateTime(),
                Payload         = persistedPayload,
                SchemaVersion   = CurrentSchemaVersion,
                IsFinal         = isFinal,
            };

            if (isInitial)
                await MetaDatabase.Get(QueryPriority.Normal).InsertAsync(persisted).ConfigureAwait(false);
            else
                await MetaDatabase.Get(QueryPriority.Normal).UpdateAsync(persisted).ConfigureAwait(false);
        }

        [CommandHandler]
        Task HandleUpdate(UpdateCommand _)
        {
            if (!_segmentSizeEstimateRunning)
            {
                _segmentSizeEstimateRunning = true;

                Stopwatch timer = Stopwatch.StartNew();

                _log.Debug("Starting segment size estimation...");

                ContinueTaskOnActorContext(
                    Task.Run(CollectSampleSetAsync),
                    async result =>
                    {
                        timer.Stop();
                        _segmentSizeEstimateRunning = false;
                        _log.Debug(Invariant($"Segment size estimation finished in {timer.ElapsedMilliseconds}ms with {result.Item1.NumSamples} samples taken"));

                        if (result.Item1.NumSamples == 0)
                            return;

                        UpdateStateWithSampleSet(result.Item1);

                        // Collect player scanning error report to memory for reporting to dashboard
                        _playerScanningErrorReport.AddPlayerScanReport(result.Item2);
                        _state.LastEstimateAt = MetaTime.FromDateTime(DateTime.UtcNow);

                        await PersistState(false, false);
                    },
                    error =>
                    {
                        _log.Error("Segment size estimate failed: {ex}", error);
                        _segmentSizeEstimateRunning = false;
                    });
            }

            return Task.CompletedTask;
        }


        [EntityAskHandler]
        public SegmentSizeEstimateResponse HandleSegmentStateRequest(SegmentSizeEstimateRequest _)
        {
            return new SegmentSizeEstimateResponse(_segmentEstimates, _state.LastEstimateAt,
                _playerScanningErrorReport.TotalPlayers, _playerScanningErrorReport.TotalErrors);
        }


        /// <summary>
        /// Fetch <see cref="BatchSize"/> amount of players from the db.
        /// Returns an IEnumerable instance that matches them with all the provided segments' conditions.
        /// </summary>
        async Task<List<SegmentQueryResult>> TryProcessSampleSetBatchAsync(IEnumerable<PlayerSegmentInfoBase> segments, string sampleId, ISharedGameConfig sharedGameConfig, int activeLogicVersion, ErrorCounter errorCounter)
        {
            IGameConfigDataResolver resolver = sharedGameConfig;
            List<PersistedPlayerBase> players = await _db.QuerySinglePageRangeAsync<PersistedPlayerBase>(
                opName: "SegmentSizeEstimator",
                sampleId,
                BatchSize,
                EntityId.Create(EntityKindCore.Player, _includeBots ? 0 : Authenticator.NumReservedBotIds).ToString(),
                EntityId.Create(EntityKindCore.Player, EntityId.ValueMask).ToString());

            return players.Where(player => player.Payload != null).Select(player =>
            {
                MetaDebug.Assert(
                    _includeBots || EntityId.ParseFromString(player.EntityId).Value >=
                    Authenticator.NumReservedBotIds, $"Got invalid player id {player.EntityId}");
                try
                {
                    EntityId entityId = EntityId.ParseFromString(player.EntityId);
                    PersistedEntityConfig entityConfig = EntityConfigRegistry.Instance.GetPersistedConfig(EntityKindCore.Player);
                    IPlayerModelBase model = entityConfig.DeserializeDatabasePayload<IPlayerModelBase>(entityId, player.Payload, resolver, activeLogicVersion);
                    model.GameConfig = sharedGameConfig;
                    model.LogicVersion = activeLogicVersion;

                    MetaTime now = MetaTime.Now;
                    model.ResetTime(now);

                    SchemaMigrator migrator = SchemaMigrationRegistry.Instance.GetSchemaMigrator(entityConfig.GetPersistedPayloadType(entityId));
                    migrator.RunMigrations(model, player.SchemaVersion);

                    // Run OnRestoredFromPersistedState the same way as when loading the model in the PlayerActor.
                    // This way an appropriate PlayerModel.CurrentTime will be available in the segment condition evaluation.
                    // \todo [nuutti] There can potentially be scenarios where this is troublesome because now the model
                    //                will diverge from "authoritative" model held by the PlayerActor. Probably not too harmful
                    //                for segment size estimation, and maybe not too harmful elsewhere in practice either.
                    //                But should still think if there's a way to do this that doesn't cause divergence from the actor.
                    MetaDuration elapsedTime = now - MetaTime.FromDateTime(player.PersistedAt);
                    model.OnRestoredFromPersistedState(now, elapsedTime);

                    return new SegmentQueryResult()
                    {
                        PlayerId = player.EntityId,
                        InSegments = segments.Select(segment => segment.MatchesPlayer(model))
                            .ToArray()
                    };
                }
                catch (Exception e)
                {
                    errorCounter.Increment(e);
                }

                return SegmentQueryResult.Error;
            }).ToList();
        }

        /// <summary>
        /// Collects <see cref="SegmentationOptions.SizeSampleCount"/> amount of player samples in batches and aggregates them to a
        /// sample set of <see cref="SegmentEstimateState.SegmentSizesSample"/>. Prunes any duplicate playerIds within a single set.
        /// If no samples could be collected, an empty sample set will be returned.
        /// </summary>
        async Task<(SegmentEstimateState.SegmentSizesSample, PlayerScanningErrorCounter.PlayerScanReport)> CollectSampleSetAsync()
        {
            ActiveGameConfig activeGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get();
            if (activeGameConfig?.BaselineGameConfig == null || activeGameConfig.BaselineGameConfig.SharedConfig.PlayerSegments.Count == 0)
            {
                return default;
            }

            ISharedGameConfig sharedGameConfig = activeGameConfig.BaselineGameConfig.SharedConfig;
            int activeLogicVersion = GlobalStateProxyActor.ActiveClientCompatibilitySettings.Get().ClientCompatibilitySettings.ActiveLogicVersion;
            IEnumerable<PlayerSegmentInfoBase> segments = sharedGameConfig.PlayerSegments.Values;
            int numSamples = RuntimeOptionsRegistry.Instance.GetCurrent<SegmentationOptions>().SizeSampleCount;
            int numBatches = (numSamples + BatchSize - 1) / BatchSize;

            ErrorCounter errorCounter = new ErrorCounter();

            HashSet<string> validPlayers = new();
            int numSegments = segments.Count();
            int[] matchingPlayers = new int[numSegments];
            int playersScanned = 0;

            foreach (EntityId randomPlayerId in GenerateRandomPlayerIds(numBatches, _includeBots))
            {
                List<SegmentQueryResult> batchResults = await TryProcessSampleSetBatchAsync(segments, randomPlayerId.ToString(), sharedGameConfig, activeLogicVersion, errorCounter).ConfigureAwait(false);

                playersScanned += batchResults.Count;

                foreach (SegmentQueryResult batchResult in batchResults)
                {
                    if (!batchResult.HasError && validPlayers.Add(batchResult.PlayerId))
                    {
                        for (int i = 0; i < numSegments; ++i)
                        {
                            if (batchResult.InSegments[i])
                                matchingPlayers[i]++;
                        }
                    }
                }
            }

            SegmentEstimateState.SegmentSizesSample result = new();
            result.Data = validPlayers.Count == 0 ?
                null :
                segments.Select((segment, index) => (segment.ConfigKey, (float)matchingPlayers[index] / validPlayers.Count))
                    .ToDictionary(x => x.ConfigKey, x => x.Item2);
            result.Timestamp = MetaTime.Now;
            result.NumSamples = validPlayers.Count;

            if (_log.IsDebugEnabled && errorCounter.Count == 0)
                _log.Debug("Collected {Count} segment size samples", result.NumSamples);
            else if (errorCounter.Count > 0)
            {
                _log.Warning("Collected {Count} segment size samples, but {NumErrors} error(s) occurred while deserializing players from database:\n{ErrorMessages}",
                    result.NumSamples, errorCounter.Count, errorCounter.GetErrorString());
            }

            return (result, PlayerScanningErrorCounter.CreateNewPlayerScanReport(playersScanned, errorCounter.Count));
        }

        /// <summary>
        /// Generate a set random unique playerIds. Can generate bot-reserved ids if <paramref name="botsOk"/> is true.
        /// </summary>
        static IEnumerable<EntityId> GenerateRandomPlayerIds(int count, bool botsOk)
        {
            HashSet<EntityId> result = new HashSet<EntityId>();

            while (result.Count < count)
            {
                EntityId playerId = EntityId.CreateRandom(EntityKindCore.Player);
                if (botsOk || playerId.Value >= Authenticator.NumReservedBotIds)
                {
                    result.Add(playerId);
                }
            }

            return result;
        }

        /// <summary>
        /// Calculate a single Dictionary from all the given sample sets by weighted-averaging the values.
        /// </summary>
        static Dictionary<PlayerSegmentId, float> CalculateRunningAverage(IEnumerable<SegmentEstimateState.SegmentSizesSample> sampleSets)
        {
            ActiveGameConfig activeGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get();
            return activeGameConfig.BaselineGameConfig.SharedConfig.PlayerSegments.Values.Select(segment =>
            {
                // Calculate weighted average of the segment ratio values over the sample sets.
                // The weight is the number of samples in the sample set.
                // Thus the result will be the proportion of matching players, over all the sample sets.
                //
                // Note that the samples in the sample sets do not include erroneous samples (= players), for example
                // when an exception was thrown from a segment's evaluation for a specific player.
                // That is the main reason why sample counts may differ between sample sets
                // and weighting is needed. (Another reason is that the sample count configuration can change.)

                double numMatchingPlayers = 0.0;
                long totalNumPlayers = 0;
                foreach (SegmentEstimateState.SegmentSizesSample sampleSet in sampleSets)
                {
                    if (sampleSet.Data.TryGetValue(segment.SegmentId, out float ratio))
                    {
                        numMatchingPlayers += (double)ratio * (double)sampleSet.NumSamples;
                        totalNumPlayers += sampleSet.NumSamples;
                    }
                }

                float? weightedAverage = totalNumPlayers == 0
                                         ? null
                                         : (float)(numMatchingPlayers / (double)totalNumPlayers);

                return (segment: segment.SegmentId, ratio: weightedAverage);
            }).Where(x => x.ratio.HasValue).ToDictionary(x => x.segment, x => x.ratio.Value);
        }

        /// <summary>
        /// Appends the given <see cref="SegmentEstimateState.SegmentSizesSample"/>
        /// to <see cref="SegmentEstimateState.History"/>. Also prunes any old sample sets from history.
        /// Calculates the average from all the collected samples.
        /// </summary>
        void UpdateStateWithSampleSet(in SegmentEstimateState.SegmentSizesSample sampleSet)
        {
            if (sampleSet.NumSamples > 0)
            {
                MetaTime historyAgeThreshold = MetaTime.Now - _maxSampleSizeAge;
                // Prune entries from the sample history to make room for the new sample and to get rid of samples that are older than MaxSizeSampleAge
                while ((_state.History.Count >= _maxHistorySize) || (_state.History.TryPeek(out SegmentEstimateState.SegmentSizesSample head) && head.Timestamp < historyAgeThreshold))
                    _state.History.Dequeue();
                // Add the new sample to the history queue
                _state.History.Enqueue(sampleSet);
            }

            _segmentEstimates = CalculateRunningAverage(_state.History);
        }
    }
}
