using System.Collections.Generic;
using Game.Logic.Leaderboard;
using Game.Logic.TypeCodes;
using Metaplay.Cloud.Entity;
using Metaplay.Core;

namespace Game.Server.Leaderboard;

[MetaMessage(MessageCodes.GetLeaderboardRequest, MessageDirection.ServerInternal)]
public class GetLeaderboardRequest : EntityAskRequest<GetLeaderboardResponse>
{
    public int TopCount { get; private set; } = 10;
    
    public GetLeaderboardRequest() { }
    public GetLeaderboardRequest(int topCount)
    {
        TopCount = topCount;
    }
}

[MetaMessage(MessageCodes.GetLeaderboardResponse, MessageDirection.ServerInternal)]
public class GetLeaderboardResponse : EntityAskResponse
{
    public List<LeaderboardEntryDto> Entries { get; set; } = new();
    
    public GetLeaderboardResponse() { }
    public GetLeaderboardResponse(List<LeaderboardEntryDto> entries)
    {
        Entries = entries;
    }
}

[MetaMessage(MessageCodes.UpdateLeaderboardRequest, MessageDirection.ServerInternal)]
public class UpdateLeaderboardRequest : MetaMessage
{
    public LeaderboardEntryDto Entry { get; set; }
    public UpdateLeaderboardRequest() { }
    public UpdateLeaderboardRequest(LeaderboardEntryDto entry)
    {
        Entry = entry;
    }
}