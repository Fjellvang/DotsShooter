// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Player;
using Metaplay.Server.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.GameConfig
{
    public class ServerGameConfigProvider
    {
        readonly struct FullArchiveKey : IEquatable<FullArchiveKey>
        {
            public readonly MetaGuid                        StaticConfigId;
            public readonly MetaGuid                        DynamicContentId;

            public FullArchiveKey(MetaGuid staticConfigId, MetaGuid dynamicContentId)
            {
                StaticConfigId = staticConfigId;
                DynamicContentId = dynamicContentId;
            }

            public bool Equals(FullArchiveKey other) => StaticConfigId == other.StaticConfigId && DynamicContentId == other.DynamicContentId;
            public override bool Equals(object obj) => obj is FullArchiveKey key && Equals(key);
            public override int GetHashCode() => Util.CombineHashCode(StaticConfigId.GetHashCode(), DynamicContentId.GetHashCode());
        }

        readonly struct FullConfigKey : IEquatable<FullConfigKey>
        {
            public readonly MetaGuid                        StaticConfigId;
            public readonly MetaGuid                        DynamicContentId;
            public readonly GameConfigSpecializationKey?    SpecializationKey;

            public FullConfigKey(MetaGuid staticConfigId, MetaGuid dynamicContentId, GameConfigSpecializationKey? specializationKey)
            {
                StaticConfigId = staticConfigId;
                DynamicContentId = dynamicContentId;
                SpecializationKey = specializationKey;
            }

            public bool Equals(FullConfigKey other) => StaticConfigId == other.StaticConfigId && DynamicContentId == other.DynamicContentId && SpecializationKey == other.SpecializationKey;
            public override bool Equals(object obj) => obj is FullConfigKey key && Equals(key);
            public override int GetHashCode() => Util.CombineHashCode(StaticConfigId.GetHashCode(), DynamicContentId.GetHashCode(), SpecializationKey?.GetHashCode() ?? 0);
        }

        public static readonly ServerGameConfigProvider     Instance        = new ServerGameConfigProvider();

        WeakReferencingCacheWithTimedRetention<FullArchiveKey, FullGameConfigImportResources> _importResourcesCache = new(minimumRetentionTime: TimeSpan.FromSeconds(20));
        WeakReferencingCacheWithTimedRetention<FullConfigKey, FullGameConfig> _configCache  = new(minimumRetentionTime: TimeSpan.FromSeconds(20));

        #region Cache entry creation and pruning metrics
        static Prometheus.Counter   c_importResourcesCreated  = Prometheus.Metrics.CreateCounter("game_config_cache_resources_created_total", "Number of game config import resources created for cache");
        static Prometheus.Counter   c_importResourcesPruned   = Prometheus.Metrics.CreateCounter("game_config_cache_resources_pruned_total", "Number of game config import resources pruned from cache dictionary");
        static Prometheus.Gauge     c_importResourcesLive     = Prometheus.Metrics.CreateGauge("game_config_cache_live_resources", "Number of live game config import resources in cache");
        static Prometheus.Counter   c_configsCreated          = Prometheus.Metrics.CreateCounter("game_config_cache_specializations_created_total", "Number of game config specializations created for cache");
        static Prometheus.Counter   c_configsPruned           = Prometheus.Metrics.CreateCounter("game_config_cache_specializations_pruned_total", "Number of game config specializations pruned from cache dictionary");
        static Prometheus.Gauge     c_configsLive             = Prometheus.Metrics.CreateGauge("game_config_cache_live_specializations", "Number of live game config specializations in cache");
        static Prometheus.Histogram c_pruneDuration           = Prometheus.Metrics.CreateHistogram("game_config_cache_prune_duration", "Duration to prune the server game config cache", Metaplay.Cloud.Metrics.Defaults.LatencyDurationConfig);
        #endregion

        #region Config metrics
        // Metrics that don't depend on specializations being used by players (only on the config deduplication resources present in the cache)
        static Prometheus.Gauge   c_numConfigIds                        = Prometheus.Metrics.CreateGauge("game_config_cache_config_ids", "Number of unique game config ids present in cache (not considering individual specializations)");
        static Prometheus.Gauge   c_totalSinglePatchDirectDuplication   = Prometheus.Metrics.CreateGauge("game_config_cache_total_single_patch_direct_duplication", "Total number of directly in-memory duplicated config items in underlying single-patch configs");
        static Prometheus.Gauge   c_totalSinglePatchIndirectDuplication = Prometheus.Metrics.CreateGauge("game_config_cache_total_single_patch_indirect_duplication", "Total number of indirectly in-memory duplicated config items in underlying single-patch configs");
        // Metrics that depend on specializations being used by players
        static Prometheus.Gauge   c_totalNumSpecializations                 = Prometheus.Metrics.CreateGauge("game_config_cache_total_specializations", "Number of config specializations present in cache");
        static Prometheus.Gauge   c_totalSpecializationSpecificDuplication  = Prometheus.Metrics.CreateGauge("game_config_cache_total_specialization_specific_duplication", "Total number of in-memory duplicated config items, specifically for an individual specialization");
        static Prometheus.Gauge   c_maxSpecializationSpecificDuplication    = Prometheus.Metrics.CreateGauge("game_config_cache_max_specialization_specific_duplication", "Maximum number (among the specializations) of in-memory duplicated config items, specifically for an individual specialization");

        static Prometheus.Histogram c_configMetricsCollectDuration = Prometheus.Metrics.CreateHistogram("game_config_cache_metrics_collect_duration", "Duration to collect config metrics from the server game config cache", Metaplay.Cloud.Metrics.Defaults.LatencyDurationConfig);
        #endregion

        IMetaLogger _log = MetaLogger.ForContext<ServerGameConfigProvider>();

        Timer _pruningTimer;
        Timer _configMetricsTimer;

        public void StartPeriodicOperations()
        {
            _pruningTimer = new Timer(
                PruningTimerCallback,
                state: null,
                dueTime: TimeSpan.FromSeconds(60),
                period: TimeSpan.FromSeconds(60));

            _configMetricsTimer = new Timer(
                MetricsTimerCallback,
                state: null,
                dueTime: TimeSpan.FromSeconds(15),
                period: TimeSpan.FromSeconds(30));
        }

        public void StopPeriodicOperations()
        {
            _pruningTimer?.Dispose();
            _configMetricsTimer?.Dispose();
        }

        void PruningTimerCallback(object state)
        {
            try
            {
                PruneCaches();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unexpected exception from {nameof(PruneCaches)}");
            }
        }

        void MetricsTimerCallback(object state)
        {
            try
            {
                ReportConfigMetrics();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unexpected exception from {nameof(ReportConfigMetrics)}");
            }
        }

        void PruneCaches()
        {
            Stopwatch sw = Stopwatch.StartNew();

            WeakReferencingCachePruneResult importResourcesCachePruneResult = _importResourcesCache.Prune();
            WeakReferencingCachePruneResult configCachePruneResult = _configCache.Prune();

            c_pruneDuration.Observe(sw.Elapsed.TotalSeconds);

            c_importResourcesPruned.Inc(importResourcesCachePruneResult.NumPruned);
            c_importResourcesLive.Set(importResourcesCachePruneResult.NewCount);
            c_configsPruned.Inc(configCachePruneResult.NumPruned);
            c_configsLive.Set(configCachePruneResult.NewCount);
        }

        void ReportConfigMetrics()
        {
            Stopwatch sw = Stopwatch.StartNew();

            ConfigMetricsInfo metricsInfo = CollectConfigMetricsInfo();

            c_configMetricsCollectDuration.Observe(sw.Elapsed.TotalSeconds);

            // Report most of the info via metrics.

            c_numConfigIds
                .Set(metricsInfo.DistinctConfigIds.Count);
            c_totalSinglePatchDirectDuplication
                .Set(metricsInfo.TotalSinglePatchDirectDuplication);
            c_totalSinglePatchIndirectDuplication
                .Set(metricsInfo.TotalSinglePatchIndirectDuplication);
            c_totalNumSpecializations
                .Set(metricsInfo.TotalNumSpecializations);
            c_totalSpecializationSpecificDuplication
                .Set(metricsInfo.TotalSpecializationSpecificDuplicationAmount);
            c_maxSpecializationSpecificDuplication
                .Set(metricsInfo.MaxSpecializationSpecificDuplicationAmount);

            // Log the specialization key of the most-duplicating specialization.
            // This cannot be easily represented in metrics, so we just log it.
            if (metricsInfo.MaxSpecializationSpecificDuplicationAmount > 0)
            {
                string specializationKeyStr;
                if (!metricsInfo.MaxDuplicatingSpecializationKey.HasValue)
                    specializationKeyStr = "<baseline>";
                else
                {
                    IEnumerable<string> nonControlVariantsWithIndexes =
                        metricsInfo.MaxDuplicatingSpecializationKey.Value.VariantIds
                            .ZipWithIndex()
                            .Where(kv => kv.Value != null)
                            .Select(kv => Invariant($"#{kv.Index}/{kv.Value}"));
                    specializationKeyStr = string.Join(", ", nonControlVariantsWithIndexes);
                }

                _log.Info("Most-duplicating specialization has {NumDuplications} bespoke item duplications. Config id: {ConfigId}, specialization key: {SpecializationKey}",
                    metricsInfo.MaxSpecializationSpecificDuplicationAmount, metricsInfo.MaxDuplicatingSpecializationConfigId, specializationKeyStr);
            }
        }

        class ConfigMetricsInfo
        {
            public ConfigMetricsInfo(int distinctConfigIdsCapacity)
            {
                DistinctConfigIds = new HashSet<MetaGuid>(capacity: distinctConfigIdsCapacity);
            }

            public HashSet<MetaGuid> DistinctConfigIds;

            public int TotalNumPatches = 0;
            public int TotalSinglePatchDirectDuplication = 0;
            public int TotalSinglePatchIndirectDuplication = 0;

            public int TotalNumSpecializations = 0;
            public int TotalSpecializationSpecificDuplicationAmount = 0;
            public int MaxSpecializationSpecificDuplicationAmount = -1;
            public GameConfigSpecializationKey? MaxDuplicatingSpecializationKey;
            public MetaGuid MaxDuplicatingSpecializationConfigId;
        }

        ConfigMetricsInfo CollectConfigMetricsInfo()
        {
            ConfigMetricsInfo metricsInfo = new ConfigMetricsInfo(distinctConfigIdsCapacity: _importResourcesCache.Count);

            // \note The callbacks for LockedForEach below should be kept reasonably cheap, since it locks
            //       the access to the cache for the duration of the whole LockedForEach.

            // Collect data from _importResourcesCache that does not depend on the active specializations of the config.
            _importResourcesCache.LockedForEach((FullArchiveKey key, FullGameConfigImportResources importResources) =>
            {
                metricsInfo.DistinctConfigIds.Add(key.StaticConfigId);

                metricsInfo.TotalNumPatches += importResources.NumPatches;
                metricsInfo.TotalSinglePatchDirectDuplication += importResources.TotalSinglePatchDirectDuplication;
                metricsInfo.TotalSinglePatchIndirectDuplication += importResources.TotalSinglePatchIndirectDuplication;
            });

            // Collect data about the specializations of the configs.
            _configCache.LockedForEach((FullConfigKey key, FullGameConfig gameConfig) =>
            {
                // \note key.StaticConfigId may or may not have been added in the above loop for _importResourcesCache.
                //       _importResourcesCache is not guaranteed to have the import resources for the configs in _configCache.
                metricsInfo.DistinctConfigIds.Add(key.StaticConfigId);

                int specializationSpecificDuplicationAmount = gameConfig.SpecializationSpecificDuplicationAmount;

                metricsInfo.TotalNumSpecializations++;
                metricsInfo.TotalSpecializationSpecificDuplicationAmount += specializationSpecificDuplicationAmount;

                if (specializationSpecificDuplicationAmount > metricsInfo.MaxSpecializationSpecificDuplicationAmount)
                {
                    metricsInfo.MaxSpecializationSpecificDuplicationAmount = specializationSpecificDuplicationAmount;
                    metricsInfo.MaxDuplicatingSpecializationKey = key.SpecializationKey;
                    metricsInfo.MaxDuplicatingSpecializationConfigId = key.StaticConfigId;
                }
            });

            return metricsInfo;
        }

        /// <summary>
        /// Returns the <see cref="FullGameConfigImportResources"/> constructed from a full game config archive.
        /// Full game configs are identified by the pair of static and dynamic source configs.
        /// </summary>
        public ValueTask<FullGameConfigImportResources> GetImportResourcesAsync(MetaGuid staticConfigId, MetaGuid dynamicContentId)
        {
            return _importResourcesCache.GetCachedOrCreateNewAsync(
                key:            new FullArchiveKey(staticConfigId, dynamicContentId),
                createNewAsync: CreateImportResourcesAsync);
        }

        /// <summary>
        /// Returns the specified baseline (i.e. unpatched) (full) game config.
        /// Using this method, rather than manually constructing GameConfig from
        /// the resources from <see cref="GetImportResourcesAsync"/>,
        /// enables caching and efficient sharing of gameconfig instances.
        /// </summary>
        public ValueTask<FullGameConfig> GetBaselineGameConfigAsync(MetaGuid staticConfigId, MetaGuid dynamicContentId)
        {
            return _configCache.GetCachedOrCreateNewAsync(
                key:            new FullConfigKey(staticConfigId, dynamicContentId, specializationKey: null),
                createNewAsync: CreateFullConfigAsync);
        }

        /// <summary>
        /// Returns the specified baseline (i.e. unpatched) (full) game config.
        /// Using this method, rather than manually constructing GameConfig from
        /// <paramref name="importResources"/>,
        /// enables caching and efficient sharing of gameconfig instances.
        /// </summary>
        /// <remarks>
        /// Use this method instead of <see cref="GetBaselineGameConfigAsync(MetaGuid, MetaGuid)"/>
        /// when you already have the <see cref="FullGameConfigImportResources"/> available
        /// and want to avoid the potentially costly (and async) operation of acquiring it.
        /// </remarks>
        public FullGameConfig GetBaselineGameConfig(MetaGuid staticConfigId, MetaGuid dynamicContentId, FullGameConfigImportResources importResources)
        {
            return _configCache.GetCachedOrCreateNew(
                key:            new FullConfigKey(staticConfigId, dynamicContentId, specializationKey: null),
                createNew:      _/*key*/ => CreateFullConfig(importResources, specializationKey: null));
        }

        /// <summary>
        /// Returns the specified full game config specialized with the specified patches.
        /// Using this method, rather than manually constructing GameConfig from
        /// the resources from <see cref="GetImportResourcesAsync"/> with the specialization,
        /// enables caching and efficient sharing of gameconfig instances.
        /// If <paramref name="specializationKey"/> is <c>null</c>, it is the same as
        /// a having all-control variants; a config with baseline contents is returned.
        /// </summary>
        public ValueTask<FullGameConfig> GetSpecializedGameConfigAsync(MetaGuid staticConfigId, MetaGuid dynamicContentId, GameConfigSpecializationKey? specializationKey)
        {
            return _configCache.GetCachedOrCreateNewAsync(
                key:            new FullConfigKey(staticConfigId, dynamicContentId, specializationKey),
                createNewAsync: CreateFullConfigAsync);
        }

        /// <summary>
        /// Returns the specified full game config specialized with the specified patches.
        /// Using this method, rather than manually constructing GameConfig from
        /// <paramref name="importResources"/> with the specialization,
        /// enables caching and efficient sharing of gameconfig instances.
        /// If <paramref name="specializationKey"/> is <c>null</c>, it is the same as
        /// a having all-control variants; a config with baseline contents is returned.
        /// </summary>
        /// <remarks>
        /// Use this method instead of <see cref="GetSpecializedGameConfigAsync(MetaGuid, MetaGuid, GameConfigSpecializationKey?)"/>
        /// when you already have the <see cref="FullGameConfigImportResources"/> available
        /// and want to avoid the potentially costly (and async) operation of acquiring it.
        /// </remarks>
        public FullGameConfig GetSpecializedGameConfig(MetaGuid staticConfigId, MetaGuid dynamicContentId, FullGameConfigImportResources importResources, GameConfigSpecializationKey? specializationKey)
        {
            return _configCache.GetCachedOrCreateNew(
                key:            new FullConfigKey(staticConfigId, dynamicContentId, specializationKey),
                createNew:      _/*key*/ => CreateFullConfig(importResources, specializationKey));
        }

        async Task<FullGameConfig> CreateFullConfigAsync(FullConfigKey key)
        {
            FullGameConfigImportResources importResources = await GetImportResourcesAsync(key.StaticConfigId, key.DynamicContentId);
            return CreateFullConfig(importResources, key.SpecializationKey);
        }

        FullGameConfig CreateFullConfig(FullGameConfigImportResources importResources, GameConfigSpecializationKey? specializationKey)
        {
            c_configsCreated.Inc();

            OrderedSet<ExperimentVariantPair> patchIds = new OrderedSet<ExperimentVariantPair>();

            if (specializationKey.HasValue)
            {
                List<PlayerExperimentId> experimentIds = importResources.ExperimentIds;
                ExperimentVariantId[] variantIds = specializationKey.Value.VariantIds;

                if (experimentIds.Count != variantIds.Length)
                    throw new MetaAssertException("Mismatching count of import resources ExperimentIds ({0}) vs specialization key VariantIds ({1})", experimentIds.Count, variantIds.Length);

                foreach ((PlayerExperimentId experimentId, ExperimentVariantId variantId) in experimentIds.Zip(variantIds))
                {
                    if (variantId != null)
                        patchIds.Add(new ExperimentVariantPair(experimentId, variantId));
                }
            }

            // Normally we set omitPatchesInServerConfigExperiments to true here, as we're normally using
            // deduplication. In that case the patches within serverConfig.PlayerExperiments have already
            // been populated in the deduplication baseline instance.
            // But if deduplication is disabled, _and_ we're currently creating the baseline config
            // (!specializationKey.HasValue) (which happens to be the only kind of config from where
            // we need to get the patches from), then we set omitPatchesInServerConfigExperiments to false
            // in order to make the patches available.
            SystemOptions systemOpts = RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>();
            bool omitPatchesInServerConfigExperiments = systemOpts.EnableGameConfigInMemoryDeduplication
                                                        ? true
                                                        : specializationKey.HasValue;

            return FullGameConfig.CreateSpecialization(importResources, patchIds, omitPatchesInServerConfigExperiments: omitPatchesInServerConfigExperiments);
        }

        async Task<FullGameConfigImportResources> CreateImportResourcesAsync(FullArchiveKey key)
        {
            c_importResourcesCreated.Inc();

            ConfigArchive archive = await FetchFullConfigArchiveAsync(key);

            SystemOptions systemOpts = RuntimeOptionsRegistry.Instance.GetCurrent<SystemOptions>();
            GameConfigRuntimeStorageMode configRuntimeStorageMode = systemOpts.EnableGameConfigInMemoryDeduplication
                                                                    ? GameConfigRuntimeStorageMode.Deduplicating
                                                                    : GameConfigRuntimeStorageMode.Solo;

            return FullGameConfigImportResources.CreateWithAllConfiguredPatches(archive, configRuntimeStorageMode);
        }

        async Task<ConfigArchive> FetchFullConfigArchiveAsync(FullArchiveKey key)
        {
            if (!key.StaticConfigId.IsValid)
                throw new ArgumentException($"Trying to fetch invalid StaticConfigId from database");

            // Fetch the active StaticGameConfig from database
            MetaDatabase db = MetaDatabase.Get(QueryPriority.Normal);
            PersistedStaticGameConfig archive = await db.TryGetAsync<PersistedStaticGameConfig>(key.StaticConfigId.ToString());
            if (archive == null)
                throw new InvalidOperationException($"{nameof(PersistedStaticGameConfig)} version {key.StaticConfigId} not found in database!");

            // Extract the archive as a ReadOnlyArchive
            ConfigArchive fullArchive = ConfigArchive.FromBytes(archive.ArchiveBytes);

            // \todo [petri] combine with Dynamic data in the future in here

            return fullArchive;
        }
    }
}
