// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Dapper;
using Metaplay.Cloud.RuntimeOptions;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Cloud.Persistence
{
    // DatabaseBackendMySql

    public class DatabaseBackendMySql : DatabaseBackend
    {
        public override DatabaseBackendType BackendType => DatabaseBackendType.MySql;

        string[]            _readWriteConnStrings;
        string[]            _readOnlyConnStrings;
        MySqlDataSource[]   _readWriteDataSources;
        MySqlDataSource[]   _readOnlyDataSources;

        public DatabaseBackendMySql(int numActiveShards, DatabaseShardSpec[] shardSpecs, Func<MetaDbContext> createGameDbContext)
            : base(numActiveShards, shardSpecs, createGameDbContext)
        {
            // Validate inputs
            foreach (DatabaseShardSpec shard in shardSpecs)
            {
                if (string.IsNullOrEmpty(shard.DatabaseName))
                    throw new ArgumentException("Shard with empty DatabaseName given");

                if (string.IsNullOrEmpty(shard.ReadWriteHost))
                    throw new ArgumentException("Shard with empty ReadWriteHost given");

                if (string.IsNullOrEmpty(shard.ReadOnlyHost))
                    throw new ArgumentException("Shard with empty ReadOnlyHost given");
            }

            // \note Also using ConnectionReset=True as it's safer (but slower) -- ConnectionReset=false can throw exceptions in Open() which we don't handle
            // \todo [petri] Could wrap calls to OpenAsync() with try-catch-retry to allow ConnectionReset=true
            DatabaseOptions dbOpts = RuntimeOptionsRegistry.Instance.GetCurrent<DatabaseOptions>();
            int maxConnectionPoolSize = dbOpts.MaxConnectionsPerShard;
            string common = Invariant($"Pipelining=true;Pooling=True;ConnectionReset=True;MaximumPoolSize={maxConnectionPoolSize};ConnectionTimeout=30");
            _readWriteConnStrings = shardSpecs.Select(shard => $"Server={shard.ReadWriteHost};database={shard.DatabaseName};uid={shard.UserId};pwd={shard.Password};{common}").ToArray();
            _readOnlyConnStrings = shardSpecs.Select(shard => $"Server={shard.ReadOnlyHost};database={shard.DatabaseName};uid={shard.UserId};pwd={shard.Password};{common}").ToArray();

            // Use default Serilog for logging
            LoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddSerilog(Serilog.Log.Logger);

            // Create MySqlDataSources (with logging enabled)
            _readWriteDataSources = new MySqlDataSource[shardSpecs.Length];
            _readOnlyDataSources = new MySqlDataSource[shardSpecs.Length];
            for (int shardNdx = 0; shardNdx < shardSpecs.Length; shardNdx++)
            {
                // ReadWrite
                MySqlDataSourceBuilder readWriteBuilder = new MySqlDataSourceBuilder(GetConnectionString(DatabaseReplica.ReadWrite, shardNdx));
                readWriteBuilder.UseLoggerFactory(loggerFactory);
                _readWriteDataSources[shardNdx] = readWriteBuilder.Build();

                // ReadOnly
                MySqlDataSourceBuilder readOnlyBuilder = new MySqlDataSourceBuilder(GetConnectionString(DatabaseReplica.ReadOnly, shardNdx));
                readOnlyBuilder.UseLoggerFactory(loggerFactory);
                _readOnlyDataSources[shardNdx] = readOnlyBuilder.Build();
            }

            Serilog.Log.Information("Initializing DatabaseBackendMySql with version {MySqlVersion} with pipelining enabled", dbOpts.MySqlVersion);
        }

        public override void Dispose()
        {
            foreach (MySqlDataSource source in _readWriteDataSources)
                source.Dispose();
            foreach (MySqlDataSource source in _readOnlyDataSources)
                source.Dispose();

            base.Dispose();
        }

        protected override string GetConnectionString(DatabaseReplica replica, int shardNdx)
        {
            if (shardNdx < 0 || shardNdx >= _readWriteConnStrings.Length)
                throw new ArgumentException($"Invalid shardNdx {shardNdx} (numTotalShards={NumTotalShards})");

            return (replica == DatabaseReplica.ReadWrite) ? _readWriteConnStrings[shardNdx] : _readOnlyConnStrings[shardNdx];
        }

        public override async Task<DbConnection> CreateConnectionAsync(DatabaseReplica replica, int shardNdx)
        {
            if (shardNdx < 0 || shardNdx >= _readWriteConnStrings.Length)
                throw new ArgumentException($"Invalid shardNdx {shardNdx} (numTotalShards={NumTotalShards})");

            if (replica == DatabaseReplica.ReadWrite)
                return await _readWriteDataSources[shardNdx].OpenConnectionAsync().ConfigureAwait(false);
            else
                return await _readOnlyDataSources[shardNdx].OpenConnectionAsync().ConfigureAwait(false);
        }

        public override async Task<string[]> GetTableNamesAsync(int shardNdx)
        {
            return await ExecuteRawAsync(DatabaseReplica.ReadWrite, shardNdx, async conn =>
            {
                IEnumerable<string> tableNames = await conn.QueryAsync<string>($"SELECT table_name FROM information_schema.tables WHERE table_schema = '{_shardSpecs[shardNdx].DatabaseName}' AND table_type = 'BASE TABLE'").ConfigureAwait(false);
                return tableNames.ToArray();
            }).ConfigureAwait(false);
        }

        public override Task<bool> TableExistsAsync(string tableName)
        {
            return DapperExecuteAsync<bool>(DatabaseThrottleNop.Instance, DatabaseReplica.ReadWrite, tableName, shardNdx: 0, "Get", async conn =>
            {
                int numRows = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{_shardSpecs[0].DatabaseName}' AND table_name = '{tableName}'").ConfigureAwait(false);
                return numRows > 0;
            });
        }

        public override async Task<int> EstimateItemCountInTableAsync(int shardNdx, string tableName)
        {
            return await ExecuteRawAsync(DatabaseReplica.ReadOnly, shardNdx, async conn =>
            {
                // Get estimate first & return it for large tables (to avoid slow scans)
                string query = $"SELECT IF(AVG_ROW_LENGTH=0, 0, CAST(DATA_LENGTH DIV AVG_ROW_LENGTH AS SIGNED)) FROM information_schema.tables WHERE table_schema = '{_shardSpecs[shardNdx].DatabaseName}' AND table_name = '{tableName}'";
                int count = await conn.QuerySingleAsync<int>(query).ConfigureAwait(false);
                if (count >= 10000)
                    return count;

                // For small tables, return exact count
                return await conn.QuerySingleAsync<int>($"SELECT count(*) FROM {tableName}").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public override bool SupportsDeleteOrderBy() => true;

        /// <summary>
        /// Computes 32-bit MD5 value matching Metaplay MiniMD5. Useful for pruning database shards after cloning when doing up-sharding in integer multiples.
        /// </summary>
        public override string GetShardingSql(string keyName) => $"conv(substr(md5({keyName}), 1, 8), 16, 10)";

        public override string GetInsertOrIgnoreSql() => "INSERT IGNORE";
        public override string GetInsertOrReplaceSql() => "REPLACE";
        public override string GetInsertOnConflictSetSql(string primaryKeyName, string setValuesSqlFragment) => $"ON DUPLICATE KEY UPDATE {setValuesSqlFragment}";
    }
}
