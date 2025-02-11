// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Dapper;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Persistence
{
    /// <summary>
    /// Database migrator. Ensures that database shards are both up-to-date with latest database schema (see GameDbContext)
    /// and handles re-sharding of databases when shards are added or removed.
    /// </summary>
    public class DatabaseMigrator
    {
        IMetaLogger         _log        = MetaLogger.ForContext<DatabaseMigrator>();
        MetaDatabase        _db         = MetaDatabase.Get(QueryPriority.Normal);
        DatabaseBackend     _backend    = DatabaseBackend.Instance;

        public DatabaseMigrator()
        {
        }

        #region Schema migration

        /// <summary>
        /// Drop all the tables in a database shard, except 'MetaInfo' is only dropped if <paramref name="dropMetaInfoTable"/> is true.
        /// </summary>
        /// <param name="shardNdx">Database shard index to reset</param>
        /// <returns></returns>
        async Task ResetShardAsync(int shardNdx, bool dropMetaInfoTable)
        {
            // Fetch all table names in the database
            string[] tableNames = await _backend.GetTableNamesAsync(shardNdx);

            // If removing MetaInfo is not allowed, filter it from the list of tables to remove
            // \note On some platforms (MySQL on Windows), the table names can be returned in lower case so use case-insensitive comparison!
            if (!dropMetaInfoTable)
                tableNames = tableNames.Where(tableName => !string.Equals(tableName, DatabaseMetaInfo.TableName, StringComparison.OrdinalIgnoreCase)).ToArray();

            // Drop the tables (except MetaInfo)
            // \note drop tables one at a time, because SQLite doesn't support multiple tables in one command
            _log.Information("Dropping database tables in shard #{Shard}: {TableNames}", shardNdx, PrettyPrint.Compact(tableNames));
            await _backend.ExecuteRawAsync(DatabaseReplica.ReadWrite, shardNdx, async conn =>
            {
                foreach (string tableName in tableNames)
                    await conn.ExecuteAsync($"DROP TABLE IF EXISTS {tableName}").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Reset all the database shards to the empty state. The MetaInfo table stores the intention-to-reset the
        /// database (MasterVersion==ResetInProgressMasterVersion) so we must delete the MetaInfo (on shard #0) as the
        /// last one in order to be able to resume the reset logic at the next server boot in case the resetting is
        /// interrupted for any reason.
        /// </summary>
        /// <param name="numShards">Number of shards to reset</param>
        /// <returns></returns>
        async Task ResetShardRangeAsync(int numShards)
        {
            // First drop all tables except for 'MetaInfo' (leave that for later to keep a resumable state)
            await Task.WhenAll(
                Enumerable.Range(0, numShards)
                .Select(async shardNdx => await ResetShardAsync(shardNdx, dropMetaInfoTable: false).ConfigureAwait(false)))
                .ConfigureAwait(false);

            // Finally, drop the 'MetaInfo' table in reverse order -- use reverse order so MetaInfo (on shard #0) is the last one to be removed
            // table (on shard#0) is deleted last (as it marks the deletion-in-progress).
            for (int shardNdx = numShards - 1; shardNdx >= 0; shardNdx--)
                await ResetShardAsync(shardNdx, dropMetaInfoTable: true).ConfigureAwait(false);
        }

        /// <summary>
        /// Migrate a database shard to the latest EFCore schema.
        /// </summary>
        /// <param name="shardNdx"></param>
        /// <returns></returns>
        async Task MigrateShardAsync(int shardNdx)
        {
            // For MySql, ensure database uses utf8mb4 and binary comparisons, so EntityId etc. columns use case-sensitive comparisons.
            // MySql defaults to case-insensitive comparison, which causes Player profiles to conflict where id only differs by case.
            // Note: Gets applied on every start (even if no migration is needed) -- a bit wasteful but not a problem
            if (_backend.BackendType == DatabaseBackendType.MySql)
            {
                await _backend.ExecuteRawAsync(DatabaseReplica.ReadWrite, shardNdx, conn =>
                {
                    return conn.ExecuteAsync("alter database character set utf8mb4 collate utf8mb4_bin");
                }).ConfigureAwait(false);
            }

            // Perform all pending EFCore migrations.
            await _backend.ExecuteShardedDbContextAsync(DatabaseReplica.ReadWrite, shardNdx, async context =>
            {
                // Start task that prints time progression periodically
                Stopwatch sw = Stopwatch.StartNew();
                await RunTaskAndPrintProgressAsync(
                    async () =>
                    {
                        // Allow lots of time (migration can take a long time for large databases)
                        TimeSpan MigrationTimeout = TimeSpan.FromHours(1);
                        _log.Information("Start migration of database shard #{ShardNdx} (timeout={MigrationTimeout})..", shardNdx, MigrationTimeout);
                        context.Database.SetCommandTimeout(MigrationTimeout);

                        // \note MySQL doesn't support rollbacks for DDL operations (like modifying tables), so even though underlying
                        //       migrations use transactions, they don't actually do much at all.
                        string[] appliedMigrations = (await context.Database.GetAppliedMigrationsAsync().ConfigureAwait(false)).ToArray();
                        string[] knownMigrations = context.Database.GetMigrations().ToArray();

                        // Log what we are about to migrate.
                        string[] pendingMigrations = knownMigrations.Except(appliedMigrations).ToArray();
                        if (pendingMigrations.Length > 0)
                            _log.Information("Migration of database shard #{ShardNdx} has {NumPending} pending migration(s): [{Pending}]", shardNdx, pendingMigrations.Length, string.Join(", ", pendingMigrations));

                        // Log if there are migrations applied that we don't know of.
                        string[] unknownMigrations = appliedMigrations.Except(knownMigrations).ToArray();
                        if (unknownMigrations.Length > 0)
                            _log.Warning("Database shard #{ShardNdx} has {NumUnknown} unknown migration(s): [{Unknown}]", shardNdx, unknownMigrations.Length, string.Join(", ", unknownMigrations));

                        // Execute migration steps
                        await context.Database.MigrateAsync().ConfigureAwait(false);

                        _log.Information("Migration of database shard #{ShardNdx} completed ({MigrationElapsed}s elapsed)", shardNdx, sw.Elapsed.TotalSeconds);
                    },
                    progressInterval: TimeSpan.FromSeconds(10),
                    progressCallback: () =>
                    {
                        _log.Information("Migrating database shard #{ShardNdx}: {TimeElapsed} elapsed", shardNdx, sw.Elapsed);
                    }).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Initialize a database shard to the latest EFCore schema, using "EnsureCreated".
        /// </summary>
        /// <param name="shardNdx"></param>
        /// <returns></returns>
        public async Task CreateShardAsync(int shardNdx)
        {
            await _backend.ExecuteShardedDbContextAsync(
                DatabaseReplica.ReadWrite,
                shardNdx,
                async context =>
                {
                    await context.Database.EnsureCreatedAsync();
                });
        }

        /// <summary>
        /// Migrate the given shard range to latest database schema. Performs migration in parallel.
        /// </summary>
        /// <param name="startNdx">Start index of shards to migrate</param>
        /// <param name="numShards">Number of shards to migrate</param>
        /// <param name="shardMigrationFunction">The migration operation to perform on each shard, either <see cref="MigrateShardAsync"/> or <see cref="CreateShardAsync"/>.</param>
        /// <returns></returns>
        public async Task MigrateShardsAsync(int startNdx, int numShards, Func<int, Task> shardMigrationFunction)
        {
            // Migrate all shards to latest schema version (in parallel)
            await Task.WhenAll(
                Enumerable.Range(startNdx, numShards)
                .Select(async shardNdx =>
                {
                    // Wrap in Task.Run() to ensure SQLite also runs in parallel
                    await Task.Run(async () =>
                    {
                        try
                        {
                            await shardMigrationFunction(shardNdx).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Failed to migrate database shard #{ShardNdx}: {Exception}", shardNdx, ex);
                            throw;
                        }
                    }).ConfigureAwait(false);
                })).ConfigureAwait(false);
        }

        #endregion // Schema migration

        #region Re-sharding

        /// <summary>
        /// Read all items from a database table in a streaming fashion, with item batching.
        /// </summary>
        /// <typeparam name="TItem">Type of item to read</typeparam>
        /// <param name="conn">Database connection object</param>
        /// <param name="query">Query to execute</param>
        /// <param name="param">Query parameters object</param>
        /// <param name="batchSize">Number of items to return in each batch</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> of item batches</returns>
        async IAsyncEnumerable<List<TItem>> ScanTableItemsBatched<TItem>(DbConnection conn, string query, object param, int batchSize)
        {
            DbDataReader reader = await conn.ExecuteReaderAsync(query, param, commandTimeout: 3600);
            Func<DbDataReader, TItem> parseRow = reader.GetRowParser<TItem>();

            List<TItem> list = new List<TItem>(capacity: batchSize);
            while (await reader.ReadAsync())
            {
                TItem item = parseRow(reader);
                list.Add(item);
                if (list.Count == batchSize)
                {
                    yield return list;
                    list = new List<TItem>(capacity: batchSize);
                }
            }

            // Return last batch
            if (list.Count > 0)
                yield return list;
        }

        /// <summary>
        /// Migrate entities of type <typeparamref name="TItem"/> from shard <paramref name="srcShardNdx"/> to all other shards.
        /// </summary>
        /// <param name="srcShardNdx"></param>
        /// <returns></returns>
        async Task ReshardItemsFromShardAsync<TItem>(int srcShardNdx) where TItem : IPersistedItem
        {
            _log.Information("Re-sharding table {EntityType} items from shard #{SrcShardNdx}", typeof(TItem).Name, srcShardNdx);
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<TItem>();

            string query = $"SELECT * FROM {itemSpec.TableName} WHERE ({_backend.GetShardingSql(itemSpec.PartitionKeyName)} % @NumShards) <> @ShardNdx";
            await _backend.ExecuteRawAsync(DatabaseReplica.ReadWrite, srcShardNdx, async srcConn =>
            {
                IAsyncEnumerable<List<TItem>> batches = ScanTableItemsBatched<TItem>(srcConn, query, new { ShardNdx = srcShardNdx, NumShards = _db.NumActiveShards }, batchSize: 1000);
                await foreach (List<TItem> batch in batches)
                {
                    // Write entities into the correct shard
                    // \todo [petri] would it be safer to overwrite (could help in scenarios where re-sharding into a shard with stale data)?
                    await _db.MultiInsertOrIgnoreAsync(batch).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Reshard all items in all tables from the given source shard.
        /// </summary>
        /// <param name="srcShardNdx"></param>
        /// <returns></returns>
        async Task ReshardAllTablesFromShardAsync(int srcShardNdx)
        {
            // Reshard all tables sequentially (to limit parallelism somewhat)
            MethodInfo reshardTableMethod = typeof(DatabaseMigrator).GetMethod(nameof(ReshardItemsFromShardAsync), BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (DatabaseItemSpec itemSpec in DatabaseTypeRegistry.ItemSpecs)
            {
                // Only reshard partitioned items
                if (itemSpec.IsPartitioned)
                {
                    _log.Information("Re-sharding table {TableName} items from shard #{SrcShardNdx}", itemSpec.TableName, srcShardNdx);
                    MethodInfo reshardMethod = reshardTableMethod.MakeGenericMethod(itemSpec.ItemType);
                    Task task = (Task)reshardMethod.Invoke(this, new object[] { srcShardNdx });
                    await task.ConfigureAwait(false);
                }
            }
        }

        async Task<bool> AreShardsTableEqualAsync<TItem>(int srcShardNdx, List<int> dstShardNdxs) where TItem : IPersistedItem
        {
            const string    OpName      = "ShardEquality";
            const int       PageSize    = 1000;

            // Only compare partitioned tables (non-partitioned should not be equal)
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<TItem>();
            if (!itemSpec.IsPartitioned)
                throw new InvalidOperationException($"Trying to compare non-partitioned table {itemSpec.TableName} for shard inequality");

            // Initialize iterator keys
            int         numTargets              = dstShardNdxs.Count;
            string      srcIteratorStartKey     = "";
            string[]    dstIteratorStartKeys    = dstShardNdxs.Select(_ => "").ToArray();

            while (true)
            {
                // Query page of source items
                List<TItem> srcItems =
                    (await _db.QueryPagedFullSingleShardAsync<TItem>(
                        opName:                     OpName,
                        shardNdx:                   srcShardNdx,
                        iteratorStartKeyExclusive:  srcIteratorStartKey,
                        pageSize:                   PageSize).ConfigureAwait(false))
                    .ToList();
                int numItems = srcItems.Count;

                // Update iterator
                if (srcItems.Count > 0)
                    srcIteratorStartKey = itemSpec.GetItemPrimaryKey(srcItems.Last());

                // Fetch page of items from each target shard
                TItem[][] dstItems = await Task.WhenAll(
                    Enumerable.Range(0, numTargets)
                    .Select(async ndx => (await _db.QueryPagedFullSingleShardAsync<TItem>(OpName, dstShardNdxs[ndx], dstIteratorStartKeys[ndx], PageSize).ConfigureAwait(false)).ToArray()))
                    .ConfigureAwait(false);

                // Check lengths of each target page and update iterators
                for (int ndx = 0; ndx < numTargets; ndx++)
                {
                    // If count mismatches, shards cannot be equal
                    if (dstItems[ndx].Length != numItems)
                    {
                        _log.Information("Detected mismatched item count in table {TableName} between shards #{SrcShardNdx} and #{DstShardNdx}", typeof(TItem).Name, srcShardNdx, dstShardNdxs[ndx]);
                        return false;
                    }

                    // Update iterator
                    if (dstItems[ndx].Length > 0)
                        dstIteratorStartKeys[ndx] = itemSpec.GetItemPrimaryKey(dstItems[ndx].Last());
                }

                // Compare all items
                // \todo [petri] this only compares primary keys, compare other members as well?
                for (int itemNdx = 0; itemNdx < numItems; itemNdx++)
                {
                    TItem srcItem = srcItems[itemNdx];
                    string srcPrimaryKey = itemSpec.GetItemPrimaryKey(srcItem);

                    for (int ndx = 0; ndx < numTargets; ndx++)
                    {
                        TItem dstItem = dstItems[ndx][itemNdx];
                        string dstPrimaryKey = itemSpec.GetItemPrimaryKey(dstItem);
                        if (dstPrimaryKey != srcPrimaryKey)
                        {
                            _log.Information("Detected mismatched primary key in table {TableName} between shards #{SrcShardNdx} and #{DstShardNdx}", typeof(TItem).Name, srcShardNdx, dstShardNdxs[ndx]);
                            return false;
                        }

                        // If item is an IPersistedEntity, make a more thorough check
                        // \todo [petri] Check more members of non-IPersistedEntity types, too?
                        if (srcItem is IPersistedEntity srcEntity)
                        {
                            IPersistedEntity dstEntity = (IPersistedEntity)dstItem;
                            if (srcEntity.PersistedAt != dstEntity.PersistedAt)
                            {
                                _log.Warning("Detected mismatched PersistedAt in table {TableName} for item {EntityId}: {SrcPersistedAt} in #{SrcShardNdx} vs {DstPersistedAt} in #{DstShardNdx}", typeof(TItem).Name, srcEntity.EntityId, srcEntity.PersistedAt, srcShardNdx, dstEntity.PersistedAt, dstShardNdxs);
                                return false;
                            }

                            if (srcEntity.SchemaVersion != dstEntity.SchemaVersion)
                            {
                                _log.Warning("Detected mismatched SchemaVersion in table {TableName} for item {EntityId}: {SrcPersistedAt} in #{SrcShardNdx} vs {DstPersistedAt} in #{DstShardNdx}", typeof(TItem).Name, srcEntity.EntityId, srcEntity.SchemaVersion, srcShardNdx, dstEntity.SchemaVersion, dstShardNdxs);
                                return false;
                            }

                            if (!Util.ArrayEqual(srcEntity.Payload, dstEntity.Payload))
                            {
                                _log.Warning("Detected mismatched Payload in table {TableName} for item {EntityId}: #{SrcShardNdx} vs #{DstShardNdx}", typeof(TItem).Name, srcEntity.EntityId, srcShardNdx, dstShardNdxs);
                                return false;
                            }
                        }
                    }
                }

                // If no items, all target shards match source!
                if (srcItems.Count == 0)
                    return true;
            }
        }

        async Task<bool> AreShardsEqualAsync(int srcShardNdx, List<int> dstShardNdxs)
        {
            // Compare all tables sequentially (to limit parallelism)
            MethodInfo compareTableMethod = typeof(DatabaseMigrator).GetMethod(nameof(AreShardsTableEqualAsync), BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (DatabaseItemSpec itemSpec in DatabaseTypeRegistry.ItemSpecs)
            {
                // Only check partitioned items (non-partitioned are always on shard #0) with primary key
                // \todo [petri] Skipping the PrimaryKeyless items is not strictly correct, but comparing all the other tables should be safe enough
                if (itemSpec.IsPartitioned && itemSpec.HasPrimaryKey)
                {
                    _log.Information("Comparing table {TableName} shards for equality to shard #{SrcShardNdx}", itemSpec.TableName, srcShardNdx);
                    MethodInfo compareMethod = compareTableMethod.MakeGenericMethod(itemSpec.ItemType);
                    Task<bool> compareTask = (Task<bool>)compareMethod.Invoke(this, new object[] { srcShardNdx, dstShardNdxs });
                    if (!await compareTask.ConfigureAwait(false))
                    {
                        _log.Information("Table {TableName} new shards failed equality comparison to source shard #{SrcShardNdx}", itemSpec.TableName, srcShardNdx);
                        return false;
                    }

                    _log.Information("Table {TableName} new shards are all equal to source shard #{SrcShardNdx}", itemSpec.TableName, srcShardNdx);
                }
            }

            return true;
        }

        /// <summary>
        /// Reshard the database from <paramref name="numOldShards"/> shards to <see cref="MetaDatabase.NumActiveShards"/> (of <see cref="_db"/>).
        /// The resharding itself is idempotent, so it is safe to restart it after it has terminated incorrectly.
        /// The idempotency is achieved by first copying (without removing) all the items to their correct shards,
        /// and only then pruning (removing) any items that don't belong on other shards.
        /// </summary>
        /// <param name="numOldShards">Number of previously active shards</param>
        /// <returns></returns>
        async Task ReshardAllEntitiesAsync(int numOldShards)
        {
            // Check if we can use integer multiple up-sharding fast path: new number of shards must be an integer
            // multiple of the old number, and all the new shards must be in-order clones of the original shards.
            bool useIntegerMultipleReshardingFastPath = false;
            if (_db.NumActiveShards > numOldShards && _db.NumActiveShards % numOldShards == 0)
            {
                int multiplier = _db.NumActiveShards / numOldShards;
                _log.Information("Testing whether fast-path for integer multiple re-sharding can be used (with multiplier {Multiplier})", multiplier);

                // Spawn task for each source shard to check whether all the new shards are equal to the corresponding original shards
                // \todo [petri] could optimize by canceling all the tasks when the first one finds a difference
                bool[] areEntitiesEqual = await Task.WhenAll(
                    Enumerable.Range(0, numOldShards)
                    .Select(async srcShardNdx =>
                    {
                        List<int> dstShards = Enumerable.Range(0, multiplier - 1).Select(i => srcShardNdx + (i + 1) * numOldShards).ToList();
                        return await AreShardsEqualAsync(srcShardNdx, dstShards).ConfigureAwait(false);
                    })).ConfigureAwait(false);

                // _database.Execute all the tasks and combine results
                useIntegerMultipleReshardingFastPath = areEntitiesEqual.All(canUseFastPath => canUseFastPath);
                if (useIntegerMultipleReshardingFastPath)
                    _log.Information("Can use integer multiple re-sharding fast path");
            }

            // If fast-path cannot be used, perform full re-sharding
            if (!useIntegerMultipleReshardingFastPath)
            {
                // _database.Execute a migration task for each src-dst shard pair, in parallel
                _log.Information("Performing full re-sharding from all source shards to all other shards (integer multiple fast-path cannot be used)");
                await Task.WhenAll(
                    Enumerable.Range(0, numOldShards)
                    .Select(async srcShardNdx => await ReshardAllTablesFromShardAsync(srcShardNdx).ConfigureAwait(false)))
                    .ConfigureAwait(false);
            }

            // Prune all non-belonging items from all shards, in parallel
            await Task.WhenAll(
                Enumerable.Range(0, _db.NumActiveShards)
                .Select(async shardNdx => await PruneShardPostDuplicateAsync(shardNdx).ConfigureAwait(false)))
                .ConfigureAwait(false);

            // Purge any old shards that are no longer used
            // \todo If this is interrupted, the to-be-removed shards can be left with some tables in them. Implement more robustly.
            if (numOldShards > _db.NumActiveShards)
            {
                _log.Information("Resetting to-be-removed shards #{StartShardNdx}..#{EndShardNdx}", _db.NumActiveShards, numOldShards - 1);
                await Task.WhenAll(
                    Enumerable.Range(_db.NumActiveShards, numOldShards - _db.NumActiveShards)
                    .Select(async shardNdx => await ResetShardAsync(shardNdx, dropMetaInfoTable: true).ConfigureAwait(false)))
                    .ConfigureAwait(false);
            }
        }

        async Task PruneShardPostDuplicateAsync(int shardNdx)
        {
            // Prune all item types
            // \note Doing this sequentially (instead of in parallel). The assumption is that the database would perform better as it can focus on smaller dataset at a time.
            foreach (DatabaseItemSpec item in DatabaseTypeRegistry.ItemSpecs)
            {
                if (item.IsPartitioned)
                {
                    _log.Information("Shard #{ShardNdx} table {TableName}: Pruning partitioned items that don't belong on this shard", shardNdx, item.TableName);
                    await _backend.ExecuteRawAsync(DatabaseReplica.ReadWrite, shardNdx, async conn =>
                    {
                        int numBefore = await conn.QuerySingleAsync<int>($"SELECT COUNT(*) FROM {item.TableName}", commandTimeout: 3600).ConfigureAwait(false);
                        int numRemoved = await conn.ExecuteAsync($"DELETE FROM {item.TableName} WHERE ({_backend.GetShardingSql(item.PartitionKeyName)} % @NumShards) <> @ShardNdx", new { ShardNdx = shardNdx, NumShards = _db.NumActiveShards }, commandTimeout: 3600).ConfigureAwait(false);
                        _log.Information("Shard #{0} table {1}: {2} items removed of {3} total, {4} remaining", shardNdx, item.TableName, numRemoved, numBefore, numBefore - numRemoved);
                    }).ConfigureAwait(false);
                }
                else
                {
                    // For non-partitioned tables, only keep items in shard 0, drop all items from other shards
                    if (shardNdx != 0)
                    {
                        _log.Information("Shard #{ShardNdx} table {TableName}: Pruning all non-partitioned items as not primary shard", shardNdx, item.TableName);
                        await _backend.ExecuteRawAsync(DatabaseReplica.ReadWrite, shardNdx, async conn =>
                        {
                            await conn.ExecuteAsync($"DELETE FROM {item.TableName}").ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }
                }
            }
        }

        #endregion // Re-sharding

        /// <summary>
        /// Ensure that database schemas and sharding is up-to-date. The operations performed:
        /// - Reset the whole database on MasterVersion mismatch (and safety switches allow it).
        /// - Migrate all database shards to the current EFCore schema.
        /// - Re-shard database to the desired number of shards.
        /// </summary>
        /// <returns></returns>
        public async Task EnsureMigratedAsync()
        {
            DatabaseOptions dbOpts = RuntimeOptionsRegistry.Instance.GetCurrent<DatabaseOptions>();

            // Database master version must be defined in Options.xx.yaml
            if (dbOpts.MasterVersion <= 0)
                throw new InvalidOperationException($"{nameof(DatabaseOptions)}.{nameof(DatabaseOptions.MasterVersion)} is invalid, set it to a positive integer value in Config/Options.base.yaml");

            // Fetch latest meta info (null if table or row doesn't exists)
            DatabaseMetaInfo metaInfo = await _db.TryGetLatestMetaInfoAsync().ConfigureAwait(false);

            // Number of total shards to consider (includes to-be-added and to-be-deleted shards when re-sharding)
            int numOldShards = metaInfo != null ? metaInfo.NumShards : 0;
            int numTotalShards = Math.Max(_db.NumActiveShards, numOldShards);
            _log.Information("Migrating database with {NumTotalShards} shards: {NumActiveShards} shards active in configuration, {NumDbShards} shards in use", numTotalShards, _db.NumActiveShards, numOldShards);

            // Check that all required shards are configured
            if (numTotalShards > _backend.NumTotalShards)
                throw new InvalidOperationException($"Missing database shard definitions! {numTotalShards} shards are referenced ({_db.NumActiveShards} are active and database has {metaInfo.NumShards}) , but only {_backend.NumTotalShards} shards are configured in runtime options!");

            // RESET ON MASTER VERSION CHANGE (for all shards, including to-be-removed and to-be-added ones due to re-sharding)

            // Handle database master version
            if (metaInfo == null)
            {
                // No meta info found, create empty MetaInfo
                _log.Information("No meta-info found in database, assuming empty database");
                metaInfo = new DatabaseMetaInfo(version: 0, DateTime.UnixEpoch, masterVersion: 0, numShards: _db.NumActiveShards);
            }
            else
            {
                // Database exists, check if should reset database due to master version mismatch
                if (metaInfo.MasterVersion != dbOpts.MasterVersion)
                {
                    // Check that resets are allowed
                    if (!dbOpts.NukeOnVersionMismatch)
                        throw new InvalidOperationException($"Database master version mismatch: got v{metaInfo.MasterVersion}, expecting v{dbOpts.MasterVersion}, database reset not allowed!");

                    // Store the magic value -4004 as MasterVersion so that the reset is resumed on next server start, even if interrupted
                    if (metaInfo.MasterVersion != DatabaseMetaInfo.ResetInProgressMasterVersion)
                        await _db.InsertMetaInfoAsync(new DatabaseMetaInfo(metaInfo.Version + 1, DateTime.UtcNow, DatabaseMetaInfo.ResetInProgressMasterVersion, metaInfo.NumShards)).ConfigureAwait(false);

                    // Warn if a previous reset was interrupted / master version change
                    if (metaInfo.MasterVersion == DatabaseMetaInfo.ResetInProgressMasterVersion)
                        _log.Warning("Previous database reset was interrupted, resuming...");
                    else
                        _log.Warning("Database master version has changed from v{ExistingDBVersion} to v{ServerDBVersion}, resetting all database shards!", metaInfo.MasterVersion, dbOpts.MasterVersion);

                    // Drop all tables in all shards (including to-be-added and to-be-removed shards when re-sharding)
                    await ResetShardRangeAsync(numTotalShards).ConfigureAwait(false);
                }
            }

            // SCHEMA MIGRATION (for all shards, including to-be-added and to-be-removed shards when re-sharding)

            // Migrate all shards to latest schema version
            // \todo This will fail if to-be-removed shards are in an inconsistent state (some tables removed) !!
            await MigrateShardsAsync(0, numTotalShards, dbOpts.InitSchemaWithoutMigrations ? CreateShardAsync : MigrateShardAsync).ConfigureAwait(false);

            // RE-SHARDING (and reset abandoned shards if down-sharding)
            // \todo Re-sharding isn't currently resumable: if interrupted when deleting tables, the subsequent retry wil fail??

            // Check if need re-sharding: number of shards has changed and the database was initialized (has non-zero shards)
            if (numOldShards != 0 && _db.NumActiveShards != numOldShards)
            {
                // Log tables to be re-sharded
                _log.Information("Re-sharding database from {NumOldShards} to {NumNewShards} shards, with tables:", numOldShards, _db.NumActiveShards);
                foreach (DatabaseItemSpec item in DatabaseTypeRegistry.ItemSpecs)
                    _log.Information("  {TableName}: primaryKey={PrimaryKey} partitionKey={PartitionKey}", item.TableName, item.PrimaryKeyName, item.IsPartitioned ? item.PartitionKeyName : "<none>");

                // Perform the re-sharding
                await ReshardAllEntitiesAsync(numOldShards).ConfigureAwait(false);
            }

            // Write meta info (if MasterVersion or NumShards changed)
            if (metaInfo.MasterVersion != dbOpts.MasterVersion || _db.NumActiveShards != metaInfo.NumShards)
                await _db.InsertMetaInfoAsync(new DatabaseMetaInfo(metaInfo.Version + 1, DateTime.UtcNow, dbOpts.MasterVersion, _db.NumActiveShards)).ConfigureAwait(false);
        }

        /// <summary>
        /// Run a long-running task with periodic callbacks to print progress on the task.
        /// </summary>
        /// <param name="operationCallback"></param>
        /// <param name="progressInterval"></param>
        /// <param name="progressCallback"></param>
        /// <returns></returns>
        async Task RunTaskAndPrintProgressAsync(Func<Task> operationCallback, TimeSpan progressInterval, Action progressCallback)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            // Start task that prints time progression periodically
            Task printProgressTask = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(progressInterval, cts.Token).ConfigureAwait(false);
                        if (cts.IsCancellationRequested)
                            break;
                        progressCallback();
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            });

            try
            {
                // Perform operation
                await operationCallback();
            }
            finally
            {
                // Cancel periodic progress reporting
                cts.Cancel();
                await printProgressTask;
                printProgressTask.Dispose();
            }
        }
    }
}
