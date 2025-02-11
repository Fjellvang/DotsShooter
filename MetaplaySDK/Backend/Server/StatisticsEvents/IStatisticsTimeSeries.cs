// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Metaplay.Server.StatisticsEvents;

/// <summary>
/// A slice of time gathered from a time series.
/// The slice is a collection of buckets, each representing a time interval.
/// StartTime and EndTime are the time bounds of the slice,
/// and can be different from the time requested originally.
/// </summary>
public class StatisticsTimeSeriesView<TNumber> : IEnumerable<(MetaTime time, TNumber? value)>
    where TNumber : struct, INumber<TNumber>
{
    /// <summary>
    /// The timeline that the view is based on.
    /// Matches the timeline given when querying this view.
    /// </summary>
    public StatisticsPageTimeline Timeline   { get; init; }
    /// <summary>
    /// The inclusive start time of the view.
    /// </summary>
    public MetaTime               StartTime  { get; init; }
    /// <summary>
    /// The exclusive end time of the view.
    /// </summary>
    public MetaTime               EndTime    { get; init; }
    /// <summary>
    /// The number of buckets in the view.
    /// </summary>
    public int                    NumBuckets { get; init; }
    /// <summary>
    /// The values of the buckets.
    /// </summary>
    public TNumber?[]             Buckets    { get; init; }

    /// <summary>
    /// The interval of time between each bucket. Matches the resolution of the Timeline.
    /// </summary>
    public MetaDuration BucketInterval => Timeline.Resolution.BucketInterval;

    internal StatisticsTimeSeriesView(StatisticsPageTimeline timeline, MetaTime startTime, MetaTime endTime, int numBuckets,
        TNumber?[] buckets)
    {
        Timeline   = timeline;
        StartTime  = startTime;
        EndTime    = endTime;
        NumBuckets = numBuckets;
        Buckets    = buckets;
    }

    public MetaTime GetTimeAtBucketIndex(int index)
    {
        return StartTime + (index * BucketInterval);
    }

    public IEnumerator<(MetaTime time, TNumber? value)> GetEnumerator()
    {
        MetaTime time = StartTime;
        MetaDuration interval = BucketInterval;
        for (int i = 0; i < NumBuckets; i++)
        {
            yield return (time, Buckets[i]);
            time += interval;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class StatisticsTimeSeriesDashboardInfo
{
    public string Name                      { get; init; }
    public string PurposeDescription        { get; init; }
    public string ImplementationDescription { get; init; }
    public string Category                  { get; init; }
    public bool   HasCohorts                { get; init; }
    public string Unit                      { get; init; }
    public bool   UnitOnFront               { get; init; }
    public int    DecimalPrecision          { get; init; }
    public bool   IsHidden                  { get; init; }
    public float  YMinValue                 { get; init; }
    public bool   DailyOnly                 { get; init; }
    public int    OrderIndex                { get; init; } = 0;
    public bool   CanBeNull                 { get; init; } = false;

    [JsonIgnore]
    // \todo [nomi] Change to a enum and do the work on the dashboard? Or output a (double?, string) tuple?
    public Func<double?, double?> ValueTransformer  { get; init; }
    [JsonIgnore]
    public Func<string, string>   CohortTransformer { get; init; }

    StatisticsTimeSeriesDashboardInfo() { }

    StatisticsTimeSeriesDashboardInfo(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category, bool hasCohorts, int decimalPrecision, bool canBeNull)
    {
        Name                      = name;
        PurposeDescription        = purposeDescription;
        ImplementationDescription = implementationDescription;
        Category                  = category.Name;
        HasCohorts                = hasCohorts;
        DecimalPrecision          = decimalPrecision;
        CanBeNull                 = canBeNull;
    }

    /// <summary>
    /// Dashboard info preset for simple time series metrics.
    /// </summary>
    public static StatisticsTimeSeriesDashboardInfo Simple<TNumber>(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category) where TNumber : struct, INumber<TNumber>
    {
        return new StatisticsTimeSeriesDashboardInfo(
            name: name,
            purposeDescription: purposeDescription,
            implementationDescription: implementationDescription,
            category: category,
            hasCohorts: false,
            decimalPrecision: DefaultPrecision<TNumber>(),
            false);
    }

    /// <summary>
    /// A dashboard info preset for simple time series metrics that are displayed as percentages.
    /// The value transformer is set to multiply the values by 100, which means the raw values are expected to be in the range [0, 1].
    /// </summary>
    public static StatisticsTimeSeriesDashboardInfo SimplePercentage<TNumber>(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category) where TNumber : struct, INumber<TNumber>
    {
        return Simple<TNumber>(name, purposeDescription, implementationDescription, category)
            .WithValueTransformer((value) => value * 100)
            .WithUnit("%")
            .WithDecimalPrecision(2);
    }

    /// <summary>
    /// A dashboard info preset for time series metrics that contain cohorts.
    /// If the cohort metric is of type <see cref="StatisticsDailyCohortTimeSeries{TNumber}"/>, use the <see cref="DailyCohort{TNumber}"/> method instead.
    /// </summary>
    public static StatisticsTimeSeriesDashboardInfo Cohort<TNumber>(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category) where TNumber : struct, INumber<TNumber>
    {
        return new StatisticsTimeSeriesDashboardInfo(
            name: name,
            purposeDescription: purposeDescription,
            implementationDescription: implementationDescription,
            category: category,
            hasCohorts: true,
            decimalPrecision: DefaultPrecision<TNumber>(),
            true);
    }

    /// <summary>
    /// A dashboard info preset for time series metrics that contain cohorts and are displayed as percentages.
    /// The value transformer is set to multiply the values by 100, which means the raw values are expected to be in the range [0, 1].
    /// If the cohort metric is of type <see cref="StatisticsDailyCohortTimeSeries{TNumber}"/>, use the <see cref="DailyCohortPercentage{TNumber}"/> method instead.
    /// </summary>
    public static StatisticsTimeSeriesDashboardInfo CohortPercentage<TNumber>(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category) where TNumber : struct, INumber<TNumber>
    {
        return Cohort<TNumber>(name, purposeDescription, implementationDescription, category)
            .WithValueTransformer((value) => value * 100)
            .WithUnit("%")
            .WithDecimalPrecision(2);
    }

    /// <summary>
    /// A dashboard info preset for time series metrics that contain daily cohorts, i.e. a
    /// time series that of type <see cref="StatisticsDailyCohortTimeSeries{TNumber}"/>.
    /// </summary>
    public static StatisticsTimeSeriesDashboardInfo DailyCohort<TNumber>(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category) where TNumber : struct, INumber<TNumber>
    {
        return Cohort<TNumber>(name, purposeDescription, implementationDescription, category)
            .WithCohortTransformer((cohort) => $"D{cohort}")
            .WithDailyOnly(true);
    }

    /// <summary>
    /// A dashboard info preset for time series metrics that contain daily cohorts and are displayed as percentages.
    /// The value transformer is set to multiply the values by 100, which means the raw values are expected to be in the range [0, 1].
    /// </summary>
    public static StatisticsTimeSeriesDashboardInfo DailyCohortPercentage<TNumber>(string name, string purposeDescription, string implementationDescription, StatisticsTimeSeriesRegistryBase.CategoryDefinition category) where TNumber : struct, INumber<TNumber>
    {
        return CohortPercentage<TNumber>(name, purposeDescription, implementationDescription, category)
            .WithCohortTransformer((cohort) => $"D{cohort}")
            .WithDailyOnly(true);
    }

    #region WithMethods

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified unit.
    /// The unit is appended, or prepended if <paramref name="unitOnFront"/> is true, to the metric value.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithUnit(string unit, bool unitOnFront = false)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = unit,
            UnitOnFront               = unitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified decimal precision.
    /// The decimal precision sets the number of decimal places to display for the metric.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithDecimalPrecision(int decimalPrecision)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = decimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified value transformer.
    /// The value transformer is used to transform the raw value before displaying it on the dashboard.
    /// This is useful for transforming the value of a percentage from [0, 1] to [0, 100], or a
    /// time in milliseconds to a time in minutes.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithValueTransformer(Func<double?, double?> transformer)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = transformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified cohort transformer.
    /// The transformer is used to transform the cohort key to a more readable format, or to add a prefix or suffix.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithCohortTransformer(Func<string, string> transformer)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = transformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified hidden flag.
    /// The flag is used to indicate whether the metric should be hidden from the dashboard.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithHidden(bool isHidden)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = isHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified minimum y value.
    /// The value is used to set the graph's y-axis minimum value.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithYMinValue(float minValue)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = minValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified daily only flag.
    /// The flag is used to indicate to the dashboard that the metric is only available in the daily resolution.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithDailyOnly(bool dailyOnly)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = dailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified order index.
    /// The order index is used to sort the metrics on the dashboard.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithOrderIndex(int orderIndex)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = orderIndex,
            CanBeNull                 = CanBeNull,
        };
    }

    /// <summary>
    /// Returns a new instance of the dashboard info with the specified can be null flag.
    /// The flag is used to indicate whether the metric values can be null.
    /// If not, any null values will be replaced with 0.
    /// </summary>
    public StatisticsTimeSeriesDashboardInfo WithCanBeNull(bool canBeNull)
    {
        return new StatisticsTimeSeriesDashboardInfo
        {
            Name                      = Name,
            Category                  = Category,
            PurposeDescription        = PurposeDescription,
            ImplementationDescription = ImplementationDescription,
            HasCohorts                = HasCohorts,
            CohortTransformer         = CohortTransformer,
            ValueTransformer          = ValueTransformer,
            DecimalPrecision          = DecimalPrecision,
            Unit                      = Unit,
            UnitOnFront               = UnitOnFront,
            IsHidden                  = IsHidden,
            YMinValue                 = YMinValue,
            DailyOnly                 = DailyOnly,
            OrderIndex                = OrderIndex,
            CanBeNull                 = canBeNull,
        };
    }

    #endregion

    static int DefaultPrecision<TNumber>() where TNumber : struct, INumber<TNumber>
    {
        switch (default(TNumber))
        {
            case float:
                return 2;
            case double:
                return 2;
            case decimal:
                return 4;
            default:
                return 0;
        }
    }
}

/// <summary>
/// An interface for accumulating events into a time series bucket.
/// The <see cref="StatisticsTimeSeriesPageWriter"/> uses this interface to let the
/// time series write events into the correct bucket.
/// </summary>
public interface IStatisticsTimeSeriesBucketAccumulator<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    /// <summary>
    /// This determines if the time series is nullable.
    /// If true, the page sections that are created for the series will also be nullable.
    /// </summary>
    bool IsNullable { get; }

    void AccumulateEvent(StatisticsEventBase statisticsEvent, IStatisticsTimeSeriesBucketWriter<TNumber> bucket);
}

/// <summary>
/// The means for a time series to write values into a bucket.
/// </summary>
public interface IStatisticsTimeSeriesBucketWriter<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    /// <summary>
    /// The value of the bucket or null if not set.
    /// REMEMBER: Math operations on nullable numbers become null if either operand is null!
    /// </summary>
    TNumber? Value { get; set; }

    /// <summary>
    /// Get a cohort value, or null if not set.
    /// A cohort can be used for non-cohort metrics as well,
    /// for example to store count and sum separately to later calculate an average.
    /// </summary>
    TNumber? GetCohort(string cohort);

    /// <summary>
    /// Set a cohort value, creating a section for the cohort if it doesn't exist.
    /// A cohort can be used for non-cohort metrics as well,
    /// for example to store count and sum separately to later calculate an average.
    /// </summary>
    void SetCohort(string cohort, TNumber? value);
}

public interface IStatisticsTimeSeries: IReadableTimeSeries
{
    string SeriesKey { get; }
}

public interface IStatisticsTimeSeries<TNumber> : IStatisticsTimeSeries
    where TNumber : struct, INumber<TNumber>
{

}

public interface ITimeSeriesReader<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    StatisticsPageTimeline Timeline { get; }
    IStatisticsPageStorage Storage  { get; }

    Task<TNumber?> Read(MetaTime time);
    Task<StatisticsTimeSeriesView<TNumber>> ReadTimeSpan(MetaTime startTime, MetaTime endTime);
}

public abstract class TimeSeriesReaderBase<TNumber> : ITimeSeriesReader<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    public StatisticsPageTimeline Timeline { get; }
    public IStatisticsPageStorage Storage  { get; }

    protected TimeSeriesReaderBase(StatisticsPageTimeline timeline, IStatisticsPageStorage storage)
    {
        Timeline = timeline;
        Storage  = storage;
    }

    public abstract Task<TNumber?> Read(MetaTime time);
    public abstract Task<StatisticsTimeSeriesView<TNumber>> ReadTimeSpan(MetaTime startTime, MetaTime endTime);

    protected async Task<TNumber?> ReadBucket(StatisticsBucketIndex bucket, string pageKey, string seriesKey, string cohort)
    {
        StatisticsPageIndex   pageIndex   = bucket.Page;

        string pageId = StatisticsStoragePage.BuildUniqueId(Timeline, pageKey, pageIndex.PageIndex);

        StatisticsStoragePage page = await Storage.TryGetAsync(pageId);
        if (page == null)
            return null;

        StatisticsPageSectionBase section = page.TryGetSection(seriesKey, cohort);
        if (section == null)
            return null;

        if (section.IsNullable && !section.HasValues[bucket.BucketIndex])
            return null;

        return section.As<TNumber>().Buckets[bucket.BucketIndex];
    }

    protected async Task<IEnumerable<TNumber?>> ReadBuckets(StatisticsBucketIndex[] buckets, string pageKey, string seriesKey, string cohort)
    {
        // Kludge to avoid reading future buckets
        // \todo [nomi] Better solution?
        MetaTime                           now              = MetaTime.Now;
        IEnumerable<StatisticsBucketIndex> nonFutureBuckets = buckets.Where(b => Timeline.GetBucketStartTime(b) <= now);

        IEnumerable<StatisticsPageIndex> pageIndices = nonFutureBuckets.Select(b => b.Page).DistinctBy(p => p.PageIndex);

        StatisticsStoragePage[] pages = await Task.WhenAll(
            pageIndices.Select(
                async pIdx =>
                {
                    string                pageId = StatisticsStoragePage.BuildUniqueId(Timeline, pageKey, pIdx.PageIndex);
                    StatisticsStoragePage page   = await Storage.TryGetAsync(pageId);
                    return page;
                }));

        Dictionary<int, StatisticsPageSectionBase> sections = pages
            .Where(p => p != null)
            .Select(p => (p.PageIndex, p.TryGetSection(seriesKey, cohort)))
            .Where(item => item.Item2 != null)
            .ToDictionary();

        return buckets.Select(
            b =>
            {
                // Kludge part 2
                if (Timeline.GetBucketStartTime(b) > now)
                    return null;

                StatisticsPageSectionBase section = sections.GetValueOrDefault(b.Page.PageIndex);
                if (section == null)
                    return null;
                if (section.IsNullable && !section.HasValues[b.BucketIndex])
                    return null;

                return (TNumber?)section.As<TNumber>().Buckets[b.BucketIndex];
            });
    }
}

public interface IReadableTimeSeries
{
    /// <summary>
    /// Dashboard visible id of the metric.
    /// </summary>
    string                            Id            { get; }
    StatisticsTimeSeriesDashboardInfo DashboardInfo { get; }
}

public interface ISimpleReadableTimeSeries<TNumber> : IReadableTimeSeries
    where TNumber : struct, INumber<TNumber>
{
    ITimeSeriesReader<TNumber> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage);
}

public interface ICohortReadableTimeSeries<TNumber> : IReadableTimeSeries
    where TNumber : struct, INumber<TNumber>
{
    public IReadOnlyCollection<string> Cohorts { get; }


    ITimeSeriesReader<TNumber> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage, string cohort);
}

public interface IWriteableStatisticsTimeSeries : IStatisticsTimeSeries
{
    IReadOnlyCollection<Type> EventTypes     { get; }
    string                    StoragePageKey { get; }
}

public interface IWriteableStatisticsTimeSeries<TNumber> : IWriteableStatisticsTimeSeries, IStatisticsTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    IStatisticsTimeSeriesBucketAccumulator<TNumber> GetBucketAccumulator();
}
