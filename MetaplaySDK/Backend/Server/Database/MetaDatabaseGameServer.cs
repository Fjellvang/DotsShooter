// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Dapper;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Debugging;
using Metaplay.Core.Player;
using Metaplay.Core.Web3;
using Metaplay.Server.Authentication;
using Metaplay.Server.EventLog;
#if !METAPLAY_DISABLE_GUILDS
using Metaplay.Core.Guild;
using Metaplay.Server.Guild;
#endif
using Metaplay.Server.InAppPurchase;
using Metaplay.Server.League;
using Metaplay.Server.StatisticsEvents;
using Metaplay.Server.Web3;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.Database
{
    public static class MetaplayServerDatabaseExtensions
    {
        #region Player

        // Construct the IN clause for the search query, consisting of a comma separated list of all priority levels
        static readonly string _playerSearchInClause = "(" + string.Join(",", Enumerable.Range(1, PersistedPlayerSearch.LowestNormalPriorityLevel).Concat(new[] { PersistedPlayerSearch.ReservedLowestPriorityLevel }).Select(priority => priority.ToString(CultureInfo.InvariantCulture))) + ")";

        public static async Task UpdatePlayerAsync(this MetaDatabase database, EntityId playerId, PersistedPlayerBase player, IEnumerable<string> newPlayerSearchStrings, PlayerDeletionStatus playerDeletionStatus)
        {
            // If search name doesn't need to be updated, do a regular Entity update
            if (newPlayerSearchStrings == null)
            {
                await database.UpdateAsync(player).ConfigureAwait(false);
                return;
            }

            // Name update is required, transactionally update the existing PersistedPlayer and the name search table
            DatabaseItemSpec playerItemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerBase>();
            DatabaseItemSpec searchItemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerSearch>();
            (string partitionKey, int shardNdx) = playerItemSpec.GetItemShardNdx(player);

            // \todo: Should check the pre-existence inside transaction, but that is difficult. Let's assume that
            //        removal of a player between these steps is unlikely.
            bool alreadyExists = await database.TestExistsAsync<PersistedPlayerBase>(playerId.ToString());
            if (!alreadyExists)
                throw new NoSuchItemException($"No item in table {playerItemSpec.TableName} shard #{shardNdx} with primary key {playerId} found");

            int _ = await database.Backend.DapperExecuteTransactionAsync(database.Throttle, DatabaseReplica.ReadWrite, IsolationLevel.ReadCommitted, shardNdx, "Player", "UpdateWithName", async (conn, txn) =>
            {
                // Start with the query and params to update the PersistedPlayer
                List<string> queries = new List<string> { playerItemSpec.UpdateQuery };
                DynamicParameters queryParams = new DynamicParameters(player);

                // \note Convert to lower case to make case insensitive
                IEnumerable<string> nameParts = newPlayerSearchStrings.Select(x => x.ToLowerInvariant());

                // Add the query for removing old name parts
                // \note Using _ prefix for non-PersistedPlayer params to avoid potential collisions
                queries.Add($"DELETE FROM {searchItemSpec.TableName} WHERE EntityId = @_EntityId");
                queryParams.Add("_EntityId", playerId.ToString());

                // Create insert query (with value for each name parts)
                if (nameParts.Count() > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"INSERT INTO {searchItemSpec.TableName} ({searchItemSpec.MemberNamesStr}) VALUES ");
                    int namePartNdx = 0;
                    foreach (string namePart in nameParts)
                    {
                        // Set priority to length of first word of name part
                        string firstWord = SearchUtil.GetFirstWord(namePart);
                        int    priority  = PersistedPlayerSearch.ReservedLowestPriorityLevel;
                        if (!string.IsNullOrEmpty(firstWord))
                            priority = firstWord.Length;
                        // Priorities are clamped
                        if (priority > PersistedPlayerSearch.LowestNormalPriorityLevel)
                            priority = PersistedPlayerSearch.LowestNormalPriorityLevel;
                        // Bots and deleted players go to the lowest priority
                        if (playerDeletionStatus != PlayerDeletionStatus.None)
                            priority = PersistedPlayerSearch.ReservedLowestPriorityLevel;
                        if (playerId.Value < Authenticator.NumReservedBotIds)
                            priority = PersistedPlayerSearch.ReservedLowestPriorityLevel;

                        string namePartParamName = Invariant($"_NamePart{namePartNdx}");
                        string priorityParamName = Invariant($"_Priority{namePartNdx}");
                        if (namePartNdx != 0)
                            sb.Append(", ");
                        sb.Append($"(@{priorityParamName}, @{namePartParamName}, @_EntityId)");
                        queryParams.Add(priorityParamName, priority);
                        queryParams.Add(namePartParamName, namePart);
                        namePartNdx++;
                    }
                    queries.Add(sb.ToString());
                }

                // Execute all the ops with single round-trip
                string fullQuery = string.Join(";\n", queries);
                int numUpdated = await conn.ExecuteAsync(fullQuery, queryParams, transaction: txn).ConfigureAwait(false);

                // Commit transaction
                await txn.CommitAsync().ConfigureAwait(false);

                return numUpdated;
            }).ConfigureAwait(false);
        }

        static async Task<List<(string playerId, uint searchPriority)>> SearchPlayerIdsByNameFromShardAsync(MetaDatabase database, int shardNdx, int count, string searchText)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerSearch>();
            // Use a correlated subquery to get the minimum priority for matching name parts while ensuring distinct results
            string query =  $"SELECT DISTINCT p.EntityId, p.Priority " +
                            $"FROM {itemSpec.TableName} p " +
                            $"WHERE p.NamePart LIKE @SearchFilter ESCAPE '/' " +
                                $"AND p.Priority = " +
                                    $"(SELECT MIN(p2.Priority) " +
                                    $"FROM {itemSpec.TableName} p2 " +
                                    $"WHERE p2.EntityId = p.EntityId " +
                                        $"AND p2.NamePart LIKE @SearchFilter ESCAPE '/') " +
                                $"AND Priority IN {_playerSearchInClause} " +
                            $"ORDER BY p.Priority " +
                            $"LIMIT @Count";
            IEnumerable<(string, uint)> playerIds = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "SearchByName", conn =>
                conn.QueryAsync<(string, uint)>(query, new {
                    SearchFilter    = SqlUtil.LikeQueryForPrefixSearch(searchText.ToLowerInvariant(), '/'), // \note: escape with / because \ constant (for ESCAPE '') is interpreted differently in across SQL engines.
                    Count           = count,
                }))
                .ConfigureAwait(false);
            return playerIds.ToList();
        }

        public static async Task<List<EntityId>> SearchPlayerIdsByNameAsync(this MetaDatabase database, int count, string searchText)
        {
            // Find results from all shards (in parallel)
            List<(string, uint)>[] results = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => SearchPlayerIdsByNameFromShardAsync(database, shardNdx, count, searchText)))
                .ConfigureAwait(false);

            // Flatten results, sort by priority number, then limit results to 'count' and discard the priority number
            return results
                .SelectMany(v => v)
                .OrderBy(x => x.Item2)
                .Take(count)
                .Select(player => EntityId.ParseFromString(player.Item1))
                .ToList();
        }

        #endregion // Player

        #region IAP

        /// <summary>
        /// Get metadatas of the IAP subscriptions with the given original transaction id.
        /// Note that although the result items are of type <see cref="PersistedInAppPurchaseSubscription"/>,
        /// only its "metadata" fields will be set; in particular,
        /// <see cref="PersistedInAppPurchaseSubscription.SubscriptionInfo"/> is omitted.
        /// </summary>
        /// <param name="originalTransactionId"></param>
        /// <returns></returns>
        public static async Task<List<PersistedInAppPurchaseSubscription>> GetIAPSubscriptionMetadatas(this MetaDatabase database, string originalTransactionId)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedInAppPurchaseSubscription>();

            string[] memberNames = new string[]
            {
                nameof(PersistedInAppPurchaseSubscription.PlayerAndOriginalTransactionId),
                nameof(PersistedInAppPurchaseSubscription.PlayerId),
                nameof(PersistedInAppPurchaseSubscription.OriginalTransactionId),
                nameof(PersistedInAppPurchaseSubscription.CreatedAt),
            };
            string query = $"SELECT {string.Join(", ", memberNames)} FROM {itemSpec.TableName} WHERE {nameof(PersistedInAppPurchaseSubscription.OriginalTransactionId)} = @OriginalTransactionId";

            IEnumerable<PersistedInAppPurchaseSubscription>[] results = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx =>
                {
                    return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "GetIAPSubscriptionMetadatas", conn =>
                    {
                        return conn.QueryAsync<PersistedInAppPurchaseSubscription>(query, new
                        {
                            OriginalTransactionId = originalTransactionId,
                        });
                    });
                }));

            return results.SelectMany(l => l).ToList();
        }

        #endregion

        #region EntitySearch

        /// <summary>
        /// Search for at most <paramref name="count"/> entities whose EntityId starts with <paramref name="searchText"/>.
        /// Note that the 'EntityKind:' prefix is automatically added to the <paramref name="searchText"/> if it's not there already.
        /// If <paramref name="searchText"/> is empty or null, no filtering is done. Uses the read replicas. Search is case-sensitive.
        /// </summary>
        public static async Task<List<TEntity>> SearchEntitiesByIdAsync<TEntity>(this MetaDatabase database, EntityKind entityKind, int count, string searchText) where TEntity : IPersistedEntity
        {
            List<IPersistedEntity> entities = await database.SearchEntitiesByIdAsync(typeof(TEntity), entityKind, count, searchText);
            return entities.Cast<TEntity>().ToList();
        }

        /// <inheritdoc cref="SearchEntitiesByIdAsync{TEntity}(MetaDatabase, EntityKind, int, string)" />
        public static async Task<List<IPersistedEntity>> SearchEntitiesByIdAsync(this MetaDatabase database, Type persistedType, EntityKind entityKind, int count, string searchText)
        {
            DatabaseItemSpec    itemSpec    = DatabaseTypeRegistry.GetItemSpec(persistedType);
            string              where       = string.IsNullOrEmpty(searchText) ? "" : "WHERE EntityId LIKE @SearchFilter ESCAPE '/'";
            string              query       = $"SELECT * FROM {itemSpec.TableName} {where} LIMIT @Count";

            // Ensure that searchText starts with the 'EntityKind:' prefix (or is empty or null).
            if (!string.IsNullOrEmpty(searchText) && !searchText.StartsWith($"{entityKind}:", StringComparison.OrdinalIgnoreCase))
                searchText = $"{entityKind}:" + searchText;

            IEnumerable<IPersistedEntity>[] results = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(async shardNdx =>
                {
                    return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "QueryById", async conn =>
                    {
                        IEnumerable<object> result = await conn.QueryAsync(itemSpec.ItemType, query, new
                        {
                            SearchFilter    = searchText == null ? null : SqlUtil.LikeQueryForPrefixSearch(searchText, '/'), // \note: escape with / because \ constant (for ESCAPE '') is interpreted differently in across SQL engines.
                            Count           = count,
                        }).ConfigureAwait(false);
                        return result.Cast<IPersistedEntity>();
                    });
                }));

            // Choose 'count' results from all queries
            return results
                .SelectMany(v => v)
                .Take(count)
                .ToList();
        }

        #endregion // EntitySearch

        #region Guild
        #if !METAPLAY_DISABLE_GUILDS

        // Construct the IN clause for the search query, consisting of a comma separated list of all priority levels
        static readonly string _guildSearchInClause = "(" + string.Join(",", Enumerable.Range(1, PersistedGuildSearch.LowestNormalPriorityLevel).Concat(new[] { PersistedGuildSearch.ReservedLowestPriorityLevel }).Select(priority => priority.ToString(CultureInfo.InvariantCulture))) + ")";

        // \todo [petri] this is mostly duplicate with UpdatePlayerAsync() -- refactor common parts
        public static async Task UpdateGuildAsync(this MetaDatabase database, EntityId guildId, PersistedGuildBase guild, string updateNameSearch, GuildLifecyclePhase guildLifecyclePhase)
        {
            // If search name doesn't need to be updated, do a regular Entity update
            if (updateNameSearch == null)
            {
                await database.UpdateAsync(guild).ConfigureAwait(false);
                return;
            }

            // Name update is required, transactionally update the existing PersistedGuild and the name search table
            DatabaseItemSpec guildItemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedGuildBase>();
            DatabaseItemSpec searchItemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedGuildSearch>();
            (string partitionKey, int shardNdx) = guildItemSpec.GetItemShardNdx(guild);
            int _ = await database.Backend.DapperExecuteTransactionAsync(database.Throttle, DatabaseReplica.ReadWrite, IsolationLevel.ReadCommitted, shardNdx, "Guild", "UpdateWithName", async (conn, txn) =>
            {
                // Start with the query and params to update the PersistedGuild
                List<string> queries = new List<string> { guildItemSpec.UpdateQuery };
                DynamicParameters queryParams = new DynamicParameters(guild);

                // If name search should be updated, add the queries and parameters
                if (updateNameSearch != null)
                {
                    // Compute all the valid name parts of the guild's name
                    // \note Convert to lower case to make case insensitive
                    List<string> nameParts = SearchUtil.ComputeSearchablePartsFromName(updateNameSearch.ToLowerInvariant(), PersistedGuildSearch.MinPartLengthCodepoints, PersistedGuildSearch.MaxPartLengthCodepoints, PersistedGuildSearch.MaxPersistNameParts);

                    // Add the query for removing old name parts
                    // \note Using _ prefix for non-PersistedGuild params to avoid potential collisions
                    queries.Add($"DELETE FROM {searchItemSpec.TableName} WHERE EntityId = @_EntityId");
                    queryParams.Add("_EntityId", guildId.ToString());

                    // Create insert query (with value for each part)
                    if (nameParts.Count > 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append($"INSERT INTO {searchItemSpec.TableName} ({searchItemSpec.MemberNamesStr}) VALUES ");
                        for (int ndx = 0; ndx < nameParts.Count; ndx++)
                        {
                            // Set priority to length of first word of name part
                            string firstWord = SearchUtil.GetFirstWord(nameParts[ndx]);
                            int    priority  = PersistedGuildSearch.ReservedLowestPriorityLevel;
                            if (!string.IsNullOrEmpty(firstWord))
                                priority = firstWord.Length;
                            // Priorities are clamped
                            if (priority > PersistedGuildSearch.LowestNormalPriorityLevel)
                                priority = PersistedGuildSearch.LowestNormalPriorityLevel;
                            // Deleted guilds go to the lowest priority
                            if (guildLifecyclePhase == GuildLifecyclePhase.Closed)
                                priority = PersistedGuildSearch.ReservedLowestPriorityLevel;

                            string namePartParamName = Invariant($"_NamePart{ndx}");
                            string priorityParamName = Invariant($"_Priority{ndx}");
                            if (ndx != 0)
                                sb.Append(", ");
                            sb.Append($"(@{priorityParamName}, @{namePartParamName}, @_EntityId)");
                            queryParams.Add(priorityParamName, priority);
                            queryParams.Add(namePartParamName, nameParts[ndx]);
                        }
                        queries.Add(sb.ToString());
                    }
                }

                // Execute all the queries with single round-trip
                string fullQuery = string.Join(";\n", queries);
                int numUpdated = await conn.ExecuteAsync(fullQuery, queryParams, transaction: txn).ConfigureAwait(false);

                // Commit transaction
                await txn.CommitAsync().ConfigureAwait(false);

                return numUpdated;
            }).ConfigureAwait(false);
        }

        static async Task<List<(string guildId, uint searchPriority)>> SearchGuildIdsByNameFromShardAsync(MetaDatabase database, int shardNdx, int count, string searchText)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedGuildSearch>();
            // Use a correlated subquery to get the minimum priority for matching name parts while ensuring distinct results
            string query =  $"SELECT DISTINCT p.EntityId, p.Priority " +
                            $"FROM {itemSpec.TableName} p " +
                            $"WHERE p.NamePart LIKE @SearchFilter ESCAPE '/' " +
                                $"AND p.Priority = " +
                                    $"(SELECT MIN(p2.Priority) " +
                                    $"FROM {itemSpec.TableName} p2 " +
                                    $"WHERE p2.EntityId = p.EntityId " +
                                        $"AND p2.NamePart LIKE @SearchFilter ESCAPE '/') " +
                                $"AND Priority IN {_guildSearchInClause} " +
                            $"ORDER BY p.Priority " +
                            $"LIMIT @Count";
            IEnumerable<(string, uint)> guildIds = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "SearchByName", conn =>
                conn.QueryAsync<(string, uint)>(query, new {
                    SearchFilter    = SqlUtil.LikeQueryForPrefixSearch(searchText.ToLowerInvariant(), '/'), // \note: escape with / because \ constant (for ESCAPE '') is interpreted differently in across SQL engines.
                    Count           = count,
                }))
                .ConfigureAwait(false);
            return guildIds.ToList();
        }

        // \todo [petri] code is mostly duplicated with player search -- refactor shared parts
        public static async Task<List<EntityId>> SearchGuildIdsByNameAsync(this MetaDatabase database, int count, string searchText)
        {
            // Find results from all shards (in parallel)
            List<(string, uint)>[] results = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => SearchGuildIdsByNameFromShardAsync(database, shardNdx, count, searchText)))
                .ConfigureAwait(false);

            // Flatten results, sort by priority number, then limit results to 'count' and discard the priority number
            return results
                .SelectMany(v => v)
                .OrderBy(x => x.Item2)
                .Take(count)
                .Select(guild => EntityId.ParseFromString(guild.Item1))
                .ToList();
        }

        #endif
        #endregion // Guild

        #region PlayerIncidents

        // \todo [petri] support paging
        public static async Task<List<PlayerIncidentHeader>> QueryPlayerIncidentHeadersAsync(this MetaDatabase database, EntityId playerId, int pageSize)
        {
            // Stored on same shard as owning player
            int shardNdx = database.Backend.GetShardIndex(playerId.ToString());

            // Query the headers
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerIncident>();
            string queryFields = string.Join(", ", PersistedPlayerIncident.HeaderMemberNames);
            string query = $"SELECT {queryFields} FROM {itemSpec.TableName} WHERE PlayerId = @PlayerId ORDER BY IncidentId DESC LIMIT @PageSize";
            IEnumerable<PersistedPlayerIncident> result = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "QueryPaged", conn =>
                conn.QueryAsync<PersistedPlayerIncident>(query, new
                {
                    PlayerId = playerId.ToString(),
                    PageSize = pageSize,
                })).ConfigureAwait(false);

            // Convert to headers
            return result.Select(persisted => persisted.ToHeader()).ToList();
        }

        static async Task<List<PlayerIncidentHeader>> QueryGlobalPlayerIncidentHeadersFromShardAsync(MetaDatabase database, int shardNdx, string fingerprint, int pageSize)
        {
            // Query the headers
            DatabaseItemSpec itemType = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerIncident>();
            string whereClause = string.IsNullOrEmpty(fingerprint) ? "" : "WHERE FingerPrint = @Fingerprint";
            string queryFields = string.Join(", ", PersistedPlayerIncident.HeaderMemberNames);
            string query = $"SELECT {queryFields} FROM {itemType.TableName} {whereClause} ORDER BY PersistedAt DESC LIMIT @PageSize";
            IEnumerable<PersistedPlayerIncident> result = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemType.TableName, shardNdx, "QueryPaged", conn =>
                conn.QueryAsync<PersistedPlayerIncident>(query, new
                {
                    PageSize    = pageSize,
                    Fingerprint = fingerprint,
                })).ConfigureAwait(false);

            // Convert to headers
            return result.Select(persisted => persisted.ToHeader()).ToList();
        }

        /// <summary>
        /// Query latest player incident reports globally (from all players). Can query all types of incidents (fingerprint == null)
        /// or of specific type (fingerprint is valid).
        /// </summary>
        /// <param name="fingerprint">Optional type of incident to query (null means all types are included)</param>
        /// <param name="count">Maximum number of incidents to retur</param>
        /// <returns></returns>
        public static async Task<List<PlayerIncidentHeader>> QueryGlobalPlayerIncidentHeadersAsync(this MetaDatabase database, string fingerprint, int count)
        {
            List<PlayerIncidentHeader>[] shardResults = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => QueryGlobalPlayerIncidentHeadersFromShardAsync(database, shardNdx, fingerprint, count)))
                .ConfigureAwait(false);

            // Combine results into one, sort by occurredAt, and return pageSize of them
            // \todo [petri] would it be better to order by persistedAt ?
            return
                shardResults
                .SelectMany(v => v)
                .OrderByDescending(header => header.OccurredAt)
                .Take(count)
                .ToList();
        }

        static async Task<List<PlayerIncidentStatistics>> QueryGlobalPlayerIncidentsStatisticsFromShardAsync(MetaDatabase database, int shardNdx, string desiredStartIncidentId, int queryLimit)
        {
            DatabaseItemSpec itemType = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerIncident>();

            // We may group-by only queryLimit items. So group-by the range ( max(startId, id-queryLimit-records-ago) ... now)
            string limitQuery = $"SELECT IncidentId FROM {itemType.TableName} ORDER BY IncidentId DESC LIMIT 1 OFFSET @QueryLimit";
            string limitQueryResult = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemType.TableName, shardNdx, "QueryPaged", conn =>
                conn.QuerySingleOrDefaultAsync<string>(limitQuery, new
                {
                    QueryLimit = queryLimit - 1, // query is inclusive, so lets go back only N-1 steps.
                })).ConfigureAwait(false);

            bool isQuerySizeLimited;
            if (limitQueryResult == null)
            {
                // query limit cannot be exceeded. No that many records.
                isQuerySizeLimited = false;
            }
            else if (string.CompareOrdinal(desiredStartIncidentId, limitQueryResult) >= 0)
            {
                // desiredStartIncidentId is more recent or equal
                isQuerySizeLimited = false;
            }
            else
            {
                // limitQueryResult is more recent
                isQuerySizeLimited = true;
            }

            // Query the headers with the range
            string startIncidentId = isQuerySizeLimited ? limitQueryResult : desiredStartIncidentId;
            string query = $"SELECT Fingerprint, MAX(Type) AS Type, MAX(SubType) AS SubType, MAX(Reason) AS Reason, COUNT(*) AS Count FROM {itemType.TableName} WHERE IncidentId >= @StartIncidentId GROUP BY Fingerprint";
            IEnumerable<PlayerIncidentStatistics> result = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemType.TableName, shardNdx, "QueryPaged", conn =>
                conn.QueryAsync<PlayerIncidentStatistics>(query, new
                {
                    StartIncidentId = startIncidentId,
                })).ConfigureAwait(false);

            List<PlayerIncidentStatistics> resultList = result.ToList();

            // If query hit the query limit, mark all groups in this query as CountIsLimitedByQuerySize
            if (isQuerySizeLimited)
            {
                foreach (PlayerIncidentStatistics group in resultList)
                    group.CountIsLimitedByQuerySize = true;
            }

            return resultList;
        }

        public static async Task<List<PlayerIncidentStatistics>> QueryGlobalPlayerIncidentsStatisticsAsync(this MetaDatabase database, MetaTime since, int queryLimitPerShard)
        {
            // Resolve start incidentId from timestamp
            string startIncidentId = PlayerIncidentUtil.EncodeIncidentId(since, 0);

            // Fetch stats from all shards
            List<PlayerIncidentStatistics>[] shardResults = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => QueryGlobalPlayerIncidentsStatisticsFromShardAsync(database, shardNdx, startIncidentId, queryLimitPerShard)))
                .ConfigureAwait(false);

            // Aggregate results from all shards together
            return
                shardResults
                .SelectMany(v => v)
                .GroupBy(v => v.Fingerprint)
                .Select(group =>
                {
                    PlayerIncidentStatistics elem = group.First();
                    bool countIsLimited = group.Any(e => e.CountIsLimitedByQuerySize);
                    return new PlayerIncidentStatistics(elem.Fingerprint, elem.Type, elem.SubType, elem.Reason, group.Sum(c => c.Count), countIsLimited);
                })
                .OrderByDescending(s => s.Count)
                .ToList();
        }

        static async Task<int> PurgePlayerIncidentsFromShardAsync(MetaDatabase database, int shardNdx, MetaTime removeUntilMetaTime, int limit)
        {
            DateTime removeUntil = removeUntilMetaTime.ToDateTime();

            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerIncident>();
            if (database.Backend.SupportsDeleteOrderBy())
            {
                // For MySQL, only delete <limit> latest incidents, to avoid timeouts in case a large number of reports have accumulated
                string query = $"DELETE FROM {itemSpec.TableName} WHERE PersistedAt < @RemoveUntil ORDER BY PersistedAt LIMIT @Limit";
                return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "DeleteBulk", conn =>
                    conn.ExecuteAsync(query, new { RemoveUntil = removeUntil, Limit = limit }))
                    .ConfigureAwait(false);
            }
            else
            {
                // SQLite doesn't support ORDER BY or LIMIT in DELETE, so delete all obsolete reports -- in SQLite environments, we don't expect a lot of build-up anyway
                string query = $"DELETE FROM {itemSpec.TableName} WHERE PersistedAt < @RemoveUntil";
                return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "DeleteBulk", conn =>
                    conn.ExecuteAsync(query, new { RemoveUntil = removeUntil }))
                    .ConfigureAwait(false);
            }
        }

        public static async Task<int> PurgePlayerIncidentsAsync(this MetaDatabase database, MetaTime removeUntil, int perShardLimit)
        {
            if (perShardLimit <= 0)
                throw new ArgumentException("Per-shard purge limit must be positive", nameof(perShardLimit));

            // Fetch stats from all shards
            IEnumerable<int> counts = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => PurgePlayerIncidentsFromShardAsync(database, shardNdx, removeUntil, perShardLimit)))
                .ConfigureAwait(false);

            return counts.Sum();
        }

        public static async Task<PersistedPlayerIncident> TryGetIncidentReportAsync(this MetaDatabase database, EntityId playerId, string incidentId)
        {
            int shardNdx = database.Backend.GetShardIndex(playerId.ToString());
            DatabaseItemSpec itemType = DatabaseTypeRegistry.GetItemSpec<PersistedPlayerIncident>();
            return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemType.TableName, shardNdx, "Get", conn =>
                conn.QuerySingleOrDefaultAsync<PersistedPlayerIncident>(itemType.GetQuery, new { Key = incidentId }))
                .ConfigureAwait(false);
        }

        #endregion // PlayerIncidents

        public static async Task<IEnumerable<PersistedLocalizations>> QueryAllLocalizations(this MetaDatabase database, bool showArchived)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedLocalizations>();
            // \todo antti: hardcode the string or make it possible to provide per-item custom specs
            string queryFields = string.Join(',', itemSpec.MemberNamesStr.Split(',', StringSplitOptions.TrimEntries).Where(x => x != "ArchiveBytes"));
            string whereClause = showArchived ? "" : "WHERE IsArchived = 0";
            string query       = $"SELECT {queryFields} FROM {itemSpec.TableName} {whereClause} ORDER BY Id DESC";
            return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, 0, "QueryAllLocalizations", conn =>
                conn.QueryAsync<PersistedLocalizations>(query)).ConfigureAwait(false);
        }

        public static async Task DeleteAllArchivedGameData<TDataType>(this MetaDatabase database) where TDataType : PersistedGameData
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<TDataType>();
            string           query    = $"DELETE FROM {itemSpec.TableName} WHERE IsArchived = TRUE";
            await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, 0, "DeleteAllArchivedGameData", conn =>
                conn.QueryAsync<PersistedStaticGameConfig>(query)).ConfigureAwait(false);
        }

        #region GameConfigs

        public static async Task<IEnumerable<PersistedStaticGameConfig>> QueryAllStaticGameConfigs(this MetaDatabase database, bool showArchived)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedStaticGameConfig>();
            // \todo antti: hardcode the string or make it possible to provide per-item custom specs
            string queryFields = string.Join(',', itemSpec.MemberNamesStr.Split(',', StringSplitOptions.TrimEntries).Where(x => x != "ArchiveBytes"));
            string whereClause = showArchived ? "" : "WHERE IsArchived = 0";
            string query = $"SELECT {queryFields} FROM {itemSpec.TableName} {whereClause} ORDER BY Id DESC";
            return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, 0, "QueryAllStaticGameConfigs", conn =>
                conn.QueryAsync<PersistedStaticGameConfig>(query)).ConfigureAwait(false);
        }

        public static async Task<MetaGuid> SearchGameDataByVersionHashAndSource<TPersistedDataType>(this MetaDatabase database, string versionHash, string source) where TPersistedDataType : PersistedGameData
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<TPersistedDataType>();
            string           query    = $"SELECT {nameof(PersistedGameData.Id)} FROM {itemSpec.TableName} where {nameof(PersistedGameData.VersionHash)} = @VersionHash AND {nameof(PersistedGameData.Source)} = @Source ORDER BY {nameof(PersistedGameData.Id)} Desc";
            string idMaybe = await database.Backend.DapperExecuteAsync(
                database.Throttle,
                DatabaseReplica.ReadWrite,
                itemSpec.TableName,
                0,
                "SearchGameDataByVersionHashAndSource",
                conn =>
                    conn.QueryFirstOrDefaultAsync<string>(
                        query,
                        new
                        {
                            VersionHash = versionHash,
                            Source      = source
                        })).ConfigureAwait(false);
            return idMaybe != null ? MetaGuid.Parse(idMaybe) : MetaGuid.None;
        }

        #endregion

        #region Entity event log segments

        public static Task<IEnumerable<TPersistedSegment>> GetAllEventLogSegmentsOfEntityAsync<TPersistedSegment>(this MetaDatabase database, EntityId ownerId) where TPersistedSegment : PersistedEntityEventLogSegment
        {
            DatabaseItemSpec    segmentItemSpec = DatabaseTypeRegistry.GetItemSpec<TPersistedSegment>();
            string              partitionKey    = PersistedEntityEventLogSegmentUtil.GetPartitionKey(ownerId);
            string              query           = $"SELECT * FROM {segmentItemSpec.TableName} WHERE {nameof(PersistedEntityEventLogSegment.OwnerId)} = @OwnerId";

            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, segmentItemSpec.TableName, partitionKey, "GetEventLog", conn =>
            {
                return conn.QueryAsync<TPersistedSegment>(query, new { OwnerId = ownerId.ToString() });
            });
        }

        public static Task RemoveAllEventLogSegmentsOfEntityAsync<TPersistedSegment>(this MetaDatabase database, EntityId ownerId) where TPersistedSegment : PersistedEntityEventLogSegment
        {
            DatabaseItemSpec    segmentItemSpec = DatabaseTypeRegistry.GetItemSpec<TPersistedSegment>();
            string              partitionKey    = PersistedEntityEventLogSegmentUtil.GetPartitionKey(ownerId);
            string              query           = $"DELETE FROM {segmentItemSpec.TableName} WHERE {nameof(PersistedEntityEventLogSegment.OwnerId)} = @OwnerId";

            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, segmentItemSpec.TableName, partitionKey, "RemoveEventLog", conn =>
            {
                return conn.ExecuteAsync(query, new { OwnerId = ownerId.ToString() });
            });
        }

        public static Task<DateTime?> TryGetEventLogSegmentLastEntryTimestamp<TPersistedSegment>(this MetaDatabase database, string primaryKey, string partitionKey) where TPersistedSegment : PersistedEntityEventLogSegment
        {
            DatabaseItemSpec    segmentItemSpec = DatabaseTypeRegistry.GetItemSpec<TPersistedSegment>();
            string              query           = $"SELECT {nameof(PersistedEntityEventLogSegment.LastEntryTimestamp)} FROM {segmentItemSpec.TableName} WHERE {segmentItemSpec.PrimaryKeyName} = @Key";

            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, segmentItemSpec.TableName, partitionKey, "GetEventLogTimestamp", async conn =>
            {
                return await conn.QuerySingleOrDefaultAsync<DateTime?>(query, new { Key = primaryKey });
            });
        }

        /// <inheritdoc cref="IEventLogStorage.TryFindTimeRangeStartSegmentAsync"/>.
        /// <remarks>
        /// <see cref="PersistedEntityEventLogSegment"/> does not have an index with <see cref="PersistedEntityEventLogSegment.LastEntryTimestamp"/>,
        /// and therefore this needs to linearly find the segment among all the segments that belong to <paramref name="ownerId"/>.
        /// The assumed viability of this relies on <see cref="EntityEventLogOptionsBase.MaxPersistedSegmentsToRetain"/>
        /// being reasonably low.
        /// </remarks>
        public static Task<TPersistedSegment> TryFindEventLogTimeRangeStartSegmentAsync<TPersistedSegment>(this MetaDatabase database, EntityId ownerId, MetaTime startTime, int segmentIdsStart, int segmentIdsEnd) where TPersistedSegment : PersistedEntityEventLogSegment
        {
            DatabaseItemSpec    segmentItemSpec = DatabaseTypeRegistry.GetItemSpec<TPersistedSegment>();
            string              partitionKey    = PersistedEntityEventLogSegmentUtil.GetPartitionKey(ownerId);
            string              query           =
                $"SELECT * FROM {segmentItemSpec.TableName} " +
                    $"WHERE {nameof(PersistedEntityEventLogSegment.OwnerId)} = @OwnerId " +
                    $"AND {nameof(PersistedEntityEventLogSegment.LastEntryTimestamp)} >= @StartTime " +
                    $"AND {nameof(PersistedEntityEventLogSegment.SegmentSequentialId)} >= @SegmentIdsStart " +
                    $"AND {nameof(PersistedEntityEventLogSegment.SegmentSequentialId)} < @SegmentIdsEnd " +
                    $"ORDER BY {nameof(PersistedEntityEventLogSegment.SegmentSequentialId)} " +
                    $"LIMIT 1";

            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, segmentItemSpec.TableName, partitionKey, "FindEventLogTimeRangeStart", async conn =>
            {
                return await conn.QuerySingleOrDefaultAsync<TPersistedSegment>(query, new
                {
                    OwnerId = ownerId.ToString(),
                    StartTime = startTime.ToDateTime(),
                    SegmentIdsStart = segmentIdsStart,
                    SegmentIdsEnd = segmentIdsEnd,
                });
            });
        }

        #endregion

        #region NFTs

        public static async Task<List<PersistedNft>> QueryNftsAsync(this MetaDatabase database, NftCollectionId collection, EntityId? owner)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedNft>();

            List<string> whereParts = new List<string>();
            if (collection != null)
                whereParts.Add("CollectionId = @CollectionId");
            if (owner.HasValue)
                whereParts.Add("OwnerEntityId = @OwnerEntityId");

            string query = $"SELECT * FROM {itemSpec.TableName}";
            if (whereParts.Count > 0)
                query += $" WHERE {string.Join(" AND ", whereParts)}";

            IEnumerable<PersistedNft>[] nfts = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx =>
                {
                    return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "QueryNfts", conn =>
                    {
                        return conn.QueryAsync<PersistedNft>(query, new
                        {
                            CollectionId = collection?.ToString(),
                            OwnerEntityId = owner?.ToString() ?? null,
                        });
                    });
                }))
                .ConfigureAwait(false);

            return nfts
                .SelectMany(l => l)
                .OrderBy(nft => nft.CollectionId, StringComparer.Ordinal)
                .ThenBy(nft => NftId.ParseFromString(nft.TokenId))
                .ToList();
        }

        public static async Task<NftId?> TryGetMaxNftIdInCollectionAsync(this MetaDatabase database, NftCollectionId collectionId)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedNft>();

            string query = $"SELECT TokenId FROM {itemSpec.TableName} WHERE CollectionId = @CollectionId ORDER BY StringComparableTokenIdEquivalent DESC LIMIT 1";

            PersistedNft[] shardNftMaybes = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx =>
                {
                    return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "QueryNfts", conn =>
                    {
                        return conn.QuerySingleOrDefaultAsync<PersistedNft>(query, new
                        {
                            CollectionId = collectionId.ToString(),
                        });
                    });
                }))
                .ConfigureAwait(false);

            IEnumerable<NftId> nftIds =
                shardNftMaybes
                .Where(nftMaybe => nftMaybe != null)
                .Select(nft => NftId.ParseFromString(nft.TokenId));

            if (nftIds.Any())
                return nftIds.Max();
            else
                return null;
        }

        #endregion

        #region Leagues

        /// <summary>
        /// Leagues specific version of <see cref="MetaDatabase.TryGetAsync{T}(string)"/>.
        /// </summary>
        public static async Task<PersistedParticipantDivisionAssociation> TryGetParticipantDivisionAssociation(this MetaDatabase database, EntityId leagueId, EntityId participant)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"SELECT * FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.ParticipantId)} = @ParticipantId AND {nameof(PersistedParticipantDivisionAssociation.LeagueId)} = @LeagueId";
            int shardNdx = itemSpec.GetKeyShardNdx(participant.ToString());

            return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "GetParticipantDivisionAssociation", conn =>
                conn.QuerySingleOrDefaultAsync<PersistedParticipantDivisionAssociation>(query, new
                {
                    ParticipantId = participant.ToString(),
                    LeagueId = leagueId.ToString(),
                })).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove multiple participant-division associations from a league.
        /// </summary>
        public static async Task<int> RemoveLeagueParticipantDivisionAssociationsAsync(this MetaDatabase database, EntityId leagueId, IEnumerable<EntityId> participants)
        {
            const int        maxParticipantsPerQuery = 128;
            DatabaseItemSpec itemSpec                = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"DELETE FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.LeagueId)} = @LeagueId AND {nameof(PersistedParticipantDivisionAssociation.ParticipantId)} IN @ParticipantIds";

            int[] shardNdxs = participants.Select(participant => itemSpec.GetKeyShardNdx(participant.ToString())).Distinct().ToArray();

            var results = await Task.WhenAll(
                shardNdxs.Select(shardNdx =>
                {
                    return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "RemoveParticipantDivisionAssociations", conn =>
                    {
                        return Task.WhenAll(participants.Where(participant => itemSpec.GetKeyShardNdx(participant.ToString()) == shardNdx)
                            .Chunk(maxParticipantsPerQuery).Select(participantChunk =>
                        {
                            return conn.ExecuteAsync(query, new
                            {
                                LeagueId = leagueId.ToString(),
                                ParticipantIds = participantChunk.Select(participant => participant.ToString()).ToArray(),
                            });
                        }));
                    });
                }))
                .ConfigureAwait(false);

            return results.SelectMany(count => count).Sum();
        }

        /// <summary>
        /// League specific version of <see cref="MetaDatabase.QueryPagedAsync{T}"/>.
        /// Will iterate participants of a single league.
        /// </summary>
        public static async Task<PagedQueryResult<PersistedParticipantDivisionAssociation>> QueryLeagueParticipantsPaged(this MetaDatabase database, EntityId leagueId, PagedIterator iterator, int pageSize)
        {
            if (pageSize <= 0)
                throw new ArgumentException($"Page size must be greater than 0, but is {pageSize}", nameof(pageSize));

            if (iterator.IsFinished)
                throw new ArgumentException("Iterator is already finished", nameof(iterator));

            // Iterator holds the key of last item of previous page, or empty string for fresh iterator
            string iteratorStartKeyExclusive = iterator.StartKeyExclusive;
            string leagueIdStr = leagueId.ToString();

            // Iterate over all shards until have full page (or all objects scanned)
            List<PersistedParticipantDivisionAssociation> items    = new List<PersistedParticipantDivisionAssociation>();
            for (int shardNdx = iterator.ShardIndex; shardNdx < database.NumActiveShards; shardNdx++)
            {
                // Fetch a batch of items and append to result
                int numRemaining = pageSize - items.Count;

                items.AddRange(await LeagueParticipantPagedQueryFullSingleShard(
                    database:                   database,
                    leagueId:                   leagueIdStr,
                    shardNdx:                   shardNdx,
                    iteratorStartKeyExclusive:  iteratorStartKeyExclusive,
                    pageSize:                   numRemaining
                    ).ConfigureAwait(false));

                // If have full page, return it
                if (items.Count == pageSize)
                {
                    return new PagedQueryResult<PersistedParticipantDivisionAssociation>(
                        new PagedIterator(
                            shardIndex:         shardNdx,
                            startKeyExclusive:  items.Last().ParticipantId,
                            isFinished:         false),
                        items);
                }
                else
                    MetaDebug.Assert(items.Count < pageSize, "pageSize was exceeded: items.Count = {0}, pageSize = {1}", items.Count, pageSize);

                // Reset iteratorStartKeyExclusive when switching shards
                iteratorStartKeyExclusive = "";
            }

            // Last batch, iterator finished
            return new PagedQueryResult<PersistedParticipantDivisionAssociation>(PagedIterator.End, items);
        }

        static Task<IEnumerable<PersistedParticipantDivisionAssociation>> LeagueParticipantPagedQueryFullSingleShard(MetaDatabase database, string leagueId, int shardNdx, string iteratorStartKeyExclusive, int pageSize)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"SELECT * FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.ParticipantId)} > @StartKeyExclusive AND {nameof(PersistedParticipantDivisionAssociation.LeagueId)} = @LeagueId ORDER BY {nameof(PersistedParticipantDivisionAssociation.ParticipantId)} LIMIT @PageSize";

            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "LeagueParticipantIteration", conn =>
                conn.QueryAsync<PersistedParticipantDivisionAssociation>(query, new
                {
                    StartKeyExclusive = iteratorStartKeyExclusive,
                    PageSize = pageSize,
                    LeagueId = leagueId,
                }));
        }

        public static async Task<List<PersistedParticipantDivisionAssociation>> QueryLeagueParticipantsByDivision(this MetaDatabase database, EntityId divisionId)
        {
            if (!divisionId.IsValid)
                throw new ArgumentException("Given divisionId should be a valid EntityId.", nameof(divisionId));

            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"SELECT * FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.DivisionId)} = @DivisionId";


            IEnumerable<PersistedParticipantDivisionAssociation>[] participants = await Task.WhenAll(
                    Enumerable.Range(0, database.NumActiveShards)
                        .Select(shardNdx =>
                        {
                            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "LeaguesQueryDivisionParticipants", conn =>
                            {
                                return conn.QueryAsync<PersistedParticipantDivisionAssociation>(query, new
                                {
                                    DivisionId = divisionId.ToString(),
                                });
                            });
                        }))
                .ConfigureAwait(false);

            return participants
                .SelectMany(l => l)
                .ToList();
        }

        public static async Task<int> CountLeagueParticipantsByDivision(this MetaDatabase database, EntityId divisionId)
        {
            if (!divisionId.IsValid)
                throw new ArgumentException("Given divisionId should be a valid EntityId.", nameof(divisionId));

            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"SELECT COUNT(*) FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.DivisionId)} = @DivisionId";


            int[] counts = await Task.WhenAll(
                    Enumerable.Range(0, database.NumActiveShards)
                        .Select(shardNdx =>
                        {
                            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "LeaguesCountDivisionParticipants", conn =>
                            {
                                return conn.QuerySingleAsync<int>(query, new
                                {
                                    DivisionId = divisionId.ToString(),
                                });
                            });
                        }))
                .ConfigureAwait(false);

            return counts.Sum();
        }

        public static async Task<int> RemoveLeagueParticipantsByDivision(this MetaDatabase database, EntityId divisionId)
        {
            if (!divisionId.IsValid)
                throw new ArgumentException("Given divisionId should be a valid EntityId.", nameof(divisionId));

            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"DELETE FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.DivisionId)} = @DivisionId";


            int[] counts = await Task.WhenAll(
                    Enumerable.Range(0, database.NumActiveShards)
                        .Select(
                            shardNdx =>
                            {
                                return database.Backend.DapperExecuteAsync<int>(
                                    database.Throttle,
                                    DatabaseReplica.ReadWrite,
                                    itemSpec.TableName,
                                    shardNdx,
                                    "LeaguesRemoveDivisionParticipants",
                                    conn =>
                                    {
                                        return conn.ExecuteAsync(
                                            query, new { DivisionId = divisionId.ToString() });
                                    });
                            }))
                .ConfigureAwait(false);

            return counts.Sum();
        }

        public static async Task<int> RemoveAllLeagueParticipantDivisionAssociationsAsync(this MetaDatabase database, EntityId leagueId)
        {
            DatabaseItemSpec itemSpec  = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();
            const int        batchSize = 10000;

            string query = $"DELETE FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.LeagueId)} = @leagueId";

            // SQLite doesn't support LIMIT in DELETE statements, so we don't use it.
            if (database.Backend.BackendType != DatabaseBackendType.Sqlite)
            {
                query += " LIMIT " + batchSize.ToString(CultureInfo.InvariantCulture);
            }

            int[] affected;
            int[] totalAffected = new int[database.NumActiveShards];
            do
            {
                affected = await Task.WhenAll(
                    Enumerable.Range(0, database.NumActiveShards)
                        .Select(
                            shardNdx =>
                                database.Backend.DapperExecuteAsync(
                                    database.Throttle,
                                    DatabaseReplica.ReadWrite,
                                    itemSpec.TableName,
                                    shardNdx,
                                    "LeagueDeleteAllAssociations",
                                    async conn =>
                                        await conn.ExecuteAsync(query, new {leagueId = leagueId.ToString()})))).ConfigureAwait(false);

                totalAffected = totalAffected.Zip(affected, (a, b) => a + b).ToArray();
            } while (affected.Sum() > 0);

            return totalAffected.Sum();
        }

        /// <summary>
        /// Returns all participant associations in a league with a state revision greater than the given state revision.
        /// </summary>
        public static async Task<List<PersistedParticipantDivisionAssociation>> QueryLeagueParticipantAssociationsByLatestKnownStateRevision(this MetaDatabase database, EntityId leagueId, int stateRevision)
        {
            if (!leagueId.IsValid)
                throw new ArgumentException("Given leagueId should be a valid EntityId.", nameof(leagueId));

            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedParticipantDivisionAssociation>();

            string query = $"SELECT * FROM {itemSpec.TableName} WHERE {nameof(PersistedParticipantDivisionAssociation.LeagueId)} = @LeagueId AND {nameof(PersistedParticipantDivisionAssociation.LeagueStateRevision)} > @StateRevision";

            IEnumerable<PersistedParticipantDivisionAssociation>[] participants = await Task.WhenAll(
                    Enumerable.Range(0, database.NumActiveShards)
                        .Select(shardNdx =>
                        {
                            return database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "LeaguesQueryByStateRevision", conn =>
                            {
                                return conn.QueryAsync<PersistedParticipantDivisionAssociation>(query, new
                                {
                                    LeagueId = leagueId.ToString(),
                                    StateRevision = stateRevision,
                                });
                            });
                        }))
                .ConfigureAwait(false);

            return participants
                .SelectMany(l => l)
                .ToList();
        }

        #endregion

        #region Statistics events

        /// <summary>
        /// Combine multiple streams of <see cref="PersistedStatisticsEvent"/> in order of increasing
        /// timestamps. Each of the streams itself needs to be in increasing order of timestamps.
        /// </summary>
        /// <param name="streams">Individual streams to combine.</param>
        /// <returns></returns>
        static async IAsyncEnumerable<PersistedStatisticsEvent> CombineAsyncEnumerables(IEnumerable<ConnectionWrappingAsyncEnumerable<PersistedStatisticsEvent>> streams)
        {
            List<IAsyncEnumerator<PersistedStatisticsEvent>> enumerators = new List<IAsyncEnumerator<PersistedStatisticsEvent>>();

            try
            {
                // Start all the enumerators
                foreach (ConnectionWrappingAsyncEnumerable<PersistedStatisticsEvent> stream in streams)
                {
                    IAsyncEnumerator<PersistedStatisticsEvent> enumerator = stream.GetAsyncEnumerator();
                    if (await enumerator.MoveNextAsync())
                        enumerators.Add(enumerator);
                }

                // Combine the streams using a simple linear scan
                while (enumerators.Count > 0)
                {
                    // Find the enumerator with the smallest current item
                    int smallestIndex = 0;
                    for (int i = 1; i < enumerators.Count; i++)
                    {
                        if (enumerators[i].Current.Timestamp < enumerators[smallestIndex].Current.Timestamp)
                            smallestIndex = i;
                    }

                    // Yield the smallest item
                    yield return enumerators[smallestIndex].Current;

                    // Move the enumerator to the next item, or remove it if it's finished
                    if (!await enumerators[smallestIndex].MoveNextAsync())
                        enumerators.RemoveAt(smallestIndex);
                }
            }
            finally
            {
                // Dispose all enumerators
                foreach (IAsyncEnumerator<PersistedStatisticsEvent> enumerator in enumerators)
                    await enumerator.DisposeAsync();
                foreach (ConnectionWrappingAsyncEnumerable<PersistedStatisticsEvent> stream in streams)
                    await stream.DisposeAsync();
            }
        }

        /// <summary>
        /// Stream a time range of <see cref="PersistedStatisticsEvent"/>s from the database.
        /// The events are returned in increasing Timestamp order across all database shards.
        /// </summary>
        /// <param name="rangeFrom">Inclusive lower-bound for timestamps</param>
        /// <param name="rangeTo">Exclusive upper-bound for timestamps</param>
        public static async Task<IAsyncEnumerable<PersistedStatisticsEvent>> StreamStatisticsEventsAsync(this MetaDatabase database, DatabaseReplica replica, MetaTime rangeFrom, MetaTime rangeTo)
        {
            // Fetch streams from each database shard
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedStatisticsEvent>();
            string query = $"SELECT * FROM {itemSpec.TableName} WHERE Timestamp >= @RangeFrom AND Timestamp < @RangeTo ORDER BY Timestamp";
            ConnectionWrappingAsyncEnumerable<PersistedStatisticsEvent>[] shardResults =
                await Task.WhenAll(
                    Enumerable.Range(0, database.NumActiveShards)
                    .Select(shardNdx =>
                    {
                        return database.Backend.DapperQueryUnbufferedAsync(database.Throttle, replica, itemSpec.TableName, shardNdx, "QueryStatisticsEvents", conn =>
                            conn.QueryUnbufferedAsync<PersistedStatisticsEvent>(query, new { RangeFrom = rangeFrom.ToDateTime(), RangeTo = rangeTo.ToDateTime() }));
                    }))
                    .ConfigureAwait(false);

            // Combine streams into a single IAsyncEnumerable -- retain Timestamp order
            return CombineAsyncEnumerables(shardResults);
        }

        #endregion
    }
}
