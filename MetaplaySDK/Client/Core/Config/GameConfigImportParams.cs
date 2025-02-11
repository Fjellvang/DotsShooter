// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Player;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// Encapsulates the resources and parameters for constructing a game config instance.
    /// Use this with <see cref="IGameConfig.Import(GameConfigImportParams)"/> to populate
    /// the config; typically this is done via <see cref="GameConfigFactory.ImportGameConfig(GameConfigImportParams)"/>.
    /// </summary>
    public class GameConfigImportParams
    {
        /// <summary>
        /// The resources that are not specific to a single specialization of the config.
        /// These resources can be re-used over multiple specializations of the config
        /// (when <see cref="GameConfigImportResources.ConfigRuntimeStorageMode"/> is
        /// <see cref="GameConfigRuntimeStorageMode.Deduplicating"/>).
        /// </summary>
        public readonly GameConfigImportResources Resources;
        /// <inheritdoc cref="GameConfigDeduplicationOwnership"/>
        public readonly GameConfigDeduplicationOwnership DeduplicationOwnership;
        /// <summary>
        /// Identifies the specialization (possibly baseline).
        /// </summary>
        public readonly OrderedSet<ExperimentVariantPair> ActivePatches;

        /// <summary>
        /// Whether the config instance is being constructed for the purpose of game config building.
        /// This affects config validation:
        /// - During config building, <see cref="GameConfigBase.Validate"/> is omitted, because config
        ///   builder instead calls <see cref="GameConfigBase.BuildTimeValidate"/>.
        /// - During config building, certain extra checks are done during config import, such as
        ///   <see cref="GameConfigBase.CheckNoDuplicatesPerItemTypeAcrossLibraries"/>.
        /// </summary>
        public readonly bool IsBuildingConfigs;

        /// <summary>
        /// Importing game config to be used as the parent config for a partial gameconfig build.
        /// </summary>
        public readonly bool IsConfigBuildParent;

        /// <summary>
        /// Filters the config entries by the entry name, this allows partial importing however it is possible to import an unusable config.
        /// This should be considered immutable, however Unity does not support a immutable hashset interface that also has a Contains method.
        /// This is used in the dashboard implementation to do lazy loading of config entries, and to allow the user to view data while some other parts of the config might not deserialize.
        /// </summary>
        public readonly HashSet<string> PartialImportEntryNames;

        internal GameConfigImportParams(GameConfigImportResources resources, GameConfigDeduplicationOwnership deduplicationOwnership, OrderedSet<ExperimentVariantPair> activePatches, bool isBuildingConfigs, bool isConfigBuildParent, HashSet<string> partialImportEntryNames = null)
        {
            Resources               = resources;
            DeduplicationOwnership  = deduplicationOwnership;
            ActivePatches           = activePatches;
            IsBuildingConfigs       = isBuildingConfigs;
            IsConfigBuildParent     = isConfigBuildParent;
            PartialImportEntryNames = partialImportEntryNames;

            foreach (ExperimentVariantPair patchId in activePatches)
            {
                if (!resources.Patches.ContainsKey(patchId))
                    throw new ArgumentException($"{nameof(activePatches)} contains {patchId} which does not exist in {nameof(resources)}.{nameof(resources.Patches)}", nameof(activePatches));
            }

            if (deduplicationOwnership == GameConfigDeduplicationOwnership.Baseline)
            {
                if (activePatches.Count != 0)
                    throw new ArgumentException($"When config has baseline ownership, it cannot have any active patches (but has [{string.Join(", ", activePatches)}])", nameof(activePatches));
            }
            else if (deduplicationOwnership == GameConfigDeduplicationOwnership.SinglePatch)
            {
                if (activePatches.Count != 1)
                    throw new ArgumentException( $"When config has single-patch ownership, it must have exactly 1 active patch (but has [{string.Join(", ", activePatches)}])", nameof(activePatches));
            }
        }

        /// <summary>
        /// Creates parameters specifying a config specialization (possibly baseline, if <paramref name="activePatches"/> is empty),
        /// using the given resources.
        /// </summary>
        public static GameConfigImportParams Specialization(GameConfigImportResources resources, OrderedSet<ExperimentVariantPair> activePatches, bool isBuildingConfigs = false, bool isConfigBuildParent = false, HashSet<string> partialImportEntryNames = null)
        {
            return new GameConfigImportParams(
                resources,
                GameConfigDeduplicationOwnership.None,
                activePatches,
                isBuildingConfigs: isBuildingConfigs,
                isConfigBuildParent: isConfigBuildParent,
                partialImportEntryNames);
        }

        /// <summary>
        /// Shorthand helper for constructing <see cref="GameConfigImportResources"/> from <paramref name="archive"/> and <paramref name="patches"/>
        /// and then constructing parameters specifying the specialization with all of those patches.
        /// <para>
        /// "Solo" signifies that there will be no config deduplication; the config instance created with these params
        /// will not share its contents with any other config instance.
        /// </para>
        /// </summary>
        public static GameConfigImportParams CreateSoloSpecialization(Type gameConfigType, ConfigArchive archive, MetaDictionary<ExperimentVariantPair, GameConfigPatchEnvelope> patches, bool isBuildingConfigs = false, bool isConfigBuildParent = false, HashSet<string> partialImportEntryNames = null)
        {
            GameConfigImportResources resources = GameConfigImportResources.Create(gameConfigType, archive, patches, GameConfigRuntimeStorageMode.Solo);
            return Specialization(resources, patches.Keys.ToOrderedSet(), isBuildingConfigs: isBuildingConfigs, isConfigBuildParent: isConfigBuildParent, partialImportEntryNames: partialImportEntryNames);
        }

        /// <summary>
        /// Shorthand helper for constructing <see cref="GameConfigImportResources"/> from <paramref name="archive"/> and no patches,
        /// and then constructing parameters specifying that baseline config.
        /// <para>
        /// "Solo" signifies that there will be no config deduplication; the config instance created with these params
        /// will not share its contents with any other config instance.
        /// </para>
        /// </summary>
        public static GameConfigImportParams CreateSoloUnpatched(Type gameConfigType, ConfigArchive archive, bool isBuildingConfigs = false, bool isConfigBuildParent = false, HashSet<string> partialImportEntryNames = null)
        {
            return CreateSoloSpecialization(
                gameConfigType,
                archive,
                patches: new MetaDictionary<ExperimentVariantPair, GameConfigPatchEnvelope>(),
                isBuildingConfigs: isBuildingConfigs,
                isConfigBuildParent: isConfigBuildParent,
                partialImportEntryNames: partialImportEntryNames);
        }

        public bool ShouldImportEntry(string entryName)
        {
            return PartialImportEntryNames == null || PartialImportEntryNames.Contains(entryName);
        }
    }
}
