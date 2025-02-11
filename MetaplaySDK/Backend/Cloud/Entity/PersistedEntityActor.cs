// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System;
using System.Globalization;
using System.Threading.Tasks;
using static Metaplay.Cloud.Sharding.EntityShard;

namespace Metaplay.Cloud.Entity
{
    [MetaMessage(MessageCodesCore.EntityEnsureOnLatestSchemaVersionRequest, MessageDirection.ServerInternal)]
    public class EntityEnsureOnLatestSchemaVersionRequest : EntityAskRequest<EntityEnsureOnLatestSchemaVersionResponse>
    {
    }

    [MetaMessage(MessageCodesCore.EntityEnsureOnLatestSchemaVersionResponse, MessageDirection.ServerInternal)]
    public class EntityEnsureOnLatestSchemaVersionResponse : EntityAskResponse
    {
        public int CurrentSchemaVersion;

        EntityEnsureOnLatestSchemaVersionResponse(){ }
        public EntityEnsureOnLatestSchemaVersionResponse(int currentSchemaVersion)
        {
            CurrentSchemaVersion = currentSchemaVersion;
        }
    }

    /// <summary>
    /// Request to "refresh" the entity: wake up, persist, and reply, but do nothing else special.
    /// The purpose is to do any cleanup that gets automatically done at wake-up and persist.
    /// </summary>
    [MetaMessage(MessageCodesCore.EntityRefreshRequest, MessageDirection.ServerInternal)]
    public class EntityRefreshRequest : EntityAskRequest<EntityRefreshResponse>
    {
    }

    [MetaMessage(MessageCodesCore.EntityRefreshResponse, MessageDirection.ServerInternal)]
    public class EntityRefreshResponse : EntityAskResponse
    {
    }

    internal static class PersistedEntityMetrics
    {
        static internal readonly Prometheus.Counter  c_nonFinalEntityRestored        = Prometheus.Metrics.CreateCounter("game_entity_non_final_entity_restored_total", "Cumulative number of entities restored from database that didn't have final persist done", "entity");
        static internal readonly Prometheus.Counter  c_entitySchemaMigrated          = Prometheus.Metrics.CreateCounter("meta_entity_schema_migrated_total", "Number of times entities of specific kind have been migrated between two versions", new string[] { "entity", "fromVersion", "toVersion" });
        static internal readonly Prometheus.Counter  c_entitySchemaMigrationFailed   = Prometheus.Metrics.CreateCounter("meta_entity_schema_migration_failed_total", "Number of times entities an entity migration has failed for a specific entity kind and fromVersion", new string[] { "entity", "fromVersion" });
    }

    /// <summary>
    /// A marker that annotates the actor is a persisted entity actor with the declared data types.
    /// </summary>
    internal interface IPersistedEntityActor<TPersisted, TPersistedPayload>
        where TPersisted : IPersistedEntity
        where TPersistedPayload : class, ISchemaMigratable
    {
    }

    /// <summary>
    /// Represents an entity which can be persisted into the database when not active.
    /// </summary>
    /// <typeparam name="TPersisted">Type of database-persisted representation of entity</typeparam>
    /// <typeparam name="TPersistedPayload">Type of the <see cref="IPersistedEntity.Payload" /> in <typeparamref name="TPersisted"/>.</typeparam>
    /// <remarks>
    /// The lifecycle of an entity is:
    /// <code>
    /// PersistedEntityActor
    ///      |
    ///      | &lt;- Creation in DB.
    ///      |      Optional. Caller creates a new DB row for the entity.
    ///      |
    /// .---&gt;|
    /// |    | &lt;- (wakeup)
    /// |    |      Entity is woken up to respond to a message, or is a service.
    /// |    |
    /// |    |- Actor.Constructor()
    /// |    |       You should initialize readonly variables here
    /// |    |
    /// |    |- EntityActor.PreStart()
    /// |    |       Optional. Akka.Net Actor start hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PreStart()
    /// |    |
    /// |    |- EntityActor.Initialize()
    /// |    |       Initialization before the first message is handled.
    /// |    |       You MUST:
    /// |    |          * Fetch the DB row from the DB. Let's call this persisted data.
    /// |    |          * If no DB row exists, either Fail or set persisted data to null.
    /// |    |          * Call PersistedEntityActor.InitializePersisted(persistedData)
    /// |    Either
    /// |    |\
    /// |    | \
    /// |    |  |
    /// |    |--+-- PersistedEntityActor.InitializeNew
    /// |    |  |     Created a new initialization data. Called in the case there was no
    /// |    |  |     persisted data or persisted was not migratable.
    /// |    |  |
    /// |    |  |-  PersistedEntityActor.RestoreFromPersisted()
    /// |    |  |     Created a new initialization data from the persisted data. Called in
    /// |    |  |     case there was persisted data.
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
    /// |    |.-'
    /// |    |
    /// |    |- PersistedEntityActor.PostLoad()
    /// |    |       The main initialization method of the actor. You should load the actor
    /// |    |       state from the initialization data from InitializeNew or RestoreFromPersisted.
    /// |    |
    /// |    |- [Actor is now running]
    /// |    |- [Message handlers]
    /// |    |- [Snapshots with PersistStateImpl]
    /// |    |
    /// |    |- EntityActor.OnShutdown()
    /// |    |       Optional. Cleanup method when just before actor is shut down.
    /// |    |       If overriden, You MUST call base.OnShutdown()
    /// |    |
    /// |    |- PersistedEntityActor.PersistStateImpl()
    /// |    |       You MUST implement this. The inverse of Initialize()
    /// |    |       You MUST:
    /// |    |          * Serialize model data and construct a DB row with following values:
    /// |    |              EntityId      = _entityId.ToString(),
    /// |    |              PersistedAt   = DateTime.UtcNow,
    /// |    |              Payload       = persistedPayload,
    /// |    |              SchemaVersion = CurrentSchemaVersion,
    /// |    |              IsFinal       = isFinal,
    /// |    |            where persistedPayload is generated with SerializeToPersistedPayload
    /// |    |          * If DB rows are allowed to not exist (see Initialize() and Creation in DB),
    /// |    |            and isInitial is set, INSERT the row into DB. Otherwise, the row should be
    /// |    |            UPDATED.
    /// |    |
    /// |    |- EntityActor.PostStop()
    /// |    |       Optional. Akka.Net actor stop hook. You should not do anything here.
    /// |    |       If overriden, You MUST call base.PostStop()
    /// |    |
    /// |    |- Actor shuts down
    /// '----'
    /// </code>
    /// </remarks>
    public abstract class PersistedEntityActor<TPersisted, TPersistedPayload>
        : EntityActor
        , IPersistedEntityActor<TPersisted, TPersistedPayload>
        where TPersisted : IPersistedEntity
        where TPersistedPayload : class, ISchemaMigratable
    {
        class TickSnapshot { public static readonly TickSnapshot Instance = new TickSnapshot(); }

        class ScheduledPersistState
        {
            public readonly int RequestId;
            public ScheduledPersistState(int requestId) { RequestId = requestId; }
        }

        protected readonly PersistedEntityConfig _entityConfig;
        protected readonly MetaVersionRange      _supportedSchemaVersions;
        DateTime                                 _lastPersistedAt;

        Action _afterPersistActions = null; // Multicast delegate of actions to execute after the next entity persist.

        // Helpers for conveniently accessing SupportedSchemaVersions
        protected int CurrentSchemaVersion         => _supportedSchemaVersions.MaxVersion;
        protected int OldestSupportedSchemaVersion => _supportedSchemaVersions.MinVersion;

        protected static readonly TimeSpan  TickSnapshotInterval    = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Interval between periodic snapshots of the Entity.
        /// </summary>
        protected abstract TimeSpan         SnapshotInterval        { get; }

        bool        _hasPendingScheduledPersist         = false;
        int         _pendingScheduledPersistRunningId   = 0;
        DateTime    _earliestScheduledPersistAt;

        /// <summary>
        /// Minimum interval between persists due to <see cref="SchedulePersistState"/>.
        /// </summary>
        protected virtual TimeSpan MinScheduledPersistStateInterval => TimeSpan.FromSeconds(10);

        protected PersistedEntityActor(EntityId entityId) : base(entityId)
        {
            // Add noise to _lastPersistedAt to smooth out persisting (even if lots of actors woken up quickly)
            _lastPersistedAt = DateTime.UtcNow + (new Random().NextDouble() - 0.5) * SnapshotInterval;

            // Initially, allow scheduling persist to happen as soon as desired.
            _earliestScheduledPersistAt = DateTime.UtcNow;

            // Start snapshotting timer
            StartRandomizedPeriodicTimer(TickSnapshotInterval, TickSnapshot.Instance);

            // Cache EntityConfig for fast access
            _entityConfig            = (PersistedEntityConfig)EntityConfigRegistry.Instance.GetConfig(entityId.Kind);
            _supportedSchemaVersions = SchemaMigrationRegistry.Instance.GetSchemaMigrator<TPersistedPayload>().SupportedSchemaVersions;
        }

        protected async Task InitializePersisted(TPersisted persisted)
        {
            // Restore from persisted (if exists), or initialize new entity
            if (persisted != null && persisted.Payload != null)
            {
                // Restore from snapshot and validate
                _log.Debug("Restoring from snapshot (persistedAt={PersistedAt}, schemaVersion={SchemaVersion}, isFinal={IsFinal}, size={NumBytes})", persisted.PersistedAt, persisted.SchemaVersion, persisted.IsFinal, persisted.Payload.Length);

                // Check if persisted schema version is still supported
                SchemaMigrator migrator = SchemaMigrationRegistry.Instance.GetSchemaMigrator<TPersistedPayload>();
                int oldestSupportedVersion = migrator.SupportedSchemaVersions.MinVersion;
                if (persisted.SchemaVersion < oldestSupportedVersion)
                {
                    _log.Warning("Schema version {PersistedSchemaVersion} is too old (oldest supported is {OldestSupportedSchemaVersion}), resetting state!", persisted.SchemaVersion, oldestSupportedVersion);

                    // \todo [petri] throw or take backup in prod build?

                    // Initialize new entity
                    TPersistedPayload payload = await InitializeNew();

                    // PostLoad()
                    await PostLoad(payload, _entityConfig.GetCurrentTimeForPersistedAt(), TimeSpan.Zero);
                }
                else
                {
                    // Log if final persist was not made (ie, entity likely has lost some state)
                    if (!persisted.IsFinal)
                    {
                        PersistedEntityMetrics.c_nonFinalEntityRestored.WithLabels(_entityId.Kind.ToString()).Inc();
                        _log.Info("Restoring from non-final snapshot!");
                    }

                    // Restore state from persisted
                    TPersistedPayload payload;
                    try
                    {
                        payload = await RestoreFromPersisted(persisted);
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

                    // PostLoad()
                    await PostLoad(payload, persisted.PersistedAt, _entityConfig.GetCurrentTimeForPersistedAt() - persisted.PersistedAt);
                }
            }
            else
            {
                // Create new state & immediately persist (an empty placeholder already existed in database)
                TPersistedPayload payload = await InitializeNew();
                await PostLoad(payload, _entityConfig.GetCurrentTimeForPersistedAt(), TimeSpan.Zero);
                await PersistState(isInitial: true, isFinal: false);
            }
        }

        /// <summary>
        /// Serialize and compress the entity payload (usually model) for persistence in the database.
        /// Also used for entity export/import archives.
        /// </summary>
        /// <typeparam name="TModel">Type of payload or model to persist</typeparam>
        /// <param name="model">Model object to persist</param>
        /// <param name="logicVersion">Logic version of the payload (if used)</param>
        /// <returns></returns>
        protected byte[] SerializeToPersistedPayload<TModel>(TModel model, IGameConfigDataResolver resolver, int? logicVersion)
        {
            // Serialize and compress the payload
            byte[] persisted = _entityConfig.SerializeDatabasePayload(_entityId, model, logicVersion);

            // Perform extra validation on the persisted state, if enabled
            _entityConfig.ValidatePersistedState<TModel>(_log, _entityId, persisted, resolver, logicVersion);

            return persisted;
        }

        /// <summary>
        /// Deserialize and decompress the entity payload from the database persisted format.
        /// </summary>
        /// <typeparam name="TModel">Type of the payload (or model) to use</typeparam>
        /// <param name="persisted">Persisted bytes (serialized and compressed)</param>
        /// <param name="resolver">Game config data resolver</param>
        /// <param name="logicVersion">Logic version to use while deserializing</param>
        /// <returns></returns>
        protected TModel DeserializePersistedPayload<TModel>(byte[] persisted, IGameConfigDataResolver resolver, int? logicVersion)
        {
            return _entityConfig.DeserializeDatabasePayload<TModel>(_entityId, persisted, resolver, logicVersion);
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        protected override async Task OnShutdown()
        {
            // Do final snapshot
            // \todo [petri] handle transient persist failures (cancel shutdown)
            _log.Debug("Entity shutdown, do final snapshot now");
            await PersistState(isInitial: false, isFinal: true);

            await base.OnShutdown();
        }

        [CommandHandler]
        async Task HandleTickSnapshot(TickSnapshot _)
        {
            // Periodic persisting
            if (IsShutdownEnqueued)
                return;
            if (DateTime.UtcNow < _lastPersistedAt + SnapshotInterval)
                return;
            await PersistStateIntermediate();
        }

        protected Task PersistStateIntermediate()
        {
            return PersistState(isInitial: false, isFinal: false);
        }

        protected async Task PersistState(bool isInitial, bool isFinal)
        {
            await PersistStateImpl(isInitial: isInitial, isFinal: isFinal);

            _lastPersistedAt = DateTime.UtcNow;

            _hasPendingScheduledPersist = false;
            _earliestScheduledPersistAt = DateTime.UtcNow + MinScheduledPersistStateInterval;

            // Flush any pending after-persist actions.
            _afterPersistActions?.Invoke();
            _afterPersistActions = null;
        }

        /// <summary>
        /// Schedule <see cref="PersistStateIntermediate"/> to be executed
        /// sometime in the near future (unless already scheduled).
        /// The scheduled persists are rate-limited by
        /// <see cref="MinScheduledPersistStateInterval"/>.
        /// </summary>
        /// <remarks>
        /// This is for non-critical persists. Critically important persists
        /// should use <see cref="PersistStateIntermediate"/> directly.
        /// </remarks>
        protected void SchedulePersistState()
        {
            if (_hasPendingScheduledPersist)
                return;

            _hasPendingScheduledPersist = true;
            _pendingScheduledPersistRunningId++;

            Context.System.Scheduler.ScheduleTellOnce(
                delay: Util.Max(TimeSpan.Zero, _earliestScheduledPersistAt - DateTime.UtcNow),
                receiver: _self,
                message: new ScheduledPersistState(_pendingScheduledPersistRunningId),
                sender: _self);
        }

        [CommandHandler]
        async Task HandleScheduledPersistState(ScheduledPersistState scheduledPersistState)
        {
            if (!_hasPendingScheduledPersist)
                return;
            // Ignore stale messages
            if (scheduledPersistState.RequestId != _pendingScheduledPersistRunningId)
                return;

            await PersistStateIntermediate();
        }

        /// <summary>
        /// Schedule <paramref name="action"/> to be called right after the next
        /// persist of this entity. It is called synchronously after
        /// <see cref="PersistStateImpl"/> has been called.
        /// Any persist will do; a periodic snapshot, or the final persist when the actor is shutting down.
        /// </summary>
        protected void RunAfterNextPersist(Action action)
        {
            _afterPersistActions += action;
        }

        [EntityAskHandler]
        async Task HandleEntityEnsureOnLatestSchemaVersionRequest(EntityAsk<EntityEnsureOnLatestSchemaVersionResponse> ask, EntityEnsureOnLatestSchemaVersionRequest _)
        {
            // Since we're successfully up and receiving messages/asks, we must be on the latest schema version.
            // Just make that persistent, and reply OK.
            // \note This is pretty much the same as EntityRefreshRequest, except for the reply.
            //       The schema version in the reply isn't really necessary since it's a global constant,
            //       so we could really get by with just EntityRefreshRequest.
            //       But I guess this is a bit more explicit, so whatever.

            // Schedule the ReplyToAsk to happen after the next persist, so the asker can be sure we've persisted.
            // Then, to fulfill it soon,
            //  - either request imminent shutdown and rely on the final persist,
            //  - or failing that, persist now.
            // The above scheduled ReplyToAsk will get done when either of those persists happens (or earlier, if an unrelated persist happens first).
            RunAfterNextPersist(() => ReplyToAsk(ask, new EntityEnsureOnLatestSchemaVersionResponse(CurrentSchemaVersion)));
            if (!TryRequestShutdownAfterLikelyOneOffRequest())
                await PersistStateIntermediate();
        }

        [EntityAskHandler]
        async Task HandleEntityRefreshRequest(EntityAsk<EntityRefreshResponse> ask, EntityRefreshRequest _)
        {
            // \note See HandleEntityEnsureOnLatestSchemaVersionRequest for comments, this is a similar case.
            RunAfterNextPersist(() => ReplyToAsk(ask, new EntityRefreshResponse()));
            if (!TryRequestShutdownAfterLikelyOneOffRequest())
                await PersistStateIntermediate();
        }

        /// <summary>
        /// Request shutdown if we don't currently have subscribers, and our shutdown policy is
        /// to shut down when we don't have subscribers.
        /// Returns true iff it requested shutdown or shutdown was already pending.
        ///
        /// Caveat, kludge:
        ///
        /// Normally, under such circumstances, the entity would automatically shut down after some timeout.
        /// This method is intended to be used to omit that timeout, and instead shut down immediately;
        /// this is used in use cases where lots of entities are woken up at a fast rate for some one-off
        /// operation, and it's desirable to stop them from lingering unnecessarily.
        ///
        /// Note that this is a kludge; a better solution would be (roughly speaking) for EntityActor to
        /// automatically understand that it was woken up not for a subscription, but for an one-off operation,
        /// and that it can shut down after the operation.
        ///
        /// Note also that using this method has a potential race with incoming subscriptions:
        /// a subscription can come in as we're shutting down, and the subscription will get terminated
        /// soon after it started. This method should thus be only used in cases where that is not
        /// expected to be a significant problem.
        /// </summary>
        bool TryRequestShutdownAfterLikelyOneOffRequest()
        {
            if (IsShutdownEnqueued)
                return true;

            if (ShutdownPolicy.mode == AutoShutdownState.Modes.NoSubscribers && _subscribers.Count == 0)
            {
                RequestShutdown();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called from <see cref="InitializePersisted"/>. Returns a payload for a new entity which will be delivered
        /// to <see cref="PostLoad"/>. Called when the entity is created for the first time, or if the persisted schema
        /// version is older than the last supported (<see cref="SchemaMigrator.OldestSupportedSchemaVersion"/> for <typeparamref name="TPersistedPayload"/>).
        /// </summary>
        protected abstract Task<TPersistedPayload> InitializeNew();

        /// <summary>
        /// Called from <see cref="InitializePersisted"/>. Returns a payload for a new entity which will be delivered
        /// to <see cref="MigrateState"/> for each required migration if any, and finally to <see cref="PostLoad"/>. Called
        /// when entity is created and there exists a persited data for it (and the persisted data schema is not older than
        /// <see cref="SchemaMigrator.OldestSupportedSchemaVersion"/> for <typeparamref name="TPersistedPayload"/>).
        /// </summary>
        protected abstract Task<TPersistedPayload> RestoreFromPersisted(TPersisted persisted);

        /// <summary>
        /// Called from <see cref="InitializePersisted"/>. Migrates payload in-place from <paramref name="fromVersion"/> to the
        /// latest version (<see cref="SchemaMigrator.CurrentSchemaVersion"/>).
        /// </summary>
        protected void MigrateState(TPersistedPayload payload, int fromVersion)
        {
            string migratableType = typeof(TPersistedPayload).ToGenericTypeString();
            SchemaMigrator migrator = SchemaMigrationRegistry.Instance.GetSchemaMigrator<TPersistedPayload>();
            int toVersion = migrator.SupportedSchemaVersions.MaxVersion;

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
                    migrator.RunMigrations(payload, fromVersion);
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

        /// <summary>
        /// Called before the <typeparamref name="TPersistedPayload"/> schema version is migrated.
        /// </summary>
        protected virtual void OnBeforeSchemaMigration(TPersistedPayload payload, int fromVersion, int toVersion) { }

        /// <summary>
        /// Called when the <typeparamref name="TPersistedPayload"/> schema version has been migrated.
        /// </summary>
        /// <param name="fromVersion">Schema version before the migration</param>
        /// <param name="toVersion">Schema version after the migration</param>
        protected virtual void OnSchemaMigrated(TPersistedPayload payload, int fromVersion, int toVersion) { }

        /// <summary>
        /// Called from <see cref="InitializePersisted"/>. Entity should load state from <paramref name="payload"/>.
        /// </summary>
        protected abstract Task PostLoad(TPersistedPayload payload, DateTime persistedAt, TimeSpan elapsedTime);

        protected abstract Task PersistStateImpl(bool isInitial, bool isFinal);
    }
}
