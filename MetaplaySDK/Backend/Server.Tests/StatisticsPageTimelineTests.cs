// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Server.StatisticsEvents;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server.Tests;

[TestFixture]
public class StatisticsPageTimelineTests
{
    public MetaTime               epochTime;
    public StatisticsPageTimeline minuteTl;
    public StatisticsPageTimeline hourTl;
    public StatisticsPageTimeline dayTl;

    const int MinuteTlNumBucketsPerPage  = 60 * 4; // 4 hours
    const int HourTlNumBucketsPerPage = 24 * 7; // 7 days
    const int DayTlNumBucketsPerPage  = 366; // 1 year


    public StatisticsPageTimelineTests()
    {
        epochTime = MetaTime.FromDateTime(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        minuteTl = new StatisticsPageTimeline(
            epochTime,
            new StatisticsPageResolution(MetaDuration.FromMinutes(1),
            MinuteTlNumBucketsPerPage)
            );
        hourTl = new StatisticsPageTimeline(
            epochTime,
            new StatisticsPageResolution(MetaDuration.FromHours(1),
            HourTlNumBucketsPerPage)
            );
        dayTl = new StatisticsPageTimeline(
            epochTime,
            new StatisticsPageResolution(MetaDuration.FromDays(1),
            DayTlNumBucketsPerPage)
            );
    }

    [Test]
    public void TestPageTimelineGetBucketIndex()
    {
        Assert.AreEqual(0, minuteTl.GetBucketIndex(epochTime).BucketIndex);
        Assert.AreEqual(0, hourTl.GetBucketIndex(epochTime).BucketIndex);
        Assert.AreEqual(0, dayTl.GetBucketIndex(epochTime).BucketIndex);

        Assert.AreEqual(0, minuteTl.GetBucketIndex(epochTime).Page.PageIndex);
        Assert.AreEqual(0, hourTl.GetBucketIndex(epochTime).Page.PageIndex);
        Assert.AreEqual(0, dayTl.GetBucketIndex(epochTime).Page.PageIndex);

        Assert.AreEqual(1, minuteTl.GetBucketIndex(epochTime + MetaDuration.Minute).BucketIndex);
        Assert.AreEqual(1, hourTl.GetBucketIndex(epochTime + MetaDuration.Hour).BucketIndex);
        Assert.AreEqual(1, dayTl.GetBucketIndex(epochTime + MetaDuration.Day).BucketIndex);

        Assert.AreEqual(0, minuteTl.GetBucketIndex(epochTime + MetaDuration.Minute).Page.PageIndex);
        Assert.AreEqual(0, hourTl.GetBucketIndex(epochTime + MetaDuration.Hour).Page.PageIndex);
        Assert.AreEqual(0, dayTl.GetBucketIndex(epochTime + MetaDuration.Day).Page.PageIndex);

        Assert.AreEqual(0, minuteTl.GetBucketIndex(epochTime + MetaDuration.Minute * MinuteTlNumBucketsPerPage).BucketIndex);
        Assert.AreEqual(0, hourTl.GetBucketIndex(epochTime + MetaDuration.Hour * HourTlNumBucketsPerPage).BucketIndex);
        Assert.AreEqual(0, dayTl.GetBucketIndex(epochTime + MetaDuration.Day * DayTlNumBucketsPerPage).BucketIndex);

        Assert.AreEqual(1, minuteTl.GetBucketIndex(epochTime + MetaDuration.Minute * MinuteTlNumBucketsPerPage).Page.PageIndex);
        Assert.AreEqual(1, hourTl.GetBucketIndex(epochTime + MetaDuration.Hour * HourTlNumBucketsPerPage).Page.PageIndex);
        Assert.AreEqual(1, dayTl.GetBucketIndex(epochTime + MetaDuration.Day * DayTlNumBucketsPerPage).Page.PageIndex);

        Assert.AreEqual(MinuteTlNumBucketsPerPage - 1, minuteTl.GetBucketIndex(epochTime + MetaDuration.Minute * MinuteTlNumBucketsPerPage - MetaDuration.FromMilliseconds(1)).BucketIndex);
        Assert.AreEqual(HourTlNumBucketsPerPage - 1, hourTl.GetBucketIndex(epochTime + MetaDuration.Hour * HourTlNumBucketsPerPage - MetaDuration.FromMilliseconds(1)).BucketIndex);
        Assert.AreEqual(DayTlNumBucketsPerPage - 1, dayTl.GetBucketIndex(epochTime + MetaDuration.Day * DayTlNumBucketsPerPage - MetaDuration.FromMilliseconds(1)).BucketIndex);

        Assert.AreEqual(0, minuteTl.GetBucketIndex(epochTime + MetaDuration.Minute * MinuteTlNumBucketsPerPage - MetaDuration.FromMilliseconds(1)).Page.PageIndex);
        Assert.AreEqual(0, hourTl.GetBucketIndex(epochTime + MetaDuration.Hour * HourTlNumBucketsPerPage - MetaDuration.FromMilliseconds(1)).Page.PageIndex);
        Assert.AreEqual(0, dayTl.GetBucketIndex(epochTime + MetaDuration.Day * DayTlNumBucketsPerPage - MetaDuration.FromMilliseconds(1)).Page.PageIndex);
    }

    [Test]
    public void TestPageTimelineWrongBucketIndex()
    {
        Assert.DoesNotThrow(() => minuteTl.GetBucketStartTime(minuteTl.GetBucketIndex(epochTime)));
        Assert.DoesNotThrow(() => hourTl.GetBucketStartTime(hourTl.GetBucketIndex(epochTime)));
        Assert.DoesNotThrow(() => dayTl.GetBucketStartTime(dayTl.GetBucketIndex(epochTime)));

        Assert.DoesNotThrow(() => minuteTl.GetBucketEndTime(minuteTl.GetBucketIndex(epochTime)));
        Assert.DoesNotThrow(() => hourTl.GetBucketEndTime(hourTl.GetBucketIndex(epochTime)));
        Assert.DoesNotThrow(() => dayTl.GetBucketEndTime(dayTl.GetBucketIndex(epochTime)));

        Assert.DoesNotThrow(() => minuteTl.GetPageStartTime(minuteTl.GetBucketIndex(epochTime).Page));
        Assert.DoesNotThrow(() => hourTl.GetPageStartTime(hourTl.GetBucketIndex(epochTime).Page));
        Assert.DoesNotThrow(() => dayTl.GetPageStartTime(dayTl.GetBucketIndex(epochTime).Page));

        Assert.DoesNotThrow(() => minuteTl.GetPageEndTime(minuteTl.GetBucketIndex(epochTime).Page));
        Assert.DoesNotThrow(() => hourTl.GetPageEndTime(hourTl.GetBucketIndex(epochTime).Page));
        Assert.DoesNotThrow(() => dayTl.GetPageEndTime(dayTl.GetBucketIndex(epochTime).Page));

        Assert.Throws<ArgumentException>(() => minuteTl.GetBucketStartTime(hourTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => minuteTl.GetBucketStartTime(dayTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => hourTl.GetBucketStartTime(minuteTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => hourTl.GetBucketStartTime(dayTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => dayTl.GetBucketStartTime(minuteTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => dayTl.GetBucketStartTime(hourTl.GetBucketIndex(epochTime)));


        Assert.Throws<ArgumentException>(() => minuteTl.GetBucketEndTime(hourTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => minuteTl.GetBucketEndTime(dayTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => hourTl.GetBucketEndTime(minuteTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => hourTl.GetBucketEndTime(dayTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => dayTl.GetBucketEndTime(minuteTl.GetBucketIndex(epochTime)));
        Assert.Throws<ArgumentException>(() => dayTl.GetBucketEndTime(hourTl.GetBucketIndex(epochTime)));

        Assert.Throws<ArgumentException>(() => minuteTl.GetPageStartTime(hourTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => minuteTl.GetPageStartTime(dayTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => hourTl.GetPageStartTime(minuteTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => hourTl.GetPageStartTime(dayTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => dayTl.GetPageStartTime(minuteTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => dayTl.GetPageStartTime(hourTl.GetBucketIndex(epochTime).Page));

        Assert.Throws<ArgumentException>(() => minuteTl.GetPageEndTime(hourTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => minuteTl.GetPageEndTime(dayTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => hourTl.GetPageEndTime(minuteTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => hourTl.GetPageEndTime(dayTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => dayTl.GetPageEndTime(minuteTl.GetBucketIndex(epochTime).Page));
        Assert.Throws<ArgumentException>(() => dayTl.GetPageEndTime(hourTl.GetBucketIndex(epochTime).Page));
    }

    [Test]
    public void TestBucketIndexGetTime()
    {
        StatisticsBucketIndex minute = minuteTl.GetBucketIndex(epochTime);
        StatisticsBucketIndex hour = hourTl.GetBucketIndex(epochTime);
        StatisticsBucketIndex day = dayTl.GetBucketIndex(epochTime);

        Assert.AreEqual(epochTime, minuteTl.GetBucketStartTime(minute));
        Assert.AreEqual(epochTime, hourTl.GetBucketStartTime(hour));
        Assert.AreEqual(epochTime, dayTl.GetBucketStartTime(day));

        Assert.AreEqual(epochTime + MetaDuration.FromMinutes(1), minuteTl.GetBucketEndTime(minute));
        Assert.AreEqual(epochTime + MetaDuration.FromHours(1), hourTl.GetBucketEndTime(hour));
        Assert.AreEqual(epochTime + MetaDuration.FromDays(1), dayTl.GetBucketEndTime(day));

        Assert.AreEqual(epochTime, minuteTl.GetPageStartTime(minute.Page));
        Assert.AreEqual(epochTime, hourTl.GetPageStartTime(hour.Page));
        Assert.AreEqual(epochTime, dayTl.GetPageStartTime(day.Page));

        Assert.AreEqual(epochTime + MetaDuration.FromMinutes(1) * MinuteTlNumBucketsPerPage, minuteTl.GetPageEndTime(minute.Page));
        Assert.AreEqual(epochTime + MetaDuration.FromHours(1) * HourTlNumBucketsPerPage, hourTl.GetPageEndTime(hour.Page));
        Assert.AreEqual(epochTime + MetaDuration.FromDays(1) * DayTlNumBucketsPerPage, dayTl.GetPageEndTime(day.Page));
    }

    [Test]
    public void TestBucketIndexGetTimeRange()
    {
        MetaTime t0 = epochTime + MetaDuration.FromMinutes(30);
        MetaTime t1 = t0 + MetaDuration.FromMinutes(1);
        MetaTime t2 = t1 + MetaDuration.FromMinutes(1);
        MetaTime t3 = t2 + MetaDuration.FromMinutes(1);
        MetaTime t4 = t3 + MetaDuration.FromMinutes(1);
        MetaTime t4s = t4 + MetaDuration.FromMilliseconds(1);
        MetaTime t5 = t4 + MetaDuration.FromMinutes(1);

        StatisticsBucketIndex[] indices = minuteTl.GetBucketsForTimeRange(t0, t4).ToArray();
        StatisticsBucketIndex[] indices2 = minuteTl.GetBucketsForTimeRange(t0, t4s).ToArray();

        Assert.AreEqual(4, indices.Length);
        Assert.AreEqual(5, indices2.Length);

        Assert.AreEqual(0, indices[0].Page.PageIndex);
        Assert.AreEqual(30, indices[0].BucketIndex);
        Assert.AreEqual(33, indices[3].BucketIndex);
        Assert.AreEqual(30, indices2[0].BucketIndex);
        Assert.AreEqual(34, indices2[4].BucketIndex);

        Assert.AreEqual(t0, minuteTl.GetBucketStartTime(indices[0]));
        Assert.AreEqual(t1, minuteTl.GetBucketStartTime(indices[1]));
        Assert.AreEqual(t2, minuteTl.GetBucketStartTime(indices[2]));
        Assert.AreEqual(t3, minuteTl.GetBucketStartTime(indices[3]));

        Assert.AreEqual(t4, minuteTl.GetBucketStartTime(indices2[4]));

        Assert.AreEqual(t1, minuteTl.GetBucketEndTime(indices[0]));
        Assert.AreEqual(t2, minuteTl.GetBucketEndTime(indices[1]));
        Assert.AreEqual(t3, minuteTl.GetBucketEndTime(indices[2]));
        Assert.AreEqual(t4, minuteTl.GetBucketEndTime(indices[3]));

        Assert.AreEqual(t5, minuteTl.GetBucketEndTime(indices2[4]));
    }

    [Test]
    public void TestBucketIndexGetTimeRangeOverPageBoundary()
    {
        MetaTime t0 = epochTime + MetaDuration.FromMinutes(MinuteTlNumBucketsPerPage - 2);
        MetaTime t1 = t0 + MetaDuration.FromMinutes(1);
        MetaTime t2 = t1 + MetaDuration.FromMinutes(1);
        MetaTime t2s = t2 + MetaDuration.FromMilliseconds(1);
        MetaTime t3 = t2 + MetaDuration.FromMinutes(1);
        MetaTime t4 = t3 + MetaDuration.FromMinutes(1);

        StatisticsBucketIndex[] indices = minuteTl.GetBucketsForTimeRange(t0, t2).ToArray();
        StatisticsBucketIndex[] indices2 = minuteTl.GetBucketsForTimeRange(t0, t2s).ToArray();
        StatisticsBucketIndex[] indices3 = minuteTl.GetBucketsForTimeRange(t0, t4).ToArray();

        Assert.AreEqual(2, indices.Length);
        Assert.AreEqual(3, indices2.Length);
        Assert.AreEqual(4, indices3.Length);

        Assert.AreEqual(0, indices[0].Page.PageIndex);
        Assert.AreEqual(1, indices2[2].Page.PageIndex);
        Assert.AreEqual(1, indices3[2].Page.PageIndex);

        Assert.AreEqual(MinuteTlNumBucketsPerPage - 2, indices[0].BucketIndex);
        Assert.AreEqual(MinuteTlNumBucketsPerPage - 1, indices[1].BucketIndex);

        Assert.AreEqual(MinuteTlNumBucketsPerPage - 2, indices2[0].BucketIndex);
        Assert.AreEqual(MinuteTlNumBucketsPerPage - 1, indices2[1].BucketIndex);
        Assert.AreEqual(0, indices2[2].BucketIndex);

        Assert.AreEqual(MinuteTlNumBucketsPerPage - 2, indices3[0].BucketIndex);
        Assert.AreEqual(MinuteTlNumBucketsPerPage - 1, indices3[1].BucketIndex);
        Assert.AreEqual(0, indices3[2].BucketIndex);
        Assert.AreEqual(1, indices3[3].BucketIndex);

        Assert.AreEqual(0, indices2[0].Page.PageIndex);
        Assert.AreEqual(1, indices2[2].Page.PageIndex);
        Assert.AreEqual(1, indices3[3].Page.PageIndex);

        Assert.AreEqual(t0, minuteTl.GetBucketStartTime(indices[0]));
        Assert.AreEqual(t1, minuteTl.GetBucketStartTime(indices[1]));
        Assert.AreEqual(t2, minuteTl.GetBucketStartTime(indices2[2]));
        Assert.AreEqual(t3, minuteTl.GetBucketStartTime(indices3[3]));


        Assert.AreEqual(t1, minuteTl.GetBucketEndTime(indices[0]));
        Assert.AreEqual(t2, minuteTl.GetBucketEndTime(indices[1]));
        Assert.AreEqual(t3, minuteTl.GetBucketEndTime(indices2[2]));
        Assert.AreEqual(t4, minuteTl.GetBucketEndTime(indices3[3]));
    }

    [Test]
    public void TestEqualityMembers()
    {
        Assert.AreEqual(minuteTl, new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromMinutes(1), MinuteTlNumBucketsPerPage)));
        Assert.AreEqual(hourTl, new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromHours(1), HourTlNumBucketsPerPage)));
        Assert.AreEqual(dayTl, new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromDays(1), DayTlNumBucketsPerPage)));

        Assert.AreNotEqual(minuteTl, hourTl);
        Assert.AreNotEqual(hourTl, dayTl);
        Assert.AreNotEqual(dayTl, minuteTl);

        Assert.AreNotEqual(minuteTl, new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromMinutes(1), MinuteTlNumBucketsPerPage - 1)));
        Assert.AreNotEqual(minuteTl, new StatisticsPageTimeline(epochTime + MetaDuration.FromMinutes(1), new StatisticsPageResolution(MetaDuration.FromMinutes(1), MinuteTlNumBucketsPerPage)));
        Assert.AreNotEqual(minuteTl, new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromMinutes(2), MinuteTlNumBucketsPerPage)));

        // Hash code
        Assert.AreEqual(minuteTl.GetHashCode(), new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromMinutes(1), MinuteTlNumBucketsPerPage)).GetHashCode());
        Assert.AreEqual(hourTl.GetHashCode(), new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromHours(1), HourTlNumBucketsPerPage)).GetHashCode());
        Assert.AreEqual(dayTl.GetHashCode(), new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromDays(1), DayTlNumBucketsPerPage)).GetHashCode());

        Assert.AreNotEqual(minuteTl.GetHashCode(), hourTl.GetHashCode());
        Assert.AreNotEqual(hourTl.GetHashCode(), dayTl.GetHashCode());
        Assert.AreNotEqual(dayTl.GetHashCode(), minuteTl.GetHashCode());

        Assert.AreNotEqual(minuteTl.GetHashCode(), new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromMinutes(1), MinuteTlNumBucketsPerPage - 1)).GetHashCode());
        Assert.AreNotEqual(minuteTl.GetHashCode(), new StatisticsPageTimeline(epochTime + MetaDuration.FromMinutes(1), new StatisticsPageResolution(MetaDuration.FromMinutes(1), MinuteTlNumBucketsPerPage)).GetHashCode());
        Assert.AreNotEqual(minuteTl.GetHashCode(), new StatisticsPageTimeline(epochTime, new StatisticsPageResolution(MetaDuration.FromMinutes(2), MinuteTlNumBucketsPerPage)).GetHashCode());
    }
}
