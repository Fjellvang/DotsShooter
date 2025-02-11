// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.DatabaseScan.User;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using static Metaplay.Cloud.Sharding.EntityShard;

namespace Metaplay.Server.DatabaseScan
{
    [Table("DatabaseScanWorkers")]
    public class PersistedDatabaseScanWorker : IPersistedEntity
    {
        [Key]
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string   EntityId        { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime PersistedAt     { get; set; }

        [Required]
        public byte[]   Payload         { get; set; }   // TaggedSerialized<DatabaseScanWorkerState>

        [Required]
        public int      SchemaVersion   { get; set; }   // Schema version for object

        [Required]
        public bool     IsFinal         { get; set; }
    }

    /// <summary>
    /// Description of the status of a some database scan work.
    /// This is stuff that workers report to others.
    /// </summary>
    [MetaSerializable]
    public class DatabaseScanWorkStatus
    {
        [MetaMember(1)] public DatabaseScanJobId                Id                      { get; private set; }
        [MetaMember(2)] public DatabaseScanWorkPhase            Phase                   { get; private set; }
        [MetaMember(3)] public DatabaseScanStatistics           ScanStatistics          { get; private set; }
        [MetaMember(4)] public DatabaseScanProcessingStatistics ProcessingStatistics    { get; private set; }
        [MetaMember(5)] public long                             StatusObservationIndex  { get; private set; } // For comparing different statuses of the same job from the same worker. Used to resolve the ordering of otherwise unordered status messages from the worker to the coordinator.

        public DatabaseScanWorkStatus(){ }
        public DatabaseScanWorkStatus(DatabaseScanJobId id, DatabaseScanWorkPhase phase, DatabaseScanStatistics scanStatistics, DatabaseScanProcessingStatistics processingStatistics, long statusObservationIndex)
        {
            Id                      = id;
            Phase                   = phase;
            ScanStatistics          = scanStatistics;
            ProcessingStatistics    = processingStatistics;
            StatusObservationIndex  = statusObservationIndex;
        }
    }

    /// <summary>
    /// Statistics about database scanning.
    /// </summary>
    [MetaSerializable]
    public class DatabaseScanStatistics
    {
        [MetaMember(1)] public int      NumItemsScanned             { get; set; } = 0;
        [MetaMember(2)] public float    ScannedRatioEstimate        { get; set; } = 0f;
        [MetaMember(3)] public int      NumWorkProcessorRecreations { get; set; } = 0;
        [MetaMember(4)] public int      NumSurrendered              { get; set; } = 0;
        [MetaMember(5)] public long     WorkerPersistTotalBytes     { get; set; } = 0;
        [MetaMember(6)] public int      WorkerPersistCount          { get; set; } = 0;

        public static DatabaseScanStatistics ComputeAggregate(IEnumerable<DatabaseScanStatistics> parts)
        {
            DatabaseScanStatistics aggregate = new DatabaseScanStatistics();

            foreach (DatabaseScanStatistics part in parts)
            {
                aggregate.NumItemsScanned               += part.NumItemsScanned;
                aggregate.ScannedRatioEstimate          += part.ScannedRatioEstimate; // \note ScannedRatioEstimate is not additive but a proportion; will be divided by count after this loop
                aggregate.NumWorkProcessorRecreations   += part.NumWorkProcessorRecreations;
                aggregate.NumSurrendered                += part.NumSurrendered;
                aggregate.WorkerPersistTotalBytes       += part.WorkerPersistTotalBytes;
                aggregate.WorkerPersistCount            += part.WorkerPersistCount;
            }

            if (parts.Count() > 0)
                aggregate.ScannedRatioEstimate /= parts.Count();

            return aggregate;
        }
    }

    /// <summary>
    /// Worker's phase for a database scan job.
    /// When initializing a new job, a worker is initially in the Paused phase.
    /// Then it can be commanded to move into the Running phase.
    /// </summary>
    [MetaSerializable]
    public enum DatabaseScanWorkPhase
    {
        /// <summary> Worker is paused, work is not yet finished. This is the initial phase. </summary>
        Paused = 2,
        /// <summary> Worker is scanning database, items are being processed. </summary>
        Running = 0,
        /// <summary>
        /// Worker is no longer working, either due to normal completion of its work
        /// or due to surrendering, and is just waiting to be stopped (i.e. told to forget the job).
        /// </summary>
        Finished = 1,
    }

    /// <summary>
    /// Represents the I'th one piece of a N-piece job, where I is <see cref="WorkerIndex"/>
    /// and N is <see cref="NumWorkers"/>.
    /// </summary>
    [MetaSerializable]
    public struct DatabaseScanWorkShard
    {
        [MetaMember(1)] public int WorkerIndex  { get; private set; }
        [MetaMember(2)] public int NumWorkers   { get; private set; }

        public DatabaseScanWorkShard(int workerIndex, int numWorkers)
        {
            WorkerIndex = workerIndex;
            NumWorkers  = numWorkers;
        }
    }

    /// <summary>
    /// This is only used to migrate from legacy database worker states, where each worker used just a single iterator:
    /// scanning started from first database shard and going forward from there.
    /// Nowadays, each worker has one iterator for each database shard, and reads from them
    /// in a round-robin way. We use <see cref="DatabaseSingleShardIterator"/> for that.
    /// See <see cref="DatabaseScanJobWorkState.EnsureMigratedToPerShardIterators"/>.
    /// </summary>
    [MetaSerializable]
    public class LegacyDatabaseIterator
    {
        [MetaMember(1)] public int     ShardIndex           { get; private set; } = 0;
        [MetaMember(2)] public string  StartKeyExclusive    { get; private set; } = "";
        [MetaMember(3)] public bool    IsFinished           { get; private set; } = false;
    }

    [MetaSerializable]
    public class DatabaseSingleShardIterator
    {
        [MetaMember(1)] public string   StartKeyExclusive   { get; }
        [MetaMember(2)] public bool     IsFinished          { get; }

        [MetaDeserializationConstructor]
        DatabaseSingleShardIterator(string startKeyExclusive, bool isFinished) { StartKeyExclusive = startKeyExclusive; IsFinished = isFinished; }

        public static readonly DatabaseSingleShardIterator Start = new DatabaseSingleShardIterator(startKeyExclusive: "", isFinished: false);
        public static readonly DatabaseSingleShardIterator End   = new DatabaseSingleShardIterator(startKeyExclusive: null, isFinished: true);

        public static DatabaseSingleShardIterator CreateIteratorAtKey(string startKeyExclusive)
        {
            return new DatabaseSingleShardIterator(
                startKeyExclusive ?? throw new ArgumentNullException(nameof(startKeyExclusive)),
                isFinished: false);
        }
    }

    /// <summary> The manner in which a worker is stopped. </summary>
    [MetaSerializable]
    public enum DatabaseScanWorkerStopFlavor
    {
        /// <summary> Worker is cleanly stopped because its phase is Finished. </summary>
        Finished,
        /// <summary> Worker is stopped due to a cancellation. This can be done regardless of the work phase. </summary>
        Cancel,
    }

    /// <summary>
    /// Commands a worker to initialize (the given shard of) the given job, if it's not already initialized.
    /// The worker is initially put into Paused phase.
    /// Represents also an assertion that the worker isn't currently doing any other job, and
    /// doesn't have the job in any phase other than Paused.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerEnsureInitialized, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerEnsureInitialized : EntityAskRequest<DatabaseScanWorkerInitializedOk>
    {
        public DatabaseScanJobId        JobId   { get; private set; }
        public DatabaseScanJobSpec      JobSpec { get; private set; }
        public DatabaseScanWorkShard    Shard   { get; private set; }

        public DatabaseScanWorkerEnsureInitialized(){ }
        public DatabaseScanWorkerEnsureInitialized(DatabaseScanJobId jobId, DatabaseScanJobSpec jobSpec, DatabaseScanWorkShard shard)
        {
            JobId   = jobId;
            JobSpec = jobSpec;
            Shard   = shard;
        }
    }

    /// <summary>
    /// Response to <see cref="DatabaseScanWorkerEnsureInitialized"/>, reporting also current status.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerInitializedOk, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerInitializedOk : EntityAskResponse
    {
        public DatabaseScanWorkStatus ActiveJobStatus { get; private set; }

        public DatabaseScanWorkerInitializedOk(){ }
        public DatabaseScanWorkerInitializedOk(DatabaseScanWorkStatus activeJobStatus) { ActiveJobStatus = activeJobStatus; }
    }

    /// <summary>
    /// Commands a worker to resume after it's been paused with <see cref="DatabaseScanWorkerEnsurePaused"/>,
    /// if it's not already resumed.
    /// Asserts that the worker currently has a job.
    /// Resuming only has an effect if the job's work phase is Paused;
    /// resuming a Finished or Running job doesn't change anything.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerEnsureResumed, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerEnsureResumed : EntityAskRequest<DatabaseScanWorkerResumedOk>
    {
        public static readonly DatabaseScanWorkerEnsureResumed Instance = new DatabaseScanWorkerEnsureResumed();
    }

    /// <summary>
    /// Response to <see cref="DatabaseScanWorkerEnsureResumed"/>, reporting also status just after reacting to the resume command.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerResumedOk, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerResumedOk : EntityAskResponse
    {
        public DatabaseScanWorkStatus ActiveJobStatus { get; private set; }

        public DatabaseScanWorkerResumedOk(){ }
        public DatabaseScanWorkerResumedOk(DatabaseScanWorkStatus activeJobStatus) { ActiveJobStatus = activeJobStatus; }
    }

    /// <summary>
    /// Commands a worker to pause the current job, if it's not already paused.
    /// Asserts that the worker currently has a job.
    /// Pausing only has an effect if the job's work phase is Running;
    /// pausing a Finished or Paused job doesn't change anything.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerEnsurePaused, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerEnsurePaused : EntityAskRequest<DatabaseScanWorkerPausedOk>
    {
        public static readonly DatabaseScanWorkerEnsurePaused Instance = new DatabaseScanWorkerEnsurePaused();
    }

    /// <summary>
    /// Response to <see cref="DatabaseScanWorkerEnsurePaused"/>, reporting also status just after reacting to the pause command.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerPausedOk, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerPausedOk : EntityAskResponse
    {
        public DatabaseScanWorkStatus ActiveJobStatus { get; private set; }

        public DatabaseScanWorkerPausedOk(){ }
        public DatabaseScanWorkerPausedOk(DatabaseScanWorkStatus activeJobStatus) { ActiveJobStatus = activeJobStatus; }
    }

    /// <summary>
    /// Commands a worker to stop its active job, if any.
    /// Also, the given stop flavor can place assertions on the worker's state.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerEnsureStopped, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerEnsureStopped : EntityAskRequest<DatabaseScanWorkerStoppedOk>
    {
        public DatabaseScanWorkerStopFlavor StopFlavor { get; private set; }

        public DatabaseScanWorkerEnsureStopped(){ }
        public DatabaseScanWorkerEnsureStopped(DatabaseScanWorkerStopFlavor stopFlavor)
        {
            StopFlavor = stopFlavor;
        }
    }

    /// <summary>
    /// Response to <see cref="DatabaseScanWorkerEnsureStopped"/>, reporting also status, if any, just before reacting to the stop command.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerStoppedOk, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerStoppedOk : EntityAskResponse
    {
        /// <summary>
        /// Status of worker's active job just before reacting to the stop command, if any; null if none.
        /// </summary>
        public DatabaseScanWorkStatus ActiveJobJustBefore { get; private set; }

        public DatabaseScanWorkerStoppedOk(){ }
        public DatabaseScanWorkerStoppedOk(DatabaseScanWorkStatus activeJobJustBefore)
        {
            ActiveJobJustBefore = activeJobJustBefore;
        }
    }

    /// <summary>
    /// Dummy message occasionally sent by coordinator to ensure that workers are woken up.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerEnsureAwake, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerEnsureAwake : MetaMessage
    {
        public static readonly DatabaseScanWorkerEnsureAwake Instance = new DatabaseScanWorkerEnsureAwake();
    }

    /// <summary>
    /// Occasionally sent by worker to coordinator, while a job is active,
    /// to report its status.
    /// </summary>
    [MetaMessage(MessageCodesCore.DatabaseScanWorkerStatusReport, MessageDirection.ServerInternal)]
    public class DatabaseScanWorkerStatusReport : MetaMessage
    {
        /// <summary>
        /// Status of worker's currently-active job, if any; null if none.
        /// </summary>
        public DatabaseScanWorkStatus ActiveJob { get; private set; }

        public DatabaseScanWorkerStatusReport(){ }
        public DatabaseScanWorkerStatusReport(DatabaseScanWorkStatus activeJob) { ActiveJob = activeJob; }
    }

    /// <summary>
    /// State of database scan work for a specific job.
    /// </summary>
    [MetaSerializable]
    public class DatabaseScanJobWorkState
    {
        [MetaMember(1)] public DatabaseScanJobId        Id                              { get; private set; }
        [MetaMember(2)] public DatabaseScanJobSpec      Spec                            { get; private set; }
        [MetaMember(3)] public DatabaseScanWorkShard    WorkShard                       { get; private set; }
        [MetaMember(4)] public DatabaseScanWorkPhase    Phase                           { get; set; }

        /// <summary>
        /// An iterator (cursor) for each database shard (this array is indexed by DB shard index).
        /// The scan worker advances these in an interleaved manner, <see cref="DatabaseShardIndex"/>
        /// identifying the next database shard to scan from.
        /// </summary>
        [MetaMember(11)] public DatabaseSingleShardIterator[] DatabaseShardIterators    { get; set; }
        /// <summary>
        /// Identifies the next database shard to scan from.
        /// The scan iterator is stored in <see cref="DatabaseShardIterators"/> at this index.
        /// </summary>
        /// <remarks>
        /// We maintain the following invariants:<br/>
        /// - <see cref="DatabaseShardIndex"/> is withing the bounds of <see cref="DatabaseShardIterators"/>.<br/>
        /// - If there is at least 1 non-finished iterator in <see cref="DatabaseShardIterators"/>
        ///   (i.e. with false <see cref="DatabaseSingleShardIterator.IsFinished"/>), then this index
        ///   identifies a non-finished iterator.
        ///   (If all iterators are finished, this index may refer to any of them.)<br/>
        /// After modifying <see cref="DatabaseShardIterators"/> and/or <see cref="DatabaseShardIndex"/>,
        /// you can use <see cref="GetValidDatabaseShardIndex"/> to maintain this.
        /// </remarks>
        [MetaMember(12)] public int                           DatabaseShardIndex        { get; set; }
        /// <summary>
        /// Legacy, used only in <see cref="EnsureMigratedToPerShardIterators"/>.
        /// </summary>
        [MetaMember(5)] public LegacyDatabaseIterator         LegacyDatabaseIterator    { get; set; }

        [MetaMember(10)] public int                     ExplicitListIndex               { get; set; }
        [MetaMember(6)] public DatabaseScanStatistics   ScanStatistics                  { get; private set; }
        [MetaMember(7)] public DatabaseScanProcessor    Processor                       { get; set; }
        [MetaMember(8)] public int                      CrashStallCounter               { get; set; }
        [MetaMember(9)] public long                     RunningStatusObservationIndex   { get; private set; }

        /// <summary>
        /// Migrate from an old state (unless already migrated) where we used <see cref="LegacyDatabaseIterator"/>.
        /// Sets an equivalent state in <see cref="DatabaseShardIterators"/> and <see cref="DatabaseShardIndex"/>,
        /// for the current number of database shards (<paramref name="numDatabaseShards"/>).
        /// </summary>
        public void EnsureMigratedToPerShardIterators(IMetaLogger log, int numDatabaseShards)
        {
            if (DatabaseShardIterators != null)
            {
                // Already migrated.
                return;
            }

            if (LegacyDatabaseIterator == null)
                throw new MetaAssertException($"Trying to migrate to {nameof(DatabaseShardIterators)} but {nameof(LegacyDatabaseIterator)} is null - shouldn't happen");

            // Create DatabaseShardIterators equivalent to LegacyDatabaseIterator.
            DatabaseShardIterators =
                Enumerable.Range(0, numDatabaseShards)
                .Select(shardNdx =>
                {
                    if (LegacyDatabaseIterator.IsFinished)
                    {
                        // Scanning had already finished, so accordingly create shard iterators representing a finished scan.
                        return DatabaseSingleShardIterator.End;
                    }
                    else if (shardNdx < LegacyDatabaseIterator.ShardIndex)
                    {
                        // Legacy iterator was already past this shard, so this shard is finished.
                        return DatabaseSingleShardIterator.End;
                    }
                    else if (shardNdx == LegacyDatabaseIterator.ShardIndex)
                    {
                        // Legacy iterator was iterating this shard, so continue from where it was at.
                        return DatabaseSingleShardIterator.CreateIteratorAtKey(LegacyDatabaseIterator.StartKeyExclusive);
                    }
                    else
                    {
                        // Legacy iterator had not yet gotten to this shard, so start this shard from the beginning.
                        return DatabaseSingleShardIterator.Start;
                    }
                })
                .ToArray();
            // Initialize database shard starting index according to our worker index.
            // But also use GetValidDatabaseShardIndex to ensure DatabaseShardIndex points at a non-finished iterator (if any).
            DatabaseShardIndex = GetOffsetDatabaseShardStartingIndex(WorkShard, numDatabaseShards);
            DatabaseShardIndex = GetValidDatabaseShardIndex(DatabaseShardIndex, DatabaseShardIterators);
            log.Info("Migrated legacy database iterator {LegacyIterator} to new per-shard iterators {ShardIterators}, current shard index {ShardIndex}", PrettyPrint.Compact(LegacyDatabaseIterator), PrettyPrint.Compact(DatabaseShardIterators), DatabaseShardIndex);
            LegacyDatabaseIterator = null;
        }

        /// <summary>
        /// If the number of database shards has changed, adjust <see cref="DatabaseShardIterators"/>
        /// (and <see cref="DatabaseShardIndex"/>) to reflect that.
        /// </summary>
        public void EnsureConsistentWithNumDatabaseShards(IMetaLogger log, int numDatabaseShards)
        {
            if (DatabaseShardIterators.Length == numDatabaseShards)
            {
                // No change.
                return;
            }

            // Create new iterators.
            // The database has been resharded, so we cannot do a perfect job with the remainder
            // of the scan, because items have been moved around and we don't force the shard
            // iterators to move in lockstep. So we choose between possibly processing some
            // items more than once, or not processing some items at all.
            // We choose to possibly process some items more than once. Specifically:
            // - If scan is fully finished (all old iterators were finished), all of the new iterators are finished.
            // - Otherwise, set _all_ of the new iterators to the _lowest_ iterator among the
            //   old iterators.

            DatabaseSingleShardIterator[] newIterators;

            if (DatabaseShardIterators.All(it => it.IsFinished))
            {
                newIterators =
                    Enumerable.Range(0, numDatabaseShards)
                    .Select(shardNdx => DatabaseSingleShardIterator.End)
                    .ToArray();
            }
            else
            {
                string minIteratorKey =
                    DatabaseShardIterators
                    .Where(it => !it.IsFinished)
                    .Select(it => it.StartKeyExclusive)
                    .Min(StringComparer.Ordinal);

                newIterators =
                    Enumerable.Range(0, numDatabaseShards)
                    .Select(shardNdx => DatabaseSingleShardIterator.CreateIteratorAtKey(minIteratorKey))
                    .ToArray();
            }
            // Ensure DatabaseShardIndex is valid for the new iterators.
            DatabaseShardIndex = GetValidDatabaseShardIndex(DatabaseShardIndex, newIterators);
            log.Info("Updated database shard iterators due to resharding: old: {OldIterators}, new: {NewIterators}, current shard index {ShardIndex}", PrettyPrint.Compact(DatabaseShardIterators), PrettyPrint.Compact(newIterators), DatabaseShardIndex);
            DatabaseShardIterators = newIterators;
        }

        /// <summary>
        /// Return the initial value for <see cref="DatabaseShardIndex"/>.
        /// It is offset according to the worker index, so different workers
        /// in the same job start scanning from different database shards.
        /// </summary>
        static int GetOffsetDatabaseShardStartingIndex(DatabaseScanWorkShard workShard, int numDatabaseShards)
        {
            return workShard.WorkerIndex * numDatabaseShards / workShard.NumWorkers;
        }

        /// <summary>
        /// Return a value for <see cref="DatabaseShardIndex"/> that satisfies the invariants
        /// described in the doc comment of <see cref="DatabaseShardIndex"/>.
        /// If <paramref name="index"/> is already a valid index, then that is returned;
        /// otherwise, the index is incremented (and wrapped) until it identifies a non-finished
        /// iterator.
        /// </summary>
        public static int GetValidDatabaseShardIndex(int index, DatabaseSingleShardIterator[] iterators)
        {
            if (iterators.All(it => it.IsFinished))
                return index;
            else
            {
                index %= iterators.Length;
                while (iterators[index].IsFinished)
                    index = (index + 1) % iterators.Length;
                return index;
            }
        }

        public DatabaseScanWorkStatus ObserveStatus()
        {
            long statusObservationIndex = RunningStatusObservationIndex++;
            return new DatabaseScanWorkStatus(Id, Phase, ScanStatistics, Processor.Stats, statusObservationIndex);
        }

        public bool HasMoreItemsToScan
        {
            get
            {
                if (Spec.ExplicitEntityList != null)
                    return ExplicitListIndex < Spec.ExplicitEntityList.Count;
                else
                    return !DatabaseShardIterators.All(it => it.IsFinished);
            }
        }

        public DatabaseScanJobWorkState(){ }
        public DatabaseScanJobWorkState(DatabaseScanJobId id, DatabaseScanJobSpec spec, DatabaseScanWorkShard workShard, int numDatabaseShards)
        {
            Id                  = id;
            Spec                = spec;
            WorkShard           = workShard;
            Phase               = DatabaseScanWorkPhase.Paused;

            DatabaseShardIterators =
                Enumerable.Range(0, numDatabaseShards)
                .Select(shardNdx => DatabaseSingleShardIterator.Start)
                .ToArray();
            DatabaseShardIndex = workShard.WorkerIndex * numDatabaseShards / workShard.NumWorkers;

            ExplicitListIndex   = 0;
            ScanStatistics      = new DatabaseScanStatistics();
            Processor           = spec.CreateProcessor(initialStatisticsMaybe: null);
            CrashStallCounter   = 0;
        }
    }

    /// <summary>
    /// State of a database scan worker.
    /// </summary>
    [MetaSerializable]
    [SupportedSchemaVersions(1, 1)]
    public class DatabaseScanWorkerState : ISchemaMigratable
    {
        [MetaMember(1)] public DatabaseScanJobWorkState ActiveJob { get; set; } = null;

        public DatabaseScanWorkerState(){ }

        public void PostLoad(IMetaLogger log, int numDatabaseShards)
        {
            if (ActiveJob != null)
            {
                // Do adjustments on DatabaseShardIterators and DatabaseShardIndex:
                // - migration from legacy iterator (R30 and older)
                // - adjust according to possible database resharding
                // Note that if database resharding has happened in the same update
                // as legacy iterator migration, this is not guaranteed to produce ideal results.
                // The legacy iterator schema did not support resharding properly at all.
                // \note EnsureMigratedToPerShardIterators could be done as an ISchemaMigratable migration,
                //       but we want `log` here.
                ActiveJob.EnsureMigratedToPerShardIterators(log, numDatabaseShards);
                ActiveJob.EnsureConsistentWithNumDatabaseShards(log, numDatabaseShards);
            }
        }

        #region Schema migrations

        // No migrations

        #endregion
    }

    [EntityConfig]
    internal sealed class DatabaseScanWorkerConfig : PersistedEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.DatabaseScanWorker;
        public override Type                EntityActorType         => typeof(DatabaseScanWorkerActor);
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.Logic;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateStaticSharded();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Actor that does database scan work, upon command from <see cref="DatabaseScanCoordinatorActor"/>.
    /// Each worker performs the work for a subset (shard) of the job,
    /// i.e. a subset of a database (in practice, a range of the entity id value space).
    ///
    /// <para>
    /// Workers are coordinated by the coordinator. To ensure a consistent state in the
    /// coordinator-workers-system, the worker shall persist its state when its state
    /// changes in a relevant way (e.g. when a job is initialized, or when the job phase changes).
    /// For example, when the worker is told to initialize a job, it shall make sure that
    /// the initialized job state is persisted before responding "OK".
    /// </para>
    /// </summary>
    /// <remarks>
    /// Database job work code speaks of two kinds of "shards":
    /// the work shards of these sharded database scan jobs,
    /// and the shards of shardy database.
    /// These are unrelated to each other, don't get confused.
    /// </remarks>
    public class DatabaseScanWorkerActor : PersistedEntityActor<PersistedDatabaseScanWorker, DatabaseScanWorkerState>, DatabaseScanProcessor.IContext
    {
        class ActiveJobUpdate { public static ActiveJobUpdate Instance = new ActiveJobUpdate(); }

        static readonly Prometheus.Counter c_itemsQueried = Prometheus.Metrics.CreateCounter("game_databasescan_items_queried_total", "Number of items queried by database scan workers (by job tag)", "job_tag");

        class ActiveJobRuntimeState
        {
            public DateTime                 NextScanAt;
            public DateTime                 NextProcessorTickAt;
            public DateTime                 NextPersistAt;
            public DateTime                 NextStatusReportAt;

            public ActiveJobRuntimeState(DateTime nextScanAt, DateTime nextProcessorTickAt, DateTime nextPersistAt, DateTime nextStatusReportAt)
            {
                NextScanAt          = nextScanAt;
                NextProcessorTickAt = nextProcessorTickAt;
                NextPersistAt       = nextPersistAt;
                NextStatusReportAt  = nextStatusReportAt;
            }
        }

        protected override sealed AutoShutdownPolicy    ShutdownPolicy                      => AutoShutdownPolicy.ShutdownNever();
        protected override sealed TimeSpan              SnapshotInterval                    => TimeSpan.FromMinutes(3);

        static TimeSpan                     StatusReportInterval                            = TimeSpan.FromSeconds(1);

        const int                           MaxCrashStallCounterBeforeProcessorRecreation   = 2;
        const int                           MaxCrashStallCounterBeforeSurrender             = 6;

        DatabaseScanWorkerState             _state;
        ActiveJobRuntimeState               _activeJobRuntimeState;

        public DatabaseScanWorkerActor(EntityId entityId) : base(entityId)
        {
        }

        protected override sealed async Task Initialize()
        {
            // Try to fetch from database & restore from it (if exists)
            PersistedDatabaseScanWorker persisted = await MetaDatabase.Get(QueryPriority.Normal).TryGetAsync<PersistedDatabaseScanWorker>(_entityId.ToString());
            await InitializePersisted(persisted);
        }

        protected override sealed Task<DatabaseScanWorkerState> InitializeNew()
        {
            // Create new state
            DatabaseScanWorkerState state = new DatabaseScanWorkerState();

            return Task.FromResult(state);
        }

        protected override sealed Task<DatabaseScanWorkerState> RestoreFromPersisted(PersistedDatabaseScanWorker persisted)
        {
            // Deserialize actual state
            DatabaseScanWorkerState state = DeserializePersistedPayload<DatabaseScanWorkerState>(persisted.Payload, resolver: null, logicVersion: null);

            return Task.FromResult(state);
        }

        protected override sealed async Task PostLoad(DatabaseScanWorkerState payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _state = payload;
            _state.PostLoad(_log, numDatabaseShards: MetaDatabase.Get().NumActiveShards);

            DatabaseScanJobWorkState activeJob = _state.ActiveJob;

            if (activeJob != null)
            {
                _log.Info("PostLoad: active job: id={JobId}, phase={JobPhase}, specType={JobSpecType}", activeJob.Id, activeJob.Phase, activeJob.Spec.GetType().Name);
                InitActiveJobRuntimeState(activeJob, _self);

                if (activeJob.Phase == DatabaseScanWorkPhase.Running)
                {
                    activeJob.CrashStallCounter++;
                    await PersistStateIntermediate();

                    if (activeJob.CrashStallCounter > MaxCrashStallCounterBeforeSurrender)
                    {
                        _log.Warning("PostLoad: CrashStallCounter is {CrashStallCounter}, surrendering (limit is {MaxCrashStallCounterBeforeSurrender})", activeJob.CrashStallCounter, MaxCrashStallCounterBeforeSurrender);
                        activeJob.Phase = DatabaseScanWorkPhase.Finished;
                        activeJob.ScanStatistics.NumSurrendered = 1;
                        await PersistStateIntermediate();
                    }
                    else if (activeJob.CrashStallCounter > MaxCrashStallCounterBeforeProcessorRecreation)
                    {
                        _log.Warning("PostLoad: CrashStallCounter is {CrashStallCounter}, creating new processor (limit is {MaxCrashStallCounterBeforeProcessorRecreation})", activeJob.CrashStallCounter, MaxCrashStallCounterBeforeProcessorRecreation);
                        DatabaseScanProcessor newProcessor = activeJob.Spec.CreateProcessor(activeJob.Processor.Stats);
                        activeJob.Processor = newProcessor;
                        activeJob.ScanStatistics.NumWorkProcessorRecreations++;
                        await PersistStateIntermediate();
                    }
                    else
                        _log.Debug("PostLoad: CrashStallCounter is {CrashStallCounter}", activeJob.CrashStallCounter);
                }
            }
        }

        protected override sealed async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            _log.Debug("Persisting state (isInitial={IsInitial}, isFinal={IsFinal}, schemaVersion={SchemaVersion})", isInitial, isFinal, CurrentSchemaVersion);

            // Serialize and compress the state
            byte[] persistedPayload = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);

            if (_state.ActiveJob != null)
            {
                DatabaseScanJobWorkState activeJob = _state.ActiveJob;
                activeJob.ScanStatistics.WorkerPersistCount++;
                activeJob.ScanStatistics.WorkerPersistTotalBytes += persistedPayload.Length;
            }

            // Persist in database
            PersistedDatabaseScanWorker persisted = new PersistedDatabaseScanWorker
            {
                EntityId        = _entityId.ToString(),
                PersistedAt     = MetaTime.Now.ToDateTime(),
                Payload         = persistedPayload,
                SchemaVersion   = CurrentSchemaVersion,
                IsFinal         = isFinal,
            };

            if (isInitial)
                await MetaDatabase.Get().InsertAsync(persisted).ConfigureAwait(false);
            else
                await MetaDatabase.Get().UpdateAsync(persisted).ConfigureAwait(false);
        }

        [EntityAskHandler]
        private async Task HandleDatabaseScanWorkerEnsureInitialized(EntityAsk<DatabaseScanWorkerInitializedOk> ask, DatabaseScanWorkerEnsureInitialized ensureInitialized)
        {
            if (_state.ActiveJob != null)
            {
                if (_state.ActiveJob.Id == ensureInitialized.JobId)
                {
                    if (_state.ActiveJob.Phase == DatabaseScanWorkPhase.Paused)
                        _log.Info("Got a request to initialize job {JobId}, we already have it", ensureInitialized.JobId);
                    else
                        throw new InvalidOperationException($"Got a request to initialize job {ensureInitialized.JobId}, but we already have it in {_state.ActiveJob.Phase} phase");
                }
                else
                    throw new InvalidOperationException($"Got a request to initialize job {ensureInitialized.JobId}, but we already have a different job {_state.ActiveJob.Id}");
            }
            else
            {
                _state.ActiveJob = new DatabaseScanJobWorkState(ensureInitialized.JobId, ensureInitialized.JobSpec, ensureInitialized.Shard, MetaDatabase.Get().NumActiveShards);
                await PersistStateIntermediate();
                InitActiveJobRuntimeState(_state.ActiveJob, _self);
                _log.Info("Initialized new job {JobId}", ensureInitialized.JobId);
            }

            ReplyToAsk(ask, new DatabaseScanWorkerInitializedOk(_state.ActiveJob.ObserveStatus()));
        }

        [EntityAskHandler]
        private async Task HandleDatabaseScanWorkerEnsureResumed(EntityAsk<DatabaseScanWorkerResumedOk> ask, DatabaseScanWorkerEnsureResumed _)
        {
            if (_state.ActiveJob != null)
            {
                DatabaseScanJobWorkState job = _state.ActiveJob;

                if (job.Phase == DatabaseScanWorkPhase.Paused)
                {
                    job.Phase = DatabaseScanWorkPhase.Running;
                    await PersistStateIntermediate();
                    _log.Info("Resumed job {JobId}", job.Id);
                }
                else if (job.Phase == DatabaseScanWorkPhase.Finished)
                {
                    _log.Info("Got a request to resume job, ours ({JobId}) is already Finished", job.Id);
                }
                else
                {
                    MetaDebug.Assert(job.Phase == DatabaseScanWorkPhase.Running, "Unknown work phase {0}", job.Phase);
                    _log.Info("Got a request to resume job, ours ({JobId}) is already Running", job.Id);
                }
            }
            else
                throw new InvalidOperationException("Got a request to resume job, but we have no job");

            ReplyToAsk(ask, new DatabaseScanWorkerResumedOk(_state.ActiveJob.ObserveStatus()));
        }

        [EntityAskHandler]
        private async Task HandleDatabaseScanWorkerEnsurePaused(EntityAsk<DatabaseScanWorkerPausedOk> ask, DatabaseScanWorkerEnsurePaused _)
        {
            if (_state.ActiveJob != null)
            {
                DatabaseScanJobWorkState job = _state.ActiveJob;

                if (job.Phase == DatabaseScanWorkPhase.Running)
                {
                    job.Phase = DatabaseScanWorkPhase.Paused;
                    await PersistStateIntermediate();
                    _log.Info("Paused job {JobId}", job.Id);
                }
                else if (job.Phase == DatabaseScanWorkPhase.Finished)
                {
                    _log.Info("Got a request to pause job, ours ({JobId}) is already Finished", job.Id);
                }
                else
                {
                    MetaDebug.Assert(job.Phase == DatabaseScanWorkPhase.Paused, "Unknown work phase {0}", job.Phase);
                    _log.Info("Got a request to pause job, ours ({JobId}) is already Paused", job.Id);
                }
            }
            else
                throw new InvalidOperationException("Got a request to pause job, but we have no job");

            ReplyToAsk(ask, new DatabaseScanWorkerPausedOk(_state.ActiveJob.ObserveStatus()));
        }

        [CommandHandler]
        private async Task HandleActiveJobUpdate(ActiveJobUpdate _)
        {
            DatabaseScanJobWorkState job = _state.ActiveJob;
            if (job == null)
                return;

            if (job.Phase == DatabaseScanWorkPhase.Running)
                await UpdateRunningJob();

            DateTime currentTime = DateTime.UtcNow;
            if (currentTime >= _activeJobRuntimeState.NextStatusReportAt)
            {
                CastMessage(DatabaseScanCoordinatorActor.EntityId, new DatabaseScanWorkerStatusReport(job.ObserveStatus()));
                _activeJobRuntimeState.NextStatusReportAt = currentTime + StatusReportInterval;
            }

            ScheduleNextActiveJobUpdate(_self);
        }

        void ScheduleNextActiveJobUpdate(Akka.Actor.IActorRef self)
        {
            DateTime nextUpdateAt;

            // Certain timers only apply in the Running phase.
            if (_state.ActiveJob.Phase == DatabaseScanWorkPhase.Running)
            {
                nextUpdateAt = Util.Min(
                    _activeJobRuntimeState.NextProcessorTickAt,
                    _activeJobRuntimeState.NextScanAt,
                    _activeJobRuntimeState.NextPersistAt,
                    _activeJobRuntimeState.NextStatusReportAt);
            }
            else
            {
                nextUpdateAt = _activeJobRuntimeState.NextStatusReportAt;
            }

            TimeSpan timeUntilNextUpdate = Util.Max(TimeSpan.Zero, nextUpdateAt - DateTime.UtcNow);

            // Regardless of phase, make sure that updates happen every
            // now and then, to ensure that the update scheduling gets
            // back on track when the phase is changed to Running, such
            // as when resuming a paused job.
            // Note that the ActiveJobUpdate handler itself is the place
            // where ScheduleNextActiveJobUpdate is called from.
            timeUntilNextUpdate = Util.Min(timeUntilNextUpdate, TimeSpan.FromSeconds(1));

            Context.System.Scheduler.ScheduleTellOnce(
                delay:          timeUntilNextUpdate,
                receiver:       self,
                message:        ActiveJobUpdate.Instance,
                sender:         self);
        }

        async Task<IEnumerable<IPersistedEntity>> ScanNextBatch()
        {
            DatabaseScanJobWorkState job = _state.ActiveJob;
            MetaDatabase db = MetaDatabase.Get(QueryPriority.Low);
            IEnumerable<IPersistedEntity> items;
            float? scannedRatioEstimate;

            if (job.Spec.ExplicitEntityList != null)
            {
                EntityId entityId = job.Spec.ExplicitEntityList[job.ExplicitListIndex++];
                PersistedEntityConfig entityConfig = EntityConfigRegistry.Instance.GetPersistedConfig(job.Spec.EntityKind);
                IPersistedEntity entity = await db.TryGetAsync<IPersistedEntity>(entityConfig.PersistedType, entityId.ToString());
                items = Enumerable.Repeat(entity, entity == null ? 0 : 1);
                scannedRatioEstimate = (float)job.ExplicitListIndex / job.Spec.ExplicitEntityList.Count;
            }
            else
            {
                EntityIdRange entityIdRange = EntityIdRangeFromWorkShard(job.Spec.EntityKind, job.Spec.EntityIdValueUpperBound, job.WorkShard);
                int pageSize = job.Processor.DesiredScanBatchSize;

                // Query items from the database, at shard DatabaseShardIndex and the corresponding iterator from DatabaseShardIterators.
                items = await QueryPagedRangeSingleShardAsync(
                    db,
                    job.Spec.EntityKind,
                    job.Spec.DatabaseQueryOpName,
                    job.DatabaseShardIndex,
                    job.DatabaseShardIterators[job.DatabaseShardIndex].StartKeyExclusive,
                    pageSize,
                    entityIdRange.FirstInclusive.ToString(),
                    entityIdRange.LastInclusive.ToString());

                // Update the iterator we just used:
                // If we got a full pageSize of items, there can be more to scan (from this shard and entity id range);
                // if got less, then we reached the end (for this shard and entity id range).
                int numItems = items.Count();
                job.DatabaseShardIterators[job.DatabaseShardIndex] =
                    numItems == pageSize    ? DatabaseSingleShardIterator.CreateIteratorAtKey(items.Last().EntityId)
                    : numItems < pageSize   ? DatabaseSingleShardIterator.End
                    : throw new MetaAssertException($"{nameof(QueryPagedRangeSingleShardAsync)} result exceeded page size: got {numItems}, page size was {pageSize}");
                // Bump the shard index for the next query, so we read from the shards in an interleaved manner.
                // (GetValidDatabaseShardIndex will do the wrap-around and ensure we don't try to re-read from finished iterators.)
                job.DatabaseShardIndex++;
                job.DatabaseShardIndex = DatabaseScanJobWorkState.GetValidDatabaseShardIndex(job.DatabaseShardIndex, job.DatabaseShardIterators);

                scannedRatioEstimate = ComputeScannedRatioEstimate(entityIdRange, job.DatabaseShardIterators);
            }

            int itemCount = items.Count();
            job.ScanStatistics.NumItemsScanned += itemCount;
            c_itemsQueried.WithLabels(job.Spec.MetricsTag).Inc(itemCount);
            if (scannedRatioEstimate.HasValue)
                job.ScanStatistics.ScannedRatioEstimate = scannedRatioEstimate.Value;

            return items;
        }

        async Task UpdateRunningJob()
        {
            DatabaseScanJobWorkState job = _state.ActiveJob;
            ActiveJobRuntimeState runtimeState = _activeJobRuntimeState;
            DateTime updateStartTime = DateTime.UtcNow;

            // Consider scanning more, if the time has come
            if (updateStartTime >= runtimeState.NextScanAt)
            {
                runtimeState.NextScanAt = updateStartTime + job.Processor.ScanInterval;

                // Scan if we've still got something to scan, and the processor can deal with more work.
                if (job.Processor.CanCurrentlyProcessMoreItems && job.HasMoreItemsToScan)
                {
                    IEnumerable<IPersistedEntity> batch = await ScanNextBatch();

                    if (batch.Any())
                        await job.Processor.StartProcessItemBatchAsync(this, batch);

                    if (job.CrashStallCounter > 0)
                    {
                        _log.Info("Resetting CrashStallCounter from {CrashStallCounter}", job.CrashStallCounter);
                        job.CrashStallCounter = 0;
                        await PersistStateIntermediate();
                    }
                }
            }

            // Tick the processor, if the time has come.
            if (updateStartTime >= runtimeState.NextProcessorTickAt)
            {
                runtimeState.NextProcessorTickAt = updateStartTime + job.Processor.TickInterval;
                await job.Processor.TickAsync(this);
            }

            // If we've scanned everything and processor is done, finish.
            // Otherwise, just persist periodically.
            if (!job.HasMoreItemsToScan && job.Processor.HasCompletedAllWorkSoFar)
            {
                job.Phase = DatabaseScanWorkPhase.Finished;
                await PersistStateIntermediate();
                _log.Info("Finished job {JobId}", job.Id);
            }
            else
            {
                if (updateStartTime >= runtimeState.NextPersistAt)
                {
                    await PersistStateIntermediate();
                    runtimeState.NextPersistAt = updateStartTime + job.Processor.PersistInterval;
                }
            }
        }

        /// <summary>
        /// Invoke <see cref="MetaDatabase.QueryPagedRangeSingleShardAsync"/>
        /// on <paramref name="db"/>, using the EntityKind's type, and up-convert the result.
        /// </summary>
        async Task<IEnumerable<IPersistedEntity>> QueryPagedRangeSingleShardAsync(MetaDatabase db, EntityKind entityKind, string opName, int shardNdx, string iteratorStartKeyExclusive, int pageSize, string rangeFirstKeyInclusive, string rangeLastKeyInclusive)
        {
            PersistedEntityConfig entityConfig = EntityConfigRegistry.Instance.GetPersistedConfig(entityKind);
            return await DatabaseScanUtil.QueryPagedRangeSingleShardAsync(db, entityConfig.PersistedType, opName, shardNdx, iteratorStartKeyExclusive, pageSize, rangeFirstKeyInclusive, rangeLastKeyInclusive);
        }

        [EntityAskHandler]
        private async Task HandleDatabaseScanWorkerEnsureStopped(EntityAsk<DatabaseScanWorkerStoppedOk> ask, DatabaseScanWorkerEnsureStopped ensureStopped)
        {
            DatabaseScanWorkStatus activeJobStatusJustBefore;

            if (_state.ActiveJob != null)
            {
                DatabaseScanJobWorkState job = _state.ActiveJob;

                activeJobStatusJustBefore = job.ObserveStatus();

                switch (ensureStopped.StopFlavor)
                {
                    case DatabaseScanWorkerStopFlavor.Finished:
                        if (job.Phase != DatabaseScanWorkPhase.Finished)
                            throw new InvalidOperationException($"Got a request to stop Finished job but ours ({job.Id}) has phase {job.Phase}");
                        break;

                    case DatabaseScanWorkerStopFlavor.Cancel:
                        if (job.Phase != DatabaseScanWorkPhase.Finished)
                        {
                            MetaDebug.Assert(job.Phase == DatabaseScanWorkPhase.Running
                                          || job.Phase == DatabaseScanWorkPhase.Paused,
                                "Unknown work phase {0}", job.Phase);

                            job.Processor.Cancel(this);
                        }
                        break;

                    default:
                        throw new MetaAssertException($"Invalid DatabaseScanWorkerStopFlavor: {ensureStopped.StopFlavor}");
                }

                _state.ActiveJob = null;
                await PersistStateIntermediate();
                DeinitActiveJobRuntimeState();

                _log.Info("Stopped job {JobId}, stop flavor {StopFlavor}", job.Id, ensureStopped.StopFlavor);
            }
            else
            {
                activeJobStatusJustBefore = null;
                _log.Info("Got request to stop job, we already have no job");
            }

            ReplyToAsk(ask, new DatabaseScanWorkerStoppedOk(activeJobStatusJustBefore));
        }

        [MessageHandler]
        private void HandleDatabaseScanWorkerEnsureAwake(DatabaseScanWorkerEnsureAwake _)
        {
            // thanks, i'm awake now
        }

        void InitActiveJobRuntimeState(DatabaseScanJobWorkState job, Akka.Actor.IActorRef self)
        {
            DateTime currentTime = DateTime.UtcNow;

            float offsetFactor = (float)job.WorkShard.WorkerIndex / (float)job.WorkShard.NumWorkers;

            _activeJobRuntimeState = new ActiveJobRuntimeState(
                nextScanAt:             currentTime + offsetFactor*job.Processor.ScanInterval,
                nextProcessorTickAt:    currentTime + offsetFactor*job.Processor.TickInterval,
                nextPersistAt:          currentTime + offsetFactor*job.Processor.PersistInterval,
                nextStatusReportAt:     currentTime + offsetFactor*StatusReportInterval);

            // Start the periodic ActiveJobUpdates. The ActiveJobUpdate handler itself
            // will schedule the next ActiveJobUpdate tell.
            self.Tell(ActiveJobUpdate.Instance, sender: self);
        }

        void DeinitActiveJobRuntimeState()
        {
            _activeJobRuntimeState = null;
        }

        IMetaLogger DatabaseScanProcessor.IContext.Log => _log;

        void DatabaseScanProcessor.IContext.ActorContinueTaskOnActorContext<TResult>(Task<TResult> asyncTask, Action<TResult> handleSuccess, Action<Exception> handleFailure)
        {
            ContinueTaskOnActorContext(asyncTask, handleSuccess, handleFailure);
        }

        Task<TResult> DatabaseScanProcessor.IContext.ActorEntityAskAsync<TResult>(EntityId targetEntityId, MetaMessage message)
        {
            return EntityAskAsync<TResult>(targetEntityId, message);
        }

        Task<TResult> DatabaseScanProcessor.IContext.ActorEntityAskAsync<TResult>(EntityId targetEntityId, EntityAskRequest<TResult> message)
        {
            return EntityAskAsync<TResult>(targetEntityId, message);
        }

        [Obsolete("Type mismatch between TResult type parameter and the annotated response type for the request parameter type.", error: true)]
        Task<TResult> DatabaseScanProcessor.IContext.ActorEntityAskAsync<TResult>(EntityId targetEntityId, EntityAskRequest message)
        {
            return EntityAskAsync<TResult>(targetEntityId, (MetaMessage)message);
        }

        static float ComputeScannedRatioEstimate(EntityIdRange range, DatabaseSingleShardIterator[] databaseShardIterators)
        {
            if (databaseShardIterators.All(it => it.IsFinished))
                return 1f;
            else
            {
                return databaseShardIterators.Average(iterator =>
                {
                    if (iterator.IsFinished)
                        return 1f;
                    else if (iterator.StartKeyExclusive == "")
                        return 0f;
                    else
                    {
                        EntityId    latestEntityId  = EntityId.ParseFromString(iterator.StartKeyExclusive);
                        float       ratio           = ComputeEntityIdDoneRatioInRange(range, latestEntityId);
                        return ratio;
                    }
                });
            }
        }

        static float ComputeEntityIdDoneRatioInRange(EntityIdRange range, EntityId doneEntityId)
        {
            ulong total = range.LastInclusive.Value - range.FirstInclusive.Value + 1;
            ulong done  = doneEntityId.Value - range.FirstInclusive.Value + 1;

            // For division, scale the numbers to a desired range to ensure they're not too big for float

            const int   DesiredBits = 20;
            int         numBits     = 32 - BitOperations.LeadingZeroCount(total);
            int         shift       = Math.Max(0, numBits - DesiredBits);

            return (float)(done >> shift) / (float)(total >> shift);
        }

        static EntityIdRange EntityIdRangeFromWorkShard(EntityKind kind, ulong entityIdValueUpperBound, DatabaseScanWorkShard workShard)
        {
            ulong firstValue    = ShardFirstEntityIdValueImpl(entityIdValueUpperBound, workShard.WorkerIndex, workShard.NumWorkers);
            ulong lastValue     = ShardFirstEntityIdValueImpl(entityIdValueUpperBound, workShard.WorkerIndex + 1, workShard.NumWorkers) - 1; // A shard's last is equal to the next shard's first minus one.

            return new EntityIdRange(kind, firstValue, lastValue);
        }

        static ulong ShardFirstEntityIdValueImpl(ulong entityIdValueUpperBound, int numeratorInt, int denominatorInt)
        {
            // \note ulong isn't enough for the calculation's intermediate values when numerator is large-ish.
            //       We could be smart about it somehow but this is ok, we're not gonna be doing this often anyway.
            BigInteger numerator    = new BigInteger(numeratorInt);
            BigInteger denominator  = new BigInteger(denominatorInt);
            BigInteger result       = numerator * entityIdValueUpperBound / denominator;

            return (ulong)result;
        }
    }
}
