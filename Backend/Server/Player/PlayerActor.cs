// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Server;
using System;
using System.Threading.Tasks;
using Game.Logic.TypeCodes;
using Game.Server.Leaderboard;
using static System.FormattableString;

namespace Game.Server.Player
{
    [EntityConfig]
    public class PlayerConfig : PlayerConfigBase
    {
        public override Type EntityActorType => typeof(PlayerActor);
    }

    /// <summary>
    /// Entity actor class representing a player.
    /// </summary>
    public sealed class PlayerActor : PlayerActorBase<PlayerModel>, IPlayerModelServerListener
    {
        public PlayerActor(EntityId playerId) : base(playerId)
        {
        }

        protected override string RandomNewPlayerName()
        {
            return Invariant($"Guest {new Random().Next(100_000)}");
        }

        protected override void OnSwitchedToModel(PlayerModel model)
        {
            model.ServerListener = this;
        }

        protected override async Task OnSessionStartAsync(PlayerSessionParamsBase start, bool isFirstLogin)
        {
            await FetchLeaderboardAsync();
        }

        public async Task FetchLeaderboardAsync()
        {
            EntityId leaderboardEntity = EntityId.Create(EntityKindGame.Leaderboard, 0);
            var request = new GetLeaderboardRequest(10);
            var response = await EntityAskAsync(leaderboardEntity, request);
        
            // Update the player model with leaderboard data
            Model.Leaderboard = response.Entries;
        }
    }
}
