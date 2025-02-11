// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.FormattableString;

namespace Metaplay.Server.StatisticsEvents;

/// <summary>
/// Base class for statistics page sections. A statistics page section contains bucket data for a single
/// cohort of a time series. The data is stored in a byte array and is accessed through a view that casts
/// the byte array to the appropriate number type.
/// </summary>
[MetaSerializable]
[MetaReservedMembers(10, 20)]
public abstract class StatisticsPageSectionBase
{
    public ref struct View<TNumber>
        where TNumber : struct, INumber<TNumber>
    {
        public Span<TNumber> Buckets { get; init; }

        public View(Span<TNumber> buckets)
        {
            Buckets = buckets;
        }
    }

    [MetaMember(10)] byte[] _bucketsData;
    [MetaMember(11)] int    NumBuckets { get; set; }

    public abstract bool IsNullable { get; }

    protected StatisticsPageSectionBase(int numBuckets, int dataTypeSizeInBytes)
    {
        NumBuckets = numBuckets;
        _bucketsData = new byte[numBuckets * dataTypeSizeInBytes];
    }

    /// <summary>
    /// Only for deserialization.
    /// </summary>
    protected StatisticsPageSectionBase() {}

    public View<TNumber> As<TNumber>() where TNumber : struct, INumber<TNumber>
    {
        Span<TNumber> tNumberSpan = MemoryMarshal.Cast<byte, TNumber>(_bucketsData.AsSpan());
        return new View<TNumber>(tNumberSpan);
    }

    public StatisticsPageNullableSection.View HasValues
    {
        get
        {
            if (this is not StatisticsPageNullableSection nullableSection)
                throw new InvalidOperationException("This section does not support null values!");

            return nullableSection.HasValues;
        }
    }
    public int DataSizeInBytes => IsNullable ? _bucketsData.Length + NumBuckets : _bucketsData.Length;

    public static StatisticsPageSectionBase Create<TNumber>(int numBuckets, bool nullable) where TNumber : struct, INumber<TNumber>
    {
        int sizeOf = Unsafe.SizeOf<TNumber>();
        if (nullable)
            return new StatisticsPageNullableSection(numBuckets, sizeOf);
        else
            return new StatisticsPageSection(numBuckets, sizeOf);
    }
}

/// <summary>
/// A derived class of <see cref="StatisticsPageSectionBase"/> that does not support null values.
/// </summary>
[MetaSerializableDerived(1)]
public sealed class StatisticsPageSection : StatisticsPageSectionBase
{
    public StatisticsPageSection(int numBuckets, int dataTypeSizeInBytes) : base(numBuckets, dataTypeSizeInBytes) { }

    StatisticsPageSection() { }

    public override bool IsNullable => false;
}

/// <summary>
/// A derived class of <see cref="StatisticsPageSectionBase"/> that supports null values.
/// </summary>
[MetaSerializableDerived(2)]
public sealed class StatisticsPageNullableSection : StatisticsPageSectionBase
{
    public class View
    {
        readonly StatisticsPageNullableSection _section;

        public View(StatisticsPageNullableSection section)
        {
            _section = section;
        }

        public bool this[int idx]
        {
            get
            {
                return _section._bucketHasData[idx] > 0;
            }
            set
            {
                _section._bucketHasData[idx] = value ? (byte)1 : (byte)0;
            }
        }
    }

    [MetaMember(1)] byte[] _bucketHasData;

    public override bool IsNullable => true;

    View            _hasValues;
    public new View HasValues
    {
        get
        {
            _hasValues ??= new View(this);
            return _hasValues;
        }
    }

    public StatisticsPageNullableSection(int numBuckets, int dataTypeSizeInBytes) : base(numBuckets, dataTypeSizeInBytes)
    {
        _bucketHasData = new byte[numBuckets];
    }

    StatisticsPageNullableSection() { }

}

/// <summary>
/// A page for storing <see cref="StatisticsPageSectionBase"/> instances. The page is identified by a unique identifier
/// consisting of the key, the timeline, and the index of the page. All of those components are needed to find and
/// access a page. The page contains a dictionary of sections, where each section is identified by an id that
/// includes the time series key and the cohort key. Each time series reads and writes its data to and from the appropriate section,
/// and each page encompasses a defined timespan of the series, determined by the timeline and the index of the page.
/// </summary>
[MetaSerializable]
public class StatisticsStoragePage
{
    public const string DefaultPageKey = "Default";

    /// <summary>
    /// Note: Not the unique identifier of this page.
    /// </summary>
    [MetaMember(1)] public string                                               PageKey      { get; private set; }
    [MetaMember(2)] public StatisticsPageTimeline                               PageTimeline { get; private set; }
    [MetaMember(3)] public int                                                  PageIndex    { get; private set; }
    [MetaMember(4)] public MetaDictionary<string, StatisticsPageSectionBase> PageSections { get; private set; }
    [MetaMember(5)] public bool                                                 IsFinal      { get; private set; }

    public StatisticsStoragePage(string pageKey, StatisticsPageTimeline pageTimeline, int pageIndex)
    {
        PageKey      = pageKey;
        PageTimeline = pageTimeline;
        PageIndex    = pageIndex;
        PageSections = new MetaDictionary<string, StatisticsPageSectionBase>();
    }

    StatisticsStoragePage() { }

    public string GetSectionId(string timeSeriesKey, string cohort)
    {
        MetaDebug.Assert(timeSeriesKey.IndexOf('/') == -1, "Invalid time series key. Cannot contain the '/' character.");
        MetaDebug.Assert(string.IsNullOrEmpty(cohort) || cohort.IndexOf('/') == -1, "Invalid cohort. Cannot contain the '/' character.");

        return string.IsNullOrEmpty(cohort) ? timeSeriesKey : $"{timeSeriesKey}/{cohort}";
    }

    public IEnumerable<string> GetCohorts(string timeSeriesKey)
    {
        foreach (string sectionId in PageSections.Keys)
        {
            int separator = sectionId.IndexOf('/');
            if (separator != -1)
            {
                if (sectionId.Substring(0, separator) == timeSeriesKey)
                    yield return sectionId.Substring(separator + 1);
            }
        }
    }

    public void SetFinal()
    {
        IsFinal = true;
    }

    public string UniqueIdentifier => BuildUniqueId(PageTimeline, PageKey, PageIndex);

    public static string BuildUniqueId(StatisticsPageTimeline timeline, string pageKey, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(pageKey))
            pageKey = DefaultPageKey;
        if (pageKey.Contains('/'))
            throw new ArgumentException("Invalid page key. Key cannot contain '/' character.", nameof(pageKey));

        string timelineStr = $"{timeline.Resolution.Name}/{timeline.Resolution.BucketInterval.Milliseconds.ToString("X", CultureInfo.InvariantCulture)}/{timeline.EpochTime.MillisecondsSinceEpoch.ToString("X", CultureInfo.InvariantCulture)}/{timeline.Resolution.NumBucketsPerPage.ToString("X", CultureInfo.InvariantCulture)}";
        return Invariant($"{timelineStr}/{pageKey}/{pageIndex}");
    }

    public static (StatisticsPageTimeline timeline, string pageKey, int pageIndex) ParseUniqueId(string uniqueId)
    {
        if (string.IsNullOrWhiteSpace(uniqueId))
            throw new ArgumentException("Unique ID cannot be null or empty.", nameof(uniqueId));

        string[] parts = uniqueId.Split('/');

        if (parts.Length != 6)
            throw new FormatException("Invalid unique ID format. Expected 6 parts.");

        // Extract the components
        string resolutionName    = parts[0];
        int    bucketInterval    = int.Parse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        long   epochTime         = long.Parse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int    numBucketsPerPage = int.Parse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        string pageKey           = parts[4];
        int    pageIndex         = int.Parse(parts[5], CultureInfo.InvariantCulture);

        // Recreate StatisticsPageTimeline
        var resolution = new StatisticsPageResolution(resolutionName, MetaDuration.FromMilliseconds(bucketInterval), numBucketsPerPage);
        var timeline   = new StatisticsPageTimeline(MetaTime.FromMillisecondsSinceEpoch(epochTime), resolution);

        return (timeline, pageKey, pageIndex);
    }

    public StatisticsPageSectionBase TryGetSection(string seriesKey, string cohort)
        => PageSections.GetValueOrDefault(GetSectionId(seriesKey, cohort));
}

/// <summary>
/// Persisted <see cref="StatisticsStoragePage"/>. Consists of
/// - <see cref="UniqueKey"/> to uniquely identify a page.
/// - <see cref="StartTime"/> to mark the start of the timespan of this page.
/// - <see cref="Payload"/> is the serialized payload of the page's contents.
/// </summary>
[Table("StatisticsPages")]
[Index(nameof(StartTime))]
[Index(nameof(EndTime))]
[Index(nameof(CreatedAt))]
public class PersistedStatisticsStoragePage : IPersistedItem
{
    [Required]
    [Key]
    [PartitionKey]
    [Column(TypeName = "varchar(128)")]
    public string UniqueKey { get; set; }

    [Required]
    [Column(TypeName = "varchar(128)")]
    public string ResolutionName { get; set; }

    [Required]
    [Column(TypeName = "DateTime")]
    public DateTime StartTime { get; set; }

    [Required]
    [Column(TypeName = "DateTime")]
    public DateTime EndTime { get; set; }

    [Required]
    [Column(TypeName = "DateTime")]
    public DateTime CreatedAt { get; set; }

    [Required]
    public byte[] Payload { get; set; } // MetaSerialized<StatisticsStoragePage>
}
