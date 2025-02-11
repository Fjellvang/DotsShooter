// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if !METAPLAY_DISABLE_GUILDS

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Guild;
using Metaplay.Core.Serialization;
using Metaplay.Server.Database;
using Metaplay.Server.Guild;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// Controller for routes that display and/or search for multiple guilds.
    /// </summary>
    [GuildsEnabledCondition]
    public class GuildsListController : GameAdminApiController
    {
        /// <summary>
        /// HTTP response for listing guilds
        /// </summary>
        public class GuildListItem
        {
            public string       EntityId        { get; set; }
            public string       DisplayName     { get; set; }
            public DateTime     CreatedAt       { get; set; }
            public DateTime     LastLoginAt     { get; set; }
            public string       Phase           { get; set; }
            public int          NumMembers      { get; set; }
            public int          MaxNumMembers   { get; set; }
        }

        /// <summary>
        /// API endpoint to return basic information about all guilds in the system, with an optional query string that
        /// works to filter guilds based on entity id and guild name
        /// <![CDATA[
        /// Usage:  GET /api/guilds
        /// Test:   curl http://localhost:5550/api/guilds?query=QUERY&count=LIMIT
        /// ]]>
        /// </summary>
        [HttpGet("guilds")]
        [RequirePermission(MetaplayPermissions.ApiGuildsView)]
        public async Task<ActionResult<IEnumerable<GuildListItem>>> Guilds([FromQuery] string query = "", [FromQuery] int count = 10)
        {
            // `query` can be null if passed as 'query='. The expected behaviour here is that `query` should be set to the default value '""',
            // so let's fix it up ourselves.
            if (query == null)
                query = "";

            ActiveGameConfig activeGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get();
            IGameConfigDataResolver resolver = activeGameConfig.BaselineGameConfig.SharedConfig;
            int activeLogicVersion = GlobalStateProxyActor.ActiveClientCompatibilitySettings.Get().ClientCompatibilitySettings.ActiveLogicVersion;

            // Search by Id if query is not empty
            MetaDatabase db = MetaDatabase.Get(QueryPriority.Low);
            List<PersistedGuildBase> persistedGuilds;
            if (query == "")
            {
                persistedGuilds = new List<PersistedGuildBase>();
            }
            else
            {
                persistedGuilds = await db.SearchEntitiesByIdAsync<PersistedGuildBase>(EntityKindCore.Guild, count, query);
            }

            List<EntityId> searchResults = new List<EntityId>();

            // Search by Name fragments
            List<string> searchStrings = SearchUtil.ComputeSearchStringsForQuery(query, maxLengthCodepoints: PersistedGuildSearch.MaxPartLengthCodepoints, limit: PersistedGuildSearch.MaxNameSearchQueries);

            // Handle empty query through name search to get results where deleted guilds are sorted at the bottom
            if (query == "")
            {
                searchStrings.Add("");
            }

            foreach (string searchStr in searchStrings)
            {
                int remainingCount = count - searchResults.Count;
                if (remainingCount <= 0)
                    break;

                List<EntityId> nameResults = await db.SearchGuildIdsByNameAsync(count, searchStr);
                HashSet<EntityId> existingResults = new HashSet<EntityId>(persistedGuilds.Select(g => EntityId.ParseFromString(g.EntityId)).Concat(searchResults));

                searchResults.AddRange(nameResults.Except(existingResults));
            }

            // Take half from entity id search and half from name search or closest to that
            int halfCount             = count / 2;
            int entityIdGuildsCount   = halfCount;
            int nameSearchGuildsCount = count - entityIdGuildsCount;

            if (searchResults.Count + persistedGuilds.Count < count)
            {
                entityIdGuildsCount   = count;
                nameSearchGuildsCount = count;
            }
            else if (persistedGuilds.Count < halfCount && searchResults.Count >= halfCount)
            {
                entityIdGuildsCount   = persistedGuilds.Count;
                nameSearchGuildsCount = count - entityIdGuildsCount;
            }
            else if (searchResults.Count < halfCount && persistedGuilds.Count >= halfCount)
            {
                nameSearchGuildsCount = searchResults.Count;
                entityIdGuildsCount   = count - nameSearchGuildsCount;
            }

            persistedGuilds = persistedGuilds.Take(entityIdGuildsCount).ToList();

            // Add persisted guilds from name search
            persistedGuilds.AddRange(
                await Task.WhenAll(
                    searchResults
                        .Take(nameSearchGuildsCount)
                        .Select((EntityId guildId) => db.TryGetAsync<PersistedGuildBase>(guildId.ToString()))
                        .Where(persisted => persisted != null)
                    )
                );

            IEnumerable<GuildListItem> result =
                persistedGuilds.Select(persisted =>
                {
                    if (persisted.Payload == null)
                    {
                        return new GuildListItem
                        {
                            EntityId    = persisted.EntityId.ToString(),
                            DisplayName = "<Empty>"
                        };
                    }

                    try
                    {
                        // \note[jarkko]: should not use global resolver. Each Model should be deserialized with the appropriate resolver. In practice
                        //                this is a bit involved (should wake actor to run migrations et al.), so let's just hope for the best :)
                        PersistedEntityConfig entityConfig = EntityConfigRegistry.Instance.GetPersistedConfig(EntityKindCore.Guild);
                        IGuildModelBase model = entityConfig.DeserializeDatabasePayload<IGuildModelBase>(EntityId.ParseFromString(persisted.EntityId), persisted.Payload, resolver, activeLogicVersion);
                        return new GuildListItem
                        {
                            EntityId        = persisted.EntityId.ToString(),
                            DisplayName     = model.DisplayName,
                            CreatedAt       = model.CreatedAt.ToDateTime(),
                            LastLoginAt     = model.GetMemberOnlineLatestAt(timestampNow: MetaTime.Now).ToDateTime(),
                            Phase           = model.LifecyclePhase.ToString(),
                            NumMembers      = model.MemberCount,
                            MaxNumMembers   = model.MaxNumMembers,
                        };
                    }
                    catch (MetaSerializationException)
                    {
                        return new GuildListItem
                        {
                            EntityId        = persisted.EntityId.ToString(),
                            DisplayName     = "<Failed to parse>",
                            CreatedAt       = new DateTime(),
                            LastLoginAt     = new DateTime(),
                            Phase           = "Unknown",
                        };
                    }
                });

            return new ActionResult<IEnumerable<GuildListItem>>(result);
        }

        /// <summary>
        /// API Endpoint to return information about active guilds - these are guilds who are actively using the game right now
        /// Usage:  GET /api/guilds/activeGuilds
        /// Test:   curl http://localhost:5550/api/guilds/activeGuilds
        /// </summary>
        [HttpGet("guilds/activeGuilds")]
        [RequirePermission(MetaplayPermissions.ApiGuildsView)]
        public async Task<ActionResult<IEnumerable<IActiveEntityInfo>>> ActiveGuilds()
        {
            var response = await EntityAskAsync(StatsCollectorManager.EntityId, new ActiveEntitiesRequest(EntityKindCore.Guild));
            return new ActionResult<IEnumerable<IActiveEntityInfo>>(response.ActiveEntities);
        }
    }
}

#endif
