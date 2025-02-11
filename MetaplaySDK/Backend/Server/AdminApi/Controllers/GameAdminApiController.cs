// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using System.Threading.Tasks;

namespace Metaplay.Server.AdminApi.Controllers
{
    /// <summary>
    /// AdminApi controller that supports game-related features (eg, players, action execution, etc.).
    /// </summary>
    public abstract class GameAdminApiController : MetaplayAdminApiController
    {
        override protected ServerApiBridge ApiBridge => (ServerApiBridge)_apiBridge;

        #region ApiBridge helpers (to allow calls without prefixes)

        /// <inheritdoc cref="ServerApiBridge.ParsePlayerIdStr(string)"/>
        protected EntityId ParsePlayerIdStr(string playerIdStr) =>
            ApiBridge.ParseEntityIdStr(playerIdStr, EntityKindCore.Player);

        /// <inheritdoc cref="ServerApiBridge.ParsePlayerIdStrAndCheckForExistenceAsync(string)"/>
        protected Task<EntityId> ParsePlayerIdStrAndCheckForExistenceAsync(string playerIdStr) =>
            ApiBridge.ParsePlayerIdStrAndCheckForExistenceAsync(playerIdStr);

        /// <inheritdoc cref="ServerApiBridge.TryGetPlayerDetailsAsync(string)"/>
        protected Task<(bool isFound, bool isInitialized, PlayerDetails details)> TryGetPlayerDetailsAsync(string playerIdStr) =>
            ApiBridge.TryGetPlayerDetailsAsync(playerIdStr);

        /// <inheritdoc cref="ServerApiBridge.GetPlayerDetailsAsync(string)"/>
        protected Task<PlayerDetails> GetPlayerDetailsAsync(string playerIdStr) =>
            ApiBridge.GetPlayerDetailsAsync(playerIdStr);

        /// <inheritdoc cref="ServerApiBridge.ExecutePlayerServerActionAsync(EntityId, PlayerActionBase)"/>
        protected Task ExecutePlayerServerActionAsync(EntityId entityId, PlayerActionBase serverAction) =>
            ApiBridge.ExecutePlayerServerActionAsync(entityId, serverAction);

        /// <inheritdoc cref="ServerApiBridge.EnqueuePlayerServerActionAsync(EntityId, PlayerActionBase)"/>
        protected Task EnqueuePlayerServerActionAsync(EntityId entityId, PlayerActionBase serverAction) =>
            ApiBridge.EnqueuePlayerServerActionAsync(entityId, serverAction);

        /// <inheritdoc cref="ServerApiBridge.GetEntityStateAsync(EntityId)"/>
        protected Task<IModel> GetEntityStateAsync(EntityId entityId) =>
            ApiBridge.GetEntityStateAsync(entityId);

        #endregion
    }
}
