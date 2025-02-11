// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Server.StatisticsEvents;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.Tests;

public class StatisticsTimeSeriesTests
{
    [MetaSerializableDerived(700)]
    public class TestStatisticsEvent : StatisticsEventBase
    {
        [MetaMember(1)] public float FloatValue { get; set; }
        [MetaMember(2)] public int   IntValue   { get; set; }

        public TestStatisticsEvent(MetaTime timestamp) : base(timestamp) { }

        TestStatisticsEvent() { }

        public override string UniqueKey => Invariant($"test/{Timestamp}");
    }

    StatisticsSimpleTimeSeries<TestStatisticsEvent, int> _testCountSeries = new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>(
                new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>.SetupParams(
                    seriesKey: "testSeries",
                    mode: StatisticsBucketAccumulationMode.Count), null);

    StatisticsSimpleTimeSeries<TestStatisticsEvent, float> _testAvgSeries = new StatisticsSimpleTimeSeries<TestStatisticsEvent, float>(
        new StatisticsSimpleTimeSeries<TestStatisticsEvent, float>.SetupParams(
            seriesKey: "testAvgSeries",
            mode: StatisticsBucketAccumulationMode.Avg,
            eventParser: ev => ev.FloatValue), null);

    StatisticsSimpleTimeSeries<TestStatisticsEvent, int> _testMinSeries = new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>(
        new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>.SetupParams(
            seriesKey: "testMinSeries",
            mode: StatisticsBucketAccumulationMode.Min,
            eventParser: ev => ev.IntValue), null);

    StatisticsSimpleTimeSeries<TestStatisticsEvent, int> _testMaxSeries = new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>(
        new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>.SetupParams(
            seriesKey: "testMaxSeries",
            mode: StatisticsBucketAccumulationMode.Max,
            eventParser: ev => ev.IntValue), null);

    StatisticsSimpleTimeSeries<TestStatisticsEvent, int> _testSumSeries = new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>(
        new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>.SetupParams(
            seriesKey: "testSumSeries",
            mode: StatisticsBucketAccumulationMode.Sum,
            eventParser: ev => ev.IntValue), null);


    StatisticsSimpleTimeSeries<TestStatisticsEvent, int> _testNewPageSumSeries = new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>(
        new StatisticsSimpleTimeSeries<TestStatisticsEvent, int>.SetupParams(
            seriesKey: "testSecondPageSeries",
            mode: StatisticsBucketAccumulationMode.Sum,
            eventParser: ev => ev.IntValue,
            storagePageKey: "second"), null);

    StatisticsSimpleCombinedSeries<int, int, float> _testCombinedSeries;


    MetaTime                    _epochTime;
    StatisticsPageTimeline      _timeline;
    MockStatisticsPageStorage   _statisticsPageStorage;
    StatisticsPageWriteBuffer   _writeBuffer;
    TieredStatisticsPageStorage _readStorage;
    MockMetaLogger              _logger;

    [SetUp]
    public void SetupTests()
    {
        _epochTime = MetaTime.FromDateTime(
            new DateTime(2000, 1, 1, 8, 0, 0, DateTimeKind.Utc));

        _timeline              = new StatisticsPageTimeline(_epochTime, StatisticsPageResolutions.Daily);
        _statisticsPageStorage = new MockStatisticsPageStorage();
        _writeBuffer          = new StatisticsPageWriteBuffer(_timeline);

        _writeBuffer.SetBackingStorage(_statisticsPageStorage);
        _readStorage = new TieredStatisticsPageStorage(_statisticsPageStorage, _writeBuffer);

        _logger = new MockMetaLogger();

        _testCombinedSeries = new StatisticsSimpleCombinedSeries<int, int, float>(
            new StatisticsSimpleCombinedSeries<int, int, float>.SetupParams(
                seriesKey: "testCombinedSeries",
                series1: _testNewPageSumSeries,
                series2: _testCountSeries,
                bucketCombiner: (sum, count) => (count ?? 0) > 0  ? sum / (float)count : null), null);
    }

    [Test]
    public async Task TestSimpleTimeSeries()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testCountSeries], new MetaNullLogger());

        List<StatisticsEventBase> events = new List<StatisticsEventBase>();
        MetaTime timestamp = _epochTime;

        for (int i = 0; i < 60 * 24 * 366; i++)
        {
            events.Add(new TestStatisticsEvent(timestamp));
            timestamp += MetaDuration.Minute;
        }

        foreach (StatisticsEventBase statisticsEventBase in events)
            writer.Write(statisticsEventBase);

        Assert.AreEqual(27, _writeBuffer.PageCount);

        await writer.FlushAsync();

        Assert.AreEqual(1, _writeBuffer.PageCount);
        Assert.AreEqual(26, _statisticsPageStorage.Pages.Count);

        TieredStatisticsPageStorage tieredStorage = new TieredStatisticsPageStorage(_statisticsPageStorage, _writeBuffer);

        ITimeSeriesReader<int> reader = _testCountSeries.GetReader(_timeline, tieredStorage);

        MetaTime endTime = _epochTime + MetaDuration.FromDays(400);

        StatisticsTimeSeriesView<int> result  = await reader.ReadTimeSpan(_epochTime, endTime);

        Assert.AreEqual(timestamp - MetaDuration.Minute, _writeBuffer.Watermark);
        Assert.AreEqual(400, result.Buckets.Length);
        Assert.AreEqual(400, result.NumBuckets);
        Assert.AreEqual(_epochTime, result.StartTime);
        Assert.AreEqual(endTime, result.EndTime);
        Assert.AreEqual(_timeline, result.Timeline);

        for (int i = 0; i < 366; i++)
        {
            Assert.True(result.Buckets[i].HasValue);
            Assert.AreEqual(1440, result.Buckets[i]); // 60 * 24
        }

        Assert.False(result.Buckets[399].HasValue);

        _logger.AssertNoFails();
    }

    [Test]
    public async Task TestSimpleAvgTimeSeries()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testAvgSeries], _logger);

        MetaTime timestamp = _epochTime;

        for (int i = 0; i < 50; i++)
        {
            TestStatisticsEvent ev = new TestStatisticsEvent(timestamp) {FloatValue = i};
            timestamp += MetaDuration.Second;
            writer.Write(ev);
        }

        await writer.FlushAsync();

        ITimeSeriesReader<float> reader = _testAvgSeries.GetReader(_timeline, _readStorage);

        float? avgValue = await reader.Read(timestamp);

        Assert.AreEqual(1, _writeBuffer.PageCount);
        Assert.AreEqual(0, _statisticsPageStorage.Pages.Count);

        Assert.AreEqual(timestamp - MetaDuration.Second, _writeBuffer.Watermark);
        Assert.True(avgValue.HasValue);
        Assert.AreEqual(24.5f, avgValue.Value);

        _logger.AssertNoFails();
    }

    [Test]
    public async Task TestWriteSameTimestamp()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testCountSeries], _logger);

        MetaTime timestamp = _epochTime;

        writer.Write(new TestStatisticsEvent(timestamp));
        writer.Write(new TestStatisticsEvent(timestamp));
        writer.Write(new TestStatisticsEvent(timestamp));
        writer.Write(new TestStatisticsEvent(timestamp));

        ITimeSeriesReader<int> reader = _testCountSeries.GetReader(_timeline, _readStorage);

        int? result = await reader.Read(timestamp);

        Assert.True(result.HasValue);
        Assert.AreEqual(4, result.Value);
        Assert.AreEqual(timestamp, _writeBuffer.Watermark);

        Assert.AreEqual(1, _writeBuffer.PageCount);
        _logger.AssertNoFails();
    }

    [Test]
    public void TestWriteOlderTimestamp()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testCountSeries], _logger);

        MetaTime timestamp = _epochTime + MetaDuration.Minute;

        Assert.DoesNotThrow(() => writer.Write(new TestStatisticsEvent(timestamp)));
        Assert.DoesNotThrow(() => writer.Write(new TestStatisticsEvent(_epochTime)));

        Assert.AreEqual(1, _writeBuffer.PageCount);
        Assert.AreEqual(1, _logger.NumWarnings);
    }

    [Test]
    public void TestWriteOlderThanEpochTime()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testCountSeries], _logger);

        MetaTime timestamp = _epochTime - MetaDuration.Minute;

        Assert.DoesNotThrow(() => writer.Write(new TestStatisticsEvent(timestamp)));

        Assert.AreEqual(0, _writeBuffer.PageCount);
        Assert.AreEqual(1, _logger.NumWarnings);
    }

    [Test]
    public async Task TestSumSeries()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testSumSeries], _logger);

        MetaTime timestamp = _epochTime;

        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 1 });
        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 2 });
        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 3 });
        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 4 });

        ITimeSeriesReader<int> reader = _testSumSeries.GetReader(_timeline, _readStorage);

        int? result = await reader.Read(timestamp);

        Assert.True(result.HasValue);
        Assert.AreEqual(10, result.Value);

        Assert.AreEqual(1, _writeBuffer.PageCount);
        Assert.AreEqual(timestamp, _writeBuffer.Watermark);

        _logger.AssertNoFails();
    }

    [Test]
    public async Task TestMinMaxSeries()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testMinSeries, _testMaxSeries, _testSumSeries], _logger);

        MetaTime timestamp = _epochTime + MetaDuration.Day;

        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = -2 });
        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = -3 });
        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 4 });
        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 3 });

        ITimeSeriesReader<int> minReader = _testMinSeries.GetReader(_timeline, _readStorage);
        ITimeSeriesReader<int> maxReader = _testMaxSeries.GetReader(_timeline, _readStorage);
        ITimeSeriesReader<int> sumReader = _testSumSeries.GetReader(_timeline, _readStorage);

        int? resultMin = await minReader.Read(timestamp);
        int? resultMax = await maxReader.Read(timestamp);
        int? resultSum = await sumReader.Read(timestamp);

        Assert.True(resultMin.HasValue);
        Assert.True(resultMax.HasValue);
        Assert.True(resultSum.HasValue);
        Assert.AreEqual(-3, resultMin.Value);
        Assert.AreEqual(4, resultMax.Value);
        Assert.AreEqual(2, resultSum.Value);

        Assert.AreEqual(1, _writeBuffer.PageCount);
        Assert.AreEqual(timestamp, _writeBuffer.Watermark);

        _logger.AssertNoFails();
    }

    [Test]
    public async Task TestSecondPageSeries()
    {
        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testNewPageSumSeries], _logger);

        MetaTime timestamp = _epochTime;

        writer.Write(new TestStatisticsEvent(timestamp) { IntValue = 1 });

        Assert.AreEqual(1, _writeBuffer.PageCount);

        StatisticsTimeSeriesPageWriter writer2 = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testCountSeries, _testNewPageSumSeries], _logger);

        writer2.Write(new TestStatisticsEvent(timestamp) { IntValue = 2 });

        Assert.AreEqual(2, _writeBuffer.PageCount);

        ITimeSeriesReader<int> countReader = _testCountSeries.GetReader(_timeline, _readStorage);
        ITimeSeriesReader<int> newPageSumReader = _testNewPageSumSeries.GetReader(_timeline, _readStorage);

        int? countResult = await countReader.Read(timestamp);
        int? newPageResult = await newPageSumReader.Read(timestamp);

        Assert.True(countResult.HasValue);
        Assert.True(newPageResult.HasValue);

        Assert.AreEqual(1, countResult.Value);
        Assert.AreEqual(3, newPageResult.Value);
    }

    [Test]
    public async Task TestReadTimeSpans()
    {
        const int days = 100;

        StatisticsTimeSeriesPageWriter writer = new StatisticsTimeSeriesPageWriter(_writeBuffer, [_testCountSeries, _testAvgSeries, _testNewPageSumSeries], _logger);

        MetaTime startTime = _epochTime;

        MetaTime time = startTime;
        for (int i = 0; i < days; i++)
        {
            for (int j = 0; j < i; j++)
            {
                writer.Write(new TestStatisticsEvent(time) {IntValue = j, FloatValue = j});
                time += MetaDuration.FromMinutes(1);
            }

            time -= MetaDuration.FromMinutes(i);
            time += MetaDuration.Day;
        }

        int expectedPages = (days / _timeline.Resolution.NumBucketsPerPage + 1) * 2;

        Assert.AreEqual(expectedPages, _writeBuffer.PageCount);

        await writer.FlushAsync();

        Assert.AreEqual(2, _writeBuffer.PageCount);
        Assert.AreEqual(expectedPages - 2, _statisticsPageStorage.Pages.Count);

        ITimeSeriesReader<int>   countReader    = _testCountSeries.GetReader(_timeline, _readStorage);
        ITimeSeriesReader<float> avgReader      = _testAvgSeries.GetReader(_timeline, _readStorage);
        ITimeSeriesReader<int>   sumReader      = _testNewPageSumSeries.GetReader(_timeline, _readStorage);
        ITimeSeriesReader<float> combinedReader = _testCombinedSeries.GetReader(_timeline, _readStorage);

        static int GetExpectedSum(int i)
        {
            return Enumerable.Range(0, i).Sum();
        }

        async Task CheckTimeSpan(int startDay, int numDays)
        {
            MetaTime startTime = _epochTime + MetaDuration.FromDays(startDay);
            MetaTime endTime   = startTime + MetaDuration.FromDays(numDays);

            StatisticsTimeSeriesView<int>   countView = await countReader.ReadTimeSpan(startTime, endTime);
            StatisticsTimeSeriesView<float> avgView   = await avgReader.ReadTimeSpan(startTime, endTime);
            StatisticsTimeSeriesView<int>   sumView   = await sumReader.ReadTimeSpan(startTime, endTime);
            StatisticsTimeSeriesView<float> comView   = await combinedReader.ReadTimeSpan(startTime, endTime);

            Assert.AreEqual(numDays, countView.NumBuckets);
            Assert.AreEqual(numDays, avgView.NumBuckets);
            Assert.AreEqual(numDays, sumView.NumBuckets);
            Assert.AreEqual(numDays, comView.NumBuckets);

            MetaTime time = startTime;

            for (int i = 0; i < numDays; i++)
            {
                int   count = startDay + i;
                int   sum   = GetExpectedSum(startDay + i);
                float? avg   = count > 0 ? sum / (float)count : null;

                Assert.True(countView.Buckets[i].HasValue);
                Assert.AreEqual(count, countView.Buckets[i].Value);

                Assert.True(sumView.Buckets[i].HasValue);
                Assert.AreEqual(sum, sumView.Buckets[i].Value);

                if (avg.HasValue)
                {
                    Assert.True(avgView.Buckets[i].HasValue);
                    Assert.AreEqual(avg, avgView.Buckets[i].Value);

                    Assert.True(comView.Buckets[i].HasValue);
                    Assert.AreEqual(avg, comView.Buckets[i].Value);
                }
                else
                {
                    Assert.False(avgView.Buckets[i].HasValue);
                    Assert.False(comView.Buckets[i].HasValue);
                }

                Assert.AreEqual(count, await countReader.Read(time));
                Assert.AreEqual(sum, await sumReader.Read(time));
                Assert.AreEqual(avg, await avgReader.Read(time));

                time += _timeline.Resolution.BucketInterval;
            }
        }

        await CheckTimeSpan(0, 100);
        await CheckTimeSpan(10, 10);
        await CheckTimeSpan(15, 10);
        await CheckTimeSpan(20, 10);
        await CheckTimeSpan(25, 20);
        await CheckTimeSpan(30, 40);
        await CheckTimeSpan(60, 40);
    }
}
