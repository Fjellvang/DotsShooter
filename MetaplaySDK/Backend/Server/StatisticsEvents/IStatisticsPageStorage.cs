// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.StatisticsEvents;

public interface IStatisticsPageStorage
{
    public bool IsWritable { get; }

    Task WriteNewAsync (StatisticsStoragePage page);
    Task<StatisticsStoragePage> TryGetAsync (string pageId);
}

public class DatabaseStatisticsPageStorage : IStatisticsPageStorage
{
    public bool IsWritable => true;

    public DatabaseStatisticsPageStorage() { }

    public async Task WriteNewAsync(StatisticsStoragePage page)
    {
        if (page == null)
            throw new ArgumentNullException(nameof(page));
        if (!page.IsFinal)
            throw new ArgumentException("Can only write final pages to the database.");

        MetaDatabase db = MetaDatabase.Get(QueryPriority.Normal);

        PersistedStatisticsStoragePage persisted = CreatePersisted(page);

        Serilog.Log.Debug("Writing statistics storage page {persisted}", PrettyPrint.Compact(persisted));

        if (!await db.InsertOrIgnoreAsync(persisted))
            Serilog.Log.Error("Failed to write statistics storage page due to duplicate id: {Id}", page.UniqueIdentifier);
    }

    public async Task<StatisticsStoragePage> TryGetAsync(string pageId)
    {
        MetaDatabase db = MetaDatabase.Get(QueryPriority.Normal);

        // \todo: Use read replica? Might mess up caching because of _confirmedMissingPages :(
        PersistedStatisticsStoragePage persisted = await db.TryGetAsync<PersistedStatisticsStoragePage>(pageId);

        if (persisted == null)
            return null;

        StatisticsStoragePage page = MetaSerialization.DeserializeTagged<StatisticsStoragePage>(
            persisted.Payload,
            MetaSerializationFlags.IncludeAll,
            null,
            null);

        return page;
    }

    static PersistedStatisticsStoragePage CreatePersisted(StatisticsStoragePage page)
    {
        StatisticsPageIndex pageIndex = page.PageTimeline.GetPage(page.PageIndex);
        return new PersistedStatisticsStoragePage
        {
            UniqueKey      = page.UniqueIdentifier,
            ResolutionName = page.PageTimeline.Resolution.Name,
            StartTime      = page.PageTimeline.GetPageStartTime(pageIndex).ToDateTime(),
            EndTime        = page.PageTimeline.GetPageEndTime(pageIndex).ToDateTime(),
            CreatedAt      = DateTime.UtcNow,
            Payload        = MetaSerialization.SerializeTagged(page, MetaSerializationFlags.IncludeAll, logicVersion: null),
        };
    }
}

public class CollectorActorStatisticsPageStorage : IStatisticsPageStorage
{
    readonly IEntityMessagingApi _messagingApi;
    readonly EntityId            _actorId;
    bool IStatisticsPageStorage. IsWritable => false;

    public CollectorActorStatisticsPageStorage(IEntityMessagingApi messagingApi, EntityId actorId)
    {
        _messagingApi = messagingApi;
        _actorId      = actorId;
    }

    public Task WriteNewAsync(StatisticsStoragePage page)
    {
        throw new NotImplementedException("This storage does not support write operations.");
    }

    public async Task<StatisticsStoragePage> TryGetAsync(string pageId)
    {
        StatsCollectorStatisticsPageReadResponse response =
            await _messagingApi.EntityAskAsync<StatsCollectorStatisticsPageReadResponse>(
            _actorId,
            new StatsCollectorStatisticsPageReadRequest(pageId));

        return response.Page;
    }
}


[MetaSerializable]
public class MockStatisticsPageStorage : IStatisticsPageStorage
{
    [MetaMember(1)] public MetaDictionary<string, StatisticsStoragePage> Pages { get; private set; }

    public MockStatisticsPageStorage()
    {
        Pages = new MetaDictionary<string, StatisticsStoragePage>();
    }

    public bool IsWritable => true;

    public Task WriteNewAsync(StatisticsStoragePage page)
    {
        if (page == null)
            throw new ArgumentNullException(nameof(page));
        if (!page.IsFinal)
            throw new ArgumentException("Can only write final pages to the database.");
        if (!Pages.TryAdd(page.UniqueIdentifier, page))
            throw new InvalidOperationException("Page already exists.");

        return Task.CompletedTask;
    }

    public Task<StatisticsStoragePage> TryGetAsync(string pageId)
    {
        return Task.FromResult(Pages.GetValueOrDefault(pageId));
    }
}

public class TieredStatisticsPageStorage : IStatisticsPageStorage
{
    readonly IStatisticsPageStorage _primary;
    readonly IStatisticsPageStorage _secondary;

    public bool IsWritable => false;

    public TieredStatisticsPageStorage(IStatisticsPageStorage primary, IStatisticsPageStorage secondary)
    {
        _primary   = primary;
        _secondary = secondary;
    }

    public Task WriteNewAsync(StatisticsStoragePage page)
    {
        throw new NotImplementedException("This storage does not support write operations.");
    }

    public async Task<StatisticsStoragePage> TryGetAsync(string pageId)
    {
        StatisticsStoragePage page = await _primary.TryGetAsync(pageId);

        if (page == null)
            page = await _secondary.TryGetAsync(pageId);

        return page;
    }
}

public class CachedStatisticsPageStorage : IStatisticsPageStorage
{
    public bool IsWritable => false;

    readonly IStatisticsPageStorage  _backingStorage;
    readonly MemoryCache             _cache;
    readonly Dictionary<string, int> _successfulReadIndex   = new Dictionary<string, int>();
    readonly bool                    _cacheNonFinal;
    readonly object                  _lockObject;
    readonly object                  _missingMarker;

    public CachedStatisticsPageStorage(IStatisticsPageStorage backingStorage, int cacheSize, bool cacheNonFinal)
    {
        _backingStorage = backingStorage;
        _cache          = new MemoryCache(new MemoryCacheOptions()
        {
            SizeLimit = cacheSize,
        });
        _cacheNonFinal = cacheNonFinal;
        _lockObject = new object();
        _missingMarker = new object();
    }

    public Task WriteNewAsync(StatisticsStoragePage page)
    {
        throw new NotImplementedException("This storage does not support write operations.");
    }

    public async Task<StatisticsStoragePage> TryGetAsync(string pageId)
    {
        if (_cache.TryGetValue(pageId, out object pageObj))
        {
            // Serilog.Log.Debug("Cache hit for {pageId} missing: {Missing}", pageId, pageObj == _missingMarker);

            if (pageObj is StatisticsStoragePage statisticsPage)
                return statisticsPage;

            if (pageObj == _missingMarker) // We know this page is missing. Return null
                return null;

            throw new InvalidOperationException($"Unexpected type found in the cache!: {pageObj?.GetType()}");
        }

        StatisticsStoragePage page = await _backingStorage.TryGetAsync(pageId);

        (StatisticsPageTimeline timeline, _, int pageIndex) = StatisticsStoragePage.ParseUniqueId(pageId);

        lock (_lockObject)
        {
            if (!_successfulReadIndex.TryGetValue(timeline.Resolution.Name, out int maxSuccessfulPageIndexForTimeline))
                maxSuccessfulPageIndexForTimeline = -1;

            if (page == null)
            {
                // We can confirm that a page is missing if the page index is earlier than
                // the max page index we've read. Future pages can be missing, but past
                // pages should not be.
                bool isConfirmedMissing = pageIndex < maxSuccessfulPageIndexForTimeline;

                MemoryCacheEntryOptions options = new()
                {
                    Size                            = pageId.Length,
                    AbsoluteExpirationRelativeToNow = isConfirmedMissing ? TimeSpan.FromHours(1) : TimeSpan.FromSeconds(5),
                };

                // Serilog.Log.Debug("Setting missing marker for {PageId}. Duration {Expiration}", pageId, options.AbsoluteExpirationRelativeToNow);

                _cache.Set(pageId, _missingMarker, options);

                return null;
            }

            if (page.IsFinal)
            {
                // Page was found as a final page.
                // We can assume any indices older than this are lost forever if not found.
                if (pageIndex > maxSuccessfulPageIndexForTimeline)
                    _successfulReadIndex[timeline.Resolution.Name] = pageIndex;

                MemoryCacheEntryOptions cacheEntryOptions = new()
                {
                    Size              = ApproximateSizeOfPage(page),
                    SlidingExpiration = TimeSpan.FromDays(1),
                };

                // Serilog.Log.Debug("Setting long cache entry for {PageId} with size {Size}", pageId, cacheEntryOptions.Size);

                // Cache.Set will trigger OvercapacityCompaction on a background thread if over capacity.
                // We don't need to manually call Compact() here.
                _cache.Set(pageId, page, cacheEntryOptions);
            }
            else if (_cacheNonFinal)
            {
                MemoryCacheEntryOptions cacheEntryOptions = new()
                {
                    Size                            = 0,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5),
                };

                // Serilog.Log.Debug("Setting short cache entry for {PageId}", pageId);

                _cache.Set(pageId, page, cacheEntryOptions);
            }
        }

        return page;
    }

    static int ApproximateSizeOfPage(StatisticsStoragePage page)
    {
        if (page == null)
            return 0;

        // Return sum of bytes in page sections + length of pageKey string
        return page.PageKey.Length + page.PageSections.Sum(section => section.Key.Length + section.Value.DataSizeInBytes);
    }
}

public static class StatisticsPageStorageExtensions
{
    public static TieredStatisticsPageStorage AsTiered(this IStatisticsPageStorage primary, IStatisticsPageStorage secondary)
        => new TieredStatisticsPageStorage(primary, secondary);

    public static CachedStatisticsPageStorage AsCached(this IStatisticsPageStorage storage, int cacheSize, bool cacheNonFinal)
        => new CachedStatisticsPageStorage(storage, cacheSize, cacheNonFinal);
}


