// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Localization;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Core.Config
{
    /// <summary>
    /// R25 debugging knobs for comparing behavior between legacy and new config parsing pipelines.
    /// </summary>
    static class ConfigBuildDevelopmentDebug
    {
        /// <summary>
        /// Force a fixed timestamp for archives, for reproducible archive contents (and thus hashes) for comparison.
        /// </summary>
        public static readonly bool ForceFixedArchiveTimestamp = false;
    }

    /// <summary>
    /// Game config build failed with an error related to the build inputs (eg, input failed to parse).
    /// Includes a <see cref="GameConfigBuildReport"/> that has the messages from the build log
    /// (<see cref="GameConfigBuildLog"/>).
    /// </summary>
    public class GameConfigBuildFailed : Exception
    {
        public readonly GameConfigBuildReport BuildReport;

        public GameConfigBuildFailed(GameConfigBuildReport buildreport) =>
            BuildReport = buildreport;

#if UNITY_2017_1_OR_NEWER
        // In Unity, override the printing to console to ensure the build report log is shown!
        public override string Message =>
            $"Game config build failed! See the log below for details.\n" +
            BuildReport.MessagesToString(maxBuildLogMessages: 20, maxValidationLogMessages: 20);
#endif

        /// <summary>
        /// Default ToString() includes the build and validation log messages to ensure the real cause of build
        /// failures is shown even if the exception is not handled.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            // Stack trace
            sb.AppendLine($"{GetType().FullName}: See the log below for details.");
            sb.AppendLine(StackTrace);

            // Include build report logs
            sb.AppendLine("");
            sb.Append(BuildReport.MessagesToString(maxBuildLogMessages: 20, maxValidationLogMessages: 20));

            return sb.ToString();
        }
    }

    public class GameConfigBuildDebugOptions
    {
        public bool EnableDebugPrints;
        /// <summary>
        /// Whether to use <see cref="ObjectGraphDump"/> for checking that <see cref="GameConfigBase.OnLoaded"/>
        /// didn't mutate items in an unsupported manner. This can use a lot of memory so it's best disabled
        /// outside local development environments.
        /// </summary>
        public bool EnableDebugDumpCheck = true;
        /// <summary>
        /// Run the full config build-time validation only for the baseline config, instead of validating
        /// all experiment variants. Setting this to true will generate a build warning in the output.
        /// </summary>
        public bool OnlyValidateBaseline;
    }

    /// <summary>
    /// Utilities provided to the game-specific game config build for populating a game config (shared or server) data from
    /// various sources.
    /// </summary>
    public interface IGameConfigBuilder
    {
        void AssignEmpty(string configEntryName);
        void AssignBaselineConfigEntryBuildResult(string configEntryName, IGameConfigEntry baselineEntry, GameConfigSourceMapping sourceMapping);
        void AssignConfigEntryBuildResult<TConfigEntryPatch>(string configEntryName, IGameConfigEntry baselineEntry, Dictionary<ExperimentVariantPair, TConfigEntryPatch> variantEntryPatches, GameConfigSourceMapping sourceMapping) where TConfigEntryPatch : GameConfigEntryPatch;
        void AssignConfigKeyValueStructureBuildResult<TStructure>(string configEntryName, List<VariantConfigStructureMember> members, GameConfigSourceMapping sourceMapping) where TStructure : GameConfigKeyValue, new();
        void AssignLibraryBuildResult<TKey, TInfo>(string configEntryName, List<VariantConfigItem<TKey, TInfo>> items, GameConfigSourceMapping sourceMapping) where TInfo : class, IGameConfigData<TKey>, new();
        void BuildGameConfigKeyValueStructure<TStructure>(string configEntryName, SpreadsheetContent sourceSheet) where TStructure : GameConfigKeyValue, new();
        void BuildGameConfigLibrary<TKey, TInfo>(string configEntryName, SpreadsheetContent sourceSheet) where TInfo : class, IGameConfigData<TKey>, new();
        void BuildGameConfigEntry(string configEntryName, SpreadsheetContent sourceSheet);
        void BuildGameConfigLibraryWithSourceTransform<TKey, TInfo, TSourceItem>(string configEntryName, SpreadsheetContent sheet)
            where TInfo : class, IGameConfigData<TKey>, new()
            where TSourceItem : IGameConfigSourceItem<TKey, TInfo>, new();

        void BuildGameConfigLibraryWithTransform<TKey, TInfo, TSourceItem>(string configEntryName, SpreadsheetContent sheet, Func<GameConfigBuildLog, TSourceItem, TInfo> transform)
            where TInfo : class, IGameConfigData<TKey>, new()
            where TSourceItem : IGameConfigSourceItem<TKey, TInfo>, new();
        List<VariantConfigItem<TKey, TItem>> ParseLibrarySheetItems<TKey, TItem>(string configEntryName, SpreadsheetContent sheet) where TItem : IHasGameConfigKey<TKey>, new();
        List<VariantConfigStructureMember> ParseStructureSheetMembers<TStructure>(string configEntryName, SpreadsheetContent sheet) where TStructure : GameConfigKeyValue, new();
        void AddCustomArchiveEntry(ConfigArchiveEntry entry);
        Type ConfigType { get; }
        GameConfigBuildLog BuildLog { get; }
        List<GameConfigSourceMapping> SourceMappings { get;  }
    }

    public interface IGameConfigBuilder<TConfig> : IGameConfigBuilder where TConfig : IGameConfig
    {
        TConfig RawOutput { get; }
    }

    public interface IGameConfigBuilderProvider<TConfig> where TConfig : IGameConfig
    {
        IGameConfigBuilder<TDerivedConfig> MakeBuilder<TDerivedConfig>() where TDerivedConfig : TConfig;
    }

    interface IGameConfigBuild
    {
        GameConfigBuildDebugOptions    DebugOptions  { get; }
        GameConfigBuildIntegration Integration { get; }
    }

    class GameConfigBuildState<TConfig> : IGameConfigBuilderProvider<TConfig> where TConfig : IGameConfig
    {
        GameConfigBuildLog                                        _buildLog;
        TConfig                                                   _output;
        HashSet<string>                                           _builtEntries         = new HashSet<string>();
        List<ConfigArchiveEntry>                                  _customArchiveEntries = new List<ConfigArchiveEntry>();
        Dictionary<string, GameConfigEntryBuildResult>            _entryBuildResults    = new Dictionary<string, GameConfigEntryBuildResult>();
        GameConfigTypeInfo                                        _configTypeInfo;
        public Type                                               GameConfigType => _configTypeInfo.GameConfigType;
        ConfigArchive                                             _parentArchive;
        public Dictionary<ExperimentVariantPair, GameConfigPatch> ParentVariantPatches;
        IGameConfigBuild                                          _build;
        public TConfig                                            RawOutput  => _output;
        public List<GameConfigSourceMapping>                      SourceMappings { get; } = new List<GameConfigSourceMapping>();

        public IGameConfigBuilder<TDerivedConfig> MakeBuilder<TDerivedConfig>() where TDerivedConfig : TConfig
        {
            return new BuilderApi<TDerivedConfig>(this);
        }

        public GameConfigBuildState(
            IGameConfigBuild build,
            ConfigArchive parentArchive,
            GameConfigBuildLog buildLog,
            TConfig output)
        {
            _build = build;
            _parentArchive = parentArchive;
            _output = output;
            _buildLog = buildLog;
            _configTypeInfo = GameConfigRepository.Instance.GetGameConfigTypeInfo(output.GetType());
        }

        public GameConfigBuildOutput Finalize(MetaTime createdAt)
        {
            // Finalize config loading
            _output.OnConfigEntriesPopulated(null, isBuildingConfigs: true);

            // Construct final archive
            ConfigArchiveEntry[]            baselineMpcEntries    = _output.ExportMpcArchiveEntries();
            List<ConfigArchiveEntry> baselineCustomEntries = _customArchiveEntries;
            Dictionary<string, ConfigArchiveEntry> baselineEntriesByName = baselineMpcEntries.Concat(baselineCustomEntries).ToDictionary(entry => entry.Name);

            // If this is a partial build, some entries might be missing from the list of newly-built entries.
            // Namely, custom (non-mpc) entries aren't exported by GameConfigBase.ExportMpcArchiveEntries,
            // and might thus be missing at this point.
            // In that case, take those entries directly from the parent archive.
            if (_parentArchive != null)
            {
                foreach (ConfigArchiveEntry entry in _parentArchive.Entries)
                {
                    if (entry.Name.EndsWith(".AliasTable.mpc", StringComparison.Ordinal) ||
                        entry.Name.EndsWith(".AliasTable2.mpc", StringComparison.Ordinal))
                        continue;
                    if (!baselineEntriesByName.ContainsKey(entry.Name))
                    {
                        if (_build.DebugOptions.EnableDebugPrints)
                            DebugLog.Info($"Copying entry {entry.Name} from baseline to new archive");
                        baselineEntriesByName.Add(entry.Name, entry);
                    }
                }
            }
            ConfigArchive baselineArchive = ConfigArchive.CreateFromBuild(createdAt, baselineEntriesByName.Values);

            Dictionary<ExperimentVariantPair, GameConfigPatchEnvelope> variantPatches =
                GetVariantEntryPatches(ParentVariantPatches)
                .ToDictionary(
                    kv => kv.Key,
                    kv =>
                    {
                        Dictionary<string, GameConfigEntryPatch> entryPatches = kv.Value;
                        GameConfigPatch configPatch = new GameConfigPatch(GameConfigType, entryPatches);
                        return configPatch.SerializeToEnvelope(MetaSerializationFlags.IncludeAll);
                    });

            return new GameConfigBuildOutput(baselineArchive, variantPatches);
        }

        /// <summary>
        /// Within the build result, checks that the MetaRefs refer to existing items.
        /// Should be called after the actual build has been done but before calling <see cref="Finalize"/>.
        /// Errors are logged to <see cref="_buildLog"/>.
        /// </summary>
        /// <remarks>
        /// This exists in order to improve error messages.
        /// Invalid MetaRefs would anyway cause errors during the config validation which happens at the end of
        /// config build, but those checks throw at the first error instead of producing a nice build log.
        /// </remarks>
        public void ValidateMetaRefs()
        {
            // Get mapping from item type to list of libraries the item type might exist in.
            // This will be used to check if a MetaRef-referred item exists.
            Dictionary<Type, List<GameConfigEntryInfo>> itemTypeToLibraries = GameConfigUtil.CollectItemTypeToLibrariesMapping(_configTypeInfo.GameConfigType);

            // Get mapping from config entry name to the baseline entry instance.
            Dictionary<string, IGameConfigEntry> entryNameToBaselineEntry =
                _configTypeInfo
                .Entries.Values
                .ToDictionary(
                    entry => entry.Name,
                    entry => (IGameConfigEntry)entry.MemberInfo.GetDataMemberGetValueOnDeclaringType()(_output));

            // Traverse all MetaRefs and check that the corresponding item exists (in the same variant, or baseline).
            // - For baseline and each variant:
            // - Traverse all MetaRefs
            //   - For baseline, traverse the MetaRefs in each baseline entry (library or GameConfigKeyValue entry)
            //   - For a variant, traverse the MetaRefs in each entry patch (library patch or GameConfigKeyValue patch) in the variant
            // - For each MetaRef encountered, check that the item exists
            //   - For baseline, the item must exist in a baseline library
            //   - For a variant, the item must exist in a baseline library or in the patch of a library in the same variant

            // For baseline, check MetaRefs in all entries
            foreach (GameConfigEntryInfo entryInfo in _configTypeInfo.Entries.Values)
            {
                MetaSerializationMetaRefTraversalParams metaRefTraversalParams = CreateValidatingMetaRefTraversalParams(
                    itemTypeToLibraries,
                    entryNameToBaselineEntry,
                    entryNameToEntryPatch: new Dictionary<string, GameConfigEntryPatch>(),
                    entryInfo,
                    variantId: null);

                // Traverse MetaRefs in the baseline library or GameConfigKeyValue.

                IGameConfigEntry entry = entryNameToBaselineEntry[entryInfo.Name];

                if (entry is IGameConfigLibraryEntry library)
                {
                    Type itemType = GameConfigUtil.GetLibraryKeyAndItemTypes(entryInfo.MemberInfo.GetDataMemberType()).ItemType;
                    IList itemsList = library.GetValuesList();
                    MetaSerialization.TraverseMetaRefsInTable(itemType, itemsList, resolver: null, metaRefTraversalParams);
                }
                else if (entry is GameConfigKeyValue keyValue)
                {
                    object keyValueObject = keyValue;
                    MetaSerialization.TraverseMetaRefs(keyValue.GetType(), ref keyValueObject, resolver: null, metaRefTraversalParams);
                }
                else
                {
                    // Custom entry type - ignore.
                }
            }

            // For each variant, check MetaRefs in the entries patched by the variant
            foreach ((ExperimentVariantPair variantId, Dictionary<string, GameConfigEntryPatch> entryNameToEntryPatch) in GetVariantEntryPatches(ParentVariantPatches))
            {
                foreach ((string entryName, GameConfigEntryPatch entryPatch) in entryNameToEntryPatch)
                {
                    GameConfigEntryInfo entryInfo = _configTypeInfo.Entries[entryName];

                    MetaSerializationMetaRefTraversalParams metaRefTraversalParams = CreateValidatingMetaRefTraversalParams(
                        itemTypeToLibraries,
                        entryNameToBaselineEntry,
                        entryNameToEntryPatch,
                        entryInfo,
                        variantId);

                    // Traverse MetaRefs in the patch.

                    if (entryPatch is IGameConfigLibraryPatch libraryPatch)
                    {
                        Type itemType = GameConfigUtil.GetLibraryKeyAndItemTypes(entryInfo.MemberInfo.GetDataMemberType()).ItemType;
                        IList itemsList = libraryPatch.GetAppendedAndReplacedItemsList();
                        MetaSerialization.TraverseMetaRefsInTable(itemType, itemsList, resolver: null, metaRefTraversalParams);
                    }
                    else if (entryPatch is IGameConfigStructurePatch structurePatch)
                    {
                        foreach ((MetaSerializableMember memberSpec, object memberValue) in structurePatch.EnumerateReplacedMemberValues())
                        {
                            Type memberType = memberSpec.MemberInfo.GetDataMemberType();

                            // Skip basic types and such, because those aren't in the registry and aren't supported by TraverseMetaRefs.
                            // This is ok, because those types don't contain MetaRefs anyway.
                            if (!MetaSerializerTypeRegistry.TryGetTypeSpec(memberType, out _))
                                continue;

                            object memberValueTmp = memberValue;
                            MetaSerialization.TraverseMetaRefs(memberType, ref memberValueTmp, resolver: null, metaRefTraversalParams);
                        }
                    }
                    else
                        throw new MetaAssertException($"Unknown {nameof(GameConfigEntryPatch)} type: {entryPatch.GetType().ToGenericTypeString()}");
                }
            }
        }

        /// <summary>
        /// Create <see cref="MetaSerializationMetaRefTraversalParams"/> that checks item existence for each encountered MetaRef and MetaConfigId.
        /// See <see cref="CheckMetaRef"/> for info about the parameters.
        /// </summary>
        MetaSerializationMetaRefTraversalParams CreateValidatingMetaRefTraversalParams(
            Dictionary<Type, List<GameConfigEntryInfo>> itemTypeToLibraries,
            Dictionary<string, IGameConfigEntry> entryNameToBaselineEntry,
            Dictionary<string, GameConfigEntryPatch> entryNameToEntryPatch,
            GameConfigEntryInfo containingEntryInfo,
            ExperimentVariantPair? variantId)
        {
            // \note For non-library entries (GameConfigKeyValue), currentItem will remain null, as visitTableTopLevelConfigItem isn't called.
            IGameConfigData currentItem = null;
            return new MetaSerializationMetaRefTraversalParams(
                visitTableTopLevelConfigItem: (ref MetaSerializationContext context, IGameConfigData item) =>
                {
                    currentItem = item;
                },
                visitMetaRef: (ref MetaSerializationContext context, IMetaRef metaRef) =>
                {
                    CheckMetaRef(itemTypeToLibraries, entryNameToBaselineEntry, entryNameToEntryPatch, metaRef, currentItem, containingEntryInfo, variantId);
                },
                visitMetaConfigId: (ref MetaSerializationContext context, IMetaConfigId metaConfigId) =>
                {
                    CheckMetaRef(itemTypeToLibraries, entryNameToBaselineEntry, entryNameToEntryPatch, metaConfigId, currentItem, containingEntryInfo, variantId);
                });
        }

        /// <summary>
        /// Check that <paramref name="metaRef"/> refers to an existing item in the baseline or variant (if any).
        /// Each call to this method happens in the context of either the baseline or a specific variant.
        /// Used by <see cref="ValidateMetaRefs"/>.
        /// <para>
        /// Checks both <see cref="MetaRef{TItem}"/> and <see cref="MetaConfigId{TItem}"/>.
        /// </para>
        /// </summary>
        /// <param name="itemTypeToLibraryInfos">Map an item type to the libraries it could potentially exist in, as with <see cref="GameConfigUtil.CollectItemTypeToLibrariesMapping"/>.</param>
        /// <param name="entryNameToBaselineEntry">Map a config entry name to the entry instance in the baseline.</param>
        /// <param name="entryNameToEntryPatch">
        /// Map a config entry name to that entry's patch in the variant, if any.
        /// If the entry isn't patched by the variant, this doesn't contain that entry.
        /// For baseline, this is an empty dictionary.
        /// </param>
        /// <param name="metaRef">The reference to check. Note the base type <see cref="IMetaRefBase"/>, meaning the reference can be of type <see cref="MetaRef{TItem}"/> or <see cref="MetaConfigId{TItem}"/>.</param>
        /// <param name="containingItemMaybe">The config item containing <paramref name="metaRef"/>, or null if <paramref name="metaRef"/> is within a <see cref="GameConfigKeyValue"/> entry.</param>
        /// <param name="containingEntry">The info for the config entry which contains <paramref name="metaRef"/>.</param>
        /// <param name="variantId">The id of the variant the <paramref name="metaRef"/> happens in. Used for build log error messages.</param>
        void CheckMetaRef(
            Dictionary<Type, List<GameConfigEntryInfo>> itemTypeToLibraryInfos,
            Dictionary<string, IGameConfigEntry> entryNameToBaselineEntry,
            Dictionary<string, GameConfigEntryPatch> entryNameToEntryPatch,
            IMetaRefBase metaRef,
            IGameConfigData containingItemMaybe,
            GameConfigEntryInfo containingEntry,
            ExperimentVariantPair? variantId)
        {
            Type   itemType = metaRef.ItemType;
            object refKey   = metaRef.KeyObject;

            // Get the list of libraries the item might possibly exist in.
            List<GameConfigEntryInfo> libraryInfos;
            if (!itemTypeToLibraryInfos.TryGetValue(itemType, out libraryInfos))
            {
                (string configKeyStr, Type containingObjectType) = ResolveContainingObjectInfo(containingEntry, containingItemMaybe);
                throw new InvalidOperationException(Invariant($"'{configKeyStr}' of type {containingObjectType.ToGenericTypeString()} contains {metaRef.GetType().ToGenericTypeString()} reference to item '{refKey}', but no library was found for the target type {itemType.ToGenericTypeString()}"));
            }

            // Look for the item in the candidate libraries.
            bool found = false;
            foreach (GameConfigEntryInfo libraryInfo in libraryInfos)
            {
                IGameConfigLibraryEntry library = (IGameConfigLibraryEntry)entryNameToBaselineEntry[libraryInfo.Name];

                // First, resolve the canonical key in case we're dealing with an alias key.
                object key = refKey;
                if (library.TryResolveRealKeyFromAlias(refKey, out object realKey))
                    key = realKey;

                // Try to find from baseline library.
                // Even if we're dealing with a variant, it's enough that the item is found from the baseline library,
                // because a variant can only replace or add new items; it cannot remove items.
                if (library.TryResolveReference(key) != null)
                {
                    found = true;
                    break;
                }

                // Try to find from the variant's patch (if dealing with baseline, entryNameToEntryPatch is empty).
                // If the item wasn't found from the baseline, it must be found in the patch.
                if (entryNameToEntryPatch.TryGetValue(libraryInfo.Name, out GameConfigEntryPatch entryPatch))
                {
                    IGameConfigLibraryPatch libraryPatch = (IGameConfigLibraryPatch)entryPatch;
                    // \note It's enough to search among the items _added_ by this patch.
                    //       No need to search among the _replaced_ items, because those ids already exist in the baseline,
                    //       and baseline existence was already checked above. (Only the item's existence matters, not its
                    //       contents, so replacement doesn't matter.)
                    if (libraryPatch.ContainsAppendedItemWithKey(key))
                    {
                        found = true;
                        break;
                    }
                }
            }

            // If not found, log an error.
            if (!found)
            {
                GameConfigBuildLog buildLog = _buildLog;
                if (variantId.HasValue)
                    buildLog = buildLog.WithVariantId(variantId.Value.ToString());

                LogMetaRefResolveError(buildLog, containingEntry, containingItemMaybe, metaRef, SourceMappings);
            }
        }

        /// <summary>
        /// Report a build log error regarding a <see cref="MetaRef{TItem}"/> or <see cref="MetaConfigId{TItem}"/> which refers to a non-existent item.
        /// Tries to find the source location of the error from the <paramref name="sourceMappings"/> on a best effort
        /// basis. If the source wasn't found, non-sourced error is still written to the build log.
        /// </summary>
        /// <param name="buildLog">The build log (scoped to the correct variant id, if any) where the error should be logged.</param>
        /// <param name="metaRef">The erroneous reference. Note: can be of type <see cref="MetaRef{TItem}"/> or <see cref="MetaConfigId{TItem}"/>,</param>
        /// <param name="containingEntryInfo">The info for the config entry which contains <paramref name="metaRef"/>.</param>
        /// <param name="configItemMaybe">The config item containing <paramref name="metaRef"/>, or null if <paramref name="metaRef"/> is within a <see cref="GameConfigKeyValue"/> entry.</param>
        static void LogMetaRefResolveError(GameConfigBuildLog buildLog, GameConfigEntryInfo containingEntryInfo, IGameConfigData configItemMaybe, IMetaRefBase metaRef, List<GameConfigSourceMapping> sourceMappings)
        {
            // \todo Using containingEntryInfo.Name as configKeyStr for KeyValueObjects, need to clean it up

            object refKey = metaRef.KeyObject;

            (string configKeyStr, Type containingObjectType) = ResolveContainingObjectInfo(containingEntryInfo, configItemMaybe);

            string message = Invariant($"Encountered a {metaRef.GetType().ToGenericTypeString()} reference to unknown item '{refKey}' in '{configKeyStr}' (type {containingObjectType.ToGenericTypeString()})");

            foreach (GameConfigSourceMapping sourceMapping in sourceMappings)
            {
                // \todo Comparing spreadsheet name vs library name which is not correct
                if (sourceMapping.SourceInfo is GameConfigSpreadsheetSourceInfo spreadsheetInfo && containingEntryInfo.Name == spreadsheetInfo.GetSheetName())
                {
                    // \todo Can we get a memberPathHint for the error (ie, where the MetaRef causing the error was a member of)?
                    if (sourceMapping.TryFindItemSource(configKeyStr, variantId: null, memberPathHint: null, out GameConfigSourceLocation itemLocation))
                    {
                        buildLog.WithLocation(itemLocation).Error(message);
                        return;
                    }
                }
            }

            // Wasn't able to find proper source mapping for where this happened so report without the information
            buildLog.Error($"{message}. Unable to resolve source location where this occurred.");
        }

        static (string ConfigKeyStr, Type ContainingObjectType) ResolveContainingObjectInfo(GameConfigEntryInfo containingEntryInfo, IGameConfigData configItemMaybe)
        {
            // \todo Using containingEntryInfo.Name as configKeyStr for KeyValueObjects, need to clean it up

            if (configItemMaybe != null)
            {
                return (
                    ConfigKeyStr: Util.ObjectToStringInvariant(GameConfigItemHelper.GetItemConfigKey(configItemMaybe)),
                    ContainingObjectType: configItemMaybe.GetType()
                    );
            }
            else
            {
                return (
                    ConfigKeyStr: containingEntryInfo.Name,
                    ContainingObjectType: containingEntryInfo.MemberInfo.GetDataMemberType()
                    );
            }
        }

        /// <summary>
        /// Get a mapping "experimentVariantKey to (entryName to entryPatch)".
        /// Should be called after all the config entries have been built.
        /// </summary>
        /// <param name="parentVariantPatches">
        /// Patches extracted from the parent config, if any (null otherwise).
        /// A parent patch (if available) will be used for an entry if that entry was not built by this current build.
        /// </param>
        Dictionary<ExperimentVariantPair, Dictionary<string, GameConfigEntryPatch>> GetVariantEntryPatches(Dictionary<ExperimentVariantPair, GameConfigPatch> parentVariantPatches)
        {
            // _entryBuildResults contains nested lookups of shape  entryName -> (experimentVariantKey -> entryPatch) .
            // Here we translate that into entryPatchesPerVariant, of shape  experimentVariantKey -> (entryName -> entryPatch) .

            Dictionary<ExperimentVariantPair, Dictionary<string, GameConfigEntryPatch>> entryPatchesPerVariant = new Dictionary<ExperimentVariantPair, Dictionary<string, GameConfigEntryPatch>>();

            void AddOrReplaceEntryPatch(string entryName, ExperimentVariantPair evKey, GameConfigEntryPatch entryPatch)
            {
                if (!entryPatchesPerVariant.TryGetValue(evKey, out Dictionary<string, GameConfigEntryPatch> entryPatchesOfThisVariant))
                {
                    entryPatchesOfThisVariant = new Dictionary<string, GameConfigEntryPatch>();
                    entryPatchesPerVariant.Add(evKey, entryPatchesOfThisVariant);
                }

                entryPatchesOfThisVariant[entryName] = entryPatch;
            }

            foreach (GameConfigEntryInfo entryInfo in _configTypeInfo.Entries.Values)
            {
                // If this entry was built now, use the patches from that build result.
                // Otherwise, use the patches from parent (if any).
                if (_entryBuildResults.ContainsKey(entryInfo.Name))
                {
                    Dictionary<ExperimentVariantPair, GameConfigEntryPatch> entryVariantPatches = _entryBuildResults[entryInfo.Name].VariantPatches;

                    foreach ((ExperimentVariantPair evKey, GameConfigEntryPatch entryPatch) in entryVariantPatches)
                        AddOrReplaceEntryPatch(entryInfo.Name, evKey, entryPatch);
                }
                else if (parentVariantPatches != null)
                {
                    foreach ((ExperimentVariantPair evKey, GameConfigPatch patch) in parentVariantPatches)
                    {
                        if (patch.TryGetSpecifiedEntryPatch(entryInfo.Name, out GameConfigEntryPatch entryPatch))
                            AddOrReplaceEntryPatch(entryInfo.Name, evKey, entryPatch);
                    }
                }
            }

            return entryPatchesPerVariant;
        }

        class GameConfigEntryBuildResult
        {
            public IGameConfigEntry Baseline;
            public Dictionary<ExperimentVariantPair, GameConfigEntryPatch> VariantPatches;

            public GameConfigEntryBuildResult(IGameConfigEntry baseline, Dictionary<ExperimentVariantPair, GameConfigEntryPatch> variantPatches)
            {
                Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
                VariantPatches = variantPatches ?? throw new ArgumentNullException(nameof(variantPatches));
            }
        }

        public class BuilderApi<TDerivedConfig> : IGameConfigBuilder<TDerivedConfig> where TDerivedConfig : TConfig
        {
            GameConfigBuildState<TConfig> _builder;

            public BuilderApi(GameConfigBuildState<TConfig> builder)
            {
                MetaDebug.Assert(typeof(TDerivedConfig).IsAssignableFrom(builder.GameConfigType), $"GameConfig type mismatch: {builder.GameConfigType} does not inherit from {typeof(TDerivedConfig)}");
                _builder = builder;
            }

            public TDerivedConfig RawOutput => (TDerivedConfig)_builder.RawOutput;

            public Type                          ConfigType     => _builder.RawOutput.GetType();
            public GameConfigBuildLog            BuildLog       => _builder._buildLog;
            public List<GameConfigSourceMapping> SourceMappings => _builder.SourceMappings;

            public void AddCustomArchiveEntry(ConfigArchiveEntry entry)
            {
                _builder._customArchiveEntries.Add(entry);
            }

            public void AssignEmpty(string configEntryName)
            {
                if (!_builder._configTypeInfo.Entries.TryGetValue(configEntryName, out GameConfigEntryInfo entryInfo))
                {
                    throw new InvalidOperationException($"Config entry '{configEntryName}' is not a registered GameConfigEntry");
                }
                AssignBaselineConfigEntryBuildResult(configEntryName, (IGameConfigEntry)Activator.CreateInstance(entryInfo.MemberInfo.GetDataMemberType(), nonPublic: true), null);
            }

            public void BuildGameConfigEntry(string configEntryName, SpreadsheetContent sourceSheet)
            {
                if (!_builder._configTypeInfo.Entries.TryGetValue(configEntryName, out GameConfigEntryInfo entryInfo))
                {
                    throw new InvalidOperationException($"Config entry '{configEntryName}' is not a registered GameConfigEntry");
                }
                Type t = entryInfo.MemberInfo.GetDataMemberType();
                MethodInfo method;
                if (t.IsGameConfigLibrary())
                {
                    if (entryInfo.BuildSourceType != null)
                    {
                        Type concreteSourceType = entryInfo.BuildSourceType;
                        // Special handling for abstract source item types
                        if (concreteSourceType.IsAbstract)
                            concreteSourceType = IntegrationRegistry.GetSingleIntegrationType(entryInfo.BuildSourceType);

                        MethodInfo prototype = GetType().GetMethod(nameof(BuildGameConfigLibraryWithSourceTransform));
                        method = prototype.GetGenericMethodDefinition().MakeGenericMethod(new Type[] { t.GetGenericArguments()[0], t.GetGenericArguments()[1], concreteSourceType });
                    }
                    else
                    {
                        MethodInfo prototype = GetType().GetMethod(nameof(BuildGameConfigLibrary));
                        method = prototype.GetGenericMethodDefinition().MakeGenericMethod(t.GetGenericArguments());
                    }
                }
                else if (t.IsDerivedFrom<GameConfigKeyValue>())
                {
                    MethodInfo prototype = GetType().GetMethod(nameof(BuildGameConfigKeyValueStructure));
                    method = prototype.GetGenericMethodDefinition().MakeGenericMethod(t);
                }
                else
                {
                    throw new InvalidOperationException($"Config entry '{configEntryName}' is not a GameConfigLibrary or GameConfigKeyValue");
                }

                method.InvokeWithoutWrappingError(this, new object[] { configEntryName, sourceSheet });
            }

            public void BuildGameConfigLibrary<TKey, TInfo>(string configEntryName, SpreadsheetContent sheet)
                where TInfo : class, IGameConfigData<TKey>, new()
            {
                // Parse items
                List<VariantConfigItem<TKey, TInfo>> items = ParseLibrarySheetItems<TKey, TInfo>(configEntryName, sheet);
                // \todo Detect configKey duplicates here? Seems like they can sneak past & cause exceptions

                // If errors occurred, bail out
                if (BuildLog.HasErrors())
                    return;

                // Register all parsed items into the mapping table so we can later resolve the source & location where the item was defined.
                GameConfigSourceMapping sourceMapping = GameConfigSourceMapping.ForGameConfigLibrary(sheet, items);

                // Create entry build result (entry baseline and patches)
                AssignLibraryBuildResult(configEntryName, items, sourceMapping);
            }

            public void BuildGameConfigLibraryWithSourceTransform<TKey, TInfo, TSourceItem>(string configEntryName, SpreadsheetContent sheet)
                where TInfo : class, IGameConfigData<TKey>, new()
                where TSourceItem : IGameConfigSourceItem<TKey, TInfo>, new()
            {
                BuildGameConfigLibraryWithTransform<TKey, TInfo, TSourceItem>(configEntryName, sheet, (GameConfigBuildLog buildLog, TSourceItem srcItem) => srcItem.ToConfigData(buildLog));
            }

            public void BuildGameConfigLibraryWithTransform<TKey, TInfo, TSourceItem>(string configEntryName, SpreadsheetContent sheet, Func<GameConfigBuildLog, TSourceItem, TInfo> transform)
                where TInfo : class, IGameConfigData<TKey>, new()
                where TSourceItem : IGameConfigSourceItem<TKey, TInfo>, new()
            {
                // Parse source items
                List<VariantConfigItem<TKey, TSourceItem>> sourceItems = ParseLibrarySheetItems<TKey, TSourceItem>(configEntryName, sheet);

                // If errors occurred, bail out
                if (BuildLog.HasErrors())
                    return;

                // Transform source items to final config items
                List<VariantConfigItem<TKey, TInfo>> transformedItems = new List<VariantConfigItem<TKey, TInfo>>(sourceItems.Count);
                foreach (VariantConfigItem<TKey, TSourceItem> srcItem in sourceItems)
                {
                    // \todo Include item identity here? Would really benefit from having the correct ConfigKey being parsed at this stage
                    GameConfigBuildLog itemBuildLog = BuildLog.WithLocation(srcItem.SourceLocation);
                    try
                    {
                        TInfo transformedItem = transform(itemBuildLog, srcItem.Item);
                        transformedItems.Add(new VariantConfigItem<TKey, TInfo>(transformedItem, srcItem.VariantIdMaybe, srcItem.Aliases, srcItem.SourceLocation));
                    }
                    catch (Exception ex)
                    {
                        itemBuildLog.Error($"Failed to transform config item '{srcItem.Item.ConfigKey}' of type '{srcItem.Item.GetType().ToGenericTypeString()}'", ex);
                    }
                }

                // If errors occurred, bail out
                if (BuildLog.HasErrors())
                    return;

                // Debug print
                DebugPrintLibraryItems(transformedItems, $"Items transformed from {sheet.Name}");

                // Build mapping table from output items back into the source locations to allow validation messages to be reported with original items
                // \note We only register the mappins for the transformed items (as those are the ones getting validated).
                GameConfigSourceMapping sourceMapping = GameConfigSourceMapping.ForGameConfigLibrary(sheet, transformedItems);

                // Create entry build result (entry baseline and patches)
                AssignLibraryBuildResult(configEntryName, transformedItems, sourceMapping);
            }

            /// <summary>
            /// Creates a baseline library and patches from the given items.
            /// Also assigns the baseline to the config entry member.
            /// </summary>
            public void AssignLibraryBuildResult<TKey, TInfo>(string configEntryName, List<VariantConfigItem<TKey, TInfo>> items, GameConfigSourceMapping sourceMapping)
                where TInfo : class, IGameConfigData<TKey>, new()
            {
                if (!_builder._configTypeInfo.Entries.TryGetValue(configEntryName, out GameConfigEntryInfo configEntryInfo))
                    throw new InvalidOperationException($"{_builder.GameConfigType} contains no entry named {configEntryName}");

                // Group the items by variant id (as well as baseline, which has null variant id)
                IEnumerable<TInfo> baselineItems = items.Where(item => item.VariantIdMaybe == null).Select(item => item.Item);
                IEnumerable<IGrouping<string, TInfo>> itemsPerVariant = items.Where(item => item.VariantIdMaybe != null).GroupBy(item => item.VariantIdMaybe, item => item.Item);

                // Construct baseline library from the baseline items
                GameConfigLibrary<TKey, TInfo> baselineLibrary = GameConfigLibrary<TKey, TInfo>.CreateSolo(
                    GameConfigUtil.ConvertToMetaDictionary<TKey, TInfo>(baselineItems));

                // Register aliases
                foreach (VariantConfigItem<TKey, TInfo> item in items.Where(item => item.Aliases != null))
                {
                    foreach (TKey alias in item.Aliases)
                    {
                        baselineLibrary.RegisterAlias(item.Item.ConfigKey, alias);
                    }
                }

                // Validate that, in the source items, variant-appended items appear after all other kinds of items.
                // \todo [nuutti] Support variant patches to insert new items at arbitrary positions, not just appending?
                {
                    // Iterate the items backwards, keeping track of the non-appended item that follows (in the forwards order) the current item.
                    // \todo This produces the errors in backwards order in case there are more than 1. Not incorrect but a bit weird.

                    VariantConfigItem<TKey, TInfo>? followingNonAppendedItem = null;

                    for (int itemNdx = items.Count-1; itemNdx >= 0; itemNdx--)
                    {
                        VariantConfigItem<TKey, TInfo> item = items[itemNdx];

                        bool isAppended = item.VariantIdMaybe != null && !baselineLibrary.ContainsKey(item.Item.ConfigKey);

                        if (isAppended)
                        {
                            if (followingNonAppendedItem.HasValue)
                            {
                                TKey appendedKey = item.Item.ConfigKey;
                                TKey nonAppendedKey = followingNonAppendedItem.Value.Item.ConfigKey;

                                BuildLog
                                    .WithLocation(item.SourceLocation)
                                    .WithVariantId(item.VariantIdMaybe)
                                    .Error($"Newly-added config variant items must appear after all other config items in the sheet. Here, newly-added {appendedKey} appears before {nonAppendedKey}.");
                            }
                        }
                        else
                            followingNonAppendedItem = item;
                    }
                }

                // Construct library patches from the variants
                Dictionary<ExperimentVariantPair, GameConfigLibraryPatch<TKey, TInfo>> variantPatches = itemsPerVariant.ToDictionary(
                    keySelector: variantItems => ParseExperimentVariantIdFromSheet(variantItems.Key),
                    elementSelector: variantItems =>
                    {
                        try
                        {
                            IEnumerable<TInfo> appendedItems = variantItems.Where(item => !baselineLibrary.ContainsKey(item.ConfigKey));
                            IEnumerable<TInfo> replacedItems = variantItems.Where(item => baselineLibrary.ContainsKey(item.ConfigKey));
                            return new GameConfigLibraryPatch<TKey, TInfo>(
                                replacedItems: replacedItems,
                                appendedItems: appendedItems);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Error in variant {variantItems.Key}: {ex.Message}", ex);
                        }
                    });

                // Assign
                AssignConfigEntryBuildResult(configEntryName, baselineLibrary, variantPatches, sourceMapping);
            }

            public void BuildGameConfigKeyValueStructure<TStructure>(string configEntryName, SpreadsheetContent sheet)
                where TStructure : GameConfigKeyValue, new()
            {
                // Parse members
                List<VariantConfigStructureMember> members = ParseStructureSheetMembers<TStructure>(configEntryName, sheet);

                // If errors occurred, bail out
                if (BuildLog.HasErrors())
                    return;

                // Construct mapping from parsed output back to source locations
                GameConfigSourceMapping sourceMapping = GameConfigSourceMapping.ForKeyValueObject(sheet, members);

                // Create entry build result (entry baseline and patches)
                AssignConfigKeyValueStructureBuildResult<TStructure>(configEntryName, members.ToList(), sourceMapping);
            }

            public void AssignConfigKeyValueStructureBuildResult<TStructure>(string configEntryName, List<VariantConfigStructureMember> members, GameConfigSourceMapping sourceMapping)
                where TStructure : GameConfigKeyValue, new()
            {
                // Group the members by variant id (as well as baseline, which has null variant id)
                IEnumerable<ConfigStructureMember>                    baselineMembers   = members.Where(member => member.VariantIdMaybe == null).Select(member => member.Member);
                IEnumerable<IGrouping<string, ConfigStructureMember>> membersPerVariant = members.Where(member => member.VariantIdMaybe != null).GroupBy(member => member.VariantIdMaybe, member => member.Member);

                TStructure baselineStructure = null;

                if (!GameConfigOutputItemParser.ShouldUseParameterizedConstructor(typeof(TStructure)))
                {
                    // Construct baseline structure from the baseline members
                    baselineStructure = new TStructure();
                    {
                        HashSet<string> specifiedMembers = new HashSet<string>();
                        foreach (ConfigStructureMember member in baselineMembers)
                        {
                            if (!specifiedMembers.Add(member.MemberName))
                                throw new InvalidOperationException($"Member {typeof(TStructure).Name}.{member.MemberName} specified multiple times");

                            member.MemberInfo.GetDataMemberSetValueOnDeclaringType()(baselineStructure, member.MemberValue);
                        }
                    }
                }
                else
                {
                    sourceMapping.TryFindItemSource(configEntryName, null, null, out GameConfigSourceLocation location);
                    if (GameConfigOutputItemParser.TryFindOrCreateCachedConstructorInfo(
                         typeof(TStructure),
                         location,
                         BuildLog,
                         out (ConstructorInfo constructorInfo, MetaDictionary<string, ParameterInfo>
                         parameterInfos) ctorInfo))
                    {
                        baselineStructure = (TStructure)GameConfigOutputItemParser.CreateObjectUsingParameterizedConstructor(
                            typeof(TStructure),
                            location,
                            BuildLog,
                            ctorInfo,
                            baselineMembers.ToDictionary(
                                x => x.MemberName.ToLowerInvariant(),
                                x => x.MemberValue));
                    }
                }

                // Construct structure patches from the variants
                Dictionary<ExperimentVariantPair, GameConfigStructurePatch<TStructure>> variantPatches = membersPerVariant.ToDictionary(
                    keySelector: variantMembers => ParseExperimentVariantIdFromSheet(variantMembers.Key),
                    elementSelector: variantMembers =>
                    {
                        try
                        {
                            return new GameConfigStructurePatch<TStructure>(
                                replacedMembersByName: variantMembers.ToMetaDictionary(
                                    member => member.MemberName,
                                    member => member.MemberValue));
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Error in variant {variantMembers.Key}: {ex.Message}", ex);
                        }
                    });

                // Assign
                AssignConfigEntryBuildResult(configEntryName, baselineStructure, variantPatches, sourceMapping);
            }

            public void AssignBaselineConfigEntryBuildResult(string configEntryName, IGameConfigEntry baselineEntry, GameConfigSourceMapping sourceMapping)
            {
                if (!_builder._configTypeInfo.Entries.TryGetValue(configEntryName, out GameConfigEntryInfo configEntryInfo))
                    throw new InvalidOperationException($"{_builder.GameConfigType} contains no entry named {configEntryName}");

                // Guard against the same entry being built multiple times (it's probably a mistake)
                if (!_builder._builtEntries.Add(configEntryName))
                    throw new InvalidOperationException($"{configEntryName} config is being built, but it was already built during this config build. This is probably a mistake.");

                if (sourceMapping != null)
                    _builder.SourceMappings.Add(sourceMapping);

                // Assign to config entry member
                // \todo: Type check
                configEntryInfo.MemberInfo.GetDataMemberSetValueOnDeclaringType()(_builder._output, baselineEntry);
            }

            public void AssignConfigEntryBuildResult<TConfigEntryPatch>(string configEntryName, IGameConfigEntry baselineEntry, Dictionary<ExperimentVariantPair, TConfigEntryPatch> variantEntryPatches, GameConfigSourceMapping sourceMapping)
                where TConfigEntryPatch : GameConfigEntryPatch
            {
                AssignBaselineConfigEntryBuildResult(configEntryName, baselineEntry, sourceMapping);

                // Create build result
                _builder._entryBuildResults.Add(configEntryName, new GameConfigEntryBuildResult(baselineEntry, variantEntryPatches.ToDictionary(kv => kv.Key, kv => (GameConfigEntryPatch)kv.Value)));
            }

            /// <summary>
            /// Parse experimentId-variantId pair from source sheet syntax: ExperimentId/VariantId
            /// </summary>
            ExperimentVariantPair ParseExperimentVariantIdFromSheet(string variantIdStr)
            {
                string[] parts = variantIdStr.Split('/');
                if (parts.Length != 2)
                    throw new ArgumentException($"Invalid experiment variant id '{variantIdStr}' . Expected two slash-separated parts.");

                return new ExperimentVariantPair(
                    PlayerExperimentId.FromString(parts[0]),
                    ExperimentVariantId.FromString(parts[1]));
            }

            public List<VariantConfigItem<TKey, TInfo>> ParseLibrarySheetItems<TKey, TInfo>(string configEntryName, SpreadsheetContent sheet) where TInfo : IHasGameConfigKey<TKey>, new()
            {
                // Scope build log to input sheet
                GameConfigBuildLog buildLog = BuildLog.WithSource(sheet.SourceInfo);

                GameConfigSyntaxAdapterAttribute[] gameConfigAdapterAttribs = _builder._configTypeInfo.GameConfigType.GetCustomAttributes<GameConfigSyntaxAdapterAttribute>().ToArray();

                GameConfigEntryInfo entryInfo = _builder._configTypeInfo.Entries[configEntryName];
                GameConfigSyntaxAdapterAttribute[] entryAdapterAttribs = entryInfo.MemberInfo.GetCustomAttributes<GameConfigSyntaxAdapterAttribute>().ToArray();

                List<VariantConfigItem<TKey, TInfo>> items;

                try
                {
                    GameConfigSyntaxAdapterAttribute[] syntaxAdapterAttribs = gameConfigAdapterAttribs.Concat(entryAdapterAttribs).ToArray();
                    UnknownConfigMemberHandling unknownMemberHandling = _builder._build.Integration.UnknownConfigItemMemberHandling;

                    items = GameConfigParsePipeline.ProcessSpreadsheetLibrary<TKey, TInfo>(
                        buildLog,
                        new GameConfigParseLibraryPipelineConfig(syntaxAdapterAttribs, unknownMemberHandling),
                        sheet)
                        ?.ToList();
                }
                catch (Exception ex)
                {
                    buildLog.Error($"Internal error while parsing game config library", ex);
                    items = null;
                }

                // Debug print
                if (items != null)
                    DebugPrintLibraryItems(items, $"Items in {sheet.Name}");

                return items;
            }

            public List<VariantConfigStructureMember> ParseStructureSheetMembers<TStructure>(string configEntryName, SpreadsheetContent sheet)
                where TStructure : GameConfigKeyValue, new()
            {
                // Scope build log to input sheet
                GameConfigBuildLog buildLog = BuildLog.WithSource(sheet.SourceInfo);

                GameConfigSyntaxAdapterAttribute[] gameConfigAdapterAttribs = _builder._configTypeInfo.GameConfigType.GetCustomAttributes<GameConfigSyntaxAdapterAttribute>().ToArray();

                GameConfigEntryInfo entryInfo = _builder._configTypeInfo.Entries[configEntryName];
                GameConfigSyntaxAdapterAttribute[] entryAdapterAttribs = entryInfo.MemberInfo.GetCustomAttributes<GameConfigSyntaxAdapterAttribute>().ToArray();

                List<VariantConfigStructureMember> members;
                try
                {
                    GameConfigSyntaxAdapterAttribute[] syntaxAdapterAttribs = gameConfigAdapterAttribs.Concat(entryAdapterAttribs).ToArray();
                    UnknownConfigMemberHandling unknownMemberHandling = _builder._build.Integration.UnknownConfigItemMemberHandling;

                    members = GameConfigParsePipeline.ProcessSpreadsheetKeyValue<TStructure>(
                        buildLog,
                        new GameConfigParseKeyValuePipelineConfig(syntaxAdapterAttribs, unknownMemberHandling),
                        sheet)
                        ?.ToList();
                }
                catch (Exception ex)
                {
                    buildLog.Error($"Internal error while parsing game config object", ex);
                    members = null;
                }

                // Debug print
                if (members != null)
                    DebugPrintStructureMembers(members, $"Members in {sheet.Name}");

                return members;
            }

            void DebugPrintLibraryItems<TRef, TItem>(IEnumerable<VariantConfigItem<TRef, TItem>> items, string title) where TItem : IHasGameConfigKey<TRef>
            {
                if (!_builder._build.DebugOptions.EnableDebugPrints)
                    return;
                DebugLog.Info("{Title}:\n{Items}\n", title, string.Join("\n", items.Select((item, ndx) =>
                {
                    string variantStr = item.VariantIdMaybe != null ? $" ({item.VariantIdMaybe})" : "";
                    return Invariant($"  #{ndx}{variantStr}: {PrettyPrint.Compact(item.Item)}");
                })));
            }

            void DebugPrintStructureMembers(IEnumerable<VariantConfigStructureMember> members, string title)
            {
                if (!_builder._build.DebugOptions.EnableDebugPrints)
                    return;
                DebugLog.Info("{Title}:\n{Members}\n", title, string.Join("\n", members.Select((member, ndx) =>
                {
                    string variantStr = member.VariantIdMaybe != null ? $" ({member.VariantIdMaybe})" : "";
                    return Invariant($"  #{ndx}{variantStr}: {member.Member.MemberName} = {PrettyPrint.Compact(member.Member.MemberValue)}");
                })));
            }
        }
    }

    struct GameConfigBuildOutput
    {
        public readonly ConfigArchive Baseline;
        public readonly Dictionary<ExperimentVariantPair, GameConfigPatchEnvelope>  VariantPatches;

        public GameConfigBuildOutput(ConfigArchive baseline, Dictionary<ExperimentVariantPair, GameConfigPatchEnvelope> variantPatches)
        {
            Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
            VariantPatches = variantPatches ?? throw new ArgumentNullException(nameof(variantPatches));
        }
    }

    readonly struct BuildSourceFetcherAndMetadata
    {
        public readonly IGameConfigSourceFetcher      Fetcher;
        public readonly GameConfigBuildSourceMetadata Metadata;

        public BuildSourceFetcherAndMetadata(IGameConfigSourceFetcher fetcher, GameConfigBuildSourceMetadata metadata)
        {
            Fetcher  = fetcher;
            Metadata = metadata;
        }
    }

    public abstract class GameConfigBuild : IMetaIntegrationConstructible<GameConfigBuild>, IGameConfigBuild
    {
        public    GameConfigBuildDebugOptions      DebugOptions          { get; set; }
        public    IGameConfigSourceFetcherProvider SourceFetcherProvider { get; set; }
        public    GameConfigBuildIntegration       Integration           { get; set; } = IntegrationRegistry.Get<GameConfigBuildIntegration>();
        protected Type                             SharedConfigType      { get; set; } = GameConfigRepository.Instance.SharedGameConfigType;
        protected Type                             ServerConfigType      { get; set; } = GameConfigRepository.Instance.ServerGameConfigType;
        protected GameConfigBuildParameters        BuildParametersBase   { get; private set; }

        readonly Dictionary<GameConfigBuildSource, BuildSourceFetcherAndMetadata> _sources = new Dictionary<GameConfigBuildSource, BuildSourceFetcherAndMetadata>();
        protected readonly MetaDictionary<string, GameConfigBuildSource> _sourcesByMemberName = new MetaDictionary<string, GameConfigBuildSource>();
        protected IGameConfigSourceFetcher SourceFetcher(GameConfigBuildSource source) => _sources[source].Fetcher;

        async Task ConfigureSourceFetchersAsync(CancellationToken ct)
        {
            // Iterate all GameConfigBuildSource members of build params and configure a source fetcher for each unique source.
            foreach (MemberInfo sourceMember in Integration.GetBuildSourcesInBuildParameters(BuildParametersBase.GetType()))
            {
                GameConfigBuildSource source = (GameConfigBuildSource)sourceMember.GetDataMemberGetValueOnDeclaringType().Invoke(BuildParametersBase);
                if (source != null)
                {
                    _sourcesByMemberName[sourceMember.Name] = source;
                    if (!_sources.ContainsKey(source))
                    {
                        IGameConfigSourceFetcher      fetcher  = await ConfigureFetcherForBuildSourceAsync(source, ct);
                        GameConfigBuildSourceMetadata metadata = await fetcher.GetMetadataAsync(ct);
                        _sources[source] = new BuildSourceFetcherAndMetadata(fetcher, metadata);
                    }
                }
            }
        }

        MetaDictionary<string, GameConfigBuildSourceMetadata> GetBuildSourcesMetadata()
        {
            MetaDictionary<string, GameConfigBuildSourceMetadata> retVal = new MetaDictionary<string, GameConfigBuildSourceMetadata>();
            foreach (MetaDictionary<string, GameConfigBuildSource>.KeyValue source in _sourcesByMemberName)
            {
                if (_sources.TryGetValue(source.Value, out var sourceFetcherAndMetaData) && sourceFetcherAndMetaData.Metadata != null)
                    retVal[source.Key] = sourceFetcherAndMetaData.Metadata;
            }
            return retVal;
        }

        protected virtual Task<IGameConfigSourceFetcher> ConfigureFetcherForBuildSourceAsync(GameConfigBuildSource source, CancellationToken ct)
        {
            return SourceFetcherProvider.GetFetcherForBuildSourceAsync(source, ct);
        }

        ConfigArchive MaybeExtractParentArchive(ConfigArchive parentFullArchive, string entryName)
        {
            return parentFullArchive != null ? ConfigArchive.FromBytes(parentFullArchive.GetEntryByName(entryName).Bytes) : null;
        }

        GameConfigBuildState<ISharedGameConfig> CreateSharedGameConfigBuilder(GameConfigBuildLog buildLog, ConfigArchive parentArchive)
        {
            ConfigArchive sharedParent = MaybeExtractParentArchive(parentArchive, "Shared.mpa");
            ISharedGameConfig output;
            if (sharedParent != null)
                output = GameConfigUtil.ImportSharedConfig(sharedParent, isBuildingConfigs: true, isConfigBuildParent: true);
            else
                output = (ISharedGameConfig)GameConfigFactory.Instance.CreateGameConfig(SharedConfigType);
            return new GameConfigBuildState<ISharedGameConfig>(this, sharedParent, buildLog, output);
        }

        GameConfigBuildState<IServerGameConfig> CreateServerGameConfigBuilder(GameConfigBuildLog buildLog, ConfigArchive parentArchive)
        {
            ConfigArchive serverParent = MaybeExtractParentArchive(parentArchive, "Server.mpa");
            IServerGameConfig output;
            if (serverParent != null)
                output = GameConfigUtil.ImportServerConfig(serverParent, isBuildingConfigs: true, isConfigBuildParent: true);
            else
                output = (IServerGameConfig)GameConfigFactory.Instance.CreateGameConfig(ServerConfigType);
            return new GameConfigBuildState<IServerGameConfig>(this, serverParent, buildLog, output);
        }

        void ValidateThatUsedVariantsAreConfigured(GameConfigBuildLog buildLog, GameConfigBuildOutput sharedOutput, GameConfigBuildOutput serverOutput)
        {
            IServerGameConfig server = GameConfigUtil.ImportServerConfig(serverOutput.Baseline, isBuildingConfigs: true);

            HashSet<ExperimentVariantPair> configuredExperimentVariantKeys =
                new HashSet<ExperimentVariantPair>(
                    server.PlayerExperiments.Values
                    .SelectMany(experimentInfo =>
                        experimentInfo.Variants.Values.Select(variant => new ExperimentVariantPair(experimentInfo.ExperimentId, variant.Id))));

            foreach ((ExperimentVariantPair key, GameConfigPatchEnvelope patch) in sharedOutput.VariantPatches)
            {
                if (!configuredExperimentVariantKeys.Contains(key))
                    buildLog.Error($"Shared Game Config entry {patch.EnumeratePatchedEntryNames().First()} has patch for variant '{key.ExperimentId}/{key.VariantId}' which hasn't been configured in the {nameof(IServerGameConfig.PlayerExperiments)} configuration");
            }

            foreach ((ExperimentVariantPair key, GameConfigPatchEnvelope patch) in serverOutput.VariantPatches)
            {
                if (!configuredExperimentVariantKeys.Contains(key))
                    buildLog.Error($"Server Game Config entry {patch.EnumeratePatchedEntryNames().First()} has patch for variant '{key.ExperimentId}/{key.VariantId}' which hasn't been configured in the {nameof(IServerGameConfig.PlayerExperiments)} configuration");
            }
        }

        List<GameConfigValidationResult> ValidateFullArchive(ConfigArchive fullArchive, List<GameConfigSourceMapping> sharedConfigSourceMapping, List<GameConfigSourceMapping> serverConfigSourceMapping, GameConfigBuildLog buildLog)
        {
            // Validate baseline, by loading it from the archive.
            FullGameConfigImportResources importResources = FullGameConfigImportResources.CreateWithAllConfiguredPatches(
                fullArchive,
                GameConfigRuntimeStorageMode.Deduplicating,
                initializeBaselineAndSingleVariantSpecializations: false);

            importResources.Shared.InitializeDeduplicationBaseline();
            importResources.Server.InitializeDeduplicationBaseline();

            DebugDumpConfigsContainer debugDumpConfigs = default;
            ObjectGraphDump.DumpResult debugDumpBefore = default;
            if (DebugOptions.EnableDebugDumpCheck)
            {
                debugDumpConfigs = new DebugDumpConfigsContainer(importResources);
                debugDumpBefore = DebugDumpConfigs(debugDumpConfigs);
            }

            importResources.Shared.InitializeAllDeduplicationSingleVariantSpecializations();
            importResources.Server.InitializeAllDeduplicationSingleVariantSpecializations();

            FullGameConfig baseline = FullGameConfig.CreateSpecialization(importResources, new OrderedSet<ExperimentVariantPair>(), isBuildingConfigs: true);

            GameConfigValidationResult baselineResult = new GameConfigValidationResult(GameConfigSourceMapping.BaselineVariantKey);

            baselineResult.ScopeTo(sharedConfigSourceMapping);
            baseline.SharedConfig.BuildTimeValidate(baselineResult);

            baselineResult.ScopeTo(serverConfigSourceMapping);
            baseline.ServerConfig.BuildTimeValidate(baselineResult);

            List<GameConfigValidationResult> validationResults = new List<GameConfigValidationResult>();
            validationResults.Add(baselineResult);

            if (!DebugOptions.OnlyValidateBaseline)
            {
                // Validate each configured variant, by loading full config from the archive patched by each variant's patch.
                // \note This does *not* validate all combinations of variants; this validates each variant individually.
                //       Ignoring any custom arbitrary config validations, this should be enough.

                List<ExperimentVariantPair> patchIds =
                    baseline.ServerConfig.PlayerExperiments.Values
                        .SelectMany(
                            experiment =>
                                experiment.Variants.Keys
                                    .Select(variantId => new ExperimentVariantPair(experiment.ExperimentId, variantId)))
                        .ToList();

                List<GameConfigValidationResult> variantResults =
                    patchIds
                        .Select(patchId => new GameConfigValidationResult(patchId.ExperimentId.Value + "/" + patchId.VariantId.Value))
                        .ToList();

                Parallel.For(
                    0,
                    patchIds.Count,
                    index =>
                    {
                        ExperimentVariantPair      patchId       = patchIds[index];
                        GameConfigValidationResult variantResult = variantResults[index];

                        // Skip empty patches, nothing to validate compared to baseline
                        if (importResources.Shared.Patches[patchId].IsEmpty
                            && importResources.Server.Patches[patchId].IsEmpty)
                        {
                            return;
                        }

                        FullGameConfig variantConfig = FullGameConfig.CreateSpecialization(
                            importResources,
                            new OrderedSet<ExperimentVariantPair> {patchId},
                            omitPatchesInServerConfigExperiments: true,
                            isBuildingConfigs: true);

                        variantResult.ScopeTo(sharedConfigSourceMapping);
                        variantConfig.SharedConfig.BuildTimeValidate(variantResult);

                        variantResult.ScopeTo(serverConfigSourceMapping);
                        variantConfig.ServerConfig.BuildTimeValidate(variantResult);

                        variantResult.DisposeScope();
                    });

                validationResults = validationResults.Concat(variantResults).ToList();
            }
            else
            {
                buildLog.Warning("Only baseline config has been validated");
            }

            if (DebugOptions.EnableDebugDumpCheck)
            {
                ObjectGraphDump.DumpResult debugDumpAfter = DebugDumpConfigs(debugDumpConfigs, referenceDump: debugDumpBefore);
                CompareConfigDebugDumps(debugDumpBefore, debugDumpAfter);
            }

            return validationResults;
        }

        class DebugDumpConfigsContainer
        {
            public ISharedGameConfig SharedGameConfig;
            public IServerGameConfig ServerGameConfig;

            public DebugDumpConfigsContainer(FullGameConfigImportResources importResources)
            {
                SharedGameConfig = (ISharedGameConfig)importResources.Shared.DeduplicationBaseline;
                ServerGameConfig = (IServerGameConfig)importResources.Server.DeduplicationBaseline;
            }
        }

        ObjectGraphDump.FieldInfoCache _debugDumpFieldInfoCache = new ObjectGraphDump.FieldInfoCache();

        ObjectGraphDump.DumpResult DebugDumpConfigs(DebugDumpConfigsContainer container, ObjectGraphDump.DumpResult referenceDump = null)
        {
            int totalConfigItemCount = CountConfigItems(container.SharedGameConfig)
                + CountConfigItems(container.ServerGameConfig);

            // Add some overhead for object count limit.
            // Even a 0-item config involves some objects.
            const int objectCountOverhead = 2000;

            int objectCollectionCapacity;
            if (referenceDump != null)
            {
                // Optimal capacity for the common case (i.e. identical dumps).
                objectCollectionCapacity = referenceDump.Objects.Count;
            }
            else
                objectCollectionCapacity = 10 * totalConfigItemCount;

            ObjectGraphDump.DumpOptions dumpOptions = new ObjectGraphDump.DumpOptions(
                objectCountSafetyLimit: 1000 * totalConfigItemCount + objectCountOverhead,
                objectCollectionInitialCapacity: objectCollectionCapacity);

            dumpOptions.FieldInfoCache = _debugDumpFieldInfoCache;

            if (DebugOptions.EnableDebugPrints)
                DebugLog.Debug($"Debug-dumping baseline config (using {nameof(dumpOptions.ObjectCountSafetyLimit)} = {{ObjectCountSafetyLimit}})...", dumpOptions.ObjectCountSafetyLimit);
            Stopwatch sw = Stopwatch.StartNew();

            ObjectGraphDump.DumpResult dumpResult = ObjectGraphDump.Dump(container, dumpOptions);

            if (DebugOptions.EnableDebugPrints)
                DebugLog.Debug("... dumping took {DumpMilliseconds} ms, dump contains {NumObjectsInDump} objects", sw.ElapsedMilliseconds, dumpResult.Objects.Count);

            return dumpResult;
        }

        void CompareConfigDebugDumps(ObjectGraphDump.DumpResult dumpBefore, ObjectGraphDump.DumpResult dumpAfter)
        {
            // Happy path: strict comparison reports no differences, all is OK.
            // If it does, then perform a more loose comparison
            // (by not requiring object identities to equal between the dumps),
            // and prefer to use that looser-comparison result if it still produces differences,
            // because they're likely to have more information (i.e. actual content differences,
            // not just identity differences).

            ObjectGraphDump.ComparisonResult comparisonResult = ObjectGraphDump.CompareDumpResults(dumpBefore, dumpAfter);
            if (!comparisonResult.DumpsAreEqual)
            {
                ObjectGraphDump.ComparisonResult looserComparisonResult = ObjectGraphDump.CompareDumpResults(dumpBefore, dumpAfter, compareObjectRuntimeIdentities: false);
                if (!looserComparisonResult.DumpsAreEqual)
                    comparisonResult = looserComparisonResult;

                throw new InvalidOperationException(
                    "Config baseline instance was modified by the construction of specialized configs! " +
                    "The following difference was found (see next line):\n" +
                    $"    {comparisonResult.Description}\n" +
                    "This likely means that a config's OnLoaded method modified items that were " +
                    "not owned by that game config instance (due to sharing of config items for deduplication). " +
                    "This should be fixed as it can cause to unpredictable bugs at runtime.");
            }
        }

        int CountConfigItems(IGameConfig gameConfig)
        {
            int totalCount = 0;

            GameConfigTypeInfo configTypeInfo = GameConfigRepository.Instance.GetGameConfigTypeInfo(gameConfig.GetType());
            foreach (GameConfigEntryInfo entryInfo in configTypeInfo.Entries.Values)
            {
                if (entryInfo.MemberInfo.GetDataMemberType().IsGameConfigLibrary())
                {
                    IGameConfigLibraryEntry library = (IGameConfigLibraryEntry)entryInfo.MemberInfo.GetDataMemberGetValueOnDeclaringType()(gameConfig);
                    totalCount += library.Count;
                }
            }

            return totalCount;
        }

        protected virtual GameConfigBuildParameters ValidateBuildParameters(GameConfigBuildParameters input)
        {
            return input ?? new DefaultGameConfigBuildParameters();
        }
        protected abstract Task BuildAsync(IGameConfigBuilderProvider<ISharedGameConfig> shared, IGameConfigBuilderProvider<IServerGameConfig> server);

        public async Task<ConfigArchive> CreateArchiveAsync(MetaTime createdAt, GameConfigBuildParameters buildParams, MetaGuid parentId, ConfigArchive parent, bool printErrorsToConsole = true)
        {
            if (!MetaplayCore.IsInitialized)
                throw new InvalidOperationException("MetaplayCore is not initialized! Check the logs for errors related to MetaplayCore.Initialize.");

            if (ConfigBuildDevelopmentDebug.ForceFixedArchiveTimestamp)
                createdAt = MetaTime.Epoch;

            // Fill in default debug options if not given
            DebugOptions ??= new GameConfigBuildDebugOptions();

            // Validate build params
            BuildParametersBase = ValidateBuildParameters(buildParams);
            if (BuildParametersBase == null)
                throw new InvalidOperationException("Config build parameters validation failed!");

            // Sanity check, cannot do incremental build without parent.
            if (BuildParametersBase.IsIncremental && parent == null)
                throw new ArgumentException("Incremental config build cannot be done without a parent config", nameof(parent));

            // Measure total build time
            Stopwatch swTotal = Stopwatch.StartNew();

            // Create build log for collecting all messages during build
            GameConfigBuildLog buildLog = new GameConfigBuildLog();

            // Initialize builders
            GameConfigBuildState<ISharedGameConfig> shared = CreateSharedGameConfigBuilder(buildLog, parent);
            GameConfigBuildState<IServerGameConfig> server = CreateServerGameConfigBuilder(buildLog, parent);

            // Get patches
            if (parent != null)
            {
                try
                {
                    ExtractPatches(parent, out server.ParentVariantPatches, out shared.ParentVariantPatches);
                }
                catch (Exception ex)
                {
                    buildLog.Error("Failed to extract patches from parent archive", ex);
                }
            }

            // Initialize fetchers for build sources and get metadata
            try
            {
                await ConfigureSourceFetchersAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                buildLog.Error("Failed to configure source fetchers for sources, are all sources valid?", ex);
            }

            if (buildLog.HasErrors())
                throw new GameConfigBuildFailed(new GameConfigBuildReport(buildLog.Messages, validationResults: null));

            // Run actual build (\note outputs errors into buildLog)
            try
            {
                await BuildAsync(shared, server);
            }
            catch (Exception ex)
            {
                buildLog.Error("Unhandled exception during game config build", ex);
            }

            // If build failed, bail out with a report
            if (buildLog.HasErrors())
                throw new GameConfigBuildFailed(new GameConfigBuildReport(buildLog.Messages, validationResults: null));

            // Validate MetaRefs and MetaConfigIds and bail out if found any invalid ones.
            // \note Invalid MetaRefs would be detected during FinalizeBuildStates anyway (in GameConfigBase.OnConfigEntriesPopulated),
            //       but this way we can provide better error messages. Namely, this logs all the errors in _buildLog instead of throwing
            //       on the first one.
            shared.ValidateMetaRefs();
            server.ValidateMetaRefs();
            if (buildLog.HasErrors())
                throw new GameConfigBuildFailed(new GameConfigBuildReport(buildLog.Messages, validationResults: null));

            (GameConfigBuildOutput sharedOutput, GameConfigBuildOutput serverOutput) = FinalizeBuildStates(buildLog, createdAt, shared, server);

            // If finalization failed, bail out with a report
            if (buildLog.HasErrors())
                throw new GameConfigBuildFailed(new GameConfigBuildReport(buildLog.Messages, validationResults: null));

            // Validate that the config doesn't have patches for variants that are not configured in the PlayerExperiments config.
            ValidateThatUsedVariantsAreConfigured(buildLog, sharedOutput, serverOutput);
            // If the above found any variant errors, bail out with a report
            if (buildLog.HasErrors())
                throw new GameConfigBuildFailed(new GameConfigBuildReport(buildLog.Messages, validationResults: null));

            // Construct full archive, containing shared & server configs as well as their patches.
            List<ConfigArchiveEntry> sharedPatchEntries =
                sharedOutput.VariantPatches
                .Select(kv =>
                {
                    (ExperimentVariantPair key, GameConfigPatchEnvelope patch) = kv;
                    return ConfigArchiveEntry.FromBlob($"SharedPatch.{key.ExperimentId}.{key.VariantId}.mpp", patch.Serialize());
                }).ToList();

            List<ConfigArchiveEntry> serverPatchEntries =
                serverOutput.VariantPatches
                .Select(kv =>
                {
                    (ExperimentVariantPair key, GameConfigPatchEnvelope patch) = kv;
                    return ConfigArchiveEntry.FromBlob($"ServerPatch.{key.ExperimentId}.{key.VariantId}.mpp", patch.Serialize());
                }).ToList();

            List<ConfigArchiveEntry> allEntries = sharedPatchEntries
                .Concat(serverPatchEntries)
                .Append(ConfigArchiveEntry.FromBlob("Shared.mpa", ConfigArchiveBuildUtility.ToBytes(sharedOutput.Baseline, CompressionAlgorithm.None, 0)))
                .Append(ConfigArchiveEntry.FromBlob("Server.mpa", ConfigArchiveBuildUtility.ToBytes(serverOutput.Baseline, CompressionAlgorithm.None, 0))).ToList<ConfigArchiveEntry>();
            ConfigArchive fullArchive = ConfigArchive.CreateFromBuild(createdAt, allEntries);

            if (DebugOptions.EnableDebugPrints)
            {
                DebugLog.Info($"Timestamp = {createdAt}");

                IReadOnlyList<ConfigArchiveEntry> sharedEntries = sharedOutput.Baseline.Entries;
                IReadOnlyList<ConfigArchiveEntry> serverEntries = serverOutput.Baseline.Entries;
                DebugLog.Info("Full archive hash (before adding _metadata): {FullArchiveContentHash}", fullArchive.Version);
                DebugLog.Info($"Shared config entries ({sharedEntries.Count}):{string.Concat(sharedEntries.Select(entry => $"\n  {entry.Name} ({entry.Bytes.Length} bytes): {entry.Hash}"))}");
                DebugLog.Info($"Server config entries ({serverEntries.Count}):{string.Concat(serverEntries.Select(entry => $"\n  {entry.Name} ({entry.Bytes.Length} bytes): {entry.Hash}"))}");
                DebugLog.Info($"Shared config patch entries ({sharedPatchEntries.Count}):{string.Concat(sharedPatchEntries.Select(entry => $"\n  {entry.Name} ({entry.Bytes.Length} bytes): {entry.Hash}"))}");
                DebugLog.Info($"Server config patch entries ({serverPatchEntries.Count}):{string.Concat(serverPatchEntries.Select(entry => $"\n  {entry.Name} ({entry.Bytes.Length} bytes): {entry.Hash}"))}\n");
            }

            // Validate all variants (and baseline) from the full archive
            List<GameConfigValidationResult> validationResults = ValidateFullArchive(fullArchive, shared.SourceMappings, server.SourceMappings, buildLog);

            // Log information about build time
            buildLog.Information(Invariant($"Total build time: {swTotal.ElapsedMilliseconds / 1000.0:.00}s"));

            // Build a report out of the build (with build and validation logs, if applicable)
            GameConfigBuildReport report = new GameConfigBuildReport(buildLog.Messages, validationResults);
            if (printErrorsToConsole)
                report.PrintToConsole();

            // Add build metadata as custom archive entry named "_metadata"
            GameConfigMetaData metaData      = GetMetaDataForBuild(parentId, parent, buildParams, report);
            byte[]             metaDataBytes = metaData.ToBytes();
            allEntries.Add(ConfigArchiveEntry.FromBlob("_metadata", metaDataBytes));

            // Return the full server archive (with variants and metadata).
            // But first, final check to make sure no errors slip by.
            if (buildLog.HasErrors())
                throw new GameConfigBuildFailed(new GameConfigBuildReport(buildLog.Messages, validationResults: null));
            return ConfigArchive.CreateFromBuild(createdAt, allEntries);
        }

        public GameConfigMetaData GetMetaDataForBuild(MetaGuid parentId, ConfigArchive parent, GameConfigBuildParameters buildParams, GameConfigBuildReport report)
        {
            return new GameConfigMetaData(
                parentConfigId: parentId,
                parentConfigHash: parent?.Version ?? ContentHash.None,
                buildParams: buildParams,
                buildReport: report,
                buildSourceMetadata: GetBuildSourcesMetadata());
        }

        static (GameConfigBuildOutput sharedOutput, GameConfigBuildOutput serverOutput) FinalizeBuildStates(GameConfigBuildLog buildLog, MetaTime createdAt, GameConfigBuildState<ISharedGameConfig> shared, GameConfigBuildState<IServerGameConfig> server)
        {
            GameConfigBuildOutput sharedOutput = shared.Finalize(createdAt);
            GameConfigBuildOutput serverOutput = server.Finalize(createdAt);
            return (sharedOutput, serverOutput);
        }

        void ExtractPatches(ConfigArchive parent,
            out Dictionary<ExperimentVariantPair, GameConfigPatch> serverPatches,
            out Dictionary<ExperimentVariantPair, GameConfigPatch> sharedPatches)
        {
            FullGameConfig parentFullConfig = FullGameConfig.CreateSoloUnpatched(parent);

            Dictionary<ExperimentVariantPair, FullGameConfigPatch> parentVariantPatches =
                parentFullConfig.ServerConfig.PlayerExperiments.Values
                .SelectMany(experiment => experiment.Variants.Values.Select(variant => (experiment, variant)))
                .ToDictionary(
                    ev => new ExperimentVariantPair(ev.experiment.ExperimentId, ev.variant.Id),
                    ev => ev.variant.ConfigPatch);

            sharedPatches = parentVariantPatches.ToDictionary(
                ev => ev.Key,
                ev => ev.Value.SharedConfigPatch);

            serverPatches = parentVariantPatches.ToDictionary(
                ev => ev.Key,
                ev => ev.Value.ServerConfigPatch);
        }
    }

    /// <summary>
    /// A GameConfig build implementation that exposes configuring the build per config entry.
    /// </summary>
    ///
    /// The implementation iterates over all of the registered config entries in Shared and Server game
    /// configs and allows derived classes to override the data fetching and building per named
    /// entry.
    ///
    /// A derived class can override the build of individual entries via overriding the <see cref="GetEntryBuilder"/>
    /// method called for each config entry in the shared and server game configs.
    ///
    /// The default implementation of entry building uses the build sources passed in <see cref="GameConfigBuildParameters"/>,
    /// such as <see cref="GameConfigBuildParameters.DefaultSource"/>, along with the optional per-entry source specifications
    /// given with <see cref="GameConfigEntryAttribute.ConfigBuildSource"/>.
    /// The concrete sources often come from the project-specific implementation of <see cref="GameConfigBuildIntegration.GetAvailableGameConfigBuildSources"/>.
    /// The intention is that most config entries are buildable via the default implementation and that the integration only
    /// needs to override entry builders for more advanced use cases.
    ///
    /// <typeparam name="TSharedConfig"></typeparam>
    /// <typeparam name="TServerConfig"></typeparam>
    public abstract class GameConfigBuildTemplate<TSharedConfig, TServerConfig> : GameConfigBuild
        where TSharedConfig : ISharedGameConfig
        where TServerConfig : IServerGameConfig
    {
        protected struct ConfigEntryBuilder
        {
            public Func<Task>                     FetchDataAsync;
            public Func<IGameConfigBuilder, Task> BuildAsync;

            public ConfigEntryBuilder(Func<Task> fetchData, Func<IGameConfigBuilder, Task> build)
            {
                FetchDataAsync = fetchData;
                BuildAsync     = build;
            }

            public static ConfigEntryBuilder Create<TConfigType>(Func<Task> fetchData, Action<IGameConfigBuilder<TConfigType>> build) where TConfigType : IGameConfig
            {
                return new ConfigEntryBuilder(
                    fetchData,
                    (builder) =>
                    {
                        build((IGameConfigBuilder<TConfigType>)builder);
                        return Task.CompletedTask;
                    });
            }
        }

        (Type ConfigType, GameConfigEntryInfo Entry)? _currentConfigEntry = null;

        IEnumerable<GameConfigEntryInfo> GetConfigEntriesToBuild(Type gameConfigType)
        {
            return GameConfigRepository.Instance.GetGameConfigTypeInfo(gameConfigType).Entries.Values;
        }

        ConfigEntryBuilder GetDefaultEntryBuilder(Type configType, string entryName)
        {
            MetaDebug.Assert(configType == _currentConfigEntry?.ConfigType, $"Called {nameof(GetDefaultEntryBuilder)} for config type '{configType}' when configuring config type '{_currentConfigEntry?.ConfigType}'");
            MetaDebug.Assert(entryName == _currentConfigEntry?.Entry.Name, $"Called {nameof(GetDefaultEntryBuilder)} for entry '{entryName}' when configuring entry '{_currentConfigEntry?.Entry.Name}'");

            if (_currentConfigEntry?.Entry.BuildParamsSourceProperty != null || BuildParametersBase.DefaultSource != null)
            {
                return GenericSpreadSheetEntryBuilder(entryName);
            }
            // Note: the below hacks will be removed
            else if (configType.ImplementsInterface(typeof(ISharedGameConfig)) && entryName == "Languages")
            {
                // Supply english as a default language
                return AssignLibraryItemsBuilder<LanguageId, LanguageInfo>(Enumerable.Repeat(new LanguageInfo(LanguageId.FromString("en"), "English"), 1));
            }
            else
            {
                return AssignEmptyEntryBuilder();
            }
        }

        IEnumerable<(IGameConfigBuilder, ConfigEntryBuilder)> GetConfigEntryBuilders(IEnumerable<IGameConfigBuilder> configBuilders)
        {
            List<(IGameConfigBuilder, ConfigEntryBuilder)> entryBuilders = new List<(IGameConfigBuilder, ConfigEntryBuilder)>();
            foreach ((IGameConfigBuilder configBuilder, GameConfigEntryInfo entry) in configBuilders.SelectMany(x => GetConfigEntriesToBuild(x.ConfigType).Select(y => (x, y))))
            {
                ConfigEntryBuilder? builder = GetEntryBuilderWrapper(configBuilder.ConfigType, entry);
                if (builder.HasValue)
                    entryBuilders.Add((configBuilder, builder.Value));
            }
            return entryBuilders;
        }

        protected IGameConfigSourceData FetchSourceData(string itemName, GameConfigBuildSource source)
        {
            // \todo: error handling
            IGameConfigSourceFetcher fetcher = SourceFetcher(source);
            return fetcher.Fetch(itemName);
        }

        protected IGameConfigSourceData FetchSourceData(string itemName, string sourcePropertyName)
        {
            // \todo: error handling
            return FetchSourceData(itemName, _sourcesByMemberName[sourcePropertyName]);
        }

        protected Func<IGameConfigSourceData> GetGenericFetchFunc(string itemName, GameConfigBuildSource source = null)
        {
            if (source == null)
            {
                if (_currentConfigEntry?.Entry.BuildParamsSourceProperty is
                    { } sourceProperty)
                {
                    if (!_sourcesByMemberName.TryGetValue(sourceProperty, out source))
                    {
                        // check if the property by name given in attribute exists on the params type
                        if (Integration.GetBuildSourcesInBuildParameters(BuildParametersBase.GetType()).All(x => x.Name != sourceProperty))
                            throw new InvalidOperationException($"The config build for entry {_currentConfigEntry?.Entry.Name} refers to build source parameter {sourceProperty} that does not exist in parameters class {BuildParametersBase.GetType()}");

                        throw new InvalidOperationException($"The config build for entry {_currentConfigEntry?.Entry.Name} requires a build source to be declared in {BuildParametersBase.GetType()}.{sourceProperty}");
                    }
                }
                else
                {
                    source = BuildParametersBase.DefaultSource;
                }
            }

            return () => FetchSourceData(itemName, source);
        }

        protected ConfigEntryBuilder CustomEntryBuildSingleSource<TSourceDataType>(Func<IGameConfigSourceData> fetchFunc, Action<IGameConfigBuilder, TSourceDataType> buildFunc)
        {
            IGameConfigSourceData data = null;
            return new ConfigEntryBuilder()
            {
                FetchDataAsync = () =>
                {
                    data = fetchFunc();
                    return Task.CompletedTask;
                },
                BuildAsync = async (builder) =>
                {
                    object resolvedData = await data.Get();
                    buildFunc(builder, (TSourceDataType)resolvedData);
                }
            };
        }

        protected ConfigEntryBuilder CustomEntryBuildSingleSource<TSourceDataType>(string sourceItemName, Action<IGameConfigBuilder, TSourceDataType> buildFunc)
        {
            return CustomEntryBuildSingleSource(GetGenericFetchFunc(sourceItemName, null), buildFunc);
        }

        protected ConfigEntryBuilder GenericSpreadSheetEntryBuilder(string sourceItemName)
        {
            string entryName = _currentConfigEntry?.Entry.Name;
            return CustomEntryBuildSingleSource<SpreadsheetContent>(sourceItemName, (builder, sourceData) => builder.BuildGameConfigEntry(entryName, sourceData));
        }

        protected ConfigEntryBuilder GoogleSheetsEntryBuilder(string sheetName)
        {
            return GenericSpreadSheetEntryBuilder(sheetName);
        }

        protected ConfigEntryBuilder CsvEntryBuilder(string filePath)
        {
            return GenericSpreadSheetEntryBuilder(filePath);
        }

        protected ConfigEntryBuilder AssignEmptyEntryBuilder()
        {
            string entryName = _currentConfigEntry?.Entry.Name;
            return new ConfigEntryBuilder()
            {
                FetchDataAsync = null,
                BuildAsync = (builder) =>
                {
                    builder.AssignEmpty(entryName);
                    return Task.CompletedTask;
                },
            };
        }

        protected ConfigEntryBuilder AssignValueEntryBuilder(IGameConfigEntry value)
        {
            string entryName = _currentConfigEntry?.Entry.Name;
            return new ConfigEntryBuilder()
            {
                FetchDataAsync = null,
                BuildAsync = (builder) =>
                {
                    builder.AssignBaselineConfigEntryBuildResult(entryName, value, null);
                    return Task.CompletedTask;
                },
            };
        }

        protected ConfigEntryBuilder AssignLibraryItemsBuilder<TKey, TInfo>(IEnumerable<TInfo> values) where TInfo : class, IGameConfigData<TKey>, new()
        {
            string entryName = _currentConfigEntry?.Entry.Name;
            return new ConfigEntryBuilder()
            {
                FetchDataAsync = null,
                BuildAsync = (builder) =>
                {
                    builder.AssignLibraryBuildResult(entryName, values.Select(x => new VariantConfigItem<TKey, TInfo>(x, null, null, sourceLocation: null)).ToList(), null);
                    return Task.CompletedTask;
                },
            };
        }

        /// <summary>
        /// Configure the data fetching and config building for a config entry in the shared game config.
        /// </summary>
        ///
        /// Returning null from this function disables the building of the particular config entry altogether, resulting
        /// in the entry to remain at its default value or its previous value if this is an incremental build.
        ///
        /// For entries that don't require a custom entry builder, call the base implementation.
        ///
        /// <param name="configType"></param>
        /// <param name="entryName"></param>
        /// <returns></returns>
        protected virtual ConfigEntryBuilder? GetEntryBuilder(Type configType, string entryName)
        {
            return GetDefaultEntryBuilder(configType, entryName);
        }

        ConfigEntryBuilder? GetEntryBuilderWrapper(Type configType, GameConfigEntryInfo entry)
        {
            MetaDebug.Assert(_currentConfigEntry == null, $"Re-entering {nameof(GetEntryBuilderWrapper)}");
            try
            {
                _currentConfigEntry = (configType, entry);
                return GetEntryBuilder(configType, entry.Name);
            }
            finally
            {
                _currentConfigEntry = null;
            }
        }

        protected override sealed Task BuildAsync(
            IGameConfigBuilderProvider<ISharedGameConfig> shared,
            IGameConfigBuilderProvider<IServerGameConfig> server)
        {
            return BuildAsync(shared.MakeBuilder<TSharedConfig>(), server.MakeBuilder<TServerConfig>());
        }

        /// <summary>
        /// The main GameConfig build entrypoint.
        /// </summary>
        /// <param name="shared"></param>
        /// <param name="server"></param>
        /// <returns></returns>
        protected virtual async Task BuildAsync(
            IGameConfigBuilder<TSharedConfig> shared,
            IGameConfigBuilder<TServerConfig> server)
        {
            // Configure builders for config entries

            IEnumerable<(IGameConfigBuilder, ConfigEntryBuilder)> entryBuilders = GetConfigEntryBuilders(new IGameConfigBuilder[] { shared, server });

            // Execute "FetchData" for each entry that has one configured

            foreach (ConfigEntryBuilder entryBuilder in entryBuilders.Where(f => f.Item2.FetchDataAsync != null).Select(f => f.Item2))
                await entryBuilder.FetchDataAsync();

            // Execute "Build" for each entry

            foreach ((IGameConfigBuilder configBuilder, ConfigEntryBuilder entryBuilder) in entryBuilders)
                await entryBuilder.BuildAsync(configBuilder);
        }
    }

    // Build template with statically known build parameters type
    public abstract class GameConfigBuildTemplate<TSharedConfig, TServerConfig, TBuildParams> : GameConfigBuildTemplate<TSharedConfig, TServerConfig>
        where TSharedConfig : ISharedGameConfig
        where TServerConfig : IServerGameConfig
        where TBuildParams : GameConfigBuildParameters
    {
        protected TBuildParams BuildParameters => (TBuildParams)BuildParametersBase;

        protected override GameConfigBuildParameters ValidateBuildParameters(GameConfigBuildParameters input)
        {
            if (input == null)
                return MetaSerializationUtil.CreateAndPopulate<TBuildParams>(Array.Empty<KeyValuePair<MetaSerializableMember, object>>());
            return input is TBuildParams ? input : null;
        }

        protected override Task BuildAsync(IGameConfigBuilder<TSharedConfig> shared, IGameConfigBuilder<TServerConfig> server)
        {
            // Call legacy build entrypoint for backwards compatibility
            return Build(shared, server, BuildParameters);
        }

        protected virtual Task Build(IGameConfigBuilder<TSharedConfig> shared, IGameConfigBuilder<TServerConfig> server, TBuildParams buildParams)
        {
            return base.BuildAsync(shared, server);
        }
    }

    public abstract class GameConfigBuildTemplate<TSharedConfig> : GameConfigBuildTemplate<TSharedConfig, ServerGameConfigBase>
        where TSharedConfig : ISharedGameConfig
    {
    }

    // The default game config implementation that doesn't require static knowledge of integration types. This implementation is used
    // when integration doesn't provide a GameConfigBuild implementation of its own.
    public class DefaultGameConfigBuild : GameConfigBuildTemplate<ISharedGameConfig, IServerGameConfig>
    {
    }

    // Integration hooks for the game config build.
    public class GameConfigBuildIntegration : IMetaIntegrationSingleton<GameConfigBuildIntegration>
    {
        public virtual UnknownConfigMemberHandling UnknownConfigItemMemberHandling => UnknownConfigMemberHandling.Error;

        /// Create and initialize a custom GameConfigBuild instance.
        public virtual GameConfigBuild MakeGameConfigBuild(IGameConfigSourceFetcherConfig fetcherConfig, GameConfigBuildDebugOptions debugOpts)
        {
            // using legacy GameConfigBuild integration hook here
            GameConfigBuild impl = IntegrationRegistry.Create<GameConfigBuild>();
            impl.SourceFetcherProvider = MakeSourceFetcherProvider(fetcherConfig);
            impl.DebugOptions = debugOpts;
            return impl;
        }

        /// Create and initialize a LocalizationsBuild instance.
        public virtual LocalizationsBuild MakeLocalizationsBuild(IGameConfigSourceFetcherConfig fetcherConfig)
        {
            LocalizationsBuild impl = IntegrationRegistry.Create<LocalizationsBuild>();

            impl.SourceFetcherProvider = MakeSourceFetcherProvider(fetcherConfig);
            return impl;
        }

        Type GetBuildParametersType(Type baseClass, Type defaultClass, string errorMsg)
        {
            IEnumerable<Type> paramsTypes = IntegrationRegistry.GetIntegrationClasses(baseClass);
            if (paramsTypes.Count() <= 1)
                return paramsTypes.SingleOrDefault();
            // remove SDK default from the list
            paramsTypes = paramsTypes.Where(x => x != defaultClass);
            if (paramsTypes.Count() > 1)
                throw new InvalidOperationException(errorMsg);
            return paramsTypes.SingleOrDefault();
        }

        public virtual Type GetDefaultGameConfigBuildParametersType()
        {
            return GetBuildParametersType(
                typeof(GameConfigBuildParameters),
                typeof(DefaultGameConfigBuildParameters),
                $"Multiple {nameof(GameConfigBuildParameters)} integrations found, override " +
            $"{nameof(GameConfigBuildIntegration)}.{nameof(GetDefaultGameConfigBuildParametersType)} to specify which one to use as default");
        }

        public virtual Type GetDefaultLocalizationsBuildParametersType()
        {
            return GetBuildParametersType(
                typeof(LocalizationsBuildParameters),
                typeof(DefaultLocalizationsBuildParameters),
                $"Multiple {nameof(LocalizationsBuildParameters)} integrations found, override " +
                $"{nameof(GameConfigBuildIntegration)}.{nameof(GetDefaultLocalizationsBuildParametersType)} to specify which one to use as default");
        }

        public IEnumerable<(string SourceProperty, IEnumerable<GameConfigBuildSource> Sources)> GetAllAvailableBuildSources(Type buildParamsType)
        {
            return GetBuildSourcesInBuildParameters(buildParamsType).Select(x => (x.Name, GetAvailableBuildSources(buildParamsType, x.Name))).Where(x => x.Item2.Any());
        }

        /// Retrieve `GameConfigBuildSource` members of a `GameConfigBuildParameters` type. The default implementation uses
        /// reflection to simply enumerate all members of the appropriate type and should do the right thing in majority
        /// of cases. Only override this function when your custom `GameConfigBuildParameters` has a more complicated
        /// layout.
        public virtual IEnumerable<MemberInfo> GetBuildSourcesInBuildParameters(Type buildParamsType)
        {
            MetaDebug.Assert(typeof(IGameDataBuildParameters).IsAssignableFrom(buildParamsType), $"Type {buildParamsType} is not a {nameof(IGameDataBuildParameters)}");
            return buildParamsType
                         .EnumerateInstanceDataMembersInUnspecifiedOrder()
                         .Where(x => typeof(GameConfigBuildSource).IsAssignableFrom(x.GetDataMemberType()));
        }

        IEnumerable<GameConfigBuildSource> GetAvailableBuildSources(Type buildParamsType, string sourcePropertyInBuildParams)
        {
            if (typeof(GameConfigBuildParameters).IsAssignableFrom(buildParamsType))
                return GetAvailableGameConfigBuildSources(sourcePropertyInBuildParams);
            else if (typeof(LocalizationsBuildParameters).IsAssignableFrom(buildParamsType))
                return GetAvailableLocalizationsBuildSources(sourcePropertyInBuildParams);

            throw new InvalidOperationException($"Unhandled build parameters type {buildParamsType}");
        }

        public virtual IEnumerable<GameConfigBuildSource> GetAvailableLocalizationsBuildSources(string sourcePropertyInBuildParams)
        {
            return Enumerable.Empty<GameConfigBuildSource>();
        }

        public virtual IEnumerable<GameConfigBuildSource> GetAvailableGameConfigBuildSources(string sourcePropertyInBuildParams)
        {
            return Enumerable.Empty<GameConfigBuildSource>();
        }

        /// Create a custom IGameConfigSourceFetcherProvider instance based on the given fetcher config. Override this
        /// if you have custom build source types that need a custom fetcher.
        public virtual IGameConfigSourceFetcherProvider MakeSourceFetcherProvider(IGameConfigSourceFetcherConfig config)
        {
            return new DefaultGameConfigSourceFetcherProvider((GameConfigSourceFetcherConfigCore)config);
        }
    }

    public static class StaticFullGameConfigBuilder
    {
        public static async Task<ConfigArchive> BuildArchiveAsync(
            MetaTime createdAt,
            MetaGuid parentId,
            ConfigArchive parent,
            GameConfigBuildParameters buildParams,
            IGameConfigSourceFetcherConfig fetcherConfig = null,
            GameConfigBuildDebugOptions debugOptions = null)
        {
            GameConfigBuild build = IntegrationRegistry.Get<GameConfigBuildIntegration>().MakeGameConfigBuild(fetcherConfig, debugOptions);
            return await build.CreateArchiveAsync(createdAt, buildParams, parentId, parent);
        }
    }
}
