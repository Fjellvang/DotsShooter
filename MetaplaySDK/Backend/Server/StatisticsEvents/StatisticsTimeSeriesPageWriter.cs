// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

namespace Metaplay.Server.StatisticsEvents;



[MetaSerializable]
public class StatisticsPageWriteBuffer : IStatisticsPageStorage
{
    [MetaMember(1)] public StatisticsPageTimeline Timeline      { get; private set; }
    [MetaMember(2)] public MetaTime               Watermark     { get; private set; }
    [MetaMember(3)] public int                    CurrentBucket { get; private set; }
    [MetaMember(4)] public int                    CurrentPage   { get; private set; }
    [MetaMember(5)] MetaDictionary<string, StatisticsStoragePage> Pages { get; set; }

    public int PageCount => Pages.Count;

    bool IStatisticsPageStorage.IsWritable => false;


    IStatisticsPageStorage _backingStorage;

    public StatisticsPageWriteBuffer(StatisticsPageTimeline timeline)
    {
        Timeline      = timeline;
        Watermark     = timeline.EpochTime;
        CurrentBucket = 0;
        CurrentPage   = 0;
        Pages         = new MetaDictionary<string, StatisticsStoragePage>();
    }

    StatisticsPageWriteBuffer() { }

    public void SetBackingStorage(IStatisticsPageStorage backingStorage)
    {
        if (backingStorage == null)
            throw new ArgumentNullException(nameof(backingStorage));
        if (!backingStorage.IsWritable)
            throw new ArgumentException("Backing storage is not writable.", nameof(backingStorage));

        _backingStorage = backingStorage;
    }

    public StatisticsBucketIndex AdvanceBuckets(MetaTime newTime)
    {
        CheckBackingStorage();

        StatisticsBucketIndex newBucketIndex = Timeline.GetBucketIndex(newTime);

        if (newBucketIndex.BucketIndex < CurrentBucket && !(newBucketIndex.Page.PageIndex > CurrentPage))
            throw new InvalidOperationException("Cannot advance buckets backwards!");
        if (newBucketIndex.Page.PageIndex < CurrentPage)
            throw new InvalidOperationException("Cannot advance pages backwards!");

        if (newBucketIndex.Page.PageIndex > CurrentPage)
        {
            // Set current pages as final.
            foreach ((_, StatisticsStoragePage page) in Pages)
            {
                if (page.PageIndex <= CurrentPage)
                    page.SetFinal();
            }
        }

        CurrentBucket = newBucketIndex.BucketIndex;
        CurrentPage   = newBucketIndex.Page.PageIndex;
        Watermark     = Timeline.GetBucketStartTime(newBucketIndex);

        return newBucketIndex;
    }

    public StatisticsStoragePage GetOrCreateWriteablePage(StatisticsPageIndex pageIndex, string pageKey)
    {
        CheckBackingStorage();

        string pageId = StatisticsStoragePage.BuildUniqueId(Timeline, pageKey, pageIndex.PageIndex);

        if (!Pages.TryGetValue(pageId, out StatisticsStoragePage page))
        {
            page = new StatisticsStoragePage(pageKey, Timeline, pageIndex.PageIndex);
            Pages.Add(pageId, page);
        }

        return page;
    }

    public StatisticsStoragePage TryGetWritablePage(StatisticsPageIndex pageIndex, string pageKey)
    {
        CheckBackingStorage();

        string pageId = StatisticsStoragePage.BuildUniqueId(Timeline, pageKey, pageIndex.PageIndex);

        if (Pages.TryGetValue(pageId, out StatisticsStoragePage page))
            return page;

        return null;
    }

    /// <summary>
    /// Flush and forget all final pages to the backing storage.
    /// Any non-final pages will be kept in memory.
    /// </summary>
    public async Task FlushAsync()
    {
        List<string> flushedPages = null;
        foreach (StatisticsStoragePage page in Pages.Values)
        {
            if (page.IsFinal)
            {
                flushedPages ??= new List<string>();
                flushedPages.Add(page.UniqueIdentifier);
                await _backingStorage.WriteNewAsync(page);
            }
        }

        if (flushedPages != null)
        {
            foreach (string flushedPage in flushedPages)
                Pages.Remove(flushedPage);
        }
    }

    public Task WriteNewAsync(StatisticsStoragePage page)
    {
        throw new NotImplementedException("This storage does not support write operations.");
    }

    public bool Exists(string pageId)
    {
        return Pages.ContainsKey(pageId);
    }

    public Task<StatisticsStoragePage> TryGetAsync(string pageId)
    {
        if (Pages.TryGetValue(pageId, out StatisticsStoragePage page))
            return Task.FromResult(page);
        else
            return Task.FromResult<StatisticsStoragePage>(null);
    }

    void CheckBackingStorage()
    {
        if (_backingStorage is null)
            throw new NullReferenceException($"Backing storage is not set on {nameof(StatisticsPageWriteBuffer)}. Please set the storage before doing any operations.");
    }

    public void AdvanceWatermark(MetaTime newTime)
    {
        if (newTime < Watermark)
            throw new ArgumentException("The watermark cannot go backwards!");
        if (newTime >= Timeline.GetBucketEndTime(Timeline.GetBucket(CurrentPage, CurrentBucket)))
            throw new ArgumentException("Cannot advance watermark past the bucket boundary!");

        Watermark = newTime;
    }
}

public class StatisticsTimeSeriesPageWriter
{
    interface ITimeSeriesWriter
    {
        IWriteableStatisticsTimeSeries TimeSeries { get; }
        void Write(StatisticsEventBase eventBase);
        void ResetPage(StatisticsPageIndex currentPage);
        void SetBucket(StatisticsBucketIndex bucketIndex);
    }

    class TimeSeriesWriter<TNumber> : ITimeSeriesWriter
        where TNumber : struct, INumber<TNumber>
    {
        class TimeSeriesBucketWriter : IStatisticsTimeSeriesBucketWriter<TNumber>
        {
            TimeSeriesWriter<TNumber> _writer;

            public TimeSeriesBucketWriter(TimeSeriesWriter<TNumber> writer)
            {
                _writer = writer;
            }

            public StatisticsPageSectionBase                     MainSection    { get; set; }
            public Dictionary<string, StatisticsPageSectionBase> CohortSections { get; set; } = new Dictionary<string, StatisticsPageSectionBase>();
            public int                                           CurrentBucket  { get; set; }
            public StatisticsPageIndex                           CurrentPage    { get; set; }


            public TNumber? Value
            {
                get
                {
                    if (MainSection == null)
                        return null;
                    if (MainSection.IsNullable && !MainSection.HasValues[CurrentBucket])
                        return null;

                    return MainSection.As<TNumber>().Buckets[CurrentBucket];
                }
                set
                {
                    if (MainSection == null)
                    {
                        if (value.HasValue)
                            MainSection = _writer.GetOrCreateSection(CurrentPage, null);
                        else
                            return;
                    }

                    if (MainSection.IsNullable)
                        MainSection.HasValues[CurrentBucket] = value.HasValue;

                    MainSection.As<TNumber>().Buckets[CurrentBucket] = value ?? TNumber.Zero;
                }
            }

            public TNumber? GetCohort(string cohort)
            {
                if (string.IsNullOrEmpty(cohort))
                    return Value;

                if (CohortSections.TryGetValue(cohort, out StatisticsPageSectionBase cohortSection))
                {
                    if (cohortSection.IsNullable && !cohortSection.HasValues[CurrentBucket])
                        return null;

                    return cohortSection.As<TNumber>().Buckets[CurrentBucket];
                }

                return null;
            }

            public void SetCohort(string cohort, TNumber? newValue)
            {
                if (string.IsNullOrEmpty(cohort))
                {
                    Value = newValue;
                    return;
                }

                if (!CohortSections.TryGetValue(cohort, out StatisticsPageSectionBase cohortSection))
                {
                    if (newValue.HasValue)
                    {
                        cohortSection = _writer.GetOrCreateSection(CurrentPage, cohort);
                        CohortSections.Add(cohort, cohortSection);
                    }else
                        return;
                }

                if (cohortSection.IsNullable)
                    cohortSection.HasValues[CurrentBucket] = newValue.HasValue;

                cohortSection.As<TNumber>().Buckets[CurrentBucket] = newValue ?? TNumber.Zero;
            }
        }

        StatisticsTimeSeriesPageWriter                  _pageWriter;
        IWriteableStatisticsTimeSeries<TNumber>         _timeSeries;
        IStatisticsTimeSeriesBucketAccumulator<TNumber> _bucketAccumulator;
        TimeSeriesBucketWriter                          _bucketWriter;

        IWriteableStatisticsTimeSeries ITimeSeriesWriter.TimeSeries => _timeSeries;
        public TimeSeriesWriter(StatisticsTimeSeriesPageWriter pageWriter, IWriteableStatisticsTimeSeries<TNumber> timeSeries)
        {
            _pageWriter        = pageWriter;
            _timeSeries        = timeSeries;
            _bucketAccumulator = _timeSeries.GetBucketAccumulator();
            _bucketWriter      = new TimeSeriesBucketWriter(this);
        }

        public void ResetPage(StatisticsPageIndex currentPage)
        {
            StatisticsStoragePage page = _pageWriter._buffer.TryGetWritablePage(currentPage, _timeSeries.StoragePageKey);

            // Find main section from storage, or null if not found
            _bucketWriter.MainSection   = page?.PageSections.GetValueOrDefault(page.GetSectionId(_timeSeries.SeriesKey, null));
            _bucketWriter.CurrentBucket = 0;

            // Clear cohort cache, and rebuild from storage
            _bucketWriter.CohortSections.Clear();

            if (page != null)
            {
                foreach (string cohort in page.GetCohorts(_timeSeries.SeriesKey))
                    _bucketWriter.CohortSections.Add(cohort, page.PageSections[page.GetSectionId(_timeSeries.SeriesKey, cohort)]);
            }

            _bucketWriter.CurrentPage = currentPage;
        }

        public void SetBucket(StatisticsBucketIndex bucketIndex)
        {
            _bucketWriter.CurrentBucket = bucketIndex.BucketIndex;
        }

        public void Write(StatisticsEventBase eventBase)
        {
            _bucketAccumulator.AccumulateEvent(eventBase, _bucketWriter);
        }

        StatisticsPageSectionBase GetOrCreateSection(StatisticsPageIndex pageIndex, string cohort)
        {
            StatisticsStoragePage page = _pageWriter._buffer.GetOrCreateWriteablePage(pageIndex, _timeSeries.StoragePageKey);

            string                    sectionId = page.GetSectionId(_timeSeries.SeriesKey, cohort);
            StatisticsPageSectionBase section;

            if (!page.PageSections.TryGetValue(sectionId, out section))
            {
                section = StatisticsPageSectionBase.Create<TNumber>(
                    _pageWriter._buffer.Timeline.Resolution.NumBucketsPerPage,
                    _bucketAccumulator.IsNullable);
                page.PageSections.Add(sectionId, section);
            }

            return section;
        }
    }

    IMetaLogger                                       _log;
    StatisticsPageWriteBuffer                        _buffer;
    List<ITimeSeriesWriter>                          _timeSeriesWriters;
    Dictionary<System.Type, List<ITimeSeriesWriter>> _eventTypeToWriter;
    StatisticsBucketIndex                            _currentBucket;
    MetaTime                                         _bucketBoundary;

    public StatisticsTimeSeriesPageWriter(StatisticsPageWriteBuffer buffer, IEnumerable<IStatisticsTimeSeries> timeSeries, IMetaLogger logger)
    {
        _log     = logger;
        _buffer = buffer;

        _currentBucket  = _buffer.Timeline.GetBucket(_buffer.CurrentPage, _buffer.CurrentBucket);
        _bucketBoundary = _buffer.Timeline.GetBucketEndTime(_currentBucket);

        RegisterTimeSeries(timeSeries);

        foreach (ITimeSeriesWriter writer in _timeSeriesWriters)
        {
            writer.ResetPage(_currentBucket.Page);
            writer.SetBucket(_currentBucket);
        }
    }

    public void Write(StatisticsEventBase eventBase)
    {
        if (!_eventTypeToWriter.TryGetValue(eventBase.GetType(), out List<ITimeSeriesWriter> writers))
            return;

        MetaTime eventTime = eventBase.Timestamp;

        if (eventTime < _buffer.Watermark)
        {
            _log.Warning("Received a statistics event that is earlier than the current watermark (Event:{EventTime} vs Watermark:{Watermark}). This event will be ignored.",
                eventTime, _buffer.Watermark);
            return;
        }

        if (eventTime >= _bucketBoundary)
        {
            int currentPage = _buffer.CurrentPage;
            _currentBucket = _buffer.AdvanceBuckets(eventTime);
            _bucketBoundary = _buffer.Timeline.GetBucketEndTime(_currentBucket);

            foreach (ITimeSeriesWriter writer in _timeSeriesWriters)
            {
                // Page was changed. Reset writers.
                if (_currentBucket.Page.PageIndex != currentPage)
                    writer.ResetPage(_currentBucket.Page);

                writer.SetBucket(_currentBucket);
            }
        }

        foreach (ITimeSeriesWriter writer in writers)
            writer.Write(eventBase);

        _buffer.AdvanceWatermark(eventTime);
    }

    public Task FlushAsync()
    {
        return _buffer.FlushAsync();
    }

    #region Initialization
    void RegisterTimeSeries(IEnumerable<IStatisticsTimeSeries> timeSeries)
    {
        MethodInfo createTimeSeriesWriterMethod = typeof(StatisticsTimeSeriesPageWriter).GetMethod(nameof(CreateTimeSeriesWriter),
            BindingFlags.Instance | BindingFlags.NonPublic);

        _timeSeriesWriters = new List<ITimeSeriesWriter>();
        _eventTypeToWriter = new Dictionary<System.Type, List<ITimeSeriesWriter>>();

        foreach (IStatisticsTimeSeries series in timeSeries)
        {
            if (!series.GetType().ImplementsInterface(typeof(IWriteableStatisticsTimeSeries)))
                continue;

            Type genericInterface = series.GetType().GetGenericInterface(typeof(IWriteableStatisticsTimeSeries<>));

            if (genericInterface == null)
                throw new InvalidOperationException($"{timeSeries.GetType().Name} that implements {nameof(IWriteableStatisticsTimeSeries)} must also implement the generic version {typeof(IWriteableStatisticsTimeSeries<>).Name}");

            Type numberType = genericInterface.GetGenericArguments()[0];

            createTimeSeriesWriterMethod.MakeGenericMethod(numberType).Invoke(this, [series]);
        }
    }

    // Used in a dynamic GetMethod call. Careful if you change anything.
    void CreateTimeSeriesWriter<TNumber>(IWriteableStatisticsTimeSeries<TNumber> timeSeries)
        where TNumber : struct, INumber<TNumber>
    {
        if (string.IsNullOrWhiteSpace(timeSeries.SeriesKey))
            throw new InvalidOperationException("Time series SeriesKey must be provided.");
        if (timeSeries.SeriesKey.Contains('/'))
            throw new InvalidOperationException("Time series SeriesKey cannot contain a '/' character.");
        if (_timeSeriesWriters.Any(w => w.TimeSeries.SeriesKey == timeSeries.SeriesKey))
            throw new InvalidOperationException($"Duplicate SeriesKey found: {timeSeries.SeriesKey}");

        TimeSeriesWriter<TNumber> writer = new TimeSeriesWriter<TNumber>(this, timeSeries);

        _timeSeriesWriters.Add(writer);

        foreach (var eventType in timeSeries.EventTypes)
        {
            if(!_eventTypeToWriter.ContainsKey(eventType))
                _eventTypeToWriter.Add(eventType, new List<ITimeSeriesWriter>());

            _eventTypeToWriter[eventType].Add(writer);
        }
    }
    #endregion
}
