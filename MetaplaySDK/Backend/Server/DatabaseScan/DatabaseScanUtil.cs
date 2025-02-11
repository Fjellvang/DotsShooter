// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Metaplay.Server.DatabaseScan.User
{
    public static class DatabaseScanUtil
    {
        static ConcurrentDictionary<Type, MethodInfo> s_specializedQueryPagedRangeSingleShardAsyncCache = new();

        /// <summary>
        /// Helper for invoking <see cref="MetaDatabase.QueryPagedRangeSingleShardAsync"/> and converting the
        /// result to a IPersistedEntity-based result.
        /// </summary>
        static async Task<IEnumerable<IPersistedEntity>> QueryPagedRangeSingleShardHelperAsync<TScannedItem>(MetaDatabase db, string opName, int shardNdx, string iteratorStartKeyExclusive, int pageSize, string rangeFirstKeyInclusive, string rangeLastKeyInclusive)
            where TScannedItem : IPersistedEntity
        {
            IEnumerable<TScannedItem> result = await db.QueryPagedRangeSingleShardAsync<TScannedItem>(opName, shardNdx, iteratorStartKeyExclusive, pageSize, rangeFirstKeyInclusive, rangeLastKeyInclusive);
            return result.Cast<IPersistedEntity>();
        }

        /// <summary>
        /// Invoke <see cref="QueryPagedRangeSingleShardHelperAsync"/>
        /// with a dynamic TScannedItem type (<paramref name="itemType"/>).
        /// </summary>
        public static Task<IEnumerable<IPersistedEntity>> QueryPagedRangeSingleShardAsync(MetaDatabase db, Type itemType, string opName, int shardNdx, string iteratorStartKeyExclusive, int pageSize, string rangeFirstKeyInclusive, string rangeLastKeyInclusive)
        {
            MethodInfo specialized = s_specializedQueryPagedRangeSingleShardAsyncCache.GetOrAdd(itemType, type =>
            {
                MethodInfo generic = typeof(DatabaseScanUtil).GetMethod(nameof(QueryPagedRangeSingleShardHelperAsync), BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo specialized = generic.MakeGenericMethod(new Type[] { type });
                return specialized;
            });
            return (Task<IEnumerable<IPersistedEntity>>)specialized.Invoke(null, new object[] { db, opName, shardNdx, iteratorStartKeyExclusive, pageSize, rangeFirstKeyInclusive, rangeLastKeyInclusive });
        }
    }
}
