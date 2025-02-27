using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Game.Server.Leaderboard;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;

namespace Game.Server.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Gets all items from the specified table across all database shards.
    /// WARNING: For large tables, this operation can be expensive and memory-intensive.
    /// Consider using pagination or filtered queries where possible.
    /// </summary>
    /// <typeparam name="T">The type of items to retrieve</typeparam>
    /// <returns>A list of all items in the table</returns>
    public static async Task<List<T>> GetAllAsync<T>(this MetaDatabase database) where T : IPersistedItem
    {
        var itemSpec = DatabaseTypeRegistry.GetItemSpec(typeof(T));
        var query = $"SELECT * FROM {itemSpec.TableName}";

        // Query from all database Shards and combine.
        var results = await Task.WhenAll(
            Enumerable.Range(0, database.NumActiveShards)
                .Select(async shardNdx =>
                {
                    return await database.Backend.DapperExecuteAsync(
                        database.Throttle,
                        // Read from Read-Replica. This can be a large scan so let's protect the primary read-write DB
                        DatabaseReplica.ReadOnly,
                        itemSpec.TableName,
                        shardNdx,
                        // Label for metrics
                        "GetAll",
                        async conn =>
                        {
                            // Query 
                            var result = await conn.QueryAsync(itemSpec.ItemType, query).ConfigureAwait(false);
                            return result.Cast<T>();
                        });
                }));

        return results
            .SelectMany(v => v)
            .ToList();
    } 
    
    public static async Task<List<LeaderboardEntry>> GetTopLeaderBoardEntries(this MetaDatabase database, int limit) 
    {
        var itemSpec = DatabaseTypeRegistry.GetItemSpec(typeof(LeaderboardEntry));
        var sql = """
                     
                             SELECT TOP (@Limit) * 
                             FROM LeaderboardEntries 
                             ORDER BY (Kills * @KillWeight + GoldCollected * @GoldWeight + RoundsCompleted * @RoundWeight) DESC
                     """;

        // Query from all database Shards and combine.
        var results = await Task.WhenAll(
            Enumerable.Range(0, database.NumActiveShards)
                .Select(async shardNdx =>
                {
                    return await database.Backend.DapperExecuteAsync(
                        database.Throttle,
                        // Read from Read-Replica. This can be a large scan so let's protect the primary read-write DB
                        DatabaseReplica.ReadOnly,
                        itemSpec.TableName,
                        shardNdx,
                        // Label for metrics
                        "GetAll",
                        async conn =>
                        {
                            // Query 
                            var result = await conn.QueryAsync(itemSpec.ItemType, sql, new
                            {
                                Limit = limit,
                                //TODO: These weights should be configurable
                                KillWeight = 2,    // Each kill is worth 100 points
                                GoldWeight = 1,    // Each gold piece is worth 0.1 points
                                RoundWeight = 2    // Each completed round is worth 500 points
                            }).ConfigureAwait(false);
                            return result.Cast<LeaderboardEntry>();
                        });
                }));

        return results
            .SelectMany(v => v)
            .ToList();
    } 
}