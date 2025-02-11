// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.League;
using Metaplay.Core.League.Player;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Server.Database;
using Metaplay.Server.League;
using Metaplay.Server.League.InternalMessages;
using Metaplay.Server.League.Player.InternalMessages;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    public abstract partial class PlayerActorBase<TModel, TPersisted>
    {
        protected abstract class LeagueComponentBase : EntityLeagueComponent, ILeagueIntegrationEntityContext
        {
            /// <inheritdoc />
            protected override EntityActor OwnerActor => PlayerActor;
            protected PlayerActorBase<TModel, TPersisted> PlayerActor { get; }

            public ILeagueIntegrationHandler this[ClientSlot slot]
            {
                get
                {
                    ILeagueIntegrationHandler handler = LeagueIntegrationHandlers.FirstOrDefault(h => h.ClientSlot == slot);
                    return handler;
                }
            }

            public IReadOnlyList<ILeagueIntegrationHandler> All => LeagueIntegrationHandlers;

            /// <summary>
            /// The set of league integration handlers that are active for the current player.
            /// These can be created with the <see cref="CreateLeagueIntegrationHandler{THandler}"/> method in the actor constructor.
            /// </summary>
            protected IReadOnlyList<ILeagueIntegrationHandler> LeagueIntegrationHandlers => _leagueIntegrationHandlers;
            List<ILeagueIntegrationHandler> _leagueIntegrationHandlers = new List<ILeagueIntegrationHandler>();

            protected LeagueComponentBase(PlayerActorBase<TModel, TPersisted> playerActor)
            {
                this.PlayerActor = playerActor;
            }

            public IEntityMessagingApi  EntityMessagingApi  => OwnerActor;
            public IMetaLogger          Logger              => PlayerActor._log;

            /// <inheritdoc />
            PlayerDivisionAvatarBase ILeagueIntegrationEntityContext.GetPlayerLeaguesDivisionAvatar(ClientSlot leagueSlot)
                => PlayerActor.GetPlayerLeaguesDivisionAvatar(leagueSlot);

            /// <inheritdoc />
            MetaActionResult ILeagueIntegrationEntityContext.ExecuteServerActionImmediately(PlayerActionBase action)
                => PlayerActor.ExecuteServerActionImmediately(action);

            /// <inheritdoc />
            void ILeagueIntegrationEntityContext.EnqueueServerAction(PlayerActionBase action)
                => PlayerActor.EnqueueServerAction(action);

            /// <inheritdoc />
            void ILeagueIntegrationEntityContext.AddEntityAssociation(AssociatedEntityRefBase association)
                => PlayerActor.AddEntityAssociation(association, removeOnSessionEnd: true, informSession: true);

            /// <inheritdoc />
            void ILeagueIntegrationEntityContext.RemoveEntityAssociation(ClientSlot clientSlot)
                => PlayerActor.RemoveEntityAssociation(clientSlot);

            public THandler CreateLeagueIntegrationHandler<THandler>(ClientSlot leagueClientSlot, uint leagueId, ILeagueIntegrationHandler.CreateFunc createHandler)
                where THandler : class, ILeagueIntegrationHandler
            {
                EntityId leagueManagerId = EntityId.Create(EntityKindCloudCore.LeagueManager, leagueId);
                THandler handler         = createHandler(leagueClientSlot, leagueManagerId, () => PlayerActor.Model, this) as THandler;

                if (handler == null)
                    throw new InvalidOperationException($"Failed to create league integration handler of type {typeof(THandler).Name}.");

                if (LeagueIntegrationHandlers.Any(h => h.LeagueId == leagueManagerId || h.ClientSlot == leagueClientSlot))
                    throw new InvalidOperationException($"League integration handler for league manager {leagueManagerId} or client slot {leagueClientSlot} already exists.");

                _leagueIntegrationHandlers.Add(handler);

                return handler;
            }

            protected bool TryResolveLeagueHandlerByEntityId(EntityId senderId, out ILeagueIntegrationHandler handler)
            {
                handler = null;

                if (senderId.Kind == EntityKindCloudCore.LeagueManager)
                    handler = LeagueIntegrationHandlers.FirstOrDefault(h => h.LeagueId == senderId);

                if (senderId.Kind == EntityKindCore.Division)
                {
                    EntityId leagueManagerId = EntityId.Create(
                        EntityKindCloudCore.LeagueManager,
                        (ulong)DivisionIndex.FromEntityId(senderId).League);

                    handler = LeagueIntegrationHandlers.FirstOrDefault(h => h.LeagueId == leagueManagerId);
                }

                if (handler == null)
                    return false;

                return true;
            }

            [MessageHandler]
            async Task HandleInternalLeagueParticipantDivisionForceUpdated(EntityId sender, InternalLeagueParticipantDivisionForceUpdated message)
            {
                if (!MetaplayCore.Options.FeatureFlags.EnablePlayerLeagues)
                    return;

                if (!TryResolveLeagueHandlerByEntityId(sender, out ILeagueIntegrationHandler handler))
                {
                    PlayerActor._log.Error("Failed to resolve league handler for message {MessageType} from origin {EntityId}.", sender, nameof(InternalLeagueParticipantDivisionForceUpdated));
                    return;
                }

                PlayerActor.KickPlayerIfConnected(PlayerForceKickOwnerReason.AdminAction);

                await handler.JoinOrUpdateDivision(message.NewDivision, subscribe: false);

                await handler.OnDivisionForceUpdated(message.NewDivision);
            }

            public virtual void PostLoad()
            {
                foreach (ILeagueIntegrationHandler handler in LeagueIntegrationHandlers)
                    handler.Setup();
            }

            public virtual async Task HandleLeaguesOnSessionSubscriber()
            {
                foreach (ILeagueIntegrationHandler handler in LeagueIntegrationHandlers)
                    await handler.HandleLeagueOnSessionSubscriber();
            }
        }

        /// <summary>
        /// Create a division avatar from the player model.
        /// Avatars are public information shown to other players, that represent a player in the division leaderboards.
        /// This takes in the ClientSlot of the division, and returns an avatar for that division.
        /// Will return a <see cref="PlayerDivisionAvatarBase.Default"/> by default.
        /// </summary>
        protected virtual PlayerDivisionAvatarBase GetPlayerLeaguesDivisionAvatar(ClientSlot leagueSlot) =>
            new PlayerDivisionAvatarBase.Default(Model.PlayerName);

        protected virtual LeagueComponentBase CreateLeagueComponent()
        {
            if (MetaplayCore.Options.FeatureFlags.EnablePlayerLeagues)
                throw new NotImplementedException($"If player leagues is enabled, please implement the method {nameof(CreateLeagueComponent)}.");

            return null;
        }

        protected LeagueComponentBase Leagues { get; private set; }
    }

    public abstract class EntityLeagueComponent : EntityComponent, IMetaIntegration<EntityLeagueComponent> { }
}

namespace Metaplay.Server.League
{
    public interface ILeagueIntegrationEntityContext
    {
        IEntityMessagingApi  EntityMessagingApi  { get; }
        IMetaLogger          Logger              { get; }

        /// <summary>
        /// Create a division avatar from the player model.
        /// By default, calls the PlayerActor's <see cref="PlayerActorBase{TModel, TPersisted}.GetPlayerLeaguesDivisionAvatar"/> method.
        /// </summary>
        PlayerDivisionAvatarBase GetPlayerLeaguesDivisionAvatar(ClientSlot leagueSlot);

        /// <inheritdoc cref="PlayerActorBase{TModel, TPersisted}.ExecuteServerActionImmediately"/>
        MetaActionResult ExecuteServerActionImmediately(PlayerActionBase action);

        /// <inheritdoc cref="PlayerActorBase{TModel, TPersisted}.EnqueueServerAction"/>
        void EnqueueServerAction(PlayerActionBase action);

        /// <inheritdoc cref="PlayerActorBase{TModel, TPersisted}.AddEntityAssociation"/>
        void AddEntityAssociation(AssociatedEntityRefBase association);

        /// <inheritdoc cref="PlayerActorBase{TModel, TPersisted}.RemoveEntityAssociation"/>
        void RemoveEntityAssociation(ClientSlot clientSlot);

        /// <inheritdoc cref="IEntityMessagingApi.EntityAskAsync{TResult}(EntityId, MetaMessage)"/>
        Task<TResult> EntityAskAsync<TResult>(EntityId entity, MetaMessage request) where TResult : MetaMessage => EntityMessagingApi.EntityAskAsync<TResult>(entity, request);

        /// <inheritdoc cref="IEntityMessagingApi.EntityAskAsync{TResult}(EntityId, EntityAskRequest{TResult})"/>
        Task<TResult> EntityAskAsync<TResult>(EntityId entity, EntityAskRequest<TResult> request) where TResult : EntityAskResponse => EntityMessagingApi.EntityAskAsync<TResult>(entity, request);

        /// <inheritdoc cref="IEntityMessagingApi.EntityAskAsync{TResult}(EntityId, EntityAskRequest)"/>
        [Obsolete("Type mismatch between TResult type parameter and the annotated response type for the request parameter type.", error: true)]
        Task<TResult> EntityAskAsync<TResult>(EntityId entity, EntityAskRequest request) where TResult : MetaMessage => EntityMessagingApi.EntityAskAsync<TResult>(entity, (MetaMessage)request);

        /// <inheritdoc cref="IEntityMessagingApi.CastMessage"/>
        void CastMessage(EntityId entity, MetaMessage message) => EntityMessagingApi.CastMessage(entity, message);

        /// <inheritdoc cref="IEntityMessagingApi.TellSelf"/>
        void TellSelf(object command) => EntityMessagingApi.TellSelf(command);

    }

    /// <summary>
    /// A handler for a single league integration.
    /// </summary>
    public interface ILeagueIntegrationHandler
    {
        ClientSlot           ClientSlot          { get; }
        EntityId             LeagueId            { get; }
        IDivisionClientState DivisionClientState { get; }

        Task OnDivisionForceUpdated(EntityId newDivision);

        /// <summary>
        /// Setup the division client state.
        /// This will get called on every actor start.
        /// </summary>
        void Setup();

        /// <summary>
        /// Emits the score event to the current Division, if any.
        /// </summary>
        void EmitDivisionScoreEvent(IDivisionScoreEvent scoreEvent);

        /// <summary>
        /// Request to join a player league. Joining a league is not possible if:
        /// <list type="bullet">
        /// <item>The player is already a participant in the league (joinedDivision is still returned)</item>
        /// <item>The league is not currently running</item>
        /// <item>The between-seasons migration has not finished for the new season</item>
        /// <item>The player does not meet the requirements set by the league manager</item>
        /// <item>The league is a guild league, in which case the guild should send the request</item>
        /// </list>
        /// Extending and passing in the <see cref="LeagueJoinRequestPayloadBase"/> class can be used to deliver any information about the player
        /// required by the league manager to decide the player's eligibility and their starting rank.
        /// </summary>
        /// <returns>A tuple of possible joinedDivision if successful, or a refusal reason if not.
        /// If the player is already a part of this league, both joinedDivision and <see cref="LeagueJoinRefuseReason"/>.<see cref="LeagueJoinRefuseReason.AlreadyInLeague"/> are returned.</returns>
        Task<(DivisionIndex? joinedDivision, LeagueJoinRefuseReason? reason)> TryJoinPlayerLeague(LeagueJoinRequestPayloadBase requestPayload = null);

        /// <summary>
        /// Request to be reassigned within a season to a (possibly) new rank and division.
        /// Reassignment can be used for example:
        /// <list type="bullet">
        /// <item>In-season rank-ups</item>
        /// <item>If divisions of the season have a shorter duration, and the player wants to participate again.</item>
        /// </list>
        /// <para>
        /// Reassignment is not possible when the league is not running, when the between-season migration is ongoing,
        /// or when the player does not meet the requirements set by the league manager.
        /// </para>
        /// <para>
        /// If the current division has not concluded, the player does not get a history entry or any associated rewards
        /// for the division, even if the division would conclude later and award the player some rewards. The player
        /// will not be able to claim these rewards later.
        /// </para>
        /// <para>
        /// This operation might leave the player without an assigned division if the reassignment fails.
        /// Most likely due to the league manager refusing entry. This method can be called again safely to retry later.
        /// </para>
        /// </summary>
        /// <param name="leaveOldDivision">If true, the player will be removed from the player listing of the old division.</param>
        /// <returns>A tuple of possible joinedDivision if successful, or a refusal reason if not.</returns>
        Task<(DivisionIndex? joinedDivision, LeagueJoinRefuseReason? reason)> TryReassignPlayer(bool leaveOldDivision, LeagueJoinRequestPayloadBase requestPayload = null);


        /// <summary>
        /// Try finalize the currently active concluded division. This will fetch the division's history entry for the player, and add it to the division history.
        /// If the division is not concluded or the current divison is not valid, this will return false.
        /// If <paramref name="leaveDivision"/> is set, this will remove the division from the active session, and set currentdivision to null.
        /// Returns true if everything went well.
        /// </summary>
        /// <param name="leaveDivision">If this is set to true, the division will be removed from the active session, and currentdivision is set to null.</param>
        /// <exception cref="InvalidOperationException">If leagues feature is not enabled or player's division client state is not valid.</exception>
        Task<bool> TryFinalizeCurrentLeagueDivision(bool leaveDivision);

        /// <summary>
        /// Handles division state updates on session start.
        /// If the player's current division has concluded, the division is added to the division history.
        /// If the player has been reassigned to a new division,
        /// the old division is added to the division history and the player joins the new division.
        /// If the old division is still running, the player updates the the division with an updated avatar.
        /// </summary>
        Task HandleLeagueOnSessionSubscriber();

        Task<bool> JoinOrUpdateDivision(EntityId division, bool subscribe = true);

        public delegate ILeagueIntegrationHandler CreateFunc(ClientSlot clientSlot, EntityId leagueManagerId,
            Func<IPlayerModelBase> getPlayerModel, ILeagueIntegrationEntityContext messagingContext);
    }

    public class DefaultPlayerLeagueIntegrationHandler<TDivisionClientState> : ILeagueIntegrationHandler
        where TDivisionClientState : PlayerSubClientStateBase, IDivisionClientState, new()
    {
        /// <inheritdoc />
        public ClientSlot ClientSlot { get; }
        /// <inheritdoc />
        public EntityId LeagueId { get; }

        protected ILeagueIntegrationEntityContext EntityContext { get; }

        readonly  Func<IPlayerModelBase> _getPlayerModel;
        protected IPlayerModelBase       PlayerModel         => _getPlayerModel();
        protected EntityId               PlayerId            => PlayerModel.PlayerId;
        public    TDivisionClientState   DivisionClientState => PlayerModel.PlayerSubClientStates[ClientSlot] as TDivisionClientState;
        protected IMetaLogger            _log                => EntityContext.Logger;

        IDivisionClientState ILeagueIntegrationHandler.DivisionClientState => DivisionClientState;

        public DefaultPlayerLeagueIntegrationHandler(ClientSlot clientSlot, EntityId leagueId, Func<IPlayerModelBase> getPlayerModel,
            ILeagueIntegrationEntityContext entityContext)
        {
            this.ClientSlot       = clientSlot;
            this.LeagueId         = leagueId;
            this._getPlayerModel  = getPlayerModel;
            this.EntityContext    = entityContext;
        }

        /// <inheritdoc />
        public static ILeagueIntegrationHandler Create(ClientSlot clientSlot, EntityId leagueManagerId, Func<IPlayerModelBase> getPlayerModel,
            ILeagueIntegrationEntityContext messagingContext)
        {
            return new DefaultPlayerLeagueIntegrationHandler<TDivisionClientState>(clientSlot, leagueManagerId, getPlayerModel, messagingContext);
        }

        /// <inheritdoc />
        public virtual Task OnDivisionForceUpdated(EntityId newDivision) => Task.CompletedTask;

        /// <inheritdoc />
        public virtual void Setup()
        {
            if (PlayerModel.PlayerSubClientStates.ContainsKey(ClientSlot))
                return;

            PlayerModel.PlayerSubClientStates[ClientSlot] = new TDivisionClientState();
        }

        /// <inheritdoc />
        public virtual void EmitDivisionScoreEvent(IDivisionScoreEvent scoreEvent)
        {
            if (!MetaplayCore.Options.FeatureFlags.EnablePlayerLeagues)
                return;

            IDivisionClientState divisionClientState = DivisionClientState;

            EntityId currentDivision = divisionClientState.CurrentDivision;

            if (!currentDivision.IsValid)
                return;

            InternalDivisionScoreEventMessage scoreMessage = new InternalDivisionScoreEventMessage(
                participantId: PlayerModel.PlayerId,
                playerId: PlayerModel.PlayerId,
                scoreEvent: scoreEvent);

            EntityContext.CastMessage(currentDivision, scoreMessage);
        }

        /// <inheritdoc />
        public async Task<bool> JoinOrUpdateDivision(EntityId division, bool subscribe = true)
        {
            PlayerDivisionAvatarBase avatar = EntityContext.GetPlayerLeaguesDivisionAvatar(ClientSlot);
            if (avatar == null)
                throw new NullReferenceException($"{nameof(EntityContext.GetPlayerLeaguesDivisionAvatar)} returned a null avatar.");

            bool success = false;
            try
            {
                InternalPlayerDivisionJoinOrUpdateAvatarResponse joinResponse =
                    await EntityContext.EntityAskAsync(division, new InternalPlayerDivisionJoinOrUpdateAvatarRequest(PlayerId, avatarDataEpoch: 0, avatar, allowJoiningConcluded: false));

                // Update model state.
                if (DivisionClientState.CurrentDivision != division || DivisionClientState.CurrentDivisionParticipantIdx != joinResponse.DivisionParticipantIndex)
                    EntityContext.ExecuteServerActionImmediately(new PlayerSetCurrentDivision(ClientSlot, division, joinResponse.DivisionParticipantIndex));

                success = true;
            }
            catch (InternalEntityAskNotSetUpRefusal)
            {
                _log.Error("Unable to join or update division {DivisionId}. Division state is not set up.", division);
                EntityContext.CastMessage(LeagueId, new InternalLeagueReportInvalidDivisionState(division));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Updating division avatar for {division} failed.");
            }

            if (!subscribe)
                return success;

            if (success)
            {
                // Set player associated with the division.
                EntityContext.AddEntityAssociation(
                    new AssociatedDivisionRef(
                        ClientSlot,
                        PlayerId,
                        division,
                        divisionAvatarEpoch: 0));

                // Success
                return true;
            }

            return false;
        }

        /// <inheritdoc />
        public virtual async Task<(DivisionIndex? joinedDivision, LeagueJoinRefuseReason? reason)> TryJoinPlayerLeague(LeagueJoinRequestPayloadBase requestPayload = null)
        {
            if (!MetaplayCore.Options.FeatureFlags.EnablePlayerLeagues)
                throw new InvalidOperationException("Cannot join leagues if leagues is not enabled.");

            DivisionIndex?          joinedDivision = null;
            LeagueJoinRefuseReason? refuseReason   = null;

            PersistedParticipantDivisionAssociation oldAssociation = await MetaDatabase.Get().TryGetParticipantDivisionAssociation(LeagueId, PlayerId);

            // If already in league, subscribe and early exit.
            if (oldAssociation != null && oldAssociation.LeagueId == LeagueId.ToString())
            {
                EntityId existingDivision = EntityId.ParseFromString(oldAssociation.DivisionId);

                if (DivisionClientState.HistoricalDivisions.Any(division => division.DivisionId == existingDivision))
                {
                    // Already in league and division is in history. Next season has not started yet.
                    return (null, LeagueJoinRefuseReason.LeagueNotStarted);
                }

                if (!await JoinOrUpdateDivision(existingDivision))
                    // Joining division failed.
                    return (null, LeagueJoinRefuseReason.UnknownReason);

                return (DivisionIndex.FromEntityId(existingDivision), LeagueJoinRefuseReason.AlreadyInLeague);
            }

            // Not already a participant. Try to join league.
            InternalLeagueJoinResponse response = await EntityContext.EntityAskAsync(
                LeagueId,
                new InternalLeagueJoinRequest(PlayerId, requestPayload ?? EmptyLeagueJoinRequestPayload.Instance));

            EntityId newDivision;

            if (response.Success)
            {
                joinedDivision = response.DivisionToJoin;
                newDivision    = joinedDivision.Value.ToEntityId();
            }
            else
            {
                refuseReason = response.RefuseReason;

                _log.Debug($"League join refused. Reason: {refuseReason}");
                return (null, refuseReason);
            }

            if (await JoinOrUpdateDivision(newDivision))
                return (joinedDivision, null);

            // DivisionId was not valid or joining division failed.
            return (null, LeagueJoinRefuseReason.UnknownReason);
        }

        /// <inheritdoc />
        public async Task<(DivisionIndex? joinedDivision, LeagueJoinRefuseReason? reason)> TryReassignPlayer(bool leaveOldDivision, LeagueJoinRequestPayloadBase requestPayload)
        {
            if (!MetaplayCore.Options.FeatureFlags.EnablePlayerLeagues)
                throw new InvalidOperationException("Cannot use leagues if leagues is not enabled.");

            DivisionIndex?          joinedDivision = null;
            LeagueJoinRefuseReason? refuseReason   = null;

            await TryFinalizeCurrentLeagueDivision(true);

            // Ask league manager to reassign.
            InternalLeagueJoinResponse response = await EntityContext.EntityAskAsync<InternalLeagueJoinResponse>(
                LeagueId,
                new InternalLeagueReassignRequest(PlayerId, requestPayload, leaveOldDivision));

            if (!response.Success)
            {
                refuseReason = response.RefuseReason;

                _log.Information($"League reassign refused. Reason: {refuseReason}");
                return (null, refuseReason);
            }

            joinedDivision       = response.DivisionToJoin;
            EntityId newDivision = joinedDivision.Value.ToEntityId();


            if (await JoinOrUpdateDivision(newDivision))
                return (joinedDivision, null);

            // DivisionId was not valid or joining division failed.
            return (null, LeagueJoinRefuseReason.UnknownReason);
        }

        /// <inheritdoc />
        public async Task<bool> TryFinalizeCurrentLeagueDivision(bool leaveDivision)
        {
            if (!MetaplayCore.Options.FeatureFlags.EnablePlayerLeagues)
                throw new InvalidOperationException("Cannot use leagues if leagues is not enabled.");

            IDivisionClientState divisionClientState = DivisionClientState;
            EntityId             currentDivision     = divisionClientState.CurrentDivision;

            if (!currentDivision.IsValid)
                return false; // No valid division.

            // If no history entry exists, add one.
            if (divisionClientState.HistoricalDivisions.All(division => division.DivisionId != currentDivision))
            {
                try
                {
                    InternalDivisionParticipantHistoryResponse historyResponse = await EntityContext.EntityAskAsync(currentDivision, new InternalDivisionParticipantHistoryRequest(PlayerId));

                    if (historyResponse.IsParticipant && historyResponse.IsConcluded && historyResponse.HistoryEntry != null)
                    {
                        EntityContext.EnqueueServerAction(new PlayerAddHistoricalDivisionEntry(ClientSlot, historyResponse.HistoryEntry, leaveDivision));
                        _log.Debug("Current division was concluded. Adding a history entry.");

                        if (leaveDivision) // Leave division
                        {
                            _log.Debug("Leaving division.");
                            EntityContext.RemoveEntityAssociation(ClientSlot);
                        }

                        return true;
                    }
                }
                catch (InternalEntityAskNotSetUpRefusal)
                {
                    _log.Error("Unable to receive history state for current division {DivisionId}. Division state is not set up.", currentDivision);
                    EntityContext.CastMessage(LeagueId, new InternalLeagueReportInvalidDivisionState(currentDivision));
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Unable to receive history state for current division {DivisionId}.", currentDivision);
                }
            }

            return false;
        }

        /// <inheritdoc />
        public async Task HandleLeagueOnSessionSubscriber()
        {
            IDivisionClientState divisionClientState = DivisionClientState;
            EntityId currentDivision = divisionClientState.CurrentDivision;

            PersistedParticipantDivisionAssociation divisionAssociation = await MetaDatabase.Get().TryGetParticipantDivisionAssociation(LeagueId, PlayerId);

            EntityId expectedDivision = EntityId.None;

            if (divisionAssociation != null)
                expectedDivision = EntityId.ParseFromString(divisionAssociation.DivisionId);

            if (expectedDivision.IsValid && divisionClientState.HistoricalDivisions.Any(division => division.DivisionId == expectedDivision))
            {
                // History state has already been received for expected division. No need to join.

                if (currentDivision == expectedDivision) // Current division is expected division. Clear state.
                {
                    _log.Debug("Current division was not cleared on adding historical entry. Clearing now.");
                    divisionClientState.CurrentDivision = EntityId.None;
                }

                return;
            }

            // Check if current division has concluded without being reassigned.
            if (expectedDivision == currentDivision && currentDivision.IsValid)
            {
                if (await TryFinalizeCurrentLeagueDivision(leaveDivision: false))
                {
                    divisionClientState.CurrentDivision = EntityId.None;
                    return;
                }
            }

            // Has been reassigned by the league manager.
            if (expectedDivision != currentDivision)
            {
                // Leave current division if valid.
                if (currentDivision.IsValid)
                    await TryFinalizeCurrentLeagueDivision(leaveDivision: false);

                // Set current to new division
                divisionClientState.CurrentDivision = expectedDivision;
                currentDivision = expectedDivision;
            }

            // Send updated avatar to current division. If valid.
            if (currentDivision.IsValid)
            {
                PlayerDivisionAvatarBase avatar = EntityContext.GetPlayerLeaguesDivisionAvatar(ClientSlot);
                if (avatar == null)
                    throw new NullReferenceException($"{nameof(EntityContext.GetPlayerLeaguesDivisionAvatar)} returned a null avatar.");

                bool success = false;
                try
                {
                    InternalPlayerDivisionJoinOrUpdateAvatarResponse joinResponse =
                        await EntityContext.EntityAskAsync(currentDivision, new InternalPlayerDivisionJoinOrUpdateAvatarRequest(PlayerId, avatarDataEpoch: 0, avatar, allowJoiningConcluded: false));
                    divisionClientState.CurrentDivisionParticipantIdx = joinResponse.DivisionParticipantIndex;

                    success = true;
                }
                catch (InternalEntityAskNotSetUpRefusal)
                {
                    _log.Warning("Unable to join or update avatar for division {DivisionId}. Division state is not set up.", currentDivision);
                    EntityContext.CastMessage(LeagueId, new InternalLeagueReportInvalidDivisionState(currentDivision));
                }
                catch (Exception e)
                {
                    _log.Warning("Unable to join or update avatar for division {DivisionId}. Division might be concluded already. {Error}", currentDivision, e.Message);
                }

                if (success)
                {
                    // Set player associated with the division.
                    EntityContext.AddEntityAssociation(new AssociatedDivisionRef(ClientSlot, PlayerId, expectedDivision, divisionAvatarEpoch: 0));
                }
            }
        }
    }
}

