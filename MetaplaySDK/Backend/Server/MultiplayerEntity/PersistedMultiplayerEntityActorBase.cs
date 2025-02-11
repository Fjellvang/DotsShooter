// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace Metaplay.Server.MultiplayerEntity
{
    /// <summary>
    /// The base of <see cref="IPersistedEntity"/> for persisted Multiplayer Entity. Implementations should inherit this class
    /// for each concrete MultiplayerEntity and add the appropriate [Table(...)] attribute. If new fields are added,
    /// actor implementation should override CreatePersisted with a custom implementation.
    /// </summary>
    public abstract class PersistedMultiplayerEntityBase : IPersistedEntity
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
    }

    /// <summary>
    /// Base class for actors managing a database-persisted a Multiplayer Entity.
    /// <para>
    /// Multiplayer Entity is an basic implementation for server-driven Entity. Multiple Clients may subscribe to an Multiplayer Entity and propose
    /// actions. Actions and Ticks executed by server are delivered back to client, allowing clients to see the changes in their copy of the Model.
    /// </para>
    /// <para>
    /// To use this base class, you should create actor-specific <typeparamref name="TPersisted"/> which may be empty, with an appropriate [Table(...)] attribute.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The lifecycle of an entity is:
    /// <code>
    /// PersistedMultiplayerEntityActorBase
    ///      |
    ///      | &lt;- Creation in DB.
    ///      |      Create a new DB row / EntityId with DatabaseEntityUtil.CreateNewEntityAsync.
    ///      |
    /// .---&gt;|
    /// |    |
    /// |    |- Actor.Constructor()
    /// |    |       You should initialize readonly variables here
    /// |    |
    /// |    |- EntityActor.PreStart()
    /// |    |       Optional. Akka.Net Actor start hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PreStart()
    /// |    |
    /// |    Either
    /// |    |\
    /// |    | \
    /// |    |  \
    /// |    |  |
    /// |    |&lt;-+- (setup)
    /// |    |  |   Entity is woken up with InternalEntitySetupRequest message.
    /// |    |  |   This can be done only once per entity.
    /// |    |  |
    /// |    |--+- MultiplayerEntityActorBase.OnSwitchedToModel()
    /// |    |  |    Optional. New model becomes active. You may attach Server-Listeners to the model.
    /// |    |  |
    /// |    |--+- MultiplayerEntityActorBase.SetUpModelAsync()
    /// |    |  |    Setups the initial Model state from InternalEntitySetupRequest parameters. A battle
    /// |    |  |    would set up its players.
    /// |    |  |
    /// |    |  Either
    /// |    |  |\
    /// |    |  | \
    /// |    |  |  |- PersistedEntityActor.OnBeforeSchemaMigration
    /// |    |  |  |
    /// |    |  |  |- PersistedEntityActor.OnSchemaMigrated
    /// |    |  |  |
    /// |    |  |.-'
    /// |    |  |
    /// |    |  |- MultiplayerEntityActorBase.OnSwitchedToModel()
    /// |    |  |    Optional. New model becomes active. You may attach Server-Listeners to the model.
    /// |    |  |
    /// |    |  |- PersistedMultiplayerEntityActorBase.OnFastForwardedTime()
    /// |    |  |   Optional. This path is taken when a per-existing entity is woken up from persisted
    /// |    |  |   storage. Use this to update Model's internal time-based logic.
    /// |    |  |
    /// |    |.-'
    /// |    |
    /// |    |- MultiplayerEntityActorBase.OnEntityInitialized()
    /// |    |       The Main initialization method. The model has been set up.
    /// |    |
    /// |    |- [Entity is now running]
    /// |    |- [Message handlers]
    /// |    |
    /// |    |- EntityActor.OnShutdown()
    /// |    |       Optional. Cleanup method when just before actor is shut down.
    /// |    |       If overriden, You MUST call base.OnShutdown()
    /// |    |
    /// |    |- EntityActor.PostStop()
    /// |    |       Optional. Akka.Net actor stop hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PostStop()
    /// |    |
    /// |    |- Actor shuts down
    /// '----'
    /// </code>
    /// </remarks>
    public abstract partial class PersistedMultiplayerEntityActorBase<TModel, TAction, TPersisted>
        : MultiplayerEntityActorBase<TModel, TAction>
        , IPersistedEntityActor<TPersisted, TModel>
        where TModel : class, IMultiplayerModel<TModel>, new()
        where TAction : ModelAction
        where TPersisted : PersistedMultiplayerEntityBase, new()
    {
        class TickSnapshot { public static readonly TickSnapshot Instance = new TickSnapshot(); }

        readonly PersistedEntityConfig  _entityConfig;
        readonly SchemaMigrator         _migrator;
        DateTime                        _nextAutomaticSnapshotEarliestAt;

        [EntityActorRegisterCallback]
        static void ValidateEntityConfig(EntityConfigBase config)
        {
            if (config is not PersistedEntityConfig persistedConfig)
            {
                throw new InvalidOperationException(
                    $"{config.EntityActorType.ToNamespaceQualifiedTypeString()} actor " +
                    $"entity config {config.GetType().ToNamespaceQualifiedTypeString()} must be PersistedEntityConfig.");
            }

            // Check persisted type is PersistedMultiplayerEntityBase
            // \note: currently checked by the actor generic constraint, so somewhat redundant.
            // \todo: should this be relaxed with some attribute?
            if (!persistedConfig.PersistedType.IsDerivedFrom<PersistedMultiplayerEntityBase>())
            {
                throw new InvalidOperationException(
                    $"{config.EntityActorType.ToNamespaceQualifiedTypeString()} actor " +
                    $"entity config {config.GetType().ToNamespaceQualifiedTypeString()} has " +
                    $"PersistedType {persistedConfig.PersistedType.ToNamespaceQualifiedTypeString()}. " +
                    $"This type should inherit from PersistedMultiplayerEntityBase.");
            }

            PropertyInfo payloadProp = persistedConfig.PersistedType.GetProperty(nameof(IPersistedEntity.Payload));
            if (payloadProp == null)
            {
                throw new InvalidOperationException(
                    $"Internal error: {config.EntityActorType.ToNamespaceQualifiedTypeString()} actor " +
                    $"entity config {config.GetType().ToNamespaceQualifiedTypeString()} has " +
                    $"PersistedType {persistedConfig.PersistedType.ToNamespaceQualifiedTypeString()} which doesn't contain 'Payload' property.");
            }

            // Check Payload is nullable in the database.
            RequiredAttribute requiredAttributeMaybe = payloadProp.GetCustomAttribute<RequiredAttribute>();
            if (requiredAttributeMaybe != null)
            {
                throw new InvalidOperationException(
                    $"{config.EntityActorType.ToNamespaceQualifiedTypeString()} actor " +
                    $"entity config {config.GetType().ToNamespaceQualifiedTypeString()} has " +
                    $"PersistedType {persistedConfig.PersistedType.ToNamespaceQualifiedTypeString()} for which" +
                    $"Payload field is marked as [Required]. Multiplayer entities must not have [Required] Payload.");
            }
        }

        /// <inheritdoc cref="MultiplayerEntityActorBase{TModel, TAction}.MultiplayerEntityActorBase(EntityId, string)"/>
        protected PersistedMultiplayerEntityActorBase(EntityId entityId, string logChannelName = null) : base(entityId, logChannelName)
        {
            _entityConfig = EntityConfigRegistry.Instance.GetPersistedConfig(entityId.Kind);
            _migrator = SchemaMigrationRegistry.Instance.GetSchemaMigrator(typeof(TModel));
        }

        protected override sealed async Task Initialize()
        {
            await RestoreStateFromDatabase();

            // Start snapshot timer.
            // We start it regardless if the entity has state or not. The timer handler will filter out unnecessary snapshots.
            // Add noise to _nextAutomaticSnapshotEarliestAt to smooth out persisting (even if lots of actors woken up quickly)
            _nextAutomaticSnapshotEarliestAt = DateTime.UtcNow + (new Random().NextDouble() + 0.5) * SnapshotInterval;
            StartRandomizedPeriodicTimer(TimeSpan.FromSeconds(30), TickSnapshot.Instance);

            await base.Initialize();
        }

        protected override async Task OnShutdown()
        {
            // Do final snapshot
            // \todo [petri] handle transient persist failures (cancel shutdown)
            _log.Debug("Entity shutdown, do final snapshot now");
            await WriteModelToPersistedStorageAsync(isFinal: true);

            await base.OnShutdown();
        }

        [CommandHandler]
        async Task HandleTickSnapshot(TickSnapshot _)
        {
            // Periodic persisting
            if (IsShutdownEnqueued)
                return;
            if (DateTime.UtcNow < _nextAutomaticSnapshotEarliestAt)
                return;
            await PersistSnapshot();
        }

        async Task WriteModelToPersistedStorageAsync(bool isFinal)
        {
            _nextAutomaticSnapshotEarliestAt = DateTime.UtcNow + SnapshotInterval;

            if (_journal == null)
            {
                // If entity was not set up, there is nothing to save.
                return;
            }

            if (!isFinal)
                _log.Debug("Persisting entity state snapshot (schemaVersion={SchemaVersion})", _migrator.CurrentSchemaVersion);
            else
                _log.Debug("Persisting final entity state before shutdown (schemaVersion={SchemaVersion})", _migrator.CurrentSchemaVersion);

            MetaTime now = MetaTime.Now;
            OnBeforePersist();

            // Entity has been set up. Serialize and validate the contents.
            // On final persist, ensure Model is up-to-date
            if (isFinal)
            {
                MetaDuration elapsedTime = now - ModelUtil.TimeAtTick(Model.CurrentTick, Model.TimeAtFirstTick, Model.TicksPerSecond);
                if (elapsedTime > MetaDuration.Zero)
                {
                    _log.Debug("Fast-forwarding Model {ElapsedTime} before final persist", elapsedTime);
                    Model.ResetTime(now);
                    Model.OnFastForwardTime(elapsedTime);
                }
            }

            // Serialize and compress the payload
            byte[] persistedPayload = _entityConfig.SerializeDatabasePayload(_entityId, Model, logicVersion: null);

            // Perform extra validation on the persisted state, if enabled
            _entityConfig.ValidatePersistedState<TModel>(_log, _entityId, persistedPayload, resolver: _baselineGameConfigResolver, logicVersion: null);

            // Persist in database
            TPersisted persisted = CreatePersisted(
                persistedAt:        now.ToDateTime(),
                payload:            persistedPayload,
                schemaVersion:      _migrator.CurrentSchemaVersion,
                isFinal:            isFinal
            );
            await MetaDatabase.Get().UpdateAsync(persisted);

            UpdateInActiveEntitiesList();
        }

        /// <summary>
        /// Persists a snapshot of the current entity state to a persisted storage.
        /// If the entity crashes, it will continue next time from this snapshot.
        /// </summary>
        public override Task PersistSnapshot() => WriteModelToPersistedStorageAsync(isFinal: false);

        #region Restore logic

        async Task RestoreStateFromDatabase()
        {
            // Fetch state from database (at least an empty state must exist)
            TPersisted persisted = await MetaDatabase.Get().TryGetAsync<TPersisted>(_entityId.ToString());
            if (persisted == null)
                throw new InvalidOperationException($"Trying to initialize {GetType().ToGenericTypeString()} for whom no state exists in database. At least an empty {typeof(TPersisted).ToGenericTypeString()} must exist in database. A new entity can be created with DatabaseEntityUtil.CreateNewEntityAsync<{typeof(TPersisted).ToGenericTypeString()}>()");

            if (persisted.Payload == null)
            {
                // An empty placeholder already existed in database
                await InitializeWithNoState();
                return;
            }

            // Restore from snapshot and validate
            _log.Debug("Restoring from snapshot (persistedAt={PersistedAt}, schemaVersion={SchemaVersion}, isFinal={IsFinal}, size={NumBytes})", persisted.PersistedAt, persisted.SchemaVersion, persisted.IsFinal, persisted.Payload.Length);

            // Log if final persist was not made (ie, entity likely has lost some state)
            if (!persisted.IsFinal)
            {
                PersistedEntityMetrics.c_nonFinalEntityRestored.WithLabels(_entityId.Kind.ToString()).Inc();
                _log.Info("Restoring from non-final snapshot!");
            }

            // Check if persisted schema version is still supported
            int oldestSupportedVersion = _migrator.SupportedSchemaVersions.MinVersion;
            if (persisted.SchemaVersion < oldestSupportedVersion)
            {
                _log.Warning("Schema version {PersistedSchemaVersion} is too old (oldest supported is {OldestSupportedSchemaVersion}), resetting state!", persisted.SchemaVersion, oldestSupportedVersion);

                // \todo [petri] throw or take backup in prod build?

                // Initialize new entity
                await InitializeWithNoState();
                return;
            }

            // Restore state from persisted
            TModel payload;
            try
            {
                payload = _entityConfig.DeserializeDatabasePayload<TModel>(_entityId, persisted.Payload, _baselineGameConfigResolver, _logicVersion);
            }
            catch (Exception ex)
            {
                _log.Error("Failed to deserialize {TypeName}: {Exception}", typeof(TPersisted).ToGenericTypeString(), ex);
                // Dump raw payload on debug log level
                if (_log.IsDebugEnabled)
                {
                    _log.Debug("Persisted {Type} ({NumBytes} bytes): {Payload}.", typeof(TPersisted), persisted.Payload.Length, Convert.ToBase64String(persisted.Payload));

                    // Dump with TaggedWireSerializer, but first decompress if needed
                    // \todo This is modified copypaste from PersistedEntityConfig.DeserializeDatabasePayload,
                    //       and feels like it should be part of that.
                    string serializedStringDump;
                    if (BlobCompress.IsCompressed(persisted.Payload))
                    {
                        // Decompress payload
                        using FlatIOBuffer uncompressed = BlobCompress.DecompressBlob(persisted.Payload);

                        // Deserialize object
                        using IOReader reader = new IOReader(uncompressed);
                        serializedStringDump = TaggedWireSerializer.ToString(reader);
                    }
                    else // not compressed, just deserialize
                    {
                        serializedStringDump = TaggedWireSerializer.ToString(persisted.Payload);
                    }
                    _log.Debug("Tagged structure: {Data}", serializedStringDump);
                }
                throw;
            }

            // Migrate state to latest supported version
            MigrateState(payload, persisted.SchemaVersion);

            await InitializeWithRestoredState(payload, persisted.PersistedAt, MetaTime.Now.ToDateTime() - persisted.PersistedAt);
        }

        void MigrateState(TModel payload, int fromVersion)
        {
            string migratableType = typeof(TModel).ToGenericTypeString();
            int toVersion = _migrator.CurrentSchemaVersion;

            if (fromVersion == toVersion)
                return; // already up-to-date
            else if (fromVersion > toVersion)
                throw new InvalidOperationException($"Migratable type {payload.GetType().ToGenericTypeString()} is in a future schema v{fromVersion} when maximum supported is v{toVersion}");
            else
            {
                _log.Info("Migrating from schema v{OldSchemaVersion} to v{NewSchemaVersion}", fromVersion, toVersion);
                try
                {
                    OnBeforeSchemaMigration(payload, fromVersion, toVersion);
                    _migrator.RunMigrations(payload, fromVersion);
                    PersistedEntityMetrics.c_entitySchemaMigrated.WithLabels(migratableType, fromVersion.ToString(CultureInfo.InvariantCulture), toVersion.ToString(CultureInfo.InvariantCulture)).Inc();
                    OnSchemaMigrated(payload, fromVersion, toVersion);
                }
                catch (SchemaMigrationError ex)
                {
                    PersistedEntityMetrics.c_entitySchemaMigrationFailed.WithLabels(migratableType, ex.FromVersion.ToString(CultureInfo.InvariantCulture)).Inc();
                    _log.Error("Migration failure from schema v{FromVersion} to v{ToVersion}: {Exception}", ex.FromVersion, ex.FromVersion+1, ex.InnerException);
                    throw;
                }
            }
        }

        Task InitializeWithNoState()
        {
            // Nothign to do.
            _log.Info("No entity state in database. Waiting for Setup request");
            return Task.CompletedTask;
        }

        async Task InitializeWithRestoredState(TModel model, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _log.Info("Restored entity state from database. LogicVersion={LogicVersion}, away for {AwayDuration} (since {PersistedAt})", _entityId, _logicVersion, elapsedTime, persistedAt);

            AssignBasicRuntimePropertiesToModel(model, _modelLogChannel);

            // Fast forward any time this entity was suspended
            model.ResetTime(MetaTime.FromDateTime(persistedAt) + MetaDuration.FromTimeSpan(elapsedTime));
            model.OnFastForwardTime(MetaDuration.FromTimeSpan(elapsedTime));

            OnFastForwardedTime(model, MetaDuration.FromTimeSpan(elapsedTime));

            // Reset clock and start from this point
            OnSwitchedToModelCore(model);
            ResetJournalToModel(model);

            // Initialization via wakeup
            await OnEntityInitialized();

            // Start ticking
            if (IsTicking)
                StartTickTimer();
        }

        #endregion

        #region PersistedEntityActor API compatibility

        [EntityAskHandler]
        async Task<EntityEnsureOnLatestSchemaVersionResponse> HandleEntityEnsureOnLatestSchemaVersionRequest(EntityEnsureOnLatestSchemaVersionRequest _)
        {
            await PersistStateIntermediate();
            TryRequestShutdownAfterLikelyOneOffRequest();
            return new EntityEnsureOnLatestSchemaVersionResponse(_migrator.CurrentSchemaVersion);
        }

        [EntityAskHandler]
        async Task<EntityRefreshResponse> HandleEntityRefreshRequest(EntityRefreshRequest _)
        {
            await PersistStateIntermediate();
            TryRequestShutdownAfterLikelyOneOffRequest();
            return new EntityRefreshResponse();
        }

        void TryRequestShutdownAfterLikelyOneOffRequest()
        {
            if (ShutdownPolicy.mode == AutoShutdownState.Modes.NoSubscribers && _subscribers.Count == 0)
                RequestShutdown();
        }

        /// <summary>
        /// Interval between periodic snapshots of the Entity.
        /// </summary>
        protected virtual TimeSpan SnapshotInterval => TimeSpan.FromMinutes(3);

        /// <inheritdoc cref="PersistSnapshot"/>
        public Task PersistStateIntermediate() => PersistSnapshot();

        #endregion

        #region Callbacks to userland

        /// <summary>
        /// Called after the model has been fast forwarded in PostLoad.
        /// </summary>
        protected virtual void OnFastForwardedTime(TModel model, MetaDuration elapsed) { }

        /// <summary>
        /// Called just before an entity state is written to the persisted storage. This can be used
        /// to persist external data or to modify model state.
        /// </summary>
        protected virtual void OnBeforePersist() { }

        /// <summary>
        /// Creates a <typeparamref name="TPersisted"/> representation of the current Model with the given arguments. The current
        /// model is given in <paramref name="payload"/> and will be <c>null</c> if the entity is not set up yet.
        /// </summary>
        protected virtual TPersisted CreatePersisted(DateTime persistedAt, byte[] payload, int schemaVersion, bool isFinal)
        {
            TPersisted persisted = new TPersisted();
            persisted.EntityId      = _entityId.ToString();
            persisted.PersistedAt   = persistedAt;
            persisted.Payload       = payload;
            persisted.SchemaVersion = schemaVersion;
            persisted.IsFinal       = isFinal;
            return persisted;
        }

        /// <inheritdoc cref="PersistedEntityActor{TPersisted, TPersistedPayload}.OnBeforeSchemaMigration(TPersistedPayload, int, int)"/>
        protected virtual void OnBeforeSchemaMigration(TModel payload, int fromVersion, int toVersion) { }

        /// <inheritdoc cref="PersistedEntityActor{TPersisted, TPersistedPayload}.OnSchemaMigrated(TPersistedPayload, int, int)"/>
        protected virtual void OnSchemaMigrated(TModel payload, int fromVersion, int toVersion) { }

        #endregion
    }
}
