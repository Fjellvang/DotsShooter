// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Model;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// Introduce additional serializable types related to GameConfig classes
    /// </summary>
    [MetaSerializableTypeProvider]
    static class GameConfigSerializableTypeProvider
    {
        /// <summary>
        /// For each <see cref="GameConfigLibrary{TKey, TInfo}"/>, generate <see cref="GameConfigLibraryAliasTable{TKey}"/> and <see cref="GameConfigDataContent{TConfigData}"/>.
        /// </summary>
        [MetaSerializableTypeGetter]
        public static IEnumerable<Type> GetSerializableTypes()
        {
            foreach (GameConfigTypeInfo config in GameConfigRepository.Instance.AllGameConfigTypes)
            {
                if (!config.GameConfigType.IsMetaFeatureEnabled())
                    continue;

                foreach (GameConfigEntryInfo entry in config.Entries.Values)
                {
                    Type entryType = entry.MemberInfo.GetDataMemberType();
                    if (!entryType.IsGameConfigLibrary())
                        continue;
                    Type[] typeArguments = entryType.GetGenericArguments();

                    // Register alias table type.
                    Type keyType = typeArguments[0];
                    yield return typeof(GameConfigLibraryAliasTable<>).MakeGenericType(keyType);

                    // Register GameConfigDataContent<> type.
                    // Among other uses, this is used for cloning config items when doing
                    // MetaRef-based item duplication when constructing deduplicating config specializations.
                    Type itemType = typeArguments[1];
                    yield return typeof(GameConfigDataContent<>).MakeGenericType(itemType);
                }
            }
        }
    }

    [MetaSerializableTypeProvider]
    static class FullGameConfigPatchSerializableTypeProvider
    {
        /// <summary>
        /// For each game config entry, generate <see cref="GameConfigLibraryPatch{TKey, TInfo}"/> or <see cref="GameConfigStructurePatch{TStructure}"/>.
        /// </summary>
        [MetaSerializableTypeGetter]
        public static IEnumerable<Type> GetSerializableTypes()
        {
            foreach (GameConfigTypeInfo config in GameConfigRepository.Instance.AllGameConfigTypes)
            {
                if (!config.GameConfigType.IsMetaFeatureEnabled())
                    continue;

                foreach (GameConfigEntryInfo entry in config.Entries.Values)
                {
                    Type entryType = entry.MemberInfo.GetDataMemberType();
                    yield return GameConfigPatchUtil.GetEntryPatchType(entryType);
                }
            }

            yield return typeof(Dictionary<string, byte[]>); // used for top-level entry->patch lookup
        }
    }
}
