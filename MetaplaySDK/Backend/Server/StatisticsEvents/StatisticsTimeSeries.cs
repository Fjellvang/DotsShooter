// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Metaplay.Server.StatisticsEvents;

public class StatisticsTimeSeriesSetupParams
{
    public string SeriesKey { get; init; }

    public StatisticsTimeSeriesSetupParams(string seriesKey)
    {
        if (string.IsNullOrWhiteSpace(seriesKey)) throw new ArgumentNullException(nameof(seriesKey));

        SeriesKey = seriesKey;
    }
}

public class WriteableStatisticsTimeSeriesSetupParams : StatisticsTimeSeriesSetupParams
{
    public WriteableStatisticsTimeSeriesSetupParams(Type[] eventTypes, string seriesKey, string storagePageKey = null) :
        base(seriesKey: seriesKey)
    {
        EventTypes     = eventTypes;
        StoragePageKey = storagePageKey;
        SeriesKey      = seriesKey;
    }

    public Type[] EventTypes     { get; init; }
    public string StoragePageKey { get; init; }

}

public abstract class StatisticsTimeSeriesBase<TNumber> : IStatisticsTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    public string                            Id            { get; }
    public string                            SeriesKey     { get; }
    public StatisticsTimeSeriesDashboardInfo DashboardInfo { get; }

    protected StatisticsTimeSeriesBase(StatisticsTimeSeriesSetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo)
    {
        Id            = setupParams.SeriesKey;
        SeriesKey     = setupParams.SeriesKey;
        DashboardInfo = dashInfo;

        if (dashInfo != null && this is ISimpleReadableTimeSeries<TNumber>)
            MetaDebug.Assert(!dashInfo.HasCohorts, "DashboardInfo must have HasCohorts set to false for series {0}", SeriesKey);
        else if (dashInfo != null && this is ICohortReadableTimeSeries<TNumber>)
            MetaDebug.Assert(dashInfo.HasCohorts, "DashboardInfo must have HasCohorts set to true for series {0}", SeriesKey);
    }

    public abstract IStatisticsTimeSeriesBucketAccumulator<TNumber> GetBucketAccumulator();
}

public abstract class WriteableStatisticsTimeSeriesBase<TNumber> : StatisticsTimeSeriesBase<TNumber>, IWriteableStatisticsTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    public IReadOnlyCollection<Type> EventTypes     { get; init; }
    public string                    StoragePageKey { get; init; }

    protected WriteableStatisticsTimeSeriesBase(WriteableStatisticsTimeSeriesSetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) : base(setupParams, dashInfo)
    {
        EventTypes     = setupParams.EventTypes ?? Array.Empty<Type>();
        StoragePageKey = setupParams.StoragePageKey ?? "";
    }
}

public enum StatisticsBucketAccumulationMode
{
    Count,
    Sum,
    Avg,
    Min,
    Max,
}

public class StatisticsSimpleTimeSeries<TNumber> : WriteableStatisticsTimeSeriesBase<TNumber>, ISimpleReadableTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    const string CountCohortKey = "count";

    public class SetupParams : WriteableStatisticsTimeSeriesSetupParams
    {
        public StatisticsBucketAccumulationMode    StatisticsAccumulationMode { get; init; }
        public Func<StatisticsEventBase, TNumber?> EventParser                { get; init; }

        public SetupParams(Type[] eventTypes, string seriesKey, StatisticsBucketAccumulationMode mode, Func<StatisticsEventBase, TNumber?> eventParser = null, string storagePageKey = null) :
            base(
            eventTypes: eventTypes,
            seriesKey: seriesKey,
            storagePageKey: storagePageKey)
        {
            StatisticsAccumulationMode = mode;
            EventParser                = eventParser;
        }
    }

    class BucketAccumulator : IStatisticsTimeSeriesBucketAccumulator<TNumber>
    {
        private readonly StatisticsSimpleTimeSeries<TNumber> _series;

        public BucketAccumulator(StatisticsSimpleTimeSeries<TNumber> series)
        {
            this._series = series;
        }

        bool IStatisticsTimeSeriesBucketAccumulator<TNumber>.IsNullable => _series._statisticsAccumulationMode
            is StatisticsBucketAccumulationMode.Avg
            or StatisticsBucketAccumulationMode.Min
            or StatisticsBucketAccumulationMode.Max;

        public void AccumulateEvent(StatisticsEventBase statisticsEvent, IStatisticsTimeSeriesBucketWriter<TNumber> bucket)
        {
            TNumber? num = _series._eventParser?.Invoke(statisticsEvent);

            if (_series._statisticsAccumulationMode != StatisticsBucketAccumulationMode.Count && !num.HasValue)
                return;

            switch (_series._statisticsAccumulationMode)
            {
                case StatisticsBucketAccumulationMode.Count:
                {
                    // Nullable++ equals null, so this part is ugly
                    bucket.Value = ++bucket.Value ?? TNumber.One;
                    break;
                }
                case StatisticsBucketAccumulationMode.Sum:
                {
                    // Same excuse as above
                    bucket.Value = bucket.Value + num ?? num;
                    break;
                }
                case StatisticsBucketAccumulationMode.Avg:
                {
                    TNumber? count = bucket.GetCohort(CountCohortKey);
                    count = ++count ?? TNumber.One;
                    bucket.SetCohort(CountCohortKey, count);

                    bucket.Value = bucket.Value + num ?? num;

                    break;
                }
                case StatisticsBucketAccumulationMode.Min:
                {
                    if (bucket.Value.HasValue)
                        bucket.Value = TNumber.Min(num!.Value, bucket.Value.Value);
                    else
                        bucket.Value = num;
                    break;
                }
                case StatisticsBucketAccumulationMode.Max:
                {
                    if (bucket.Value.HasValue)
                        bucket.Value = TNumber.Max(num!.Value, bucket.Value.Value);
                    else
                        bucket.Value = num;
                    break;
                }
                default:
                    throw new InvalidOperationException($"Invalid statistics accumulation mode: {_series._statisticsAccumulationMode}");
            }
        }
    }

    class Reader : TimeSeriesReaderBase<TNumber>
    {
        StatisticsSimpleTimeSeries<TNumber> _series;

        public Reader(StatisticsSimpleTimeSeries<TNumber> series, StatisticsPageTimeline timeline, IStatisticsPageStorage storage)
            : base(timeline, storage)
        {
            _series  = series;
        }

        // Calculate average
        static TNumber? CalculateAverage(TNumber? sum, TNumber? count)
        {
            if (sum.HasValue && count.HasValue && count.Value > TNumber.Zero)
                return sum / count;

            return null;
        }

        public override async Task<TNumber?> Read(MetaTime time)
        {
            TNumber? value = await ReadBucket(Timeline.GetBucketIndex(time), _series.StoragePageKey, _series.SeriesKey, null);
            if (value.HasValue && _series._statisticsAccumulationMode == StatisticsBucketAccumulationMode.Avg)
            {
                TNumber? count = await ReadBucket(Timeline.GetBucketIndex(time), _series.StoragePageKey, _series.SeriesKey, CountCohortKey);
                value = CalculateAverage(value, count);
            }
            return value;
        }

        public override async Task<StatisticsTimeSeriesView<TNumber>> ReadTimeSpan(MetaTime startTime, MetaTime endTime)
        {
            StatisticsBucketIndex[] allIndexes = Timeline.GetBucketsForTimeRange(startTime, endTime).ToArray();

            TNumber?[] buckets = (await ReadBuckets(allIndexes, _series.StoragePageKey, _series.SeriesKey, null)).ToArray();

            if (_series._statisticsAccumulationMode == StatisticsBucketAccumulationMode.Avg)
            {
                IEnumerable<TNumber?> counts = await ReadBuckets(allIndexes, _series.StoragePageKey, _series.SeriesKey, CountCohortKey);

                buckets = buckets.Zip(counts, CalculateAverage).ToArray();
            }

            return new StatisticsTimeSeriesView<TNumber>(
                Timeline,
                Timeline.GetBucketStartTime(allIndexes[0]),
                Timeline.GetBucketEndTime(allIndexes[^1]),
                buckets.Length,
                buckets);
        }
    }

    StatisticsBucketAccumulationMode    _statisticsAccumulationMode;
    Func<StatisticsEventBase, TNumber?> _eventParser;

    public StatisticsSimpleTimeSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) :
        base(setupParams, dashInfo)
    {
        if (setupParams.EventTypes == null && setupParams.StatisticsAccumulationMode != StatisticsBucketAccumulationMode.Count)
            throw new ArgumentException($"If {nameof(setupParams.EventParser)} is left blank, the accumulation mode needs to be set to Count", nameof(setupParams));

        _statisticsAccumulationMode = setupParams.StatisticsAccumulationMode;
        _eventParser                = setupParams.EventParser;
    }

    public override IStatisticsTimeSeriesBucketAccumulator<TNumber> GetBucketAccumulator()
    {
        return new BucketAccumulator(this);
    }

    public ITimeSeriesReader<TNumber> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage)
    {
        return new Reader(this, timeline, storage);
    }
}

public class StatisticsSimpleTimeSeries<TEvent, TNumber> : StatisticsSimpleTimeSeries<TNumber>
    where TEvent : StatisticsEventBase
    where TNumber : struct, INumber<TNumber>
{
    public new class SetupParams : StatisticsSimpleTimeSeries<TNumber>.SetupParams
    {
        public SetupParams(string seriesKey, StatisticsBucketAccumulationMode mode, Func<TEvent, TNumber?> eventParser = null, string storagePageKey = null) :
            base([typeof(TEvent)], seriesKey, mode,
                ev => eventParser?.Invoke((TEvent)ev), storagePageKey)
        { }
    }

    public StatisticsSimpleTimeSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) : base(setupParams, dashInfo) { }
}

public class StatisticsCohortTimeSeries<TNumber> : WriteableStatisticsTimeSeriesBase<TNumber>, ICohortReadableTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    public class SetupParams : WriteableStatisticsTimeSeriesSetupParams
    {
        public string[]                                                   Cohorts     { get; init; }
        public Func<StatisticsEventBase, (string cohort, TNumber? value)> EventParser { get; init; }

        public SetupParams(
            Type[] eventTypes,
            string seriesKey,
            string storagePageKey,
            string[] cohorts,
            Func<StatisticsEventBase, (string cohort, TNumber? value)> eventParser
            ) : base(eventTypes, seriesKey, storagePageKey)
        {
            Cohorts = cohorts ?? throw new ArgumentNullException(nameof(cohorts));
            EventParser = eventParser ?? throw new ArgumentNullException(nameof(eventParser));
        }
    }

    class BucketAccumulator : IStatisticsTimeSeriesBucketAccumulator<TNumber>
    {
        StatisticsCohortTimeSeries<TNumber> _series;

        public bool IsNullable => false;

        public BucketAccumulator(StatisticsCohortTimeSeries<TNumber> series)
        {
            _series = series;
        }

        public void AccumulateEvent(StatisticsEventBase statisticsEvent, IStatisticsTimeSeriesBucketWriter<TNumber> bucket)
        {
            (string cohort, TNumber? value) = _series.EventParser(statisticsEvent);

            if (!value.HasValue || string.IsNullOrWhiteSpace(cohort))
                return;

            MetaDebug.Assert(_series.Cohorts.Contains(cohort), "Event parser produced an unknown cohort in series:{0} cohort,value: ({1}), expected: [{2}], event: {3}",
                _series.SeriesKey, $"{cohort}, {value.ToString()}", string.Join(',', _series.Cohorts), PrettyPrinter.Compact(statisticsEvent).ToString());
            
            // \todo [nomi]: Support other bucket accumulation modes?

            TNumber? num = bucket.GetCohort(cohort);
            num = num + value ?? value;
            bucket.SetCohort(cohort, num);
        }
    }

    class Reader : TimeSeriesReaderBase<TNumber>
    {
        StatisticsCohortTimeSeries<TNumber> _series;
        string                              _cohort;

        public Reader(StatisticsCohortTimeSeries<TNumber> series, StatisticsPageTimeline timeline, IStatisticsPageStorage storage, string cohort) : base(timeline, storage)
        {
            _series = series;
            _cohort = cohort;
        }

        public override Task<TNumber?> Read(MetaTime time)
        {
            StatisticsBucketIndex bucketIndex = Timeline.GetBucketIndex(time);

            return ReadBucket(bucketIndex, _series.StoragePageKey, _series.SeriesKey, _cohort);
        }

        public override async Task<StatisticsTimeSeriesView<TNumber>> ReadTimeSpan(MetaTime startTime, MetaTime endTime)
        {
            StatisticsBucketIndex[] allIndexes = Timeline.GetBucketsForTimeRange(startTime, endTime).ToArray();

            TNumber?[] buckets = (await ReadBuckets(allIndexes, _series.StoragePageKey, _series.SeriesKey, _cohort)).ToArray();

            return new StatisticsTimeSeriesView<TNumber>(
                Timeline,
                Timeline.GetBucketStartTime(allIndexes[0]),
                Timeline.GetBucketEndTime(allIndexes[^1]),
                buckets.Length,
                buckets);
        }
    }

    public StatisticsCohortTimeSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) : base(setupParams, dashInfo)
    {
        Cohorts      = setupParams.Cohorts;
        EventParser = setupParams.EventParser;
    }

    public override IStatisticsTimeSeriesBucketAccumulator<TNumber> GetBucketAccumulator()
    {
        return new BucketAccumulator(this);
    }

    public IReadOnlyCollection<string> Cohorts { get; }

    Func<StatisticsEventBase, (string cohort, TNumber? value)> EventParser { get; }

    public virtual ITimeSeriesReader<TNumber> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage, string cohort)
    {
        return new Reader(this, timeline, storage, cohort);
    }
}

public class StatisticsCohortTimeSeries<TEvent, TNumber> : StatisticsCohortTimeSeries<TNumber>
    where TEvent : StatisticsEventBase
    where TNumber : struct, INumber<TNumber>
{
    public new class SetupParams : StatisticsCohortTimeSeries<TNumber>.SetupParams
    {
        public SetupParams(string seriesKey, string storagePageKey, string[] cohorts, Func<TEvent, (string, TNumber?)> eventParser) :
            base(
                [typeof(TEvent)],
                seriesKey,
                storagePageKey,
                cohorts,
                ev => eventParser!.Invoke((TEvent)ev))
        {
            ArgumentNullException.ThrowIfNull(eventParser);
        }
    }

    public StatisticsCohortTimeSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) : base(setupParams, dashInfo) { }
}

public class StatisticsDailyCohortTimeSeries<TNumber> : StatisticsCohortTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
{
    public new class SetupParams : StatisticsCohortTimeSeries<TNumber>.SetupParams
    {
        public int[] DailyCohorts { get; }
        public SetupParams(Type[] eventTypes, string seriesKey, string storagePageKey, int[] dailyCohorts, Func<StatisticsEventBase, (int? day, TNumber? value)> eventParser) :
            base(
                eventTypes,
                seriesKey,
                storagePageKey,
                dailyCohorts.Select(d => d.ToString(CultureInfo.InvariantCulture)).ToArray(),
                ev =>
                {
                    (int? day, TNumber? value) = eventParser(ev);
                    return (day?.ToString(CultureInfo.InvariantCulture), value);
                })
        {
            DailyCohorts = dailyCohorts;
            ArgumentNullException.ThrowIfNull(eventParser);
        }
    }

    class FutureLookingReader : TimeSeriesReaderBase<TNumber>
    {
        readonly StatisticsCohortTimeSeries<TNumber> _series;
        readonly int                                 _cohort;
        readonly string                              _cohortStr;
        readonly MetaDuration                        _readOffset;

        public FutureLookingReader(StatisticsCohortTimeSeries<TNumber> series, StatisticsPageTimeline timeline, IStatisticsPageStorage storage, int cohort, string cohortStr) : base(timeline, storage)
        {
            _series     = series;
            _cohort     = cohort;
            _cohortStr  = cohortStr;
            _readOffset = MetaDuration.FromDays(cohort);
        }

        public override Task<TNumber?> Read(MetaTime time)
        {
            StatisticsBucketIndex bucketIndex = Timeline.GetBucketIndex(time + _readOffset);

            return ReadBucket(bucketIndex, _series.StoragePageKey, _series.SeriesKey, _cohortStr);
        }

        public override async Task<StatisticsTimeSeriesView<TNumber>> ReadTimeSpan(MetaTime startTime, MetaTime endTime)
        {
            StatisticsBucketIndex[] allIndexes = Timeline.GetBucketsForTimeRange(startTime + _readOffset, endTime + _readOffset).ToArray();

            TNumber?[] buckets = (await ReadBuckets(allIndexes, _series.StoragePageKey, _series.SeriesKey, _cohortStr)).ToArray();

            return new StatisticsTimeSeriesView<TNumber>(
                Timeline,
                Timeline.GetBucketStartTime(allIndexes[0]) - _readOffset,
                Timeline.GetBucketEndTime(allIndexes[^1]) - _readOffset,
                buckets.Length,
                buckets);
        }
    }

    public IReadOnlyList<int> DailyCohorts { get; }

    public StatisticsDailyCohortTimeSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) : base(setupParams, dashInfo)
    {
        DailyCohorts = setupParams.DailyCohorts;
    }

    public override ITimeSeriesReader<TNumber> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage, string cohort)
    {
        if (!int.TryParse(cohort, CultureInfo.InvariantCulture, out int cohortInt))
            throw new ArgumentException($"Invalid daily cohort: {cohort}! Cannot parse.");
        if (!DailyCohorts.Contains(cohortInt))
            throw new ArgumentException($"Invalid daily cohort: {cohort}! Not in list of cohorts.");
        if (timeline.Resolution.BucketInterval != MetaDuration.Day)
            throw new ArgumentException("Invalid timeline for daily cohorts. Bucket interval must be a day!");


        return new FutureLookingReader(this, timeline, storage, cohortInt, cohort);
    }
}

public class StatisticsDailyCohortTimeSeries<TEvent, TNumber> : StatisticsDailyCohortTimeSeries<TNumber>
    where TNumber : struct, INumber<TNumber>
    where TEvent : StatisticsEventBase
{
    public new class SetupParams : StatisticsDailyCohortTimeSeries<TNumber>.SetupParams
    {
        public SetupParams(string seriesKey, string storagePageKey, int[] dailyCohorts,
            Func<TEvent, (int? day, TNumber? value)> eventParser) : base(
            [typeof(TEvent)],
            seriesKey,
            storagePageKey,
            dailyCohorts,
            ev => eventParser((TEvent)ev))
        {
            ArgumentNullException.ThrowIfNull(eventParser);
        }
    }

    public StatisticsDailyCohortTimeSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo) : base(setupParams, dashInfo) { }
}

public class CombinedTimeSeriesReader<TNumber1, TNumber2, TNumberOut>  : ITimeSeriesReader<TNumberOut>
    where TNumber1 : struct, INumber<TNumber1>
    where TNumber2 : struct, INumber<TNumber2>
    where TNumberOut : struct, INumber<TNumberOut>
{
    readonly Func<TNumber1?, TNumber2?, TNumberOut?>                        _combiner;
    readonly ITimeSeriesReader<TNumber1>                                    _reader1;
    readonly ITimeSeriesReader<TNumber2>                                    _reader2;

    public StatisticsPageTimeline Timeline { get; }
    public IStatisticsPageStorage Storage  { get; }

    public CombinedTimeSeriesReader(Func<TNumber1?, TNumber2?, TNumberOut?> combiner, ITimeSeriesReader<TNumber1> reader1, ITimeSeriesReader<TNumber2> reader2, StatisticsPageTimeline timeline,
        IStatisticsPageStorage storage)
    {
        _combiner = combiner ?? throw new ArgumentNullException(nameof(combiner));
        _reader1  = reader1 ?? throw new ArgumentNullException(nameof(reader1));
        _reader2  = reader2 ?? throw new ArgumentNullException(nameof(reader2));
        Timeline  = timeline ?? throw new ArgumentNullException(nameof(timeline));
        Storage   = storage ?? throw new ArgumentNullException(nameof(storage));

        if (_reader1.Timeline != Timeline)
            throw new ArgumentException("Timelines must match!");
        if (_reader2.Timeline != Timeline)
            throw new ArgumentException("Timelines must match!");
    }

    public async Task<TNumberOut?> Read(MetaTime time)
    {
        TNumber1? n1 = await _reader1.Read(time);
        TNumber2? n2 = await _reader2.Read(time);
        return _combiner(n1, n2);
    }

    public async Task<StatisticsTimeSeriesView<TNumberOut>> ReadTimeSpan(MetaTime startTime, MetaTime endTime)
    {
        StatisticsTimeSeriesView<TNumber1> view1 = await _reader1.ReadTimeSpan(startTime, endTime);
        StatisticsTimeSeriesView<TNumber2> view2 = await _reader2.ReadTimeSpan(startTime, endTime);

        TNumberOut?[] buckets = view1.Buckets.Zip(view2.Buckets, _combiner).ToArray();

        return new StatisticsTimeSeriesView<TNumberOut>(
            view1.Timeline,
            view1.StartTime,
            view1.EndTime,
            buckets.Length,
            buckets);
    }
}

public class StatisticsSimpleCombinedSeries<TNumber1, TNumber2, TNumberOut> : IStatisticsTimeSeries<TNumberOut>, ISimpleReadableTimeSeries<TNumberOut>
    where TNumber1 : struct, INumber<TNumber1>
    where TNumber2 : struct, INumber<TNumber2>
    where TNumberOut : struct, INumber<TNumberOut>
{
    public class SetupParams : StatisticsTimeSeriesSetupParams
    {
        public ISimpleReadableTimeSeries<TNumber1>    Series1 { get; }
        public ISimpleReadableTimeSeries<TNumber2>    Series2 { get; }
        public Func<TNumber1?, TNumber2?, TNumberOut?> BucketCombiner { get; }

        public SetupParams(string seriesKey, ISimpleReadableTimeSeries<TNumber1> series1, ISimpleReadableTimeSeries<TNumber2> series2,
            Func<TNumber1?, TNumber2?, TNumberOut?> bucketCombiner) : base(seriesKey)
        {
            Series1        = series1 ?? throw new ArgumentNullException(nameof(series1));
            Series2        = series2 ?? throw new ArgumentNullException(nameof(series2));
            BucketCombiner = bucketCombiner ?? throw new ArgumentNullException(nameof(bucketCombiner));
        }
    }

    public string                            Id            { get; }
    public StatisticsTimeSeriesDashboardInfo DashboardInfo { get; }
    public string                            SeriesKey     { get; }

    ISimpleReadableTimeSeries<TNumber1> _series1;
    ISimpleReadableTimeSeries<TNumber2> _series2;

    Func<TNumber1?, TNumber2?, TNumberOut?> _bucketCombiner;

    public StatisticsSimpleCombinedSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo)
    {
        Id              = setupParams.SeriesKey;
        SeriesKey       = setupParams.SeriesKey;
        DashboardInfo   = dashInfo;
        _series1        = setupParams.Series1;
        _series2        = setupParams.Series2;
        _bucketCombiner = setupParams.BucketCombiner;

        if (dashInfo != null)
            MetaDebug.Assert(!dashInfo.HasCohorts, "DashboardInfo must have HasCohorts set to false for series {0}", SeriesKey);
    }

    public ITimeSeriesReader<TNumberOut> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage)
    {
        ITimeSeriesReader<TNumber1> reader1 = _series1.GetReader(timeline, storage);
        ITimeSeriesReader<TNumber2> reader2 = _series2.GetReader(timeline, storage);

        return new CombinedTimeSeriesReader<TNumber1,TNumber2,TNumberOut>(_bucketCombiner, reader1, reader2, timeline, storage);
    }

}


public class StatisticsCohortCombinedSeries<TNumber1, TNumber2, TNumberOut> : IStatisticsTimeSeries<TNumberOut>, ICohortReadableTimeSeries<TNumberOut>
    where TNumber1 : struct, INumber<TNumber1>
    where TNumber2 : struct, INumber<TNumber2>
    where TNumberOut : struct, INumber<TNumberOut>
{
    public class SetupParams : StatisticsTimeSeriesSetupParams
    {
        public ICohortReadableTimeSeries<TNumber1>     Series1        { get; }
        public ICohortReadableTimeSeries<TNumber2>     Series2        { get; }
        public Func<TNumber1?, TNumber2?, TNumberOut?> BucketCombiner { get; }

        public SetupParams(string seriesKey, ICohortReadableTimeSeries<TNumber1> series1, ICohortReadableTimeSeries<TNumber2> series2,
            Func<TNumber1?, TNumber2?, TNumberOut?> bucketCombiner) : base(seriesKey)
        {
            Series1        = series1 ?? throw new ArgumentNullException(nameof(series1));
            Series2        = series2 ?? throw new ArgumentNullException(nameof(series2));
            BucketCombiner = bucketCombiner ?? throw new ArgumentNullException(nameof(bucketCombiner));

            if (!series1.Cohorts.SequenceEqual(series2.Cohorts))
                throw new ArgumentException("Both series must have the same cohorts!");
        }
    }

    public string                            Id            { get; }
    public StatisticsTimeSeriesDashboardInfo DashboardInfo { get; }
    public string                            SeriesKey     { get; }

    ICohortReadableTimeSeries<TNumber1> _series1;
    ICohortReadableTimeSeries<TNumber2> _series2;

    Func<TNumber1?, TNumber2?, TNumberOut?> _bucketCombiner;

    public IReadOnlyCollection<string> Cohorts => _series1.Cohorts;

    public StatisticsCohortCombinedSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo)
    {
        Id              = setupParams.SeriesKey;
        SeriesKey       = setupParams.SeriesKey;
        DashboardInfo   = dashInfo;
        _series1        = setupParams.Series1;
        _series2        = setupParams.Series2;
        _bucketCombiner = setupParams.BucketCombiner;

        if (dashInfo != null)
            MetaDebug.Assert(dashInfo.HasCohorts, "DashboardInfo must have HasCohorts set to true for series {0}", SeriesKey);
    }


    public ITimeSeriesReader<TNumberOut> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage, string cohort)
    {
        ITimeSeriesReader<TNumber1> reader1 = _series1.GetReader(timeline, storage, cohort);
        ITimeSeriesReader<TNumber2> reader2 = _series2.GetReader(timeline, storage, cohort);

        return new CombinedTimeSeriesReader<TNumber1,TNumber2,TNumberOut>(_bucketCombiner, reader1, reader2, timeline, storage);
    }
}


public class StatisticsCohortSimpleCombinedSeries<TNumber1, TNumber2, TNumberOut> : IStatisticsTimeSeries<TNumberOut>, ICohortReadableTimeSeries<TNumberOut>
    where TNumber1 : struct, INumber<TNumber1>
    where TNumber2 : struct, INumber<TNumber2>
    where TNumberOut : struct, INumber<TNumberOut>
{
    public class SetupParams : StatisticsTimeSeriesSetupParams
    {
        public ICohortReadableTimeSeries<TNumber1>     CohortTimeSeries { get; }
        public ISimpleReadableTimeSeries<TNumber2>     SimpleTimeSeries { get; }
        public Func<TNumber1?, TNumber2?, TNumberOut?> BucketCombiner   { get; }

        public SetupParams(string seriesKey,
            ICohortReadableTimeSeries<TNumber1> cohortTimeSeries,
            ISimpleReadableTimeSeries<TNumber2> simpleTimeSeries,
            Func<TNumber1?, TNumber2?, TNumberOut?> bucketCombiner) : base(seriesKey)
        {
            SimpleTimeSeries = simpleTimeSeries ?? throw new ArgumentNullException(nameof(simpleTimeSeries));
            CohortTimeSeries = cohortTimeSeries ?? throw new ArgumentNullException(nameof(cohortTimeSeries));
            BucketCombiner   = bucketCombiner ?? throw new ArgumentNullException(nameof(bucketCombiner));
        }
    }

    public string                            Id            { get; }
    public StatisticsTimeSeriesDashboardInfo DashboardInfo { get; }
    public string                            SeriesKey     { get; }

    ICohortReadableTimeSeries<TNumber1> _cohortTimeSeries;
    ISimpleReadableTimeSeries<TNumber2> _simpleTimeSeries;

    Func<TNumber1?, TNumber2?, TNumberOut?> _bucketCombiner;

    public IReadOnlyCollection<string> Cohorts => _cohortTimeSeries.Cohorts;

    public StatisticsCohortSimpleCombinedSeries(SetupParams setupParams, StatisticsTimeSeriesDashboardInfo dashInfo)
    {
        Id                = setupParams.SeriesKey;
        SeriesKey         = setupParams.SeriesKey;
        DashboardInfo     = dashInfo;
        _simpleTimeSeries = setupParams.SimpleTimeSeries;
        _cohortTimeSeries = setupParams.CohortTimeSeries;
        _bucketCombiner   = setupParams.BucketCombiner;

        if (dashInfo != null)
            MetaDebug.Assert(dashInfo.HasCohorts, "DashboardInfo must have HasCohorts set to true for series {0}", SeriesKey);
    }


    public ITimeSeriesReader<TNumberOut> GetReader(StatisticsPageTimeline timeline, IStatisticsPageStorage storage, string cohort)
    {
        ITimeSeriesReader<TNumber1> cohortReader = _cohortTimeSeries.GetReader(timeline, storage, cohort);
        ITimeSeriesReader<TNumber2> simpleReader = _simpleTimeSeries.GetReader(timeline, storage);

        return new CombinedTimeSeriesReader<TNumber1,TNumber2,TNumberOut>(_bucketCombiner, cohortReader, simpleReader, timeline, storage);
    }
}
