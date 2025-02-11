// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Server.StatisticsEvents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Type = System.Type;

namespace Metaplay.Server.AdminApi.Controllers
{
    public interface IStatisticsPageReadStorageProvider
    {
        IStatisticsPageStorage Storage { get; }
    }

    public class BusinessMetricsStatisticsPageReadStorageProvider : IStatisticsPageReadStorageProvider
    {
        const int StatisticsPageReadStorageCacheSize = 1024 * 1024 * 4; // 4 Megabytes

        ILogger<BusinessMetricsStatisticsPageReadStorageProvider> _logger;
        IStatisticsPageStorage                                    _storage;
        SharedApiBridge                                           _bridge;

        public BusinessMetricsStatisticsPageReadStorageProvider(ILogger<BusinessMetricsStatisticsPageReadStorageProvider> logger, SharedApiBridge bridge)
        {
            _logger   = logger;
            _bridge   = bridge;

            IStatisticsPageStorage dbStorage    = new DatabaseStatisticsPageStorage();
            IStatisticsPageStorage actorStorage = new CollectorActorStatisticsPageStorage(_bridge, StatsCollectorManager.EntityId);

            _storage = dbStorage.AsTiered(actorStorage).AsCached(StatisticsPageReadStorageCacheSize, true);
        }

        public IStatisticsPageStorage Storage => _storage;
    }

    /// <summary>
    /// Controller for various user related functions
    /// </summary>
    public class MetricsController : GameAdminApiController
    {
        /// <summary>
        /// Time series resolution when querying the data.
        /// </summary>
        public enum TimeSeriesResolution
        {
            Minutely, // 1min samples
            Hourly,   // 60min samples
            Daily     // 24h samples
        }

        IStatisticsPageReadStorageProvider _statisticsPageReadStorage;

        public MetricsController(IStatisticsPageReadStorageProvider storageProvider) : base()
        {
            _statisticsPageReadStorage = storageProvider;
        }

        public class TimeseriesMetricResponse : ISingleMetricResponse
        {
            // \todo return queried range start/end (to show predictable visual range, regardless of how much data we have)
            public MetaDictionary<MetaTime, double?> Values        { get; init; }
            public string                               Id            { get; init; }
            public StatisticsTimeSeriesDashboardInfo    DashboardInfo { get; init; }

            public TimeseriesMetricResponse(string id, MetaDictionary<MetaTime, double?> values, StatisticsTimeSeriesDashboardInfo dashboardInfo)
            {
                Id            = id;
                Values        = values;
                DashboardInfo = dashboardInfo;
            }
        }

        public class CohortTimeSeriesResponse : ISingleMetricResponse
        {
            // \todo return queried range start/end (to show predictable visual range, regardless of how much data we have)
            public MetaDictionary<MetaTime, MetaDictionary<string, double?>> Cohorts       { get; init; }
            public string                                                    Id            { get; init; }
            public StatisticsTimeSeriesDashboardInfo                         DashboardInfo { get; init; }

            public CohortTimeSeriesResponse(string id, MetaDictionary<MetaTime, MetaDictionary<string, double?>> cohorts, StatisticsTimeSeriesDashboardInfo dashboardInfo)
            {
                Id            = id;
                Cohorts       = cohorts;
                DashboardInfo = dashboardInfo;
            }
        }

        public interface ISingleMetricResponse
        {
            public string                            Id            { get; init; }
            public StatisticsTimeSeriesDashboardInfo DashboardInfo { get; init; }
        }

        public class SelectedMetricsResponse
        {
            public ISingleMetricResponse[] Metrics;
        }

        public class MetricsDashboardInfoResponse
        {
            public MetricInfo[]                                          Metrics;
            public StatisticsTimeSeriesRegistryBase.CategoryDefinition[] Categories;
            public DateTime                                              EpochTime;
        }

        public class MetricInfo
        {
            public string                            Id            { get; init; }
            public StatisticsTimeSeriesDashboardInfo DashboardInfo { get; init; }
        }

        /// <summary>
        /// Use this to get a single metric, for example:
        /// <![CDATA[
        /// localhost:5551/api/businessMetrics/metrics/single/DAU?resolution=Daily&startTime=2024-01-01T00:00:00Z&endTime=2024-02-01T00:00:00Z
        /// ]]>
        /// </summary>
        [HttpGet("metrics/single/{metricId}")]
        [RequirePermission(MetaplayPermissions.ApiMetricsView)]
        public async Task<ActionResult<ISingleMetricResponse>> GetMetric(string metricId, TimeSeriesResolution resolution, DateTime? startTime, DateTime? endTime)
        {
            if (startTime == null || endTime == null)
            {
                return BadRequest($"{nameof(startTime)} or {nameof(endTime)} was not given.");
            }

            if (endTime <= startTime)
                return BadRequest($"{nameof(endTime)} must be after {nameof(startTime)}");

            if (resolution == TimeSeriesResolution.Minutely && endTime - startTime > TimeSpan.FromDays(1))
                return BadRequest("Minutely resolution can only be queried for a maximum of a single day.");
            else if (resolution == TimeSeriesResolution.Hourly && endTime - startTime > TimeSpan.FromDays(30))
                return BadRequest("Hourly resolution can only be queried for a maximum of a month.");
            else if (resolution == TimeSeriesResolution.Daily && endTime - startTime > TimeSpan.FromDays(365 * 3))
                return BadRequest("Daily resolution can only be queried for a maximum of three years.");

            if (resolution != TimeSeriesResolution.Minutely && resolution != TimeSeriesResolution.Hourly && resolution != TimeSeriesResolution.Daily)
                return BadRequest("Invalid resolution.");

            MetaTime start = MetaTime.FromDateTime(startTime.Value);
            MetaTime end   = MetaTime.FromDateTime(endTime.Value);

            StatisticsPageTimeline timeline = await GetTimeline(resolution);

            end   = MetaTime.Min(MetaTime.Now, end);
            start = MetaTime.Max(start, timeline.EpochTime);

            if (end <= start)
            {
                return BadRequest($"{nameof(endTime)} must be after {nameof(startTime)}. Start time has been adjusted to the start of the timeline data: {start.ToString()}");
            }

            IReadableTimeSeries metricById = StatisticsTimeSeriesRegistryBase.Instance.GetMetricByIdReadableTimeseries(metricId);

            if (metricById == null || metricById.DashboardInfo == null)
            {
                return NotFound();
            }

            ISingleMetricResponse response = await GetResponseFromMetric(metricById, timeline, start, end);

            return Ok(response);
        }

        async Task<StatisticsPageTimeline> GetTimeline(TimeSeriesResolution resolution)
        {
            StatsCollectorStatisticsTimelineResponse timelinesResponse = await EntityAskAsync<StatsCollectorStatisticsTimelineResponse>(StatsCollectorManager.EntityId, StatsCollectorStatisticsTimelineRequest.Instance);

            StatisticsPageTimeline timeline;

            switch (resolution)
            {
                case TimeSeriesResolution.Minutely:
                    timeline = timelinesResponse.Minutely;
                    break;
                case TimeSeriesResolution.Hourly:
                    timeline = timelinesResponse.Hourly;
                    break;
                case TimeSeriesResolution.Daily:
                    timeline = timelinesResponse.Daily;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }

            return timeline;
        }

        async Task<ISingleMetricResponse> GetResponseFromMetric(IReadableTimeSeries timeSeries, StatisticsPageTimeline timeline, MetaTime startTime, MetaTime endTime)
        {
            if (!timeSeries.DashboardInfo.HasCohorts)
            {
                Type iReadableInterface = timeSeries.GetType().GetGenericInterface(typeof(ISimpleReadableTimeSeries<>));

                if (iReadableInterface == null)
                    throw new InvalidOperationException($"Failed to find {typeof(ISimpleReadableTimeSeries<>).Name} interface for {timeSeries.GetType().Name}");

                Type numberType = iReadableInterface.GetGenericArguments()[0];

                MethodInfo getResponseMethod = GetType().GetMethod(nameof(GetResponseFromSimpleMetric), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (getResponseMethod == null)
                    throw new InvalidOperationException($"Failed to find method {nameof(GetResponseFromSimpleMetric)}");

                MethodInfo genericMethod = getResponseMethod.MakeGenericMethod([numberType]);

                return await (genericMethod.Invoke(this, [timeSeries, timeline, startTime, endTime]) as Task<TimeseriesMetricResponse>);
            }
            else
            {
                Type iReadableInterface = timeSeries.GetType().GetGenericInterface(typeof(ICohortReadableTimeSeries<>));

                if (iReadableInterface == null)
                    throw new InvalidOperationException($"Failed to find {typeof(ICohortReadableTimeSeries<>).Name} interface for {timeSeries.GetType().Name}");

                Type numberType = iReadableInterface.GetGenericArguments()[0];

                MethodInfo getResponseMethod = GetType().GetMethod(nameof(GetResponseFromCohortMetric), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (getResponseMethod == null)
                    throw new InvalidOperationException($"Failed to find method {nameof(GetResponseFromCohortMetric)}");

                MethodInfo genericMethod = getResponseMethod.MakeGenericMethod([numberType]);

                return await (genericMethod.Invoke(this, [timeSeries, timeline, startTime, endTime]) as Task<CohortTimeSeriesResponse>);
            }
        }

        async Task<TimeseriesMetricResponse> GetResponseFromSimpleMetric<TNumber>(ISimpleReadableTimeSeries<TNumber> timeSeries, StatisticsPageTimeline timeline, MetaTime startTime, MetaTime endTime) where TNumber : struct, INumber<TNumber>
        {
            IStatisticsPageStorage storage = _statisticsPageReadStorage.Storage;

            StatisticsTimeSeriesView<TNumber> view = await timeSeries.GetReader(timeline, storage).ReadTimeSpan(startTime, endTime);

            MetaDictionary<MetaTime, double?> values = new MetaDictionary<MetaTime, double?>(view.NumBuckets);
            MetaTime                             time   = view.StartTime;

            for (int i = 0; i < view.NumBuckets; i++)
            {
                if (!view.Buckets[i].HasValue)
                {
                    if (timeSeries.DashboardInfo.CanBeNull)
                        values.Add(time, null);
                    else
                        values.Add(time, 0);
                }
                else
                {
                    TNumber bucketValue = view.Buckets[i].Value;
                    double? dbl         = double.CreateSaturating(bucketValue);
                    if (timeSeries.DashboardInfo.ValueTransformer != null)
                        dbl = timeSeries.DashboardInfo.ValueTransformer(dbl);
                    values.Add(time, dbl);
                }

                time += view.BucketInterval;
            }

            return new TimeseriesMetricResponse(timeSeries.Id, values, timeSeries.DashboardInfo);
        }

        /// <summary>
        /// Converts a cohort time series into a response object.
        /// The response object contains a dictionary of time -> dictionary of cohort -> value. :)
        /// </summary>
        /// <returns></returns>
        async Task<CohortTimeSeriesResponse> GetResponseFromCohortMetric<TNumber>(ICohortReadableTimeSeries<TNumber> timeSeries, StatisticsPageTimeline timeline, MetaTime startTime, MetaTime endTime)  where TNumber : struct, INumber<TNumber>
        {
            IStatisticsPageStorage storage = _statisticsPageReadStorage.Storage;

            string[] cohorts = timeSeries.Cohorts.ToArray();

            ITimeSeriesReader<TNumber>[] cohortReaders = cohorts.Select(c => timeSeries.GetReader(timeline, storage, c)).ToArray();

            MetaDictionary<MetaTime, MetaDictionary<string, double?>> Values = new MetaDictionary<MetaTime, MetaDictionary<string, double?>>();

            for (int i = 0; i < cohorts.Length; i++)
            {
                ITimeSeriesReader<TNumber> reader = cohortReaders[i];
                string                     cohort = cohorts[i];

                if (timeSeries.DashboardInfo.CohortTransformer != null)
                    cohort = timeSeries.DashboardInfo.CohortTransformer(cohort);

                StatisticsTimeSeriesView<TNumber> view = await reader.ReadTimeSpan(startTime, endTime);

                for (int j = 0; j < view.NumBuckets; j++)
                {
                    MetaTime t = view.GetTimeAtBucketIndex(j);
                    if (!Values.TryGetValue(t, out MetaDictionary<string, double?> cohortValues))
                    {
                        cohortValues = new MetaDictionary<string, double?>();
                        Values[t]    = cohortValues;
                    }

                    double? value = view.Buckets[j].HasValue ? double.CreateSaturating(view.Buckets[j].Value) : null;

                    if (timeSeries.DashboardInfo.ValueTransformer != null)
                        value = timeSeries.DashboardInfo.ValueTransformer(value);

                    if (!value.HasValue && !timeSeries.DashboardInfo.CanBeNull)
                        value = 0;

                    cohortValues[cohort] = value;
                }
            }

            return new CohortTimeSeriesResponse(timeSeries.Id, Values, timeSeries.DashboardInfo);
        }

        [HttpGet("metrics/category/{metricCategory}")]
        [RequirePermission(MetaplayPermissions.ApiMetricsView)]
        public async Task<ActionResult<SelectedMetricsResponse>> GetMetricsPerCategory(string metricCategory, TimeSeriesResolution resolution, DateTime? startTime, DateTime? endTime)
        {
            if (startTime == null || endTime == null)
            {
                return BadRequest($"{nameof(startTime)} or {nameof(endTime)} was not given.");
            }

            if (endTime <= startTime)
                return BadRequest($"{nameof(endTime)} must be after {nameof(startTime)}");

            if (resolution == TimeSeriesResolution.Minutely && endTime - startTime > TimeSpan.FromDays(1))
                return BadRequest("Minutely resolution can only be queried for a maximum of a single day.");
            else if (resolution == TimeSeriesResolution.Hourly && endTime - startTime > TimeSpan.FromDays(30))
                return BadRequest("Hourly resolution can only be queried for a maximum of a month.");
            else if (resolution == TimeSeriesResolution.Daily && endTime - startTime > TimeSpan.FromDays(365 * 3))
                return BadRequest("Daily resolution can only be queried for a maximum of three years.");

            if (resolution != TimeSeriesResolution.Minutely && resolution != TimeSeriesResolution.Hourly && resolution != TimeSeriesResolution.Daily)
                return BadRequest("Invalid resolution.");

            MetaTime start = MetaTime.FromDateTime(startTime.Value);
            MetaTime end   = MetaTime.FromDateTime(endTime.Value);

            StatisticsPageTimeline timeline = await GetTimeline(resolution);

            end   = MetaTime.Min(MetaTime.Now, end);
            start = MetaTime.Max(start, timeline.EpochTime);

            if (end <= start)
            {
                return BadRequest($"{nameof(endTime)} must be after {nameof(startTime)}. Start time has been adjusted to the start of the timeline data: {start.ToString()}");
            }

            List<IStatisticsTimeSeries> metrics          = StatisticsTimeSeriesRegistryBase.Instance.GetMetricsList();
            List<IStatisticsTimeSeries> dailyOnlyMetrics = StatisticsTimeSeriesRegistryBase.Instance.GetDailyOnlyMetricsList();
            List<ISingleMetricResponse> responses        = new List<ISingleMetricResponse>();

            if (metrics != null)
            {
                foreach (IStatisticsTimeSeries metric in metrics)
                {
                    if (IsMetricInCategory(metric, metricCategory))
                    {
                        if (dailyOnlyMetrics.Contains(metric) && resolution != TimeSeriesResolution.Daily)
                            continue;

                        ISingleMetricResponse response = await GetResponseFromMetric(metric, timeline, start, end);
                        responses.Add(response);
                    }
                }
            }

            return new SelectedMetricsResponse
            {
                Metrics = responses.ToArray(),
            };
        }

        static bool IsMetricInCategory(IStatisticsTimeSeries metric, string categoryId)
        {
            return string.Equals(metric.DashboardInfo.Category, categoryId, StringComparison.OrdinalIgnoreCase);
        }

        [HttpGet("metrics")]
        [RequirePermission(MetaplayPermissions.ApiMetricsView)]
        public async Task<ActionResult<MetricsDashboardInfoResponse>> GetMetricsDashboardInfos()
        {
            StatsCollectorStatisticsTimelineResponse timelinesResponse = await EntityAskAsync<StatsCollectorStatisticsTimelineResponse>(StatsCollectorManager.EntityId, StatsCollectorStatisticsTimelineRequest.Instance);

            DateTime                    epochTime   = timelinesResponse.Minutely.EpochTime.ToDateTime();
            List<IStatisticsTimeSeries> metrics     = StatisticsTimeSeriesRegistryBase.Instance.GetMetricsList();
            List<MetricInfo>            metricInfos = new List<MetricInfo>();

            if (metrics != null)
            {
                foreach (IStatisticsTimeSeries metric in metrics)
                {
                    metricInfos.Add(
                        new MetricInfo()
                        {
                            Id            = metric.Id,
                            DashboardInfo = metric.DashboardInfo,
                        });
                }
            }

            IEnumerable<string> categories = metricInfos.Select(m => m.DashboardInfo.Category).Distinct();

            List<StatisticsTimeSeriesRegistryBase.CategoryDefinition> categoryOrdering = StatisticsTimeSeriesRegistryBase.Instance.GetCategories().ToList();
            foreach (string category in categories)
            {
                if (categoryOrdering.Any(c => c.Name == category))
                    continue;

                categoryOrdering.Add(new StatisticsTimeSeriesRegistryBase.CategoryDefinition(category, 999));
            }

            return new MetricsDashboardInfoResponse
            {
                Metrics    = metricInfos.ToArray(),
                Categories = categoryOrdering.OrderBy(category => category.OrderIndex).ToArray(),
                EpochTime  = epochTime,
            };
        }
    }
}
