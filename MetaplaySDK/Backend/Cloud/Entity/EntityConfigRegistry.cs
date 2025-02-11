// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Cloud.Cluster;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Sharding;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.IO;
using Metaplay.Core.Memory;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Metaplay.Cloud.Entity
{
    /// <summary>
    /// Initializing group for a given <c>EntityShard</c>. The <c>EntityShards</c> are initialized one group at a time.
    /// Within the group, the EntityShards are created in arbitrary order, then the initialization is synchronized across
    /// the cluster, and finally the entities are started in arbitrary order.
    /// </summary>
    public enum EntityShardGroup
    {
        BaseServices    = 0,    // Base services: DiagnosticTool, GlobalStateManager, StatsCollectorManager
        ServiceProxies  = 1,    // Proxies for base services: GlobalStateProxy, StatsCollectorProxy (use own group to ensure managers are initialized before)
        Workloads       = 2,    // Everything else: Players, Guilds, SegmentSizeEstimator, AdminApi, etc. This is the default value, if not overridden.

        Last
    }

    /// <summary>
    /// Specify the NodeSets in the cluster on which a particular <see cref="EntityKind"/> should be placed on.
    /// By default, the cluster has the singleton NodeSet 'service' (<see cref="Service"/>) for the global service
    /// entities and the scalable NodeSet 'logic' (<see cref="Logic"/>) for all the entities that need to be able
    /// to scale to run on multiple nodes (eg, players, connections, and guilds).
    ///
    /// Also used in the runtime options (<see cref="ClusteringOptions"/>) to specify game-specific overrides for
    /// the entity placements. This way, the game can define its own clustering topology that differs from the
    /// default service/logic topology.
    /// </summary>
    public class NodeSetPlacement
    {
        public const string SpecialValueAll = "*"; // Special value for placing on all NodeSets

        public readonly string[]    NodeSetNames;           // Names of the NodeSets on which to place the related Entity
        public readonly bool        PlaceOnAllNodeSets;     // Should this be placed on all NodeSets?

        public static NodeSetPlacement Service  = new NodeSetPlacement(["service"]);        // Place on the default 'service' NodeSet
        public static NodeSetPlacement Logic    = new NodeSetPlacement(["logic"]);          // Place on the default 'logic' NodeSet
        public static NodeSetPlacement All      = new NodeSetPlacement([SpecialValueAll]);  // Place on all NodeSets

        public NodeSetPlacement(string[] nodeSetNames)
        {
            if (nodeSetNames is null)
                throw new ArgumentNullException(nameof(nodeSetNames));
            if (nodeSetNames.Length == 0)
                throw new ArgumentException("Must specify at least one NodeSet name", nameof(nodeSetNames));
            if (nodeSetNames.Any(name => string.IsNullOrEmpty(name)))
                throw new ArgumentException("Must not have null or empty strings for NodeSet names", nameof(nodeSetNames));

            NodeSetNames = nodeSetNames;
            PlaceOnAllNodeSets = nodeSetNames.Any(name => name == SpecialValueAll);
        }
    }

    /// <summary>
    /// Base class for declaring how a given <see cref="EntityKind"/> should behave on the server,
    /// including which EntityActor class to use, how the EntityActors should be spawned and placed
    /// in the cluster, etc.
    ///
    /// You should not derive directly from this class but rather from either
    /// <see cref="EphemeralEntityConfig"/> or <see cref="PersistedEntityConfig"/>.
    /// </summary>
    public abstract class EntityConfigBase
    {
        /// <summary>
        /// Specify which <see cref="EntityKind"/> this configuration is for.
        /// </summary>
        public abstract EntityKind EntityKind { get; }

        /// <summary>
        /// Specify which <see cref="EntityActor"/> class to use for when the entity is active in memory.
        /// If this type is an abstract class, the actual entity actor type is determined by the <see cref="GetActorTypeForEntity"/> method.
        /// </summary>
        public abstract Type EntityActorType { get; }

        /// <summary>
        /// Specify the <see cref="EntityShardGroup"/> that this <c>EntityShard</c> should be started as a part of.
        /// Can be used to configure the initialization order during cluster initialization. Defaults to
        /// <see cref="EntityShardGroup.Workloads"/> as it's almost always the correct choice.
        /// </summary>
        public virtual EntityShardGroup EntityShardGroup { get => EntityShardGroup.Workloads; }

        /// <summary>
        /// Specify which NodeSets in the cluster the associated EntityKind should be placed on. Can be overridden
        /// via <see cref="ClusteringOptions.EntityPlacementOverrides"/> to change the topology from the default
        /// service/logic model.
        /// </summary>
        public abstract NodeSetPlacement NodeSetPlacement { get; }

        /// <summary>
        /// Specify the sharding strategy for the entities, i.e., how each individual entity actor is placed within
        /// the NodeSet(s) that are responsible for managing the entities when there are multiple nodes handling
        /// a given <see cref="EntityKind"/>.
        /// </summary>
        public abstract IShardingStrategy ShardingStrategy { get; }

        /// <summary>
        /// How long should the <see cref="EntityShard"/> be given time to shutdown itself when
        /// the shard is being terminated, before it is forcefully terminated.
        /// </summary>
        public abstract TimeSpan ShardShutdownTimeout { get; }

        /// <summary>
        /// Should entities be automatically spawned when a message is sent to a non-existent one?
        /// </summary>
        // \todo [petri] replace with EntitySpawnPolicy with values like AutoSpawn? add versioning policy also? do auto-spawning also via the policy?
        public virtual bool AllowEntitySpawn => true;

        /// <summary>
        /// Specify which <see cref="EntityShard"/> class to use as the container for the <see cref="EntityActor"/>s.
        /// </summary>
        public virtual Type EntityShardType => typeof(EntityShard);

        /// <summary>
        /// EntityComponent types supported by this Actor.
        /// </summary>
        public virtual List<Type> EntityComponentTypes { get; } = new List<Type>();

        /// <summary>
        /// Sets the limit on how many concurrent entities may be concurrently in shutting down on a single shard,
        /// i.e. processing <see cref="EntityActor.OnShutdown"/>. If more than the limit of entities are attempted
        /// to be shut down, the entity shutdowns are delayed such that the limit is not exceeded.
        ///
        /// This is useful for controlling load in extreme situations, such as when all player entities shut down in
        /// response to the maintenance mode.
        ///
        /// Value <c>-1</c> means no limit.
        /// </summary>
        public virtual int MaxConcurrentEntityShutdownsPerShard => -1;

        /// <summary>
        /// Whether the EntityActorType is polymorphic, i.e., the EntityActorType is an abstract class.
        /// </summary>
        public bool IsPolymorphic => EntityActorType.IsAbstract;

        /// <summary>
        /// Get the actual <see cref="EntityActor"/> type to use for the given entity.
        /// This is used when <see cref="IsPolymorphic"/> is <c>true</c>.
        /// </summary>
        public virtual Type GetActorTypeForEntity(EntityId entityId)
            => IsPolymorphic ? throw new NotImplementedException($"{nameof(GetActorTypeForEntity)} not implemented for EntityKind {EntityKind}.") : EntityActorType;


        /// <summary>
        /// Get all concrete actor types of a polymorphic entity. If the entity is not polymorphic, returns a single-element array with the entity actor type.
        /// </summary>
        public IEnumerable<Type> GetAllConcreteActorTypes()
        {
            if (!IsPolymorphic)
                return new Type[] {EntityActorType};
            else
                return TypeScanner.GetConcreteDerivedTypes(EntityActorType);
        }
    }

    /// <summary>
    /// Base <see cref="EntityKind"/> configuration class for <see cref="EphemeralEntityActor"/>s.
    /// </summary>
    public abstract class EphemeralEntityConfig : EntityConfigBase
    {
    }

    /// <summary>
    /// Base <see cref="EntityKind"/> configuration class for <see cref="PersistedEntityActor{TPersisted, TPersistedPayload}"/>s.
    /// </summary>
    public abstract class PersistedEntityConfig : EntityConfigBase
    {
        static readonly Prometheus.Counter c_entityPersistedUncompressedSize    = Prometheus.Metrics.CreateCounter("metaplay_entity_persisted_uncompressed_size_total",  "Total size (in bytes) of the entities persisted for database, before compression", new string[] { "entity" });
        static readonly Prometheus.Counter c_entityPersistedCompressedSize      = Prometheus.Metrics.CreateCounter("metaplay_entity_persisted_compressed_size_total", "Total size (in bytes) of the entities persisted for database, after compression", new string[] { "entity" });

        /// <summary>
        /// Type of the item stored in the database table (eg, PersistedPlayer). Implements <see cref="IPersistedEntity"/>.
        /// Changing this is not supported for polymorphic actors.
        /// </summary>
        public readonly Type PersistedType;

        /// <summary>
        /// Type of the object serialized as the PersistedXyz.Payload (eg, PlayerModel for PersistedPlayer)
        /// </summary>
        readonly Type _persistedPayloadType;

        // Persisted entities write their data to the DB on shutdown. Limit the load spike by throttling the count to a large value.
        public override int MaxConcurrentEntityShutdownsPerShard => 50;

        public PersistedEntityConfig()
        {
            // EntityActor type must derive from a Persisted Entity Actor. These are marked with IPersistedEntityActor
            if (!EntityActorType.ImplementsGenericInterface(typeof(IPersistedEntityActor<,>)))
                throw new InvalidOperationException($"PersistedEntityConfig is defined for EntityActor '{EntityActorType.ToGenericTypeString()}'. Actor must inherit PersistedEntityActor or PersistedMultiplayerEntityActor!");

            Type[] genericArgs = EntityActorType.GetGenericInterfaceTypeArguments(typeof(IPersistedEntityActor<,>));

            // PersistedType is the first generic argument of IPersistedEntityActor<>
            PersistedType = genericArgs[0];
            if (!PersistedType.IsDerivedFrom<IPersistedEntity>())
                throw new InvalidOperationException($"{EntityActorType.ToGenericTypeString()}'s PersistedType '{PersistedType.ToGenericTypeString()}' doesn't implement IPersistedEntity!");

            if (PersistedType.IsGenericTypeParameter)
            {
                Type[] concreteTypes = GetAllConcreteActorTypes().ToArray();
                IEnumerable<Type> persistedTypes = concreteTypes.Select(t => t.GetGenericInterfaceTypeArguments(typeof(IPersistedEntityActor<,>))[0]);
                try
                {
                    PersistedType = persistedTypes.Distinct().Single();
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException($"Unable to determine a single PersistedType for {EntityActorType.ToGenericTypeString()}. Found: {string.Join(", ", persistedTypes.Select(t => t.ToGenericTypeString()))}", e);
                }
            }

            // PersistedPayloadType is the second generic argument of IPersistedEntityActor<>
            if (!IsPolymorphic)
                _persistedPayloadType = genericArgs[1];
        }

        public byte[] SerializeDatabasePayload(EntityId entityId, object payload, int? logicVersion)
        {
            if (payload.GetType() != GetPersistedPayloadType(entityId))
                throw new InvalidOperationException($"Trying to serialize model of invalid type {payload.GetType().ToGenericTypeString()}, expecting {GetPersistedPayloadType(entityId).ToGenericTypeString()}");

            // Serialize object to buffer
            using FlatIOBuffer serializedBuffer = new FlatIOBuffer(initialCapacity: 4096);
            using (IOWriter serializerWriter = new IOWriter(serializedBuffer))
                MetaSerialization.SerializeTagged(serializerWriter, GetPersistedPayloadType(entityId), payload, MetaSerializationFlags.Persisted, logicVersion);

            // Compress the result (if compression enabled), or return the serialized bytes
            DatabaseOptions dbOpts = RuntimeOptionsRegistry.Instance.GetCurrent<DatabaseOptions>();
            CompressionAlgorithm algorithm = dbOpts.CompressionAlgorithm;
            if (algorithm == CompressionAlgorithm.None)
            {
                // \note Use the uncompressed size as the compressed size so the two remain comparable
                c_entityPersistedUncompressedSize.WithLabels(EntityKind.ToString()).Inc(serializedBuffer.Count);
                c_entityPersistedCompressedSize.WithLabels(EntityKind.ToString()).Inc(serializedBuffer.Count);

                return serializedBuffer.ToArray();
            }
            else
            {
                using (FlatIOBuffer compressed = BlobCompress.CompressBlob(serializedBuffer, algorithm))
                {
                    c_entityPersistedUncompressedSize.WithLabels(EntityKind.ToString()).Inc(serializedBuffer.Count);
                    c_entityPersistedCompressedSize.WithLabels(EntityKind.ToString()).Inc(compressed.Count);
                    //DebugLog.Info("Compressed {UncompressedBytes} bytes to {CompressedBytes} with {Algorithm} ({CompressRatio:0.00}%)", serializedBuffer.Count, compressed.Count, algorithm, compressed.Count * 100.0 / serializedBuffer.Count);

                    // \note Return a copy since we usually write to SQL anyway
                    return compressed.ToArray();
                }
            }
        }

        public TPayload DeserializeDatabasePayload<TPayload>(EntityId entityId, byte[] persisted, IGameConfigDataResolver resolver, int? logicVersion)
        {
            // Detect if compressed payload or not
            if (BlobCompress.IsCompressed(persisted))
            {
                // Decompress payload
                using FlatIOBuffer uncompressed = BlobCompress.DecompressBlob(persisted);

                // Deserialize object
                using IOReader reader = new IOReader(uncompressed);
                return (TPayload)MetaSerialization.DeserializeTagged(reader, GetPersistedPayloadType(entityId), MetaSerializationFlags.Persisted, resolver, logicVersion);
            }
            else // not compressed, just deserialize
            {
                return (TPayload)MetaSerialization.DeserializeTagged(persisted, GetPersistedPayloadType(entityId), MetaSerializationFlags.Persisted, resolver, logicVersion);
            }
        }
        
        /// <summary>
        /// Validate that a persisted entity payload can be decompressed (if compressed) and deserialized
        /// as the corresponding <typeparamref name="TPayload"/> type. The operation is only performed if
        /// <see cref="EnvironmentOptions.ExtraPersistenceChecks"/> is true.
        /// </summary>
        /// <param name="persisted"></param>
        /// <param name="resolver"></param>
        /// <param name="logicVersion"></param>
        public void ValidatePersistedState<TPayload>(IMetaLogger log, EntityId entityId, byte[] persisted, IGameConfigDataResolver resolver, int? logicVersion)
        {
            EnvironmentOptions envOpts = RuntimeOptionsRegistry.Instance.GetCurrent<EnvironmentOptions>();
            if (envOpts.ExtraPersistenceChecks)
            {
                // Decompress payload (if compressed)
                using FlatIOBuffer uncompressed =
                    BlobCompress.IsCompressed(persisted) ? BlobCompress.DecompressBlob(persisted) : FlatIOBuffer.CopyFromSpan(persisted);

                try
                {
                    // Check that serialized state can be skipped over
                    using (IOReader reader = new IOReader(uncompressed))
                        TaggedWireSerializer.TestSkip(reader);

                    // Check that serialized state can be deserialized
                    using (IOReader reader = new IOReader(uncompressed))
                        _ = (TPayload)MetaSerialization.DeserializeTagged(reader, GetPersistedPayloadType(entityId), MetaSerializationFlags.Persisted, resolver, logicVersion);
                }
                catch (Exception ex)
                {
                    // If deserialization fails, report error and immediately exit, so persisted state doesn't get corrupted!
                    log.Error("Failed to validate persisted {TypeName}!\nPersisted ({NumBytes} bytes): {Payload}.\nException: {Exception}", typeof(TPayload).Name, persisted.Length, Convert.ToBase64String(persisted), ex);
                    using IOReader reader = new IOReader(uncompressed);
                    log.Error("Failing persisted {TPayload} data: {Data}", nameof(TPayload), TaggedWireSerializer.ToString(reader));
                    throw;
                }
            }
        }

        public virtual Type GetPersistedPayloadType(EntityId entityId)
            => IsPolymorphic ? throw new NotImplementedException($"{nameof(GetPersistedPayloadType)} not implemented for polymorphic EntityKind {EntityKind}") : _persistedPayloadType;

        public MetaVersionRange GetSupportedSchemaVersions(EntityId entity)
        {
            return SchemaMigrationRegistry.Instance.GetSchemaMigrator(GetPersistedPayloadType(entity)).SupportedSchemaVersions;
        }

        /// <summary>
        /// Whether this entity kind should use real time (<see cref="DateTime.UtcNow"/>)
        /// instead of the potentially time-skip-affected game time (<see cref="MetaTime.Now"/>)
        /// for <see cref="IPersistedEntity.PersistedAt"/>.
        /// By default this is false (i.e. time skip applies).
        /// </summary>
        /// <remarks>
        /// In practice, what this affects is the parameters passed to <see cref="PersistedEntityActor{TPersisted, TPersistedPayload}.PostLoad"/>
        /// as well as the <see cref="IPersistedEntity.PersistedAt"/> assigned by DatabaseEntityUtil (in Metaplay.Server).
        /// Other than that, parts of the entity persisting code is left up to each <see cref="PersistedEntityActor{TPersisted, TPersistedPayload}"/>
        /// subclass to implement, and are not automatically affected by this flag; those implementations should make sure their assignment of
        /// <see cref="IPersistedEntity.PersistedAt"/> is consistent with this flag.
        /// </remarks>
        public virtual bool UsesRealTimeForPersistedAt => false;

        public DateTime GetCurrentTimeForPersistedAt()
        {
            return UsesRealTimeForPersistedAt
                   ? DateTime.UtcNow
                   : MetaTime.Now.ToDateTime();
        }
    }

    /// <summary>
    /// Marks the class as a configuration provider for a given <see cref="EntityKind"/>. The class should
    /// inherit either <see cref="EphemeralEntityConfig"/> or <see cref="PersistedEntityConfig"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EntityConfigAttribute : Attribute
    {
        public EntityConfigAttribute() { }
    }

    /// <summary>
    /// Registers a static method to be called during the EntityActor registry initialization. The registered method takes a single parameter
    /// of type <see cref="EntityConfigBase"/> containing the information obtained for the EntityActor type by the registry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class EntityActorRegisterCallbackAttribute : Attribute
    {
    }

    /// <summary>
    /// Metadata registry for all <see cref="EntityKind"/> configuration (classes having the <see cref="EntityConfigAttribute"/> attribute).
    /// </summary>
    public class EntityConfigRegistry
    {
        public static EntityConfigRegistry Instance => MetaplayServices.Get<EntityConfigRegistry>();

        public Dictionary<Type, EntityConfigBase>   TypeToEntityConfig          { get; private set; } = new();
        Dictionary<EntityKind, EntityConfigBase>    _entityKindToEntityConfig   = new();

        Dictionary<EntityKind, EntityConfigBase>    _featureDisabledEntityKindToEntityConfig = new();

        public EntityConfigRegistry()
        {
            Dictionary<EntityKind, Type> entityKindToEntityConfigType = new Dictionary<EntityKind, Type>(); // For checking uniqueness of EntityKind

            // Scan all concrete classes deriving from EntityConfigBase
            foreach (Type entityConfigType in TypeScanner.GetConcreteDerivedTypes<EntityConfigBase>())
            {
                // Must have the [EntityConfig] attribute
                EntityConfigAttribute entityConfigAttrib = entityConfigType.GetCustomAttribute<EntityConfigAttribute>();
                if (entityConfigAttrib == null)
                    throw new InvalidOperationException($"The type '{entityConfigType.ToGenericTypeString()}' must have [EntityConfig] attribute");

                // Instantiate the EntityConfig
                EntityConfigBase entityConfig = (EntityConfigBase)Activator.CreateInstance(entityConfigType);

                // Check EntityKind uniqueness
                EntityKind entityKind = entityConfig.EntityKind;
                if (entityKindToEntityConfigType.TryGetValue(entityKind, out Type existingConfigType))
                    throw new InvalidOperationException($"EntityConfig types '{entityConfigType.ToGenericTypeString()}' and '{existingConfigType.ToGenericTypeString()}' specify the same EntityKind '{entityKind}'");
                entityKindToEntityConfigType.Add(entityKind, entityConfigType);

                // Skip the rest for disabled types
                // \todo We'd like to check that two EntityConfigs don't specify the same EntityActor.
                //       However, currently this check does not work for disabled EntityConfigs.
                if (!entityConfigType.IsMetaFeatureEnabled())
                {
                    _featureDisabledEntityKindToEntityConfig.Add(entityKind, entityConfig);
                    continue;
                }

                // Store EntityConfig
                TypeToEntityConfig.Add(entityConfigType, entityConfig);
                _entityKindToEntityConfig.Add(entityKind, entityConfig);
            }

            // Stray attribute checks
            foreach (Type type in TypeScanner.GetClassesWithAttribute<EntityConfigAttribute>())
            {
                if (!type.IsDerivedFrom<EntityConfigBase>())
                    throw new InvalidOperationException($"Type '{type.ToGenericTypeString()}' with [EntityConfig] attribute must derive from EntityConfigBase!");
            }
        }

        public EntityConfigBase GetConfig(EntityKind entityKind)
        {
            if (_entityKindToEntityConfig.TryGetValue(entityKind, out EntityConfigBase entityConfig))
                return entityConfig;

            if (_featureDisabledEntityKindToEntityConfig.TryGetValue(entityKind, out EntityConfigBase disabledEntityConfig))
            {
                // \todo Better error message. The message should mention which feature, and also
                //       instruct how to enable it.
                throw new InvalidOperationException($"EntityKind.{entityKind} cannot be used because {disabledEntityConfig.GetType().Name} is associated with a feature that is disabled.");
            }
            else
                throw new InvalidOperationException($"No EntityConfig registered for EntityKind.{entityKind}");
        }

        /// <summary>
        /// Returns <c>true</c> and the <see cref="EntityConfigBase"/> for the kind if such config exists and is enabled. Otherwise, returns <c>false</c> and <c>null</c> in <paramref name="entityConfig"/>.
        /// A kind has no config if no config declaration is defined in code, or if the declaration is disabled with <see cref="MetaplayFeatureEnabledConditionAttribute"/>.
        /// </summary>
        public bool TryGetConfig(EntityKind entityKind, out EntityConfigBase entityConfig)
        {
            return _entityKindToEntityConfig.TryGetValue(entityKind, out entityConfig);
        }

        public PersistedEntityConfig GetPersistedConfig(EntityKind entityKind) => (PersistedEntityConfig)_entityKindToEntityConfig[entityKind];

        public bool TryGetPersistedConfig(EntityKind entityKind, out PersistedEntityConfig entityConfig)
        {
            if (_entityKindToEntityConfig.TryGetValue(entityKind, out EntityConfigBase config) && config is PersistedEntityConfig persistedConfig)
            {
                entityConfig = persistedConfig;
                return true;
            }
            else
            {
                entityConfig = null;
                return false;
            }
        }
    }

    public class EntityConfiguration
    {
        public EntityDispatcherRegistry Dispatchers { get; }

        /// <summary>
        /// Invoke the callback methods marked with <c>[EntityActorRegisterCallback]</c> on all entity kinds.
        /// </summary>
        public EntityConfiguration(EntityConfigRegistry registry, RuntimeOptionsRegistry options)
        {
            Dictionary<Type, EntityConfigBase> entityActorToEntityConfig = new(); // for checking uniqueness
            foreach ((Type entityConfigType, EntityConfigBase entityConfig) in registry.TypeToEntityConfig)
            {
                // Check that ephemeral/persisted is used consistently
                Type entityActorType = entityConfig.EntityActorType;
                if (entityConfig is EphemeralEntityConfig)
                {
                    // EntityActor type must derive from EphemeralEntityActor
                    if (!entityActorType.ImplementsInterface<IEphemeralEntityActor>())
                        throw new InvalidOperationException($"EntityConfig '{entityConfigType.ToGenericTypeString()}' is an EphemeralEntityConfig, but EntityActor '{entityActorType.ToGenericTypeString()}' is not an EphemeralEntityActor or EphemeralMultiplayerEntity!");
                }
                else if (entityConfig is PersistedEntityConfig)
                {
                    // EntityActor type must derive from PersistedEntityActor<>, PersistedMultiplayerEntityActor<> or other persisted actor.
                    if (!entityActorType.ImplementsGenericInterface(typeof(IPersistedEntityActor<,>)))
                        throw new InvalidOperationException($"EntityConfig '{entityConfigType.ToGenericTypeString()}' is a PersistedEntityConfig, but EntityActor '{entityActorType.ToGenericTypeString()}' is not a PersistedEntityActor or PersistedMultiplayerEntityActor!");
                }
                else
                    throw new InvalidOperationException($"EntityConfig '{entityConfigType.ToGenericTypeString()} is neither an EphemeralEntityConfig nor a PersistedEntityConfig -- must be one or the other!'");

                // Check that all polymorphic EntityConfigs have concrete EntityActor types
                if (!entityConfig.GetAllConcreteActorTypes().Any())
                    throw new InvalidOperationException($"EntityConfig '{entityConfigType.ToGenericTypeString()}' has no concrete EntityActor types");

                // Check for uniqueness of EntityActorType
                if (entityActorToEntityConfig.TryGetValue(entityActorType, out EntityConfigBase existingEntityConfig))
                    throw new InvalidOperationException($"EntityConfigs '{entityConfigType.ToGenericTypeString()}' and '{existingEntityConfig.GetType().ToGenericTypeString()}' specify the same EntityActor '{entityActorType.ToGenericTypeString()}'");

                entityActorToEntityConfig.Add(entityConfig.EntityActorType, entityConfig);
            }

            IEnumerable<(Type concreteType, EntityConfigBase entityConfig)> concreteEntityTypes = registry.TypeToEntityConfig.SelectMany(
                x =>
                    x.Value.IsPolymorphic ?
                        x.Value.GetAllConcreteActorTypes().Select(t => (t, x.Value)) :
                        new (Type, EntityConfigBase)[] {(x.Value.EntityActorType, x.Value)});

            // Call EntityActorRegisterCallback methods on registered entities, on the actor type
            foreach ((Type entityActorType, EntityConfigBase entityConfig) in concreteEntityTypes)
            {
                foreach (Type type in entityActorType.EnumerateTypeAndBases().Reverse())
                {
                    IEnumerable<MethodInfo> ancestorMethods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (MethodInfo method in ancestorMethods.Where(x => x.HasCustomAttribute<EntityActorRegisterCallbackAttribute>()))
                    {
                        if (!method.IsStatic)
                            throw new InvalidOperationException($"EntityActorRegisterCallback method '{method.DeclaringType.ToGenericTypeString()}.{method.Name}' must be static!");

                        try
                        {
                            method.InvokeWithoutWrappingError(null, new object[] { entityConfig });
                        }
                        catch (Exception e)
                        {
                            throw new InvalidOperationException($"Invalid EntityActorRegisterCallback method '{method.DeclaringType.ToGenericTypeString()}.{method.Name}", e);
                        }
                    }
                }
            }

            Dispatchers = new EntityDispatcherRegistry(registry);
        }
    }
}
