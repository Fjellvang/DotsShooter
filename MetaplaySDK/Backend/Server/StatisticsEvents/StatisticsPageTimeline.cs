// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;

namespace Metaplay.Server.StatisticsEvents;

[MetaSerializable]
public class StatisticsPageResolution : IEquatable<StatisticsPageResolution>
{
    [MetaMember(1)] public string       Name              { get; init; }
    [MetaMember(2)] public MetaDuration BucketInterval    { get; init; }
    [MetaMember(3)] public int          NumBucketsPerPage { get; init; }

    [MetaDeserializationConstructor]
    public StatisticsPageResolution(string name, MetaDuration bucketInterval, int numBucketsPerPage)
    {
        Name              = name;
        BucketInterval    = bucketInterval;
        NumBucketsPerPage = numBucketsPerPage;
    }

    public StatisticsPageResolution(MetaDuration bucketInterval, int numBucketsPerPage)
    {
        Name              = null;
        BucketInterval    = bucketInterval;
        NumBucketsPerPage = numBucketsPerPage;
    }

    StatisticsPageResolution() { }

    #region IEquatable Members
    public bool Equals(StatisticsPageResolution other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && BucketInterval.Equals(other.BucketInterval) && NumBucketsPerPage == other.NumBucketsPerPage;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((StatisticsPageResolution)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, BucketInterval, NumBucketsPerPage);
    }

    public static bool operator ==(StatisticsPageResolution left, StatisticsPageResolution right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StatisticsPageResolution left, StatisticsPageResolution right)
    {
        return !Equals(left, right);
    }
    #endregion
}

[MetaSerializable]
public class StatisticsPageTimeline : IEquatable<StatisticsPageTimeline>
{
    [MetaMember(1)] public MetaTime                 EpochTime         { get; init; }
    [MetaMember(2)] public StatisticsPageResolution Resolution        { get; init; }

    [MetaDeserializationConstructor]
    public StatisticsPageTimeline(MetaTime epochTime, StatisticsPageResolution resolution)
    {
        EpochTime  = epochTime;
        Resolution = resolution;
    }

    StatisticsPageTimeline() { }

    public StatisticsPageIndex GetPage(int page)
    {
        return new StatisticsPageIndex(page, this);
    }

    public StatisticsBucketIndex GetBucket(int page, int bucket)
    {
       return new StatisticsBucketIndex(bucket, new StatisticsPageIndex(page, this));
    }

    public StatisticsBucketIndex GetBucketIndex(MetaTime time)
    {
        MetaDuration timeSinceEpoch = time - EpochTime;
        int          bucketIndex    = (int)((timeSinceEpoch.Milliseconds / Resolution.BucketInterval.Milliseconds) % Resolution.NumBucketsPerPage);
        int          pageIndex      = (int)(timeSinceEpoch.Milliseconds / (Resolution.BucketInterval.Milliseconds * Resolution.NumBucketsPerPage));
        return new StatisticsBucketIndex(bucketIndex, new StatisticsPageIndex(pageIndex, this));
    }

    public MetaTime GetBucketStartTime(StatisticsBucketIndex bucketIndex)
    {
        if (bucketIndex.Page.Timeline != this)
            throw new ArgumentException("Bucket index does not match this timeline");

        return EpochTime +
            (Resolution.BucketInterval * Resolution.NumBucketsPerPage * bucketIndex.Page.PageIndex) +
            (Resolution.BucketInterval * bucketIndex.BucketIndex);
    }

    public MetaTime GetBucketEndTime(StatisticsBucketIndex bucketIndex)
    {
        if (bucketIndex.Page.Timeline != this)
            throw new ArgumentException("Bucket index does not match this timeline");

        return GetBucketStartTime(bucketIndex) + Resolution.BucketInterval;
    }

    public MetaTime GetPageStartTime(StatisticsPageIndex pageIndex)
    {
        if (pageIndex.Timeline != this)
            throw new ArgumentException("Page index does not match this timeline");

        return EpochTime + (Resolution.BucketInterval * Resolution.NumBucketsPerPage * pageIndex.PageIndex);
    }

    public MetaTime GetPageEndTime(StatisticsPageIndex pageIndex)
    {
        if (pageIndex.Timeline != this)
            throw new ArgumentException("Page index does not match this timeline");

        return GetPageStartTime(pageIndex) + (Resolution.BucketInterval * Resolution.NumBucketsPerPage);
    }

    /// <summary>
    /// Get a list of bucket indices that cover the given time range.
    /// </summary>
    /// <param name="startTime">Inclusive</param>
    /// <param name="endTime">Exclusive</param>
    public IEnumerable<StatisticsBucketIndex> GetBucketsForTimeRange(MetaTime startTime, MetaTime endTime)
    {
        if (startTime < EpochTime)
            throw new ArgumentException("Start time is before epoch time");

        if (endTime < startTime)
            throw new ArgumentException("End time is before start time");

        int startPageIndex = (int)((startTime - EpochTime).Milliseconds / (Resolution.BucketInterval.Milliseconds * Resolution.NumBucketsPerPage));
        int endPageIndex   = (int)((endTime - EpochTime).Milliseconds / (Resolution.BucketInterval.Milliseconds * Resolution.NumBucketsPerPage));

        long startBucketMilliseconds = (startTime - GetPageStartTime(new StatisticsPageIndex(startPageIndex, this))).Milliseconds;
        long endBucketMilliseconds   = (endTime - GetPageStartTime(new StatisticsPageIndex(endPageIndex, this))).Milliseconds;
        int  startBucketIndex        = (int)(startBucketMilliseconds / Resolution.BucketInterval.Milliseconds);
        // Round up to the next bucket
        int  endBucketIndex          = (int)((endBucketMilliseconds + Resolution.BucketInterval.Milliseconds - 1) / Resolution.BucketInterval.Milliseconds);

        for (int pageIndex = startPageIndex; pageIndex <= endPageIndex; pageIndex++)
        {
            int startBucket = pageIndex == startPageIndex ? startBucketIndex : 0;
            int endBucket   = pageIndex == endPageIndex ? endBucketIndex : Resolution.NumBucketsPerPage;

            for (int bucketIndex = startBucket; bucketIndex < endBucket; bucketIndex++)
                yield return new StatisticsBucketIndex(bucketIndex, new StatisticsPageIndex(pageIndex, this));
        }
    }

    #region IEquatable Members

    public bool Equals(StatisticsPageTimeline other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return EpochTime.Equals(other.EpochTime) && Equals(Resolution, other.Resolution);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((StatisticsPageTimeline)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(EpochTime, Resolution);
    }

    public static bool operator ==(StatisticsPageTimeline left, StatisticsPageTimeline right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StatisticsPageTimeline left, StatisticsPageTimeline right)
    {
        return !Equals(left, right);
    }

    #endregion
}

public struct StatisticsPageIndex
{
    public int                    PageIndex { get; private set; }

    /// <summary>
    /// Used to prevent leaking indices to other timelines.
    /// </summary>
    public StatisticsPageTimeline Timeline  { get; private set; }

    public StatisticsPageIndex(int pageIndex, StatisticsPageTimeline timeline)
    {
        PageIndex = pageIndex;
        Timeline  = timeline;
    }
}

public struct StatisticsBucketIndex
{
    public int                 BucketIndex { get; private set; }
    public StatisticsPageIndex Page   { get; private set; }

    public StatisticsBucketIndex(int bucketIndex, StatisticsPageIndex page)
    {
        BucketIndex = bucketIndex;
        Page        = page;
    }
}
