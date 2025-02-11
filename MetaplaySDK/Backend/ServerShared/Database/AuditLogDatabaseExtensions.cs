// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Dapper;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Server.AdminApi.AuditLog;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.Database
{
    public static class AuditLogDatabaseExtensions
    {
        public static async Task<List<PersistedAuditLogEvent>> QueryAuditLogEventsAsync(this MetaDatabase database, string eventIdLessThan, string targetType, string targetId, string source, string sourceIpAddress, string sourceCountryIsoCode, int pageSize)
        {
            // Construct the query dynamically
            List<string> whereClauses = new List<string>();
            dynamic paramList = new ExpandoObject();
            IEnumerable<int> shardsToQuery = Enumerable.Range(0, database.NumActiveShards);

            // Start event clause? Used for paging
            if (!string.IsNullOrEmpty(eventIdLessThan))
            {
                whereClauses.Add("EventId < @EventIdLessThan");
                paramList.EventIdLessThan = eventIdLessThan;
            }

            // Target clauses?
            if (!String.IsNullOrEmpty(targetType))
            {
                if (String.IsNullOrEmpty(targetId))
                {
                    whereClauses.Add("Target LIKE @PartialTargetString ESCAPE '/'");
                    paramList.PartialTargetString = $"{SqlUtil.EscapeSqlLike(targetType, '/')}:%";  // \note: escape with / because \ constant (for ESCAPE '') is interpreted differently in across SQL engines.
                }
                else
                {
                    whereClauses.Add("Target = @FullTargetString");
                    paramList.FullTargetString = $"{targetType}:{targetId}";

                    // If we have a full target specified then we can figure out in advance
                    // exactly which shard those events will be on (becase target is the
                    // partition key) and thus we only need to query that one shard
                    int itemShardNdx = database.Backend.GetShardIndex(paramList.FullTargetString);
                    shardsToQuery = Enumerable.Range(itemShardNdx, 1);
                }
            }

            // Source clauses?
            if (!String.IsNullOrEmpty(source))
            {
                whereClauses.Add("Source = @FullSourceString");
                paramList.FullSourceString = source;
            }
            if (!String.IsNullOrEmpty(sourceIpAddress))
            {
                whereClauses.Add("SourceIpAddress = @FullSourceIpAddressString");
                paramList.FullSourceIpAddressString = sourceIpAddress;
            }
            if (!String.IsNullOrEmpty(sourceCountryIsoCode))
            {
                whereClauses.Add("SourceCountryIsoCode = @FullSourceCountryIsoCodeString");
                paramList.FullSourceCountryIsoCodeString = sourceCountryIsoCode;
            }

            // Paging clause
            string pagingClause = "ORDER BY EventId DESC LIMIT @PageSize";
            paramList.PageSize = pageSize;

            // Construct full query
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedAuditLogEvent>();
            string query = $"SELECT * FROM {itemSpec.TableName}";
            if (whereClauses.Count > 0)
                query += $" WHERE {String.Join(" AND ", whereClauses)}";
            query += " " + pagingClause;

            // Perform query on multiple shards and collate the results
            List<PersistedAuditLogEvent>[] shardResults = await Task.WhenAll(
                shardsToQuery
                .Select(shardNdx => QueryAuditLogEventsShardAsync(database, query, (object)paramList, shardNdx)))
                .ConfigureAwait(false);
            return
                shardResults
                .SelectMany(v => v)
                .OrderByDescending(header => header.EventId, StringComparer.Ordinal)
                .Take(pageSize)
                .ToList();
        }

        public static async Task<PersistedAuditLogEvent> GetAuditLogEventAsync(this MetaDatabase database, string eventId)
        {
            // Construct full query
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedAuditLogEvent>();
            string query = $"SELECT * FROM {itemSpec.TableName} WHERE EventId = @FullEventId";
            object paramList = new
            {
                FullEventId = eventId
            };

            // We don't know which shard the event will be on so we have to query them all, but
            // we do know that the result will only exist on one shard
            List<PersistedAuditLogEvent>[] shardResults = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => QueryAuditLogEventsShardAsync(database, query, paramList, shardNdx)))
                .ConfigureAwait(false);
            return shardResults
                .SelectMany(v => v)
                .SingleOrDefault();
        }

        static async Task<List<PersistedAuditLogEvent>> QueryAuditLogEventsShardAsync(MetaDatabase database, string query, object paramList, int shardNdx)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedAuditLogEvent>();
            IEnumerable<PersistedAuditLogEvent> result = await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadOnly, itemSpec.TableName, shardNdx, "QueryPaged", conn =>
                conn.QueryAsync<PersistedAuditLogEvent>(query, paramList)).ConfigureAwait(false);
            return result.ToList();
        }

        static async Task<int> PurgeAuditLogEventsAsyncAsync(MetaDatabase database, int shardNdx, string endEventId)
        {
            DatabaseItemSpec itemSpec = DatabaseTypeRegistry.GetItemSpec<PersistedAuditLogEvent>();
            string query = $"DELETE FROM {itemSpec.TableName} WHERE EventId < @EndEventId";
            return await database.Backend.DapperExecuteAsync(database.Throttle, DatabaseReplica.ReadWrite, itemSpec.TableName, shardNdx, "DeleteBulk", conn =>
                conn.ExecuteAsync(query, new { EndEventId = endEventId }))
                .ConfigureAwait(false);
        }

        public static  async Task<int> PurgeAuditLogEventsAsync(this MetaDatabase database, MetaTime removeUntil)
        {
            // Create start eventId from timestamp
            string endEventId = EventId.SearchStringFromTime(removeUntil).ToString();

            // Fetch stats from all shards
            IEnumerable<int> counts = await Task.WhenAll(
                Enumerable.Range(0, database.NumActiveShards)
                .Select(shardNdx => PurgeAuditLogEventsAsyncAsync(database, shardNdx, endEventId)))
                .ConfigureAwait(false);

            return counts.Sum();
        }
    }
}
