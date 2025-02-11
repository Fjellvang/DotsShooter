// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Metaplay.Core.Config
{
    public class GameConfigTypeInfo
    {
        public Type                                             GameConfigType          { get; }
        /// <remarks>
        /// Key is the name given in GameConfigEntryAttribute.
        /// </remarks>
        public MetaDictionary<string, GameConfigEntryInfo>   Entries                 { get; }
        public GameConfigBinarySerialization                    Serialization           { get; }

        /// <summary>
        /// Maps the c# member name to the name given in GameConfigEntryAttribute.
        /// </summary>
        public MetaDictionary<string, string>                MemberNameToEntryName   { get; }

        public GameConfigTypeInfo(Type gameConfigType, MetaDictionary<string, GameConfigEntryInfo> entries, GameConfigBinarySerialization serialization)
        {
            GameConfigType = gameConfigType ?? throw new ArgumentNullException(nameof(gameConfigType));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            Serialization = serialization;

            MemberNameToEntryName = entries.Values.ToMetaDictionary(
                keySelector: entry => entry.MemberInfo.Name,
                elementSelector: entry => entry.Name);
        }
    }

    public class GameConfigEntryInfo
    {
        /// <summary>
        /// The name given in GameConfigEntryAttribute.
        /// </summary>
        public string     Name                      { get; }
        public bool       RequireArchiveEntry       { get; }
        public MemberInfo MemberInfo                { get; }
        public Type       BuildSourceType           { get; }
        public string     BuildParamsSourceProperty { get; }

        public GameConfigEntryInfo(
            string name,
            bool requireArchiveEntry,
            MemberInfo memberInfo,
            Type buildSourceType,
            string buildParamsSourceProperty)
        {
            Name                      = name ?? throw new ArgumentNullException(nameof(name));
            RequireArchiveEntry       = requireArchiveEntry;
            MemberInfo                = memberInfo ?? throw new ArgumentNullException(nameof(memberInfo));
            BuildSourceType           = buildSourceType;
            BuildParamsSourceProperty = buildParamsSourceProperty;
        }
    }

    public class GameConfigRepository
    {
        public static GameConfigRepository Instance => MetaplayServices.Get<GameConfigRepository>();
        public GameConfigTypeInfo GetGameConfigTypeInfo(Type gameConfigType) => _gameConfigTypes[gameConfigType];
        public IEnumerable<GameConfigTypeInfo> AllGameConfigTypes => _gameConfigTypes.Values;

        Dictionary<Type, GameConfigTypeInfo> _gameConfigTypes = new Dictionary<Type, GameConfigTypeInfo>();
        public Type SharedGameConfigType { get; private set; }
        public Type ServerGameConfigType { get; private set; }

        static bool IsGameLogicType(Type type)
        {
            foreach (string ns in MetaplayCore.Options.SharedNamespaces)
            {
                if (type.Namespace.StartsWith(ns, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public GameConfigRepository()
        {
            List<Type> gameConfigTypes = TypeScanner.GetAllTypes()
                                            .Where(type => type.IsGameConfigClass())
                                            .ToList();

            foreach (Type type in gameConfigTypes)
            {
                if (type.ImplementsInterface<IGameConfig>())
                    RegisterGameConfigType(type);

                if (IsGameLogicType(type))
                {
                    if (type.ImplementsInterface<ISharedGameConfig>())
                    {
                        if (SharedGameConfigType != null)
                            throw new InvalidOperationException($"Multiple ISharedGameConfig implementations: {SharedGameConfigType} and {type}");
                        SharedGameConfigType = type;
                    }
                    else if (type.ImplementsInterface<IServerGameConfig>())
                    {
                        if (ServerGameConfigType != null)
                            throw new InvalidOperationException($"Multiple IServerGameConfig implementations: {ServerGameConfigType} and {type}");
                        ServerGameConfigType = type;
                    }
                }
            }

            // Defaults
            if (SharedGameConfigType == null)
                SharedGameConfigType = typeof(SharedGameConfigBase);
            if (ServerGameConfigType == null)
                ServerGameConfigType = typeof(ServerGameConfigBase);
        }

        void RegisterGameConfigType(Type gameConfigType)
        {
            List<MemberInfo> entryMembers =
                gameConfigType
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(member => member.HasCustomAttribute<GameConfigEntryAttribute>())
                .Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                .Where(member => !member.HasCustomAttribute<CompilerGeneratedAttribute>())
                .ToList();

            MetaDictionary<string, GameConfigEntryInfo> entries = new MetaDictionary<string, GameConfigEntryInfo>();

            foreach (MemberInfo entryMember in entryMembers)
            {
                Type entryType = entryMember.GetDataMemberType();
                if (!typeof(IGameConfigEntry).IsAssignableFrom(entryType))
                    throw new InvalidOperationException($"{entryMember.ToMemberWithGenericDeclaringTypeString()} has {nameof(GameConfigEntryAttribute)}, but does not implement {nameof(IGameConfigEntry)}");

                GameConfigEntryAttribute entryAttribute = entryMember.GetCustomAttribute<GameConfigEntryAttribute>();
                if (entries.TryGetValue(entryAttribute.EntryName, out GameConfigEntryInfo existing))
                    throw new InvalidOperationException($"Multiple entries in {gameConfigType} with name {entryAttribute.EntryName}: {existing.MemberInfo.Name} and {entryMember.Name}");

                CheckConfigEntryType(entryType, entryMember, entryAttribute);

                Type sourceItemType = entryMember.GetCustomAttribute<GameConfigEntryTransformAttribute>()?.SourceItemType;
                if (sourceItemType != null)
                {
                    if (!entryType.IsGameConfigLibrary())
                        throw new InvalidOperationException($"SourceItemType defined on config entry {entryAttribute.EntryName} that is not a GameConfigLibrary");
                    Type keyType = entryType.GetGenericArguments()[0];
                    Type infoType = entryType.GetGenericArguments()[1];
                    if (sourceItemType.IsGenericTypeDefinition)
                    {
                        // Fill in generic type parameters when needed.
                        // When there's 1 type parameter, assume it's the info type.
                        // When there's 2 type parameters, assume they're the key and info types.

                        int numGenericParameters = sourceItemType.GetGenericArguments().Length;
                        if (numGenericParameters == 1)
                            sourceItemType = sourceItemType.MakeGenericType(infoType);
                        else if (numGenericParameters == 2)
                            sourceItemType = sourceItemType.MakeGenericType(keyType, infoType);
                        else
                            throw new InvalidOperationException($"SourceItemType for config entry {entryAttribute.EntryName} takes {numGenericParameters} type parameters, which isn't supported (must take 0, 1 or 2)");
                    }
                    else if (!sourceItemType.ImplementsInterface(typeof(IGameConfigSourceItem<,>).MakeGenericType(new Type[] { keyType, infoType })))
                    {
                        throw new InvalidOperationException($"SourceItemType for config entry {entryAttribute.EntryName} does not implement IGameConfigDataSource<{keyType}, {infoType}>");
                    }
                    if (sourceItemType.IsAbstract && IntegrationRegistry.TryGetSingleIntegrationType(sourceItemType) == null)
                    {
                        throw new InvalidOperationException($"Integration must provide implementation for abstract SourceItemType {sourceItemType.ToGenericTypeString()}");
                    }
                }

                GameConfigEntryInfo info = new GameConfigEntryInfo(
                    name:                       entryAttribute.EntryName,
                    requireArchiveEntry:        entryAttribute.RequireArchiveEntry,
                    buildSourceType:            sourceItemType,
                    memberInfo:                 entryMember,
                    buildParamsSourceProperty:  entryAttribute.ConfigBuildSource);

                entries.Add(info.Name, info);
            }

            _gameConfigTypes.Add(gameConfigType, new GameConfigTypeInfo(
                gameConfigType,
                entries,
                GameConfigBinarySerialization.Make(gameConfigType, entries.Values)));
        }

        static void CheckConfigEntryType(Type entryType, MemberInfo entryMember, GameConfigEntryAttribute entryAttribute)
        {
            MetaDebug.Assert(typeof(IGameConfigEntry).IsAssignableFrom(entryType), $"Expected an {nameof(IGameConfigEntry)}-implementing type");

            if (entryType.IsGameConfigLibrary())
            {
                // Check both T and U in GameConfigLibrary<T, U> are serializable.
                foreach (Type keyOrValue in entryType.GetGenericArguments())
                    CheckConfigTypeIsSerializable(keyOrValue, entryAttribute);

                return;
            }
            else if (entryType.IsDerivedFrom<GameConfigKeyValue>())
            {
                if (!entryType.HasGenericAncestor(typeof(GameConfigKeyValue<>)))
                    throw new InvalidOperationException($"{entryType.ToGenericTypeString()} is derived from GameConfigKeyValue; it needs to be derived from the generic GameConfigKeyValue<{entryType.Name}>");

                // Check T in GameConfigKeyValue<T> is serializable.
                CheckConfigTypeIsSerializable(entryType.GetGenericAncestorTypeArguments(typeof(GameConfigKeyValue<>))[0], entryAttribute);

                return;
            }
            else
            {
                throw new InvalidOperationException($"{entryMember.ToMemberWithGenericDeclaringTypeString()} has {nameof(GameConfigEntryAttribute)}, but isn't of a known type (GameConfigLibrary<TKey, TValue> or a GameConfigKeyValue<T>)");
            }
        }

        static void CheckConfigTypeIsSerializable(Type type, GameConfigEntryAttribute entryAttribute)
        {
            if (TaggedWireSerializer.IsBuiltinType(type))
                return;
            if (type.HasCustomAttribute<MetaSerializableAttribute>())
                return;
            if (type.HasCustomAttribute<MetaSerializableDerivedAttribute>())
                return;
            throw new InvalidOperationException($"Cannot use {type.ToGenericTypeString()} in [GameConfigEntry(\"{entryAttribute.EntryName}\")]. Type must be [MetaSerializable], [MetaSerializableDerived] or a known built-in type.");
        }
    }
}
