using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Logic.Leaderboard;
using Game.Server.Extensions;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;

namespace Game.Server.Leaderboard;

public class LeaderboardActor : PersistedEntityActor<PersistedLeaderboardModel, LeaderboardModel>
{
    LeaderboardModel _state;
    public LeaderboardActor(EntityId entityId) : base(entityId)
    {
    }
    protected override TimeSpan SnapshotInterval => TimeSpan.FromMinutes(1);
    protected override AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();
    protected override async Task Initialize()
    {
        PersistedLeaderboardModel persisted = await MetaDatabase.Get().TryGetAsync<PersistedLeaderboardModel>(_entityId.ToString());
        await InitializePersisted(persisted);
    }
    protected override async Task<LeaderboardModel> InitializeNew()
    {
        // Load top entries from database (limit to a reasonable number)
        var entries = await LoadTopEntriesFromDatabase(10);
        var model = new LeaderboardModel();
        
        foreach(var entry in entries)
        {
            model.Entries[entry.PlayerEntityId] = new LeaderboardModel.Entry(
                entry.Kills,
                entry.GoldCollected,
                entry.RoundsCompleted,
                entry.RecordedAt
            );
        }
        
        return model;
    }


    protected override Task<LeaderboardModel> RestoreFromPersisted(PersistedLeaderboardModel persisted)
    {
        LeaderboardModel state = DeserializePersistedPayload<LeaderboardModel>(persisted.Payload, resolver: null, logicVersion: null);
        return Task.FromResult(state);
    }

    protected override Task PostLoad(LeaderboardModel payload, DateTime persistedAt, TimeSpan elapsedTime)
    {
        _state = payload;
        return Task.CompletedTask;
    }

    protected override async Task PersistStateImpl(bool isInitial, bool isFinal)
    {
        byte[] serialized = SerializeToPersistedPayload(_state, resolver: null, logicVersion: null);
        var persisted = new PersistedLeaderboardModel
        {
            EntityId = _entityId.ToString(),
            PersistedAt = DateTime.UtcNow,
            Payload = serialized,
            SchemaVersion = CurrentSchemaVersion,
            IsFinal = isFinal
        };
        
        var db = MetaDatabase.Get();
        if (isInitial)
            await db.InsertAsync(persisted).ConfigureAwait(false);
        else
            await db.UpdateAsync(persisted).ConfigureAwait(false);
    }
    
    [EntityAskHandler]
    private async Task<GetLeaderboardResponse> HandleGetLeaderboardRequest(EntityId sender, GetLeaderboardRequest request)
    {
        // Get top entries based on kills
        var topEntries = await LoadTopEntriesFromDatabase(request.TopCount);
        
        
        return new GetLeaderboardResponse(topEntries.Select(ToDto).ToList());
    }

    // [EntityAskHandler]
    // UpdateLeaderboardResponse HandleUpdateLeaderboardRequest(EntityId sender, UpdateLeaderboardRequest request)
    // {
    //     // Update player's entry
    //     _state.Entries[request.PlayerId] = new LeaderboardModel.LeaderboardEntry(
    //         request.Kills,
    //         request.GoldCollected,
    //         request.RoundsCompleted,
    //         DateTime.UtcNow
    //     );
    //
    //     // Also update the database
    //     var dbEntry = new LeaderboardEntry(
    //         request.PlayerId,
    //         request.Kills,
    //         request.GoldCollected,
    //         request.RoundsCompleted,
    //         DateTime.UtcNow
    //     );
    //
    //     // Use an upsert operation
    //     MetaDatabase.Get().UpsertAsync(dbEntry).FireAndForget();
    //
    //     return new UpdateLeaderboardResponse(true);
    // }
    
    private static async Task<List<LeaderboardEntry>> LoadTopEntriesFromDatabase(int limit)
    {
        return await MetaDatabase.Get().GetTopLeaderBoardEntries(limit);
    }
    
    // TODO: move to extension method
    private LeaderboardEntryDto ToDto(LeaderboardEntry entry)
    {
        return new LeaderboardEntryDto
        {
            PlayerId = entry.PlayerId,
            Kills = entry.Kills,
            GoldCollected = entry.GoldCollected,
            RoundsCompleted = entry.RoundsCompleted,
            RecordedAt = MetaTime.FromDateTime(entry.RecordedAt)
        };
    }
}