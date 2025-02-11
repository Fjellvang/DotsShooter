// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    // See PersistedMultiplayerEntityActorBase.EntityAssociation.cs

    partial class PlayerActorBase<TModel, TPersisted>
    {
        struct PlayerAssociatedEntityRecord
        {
            public AssociatedEntityRefBase EntityRef;
            public bool RemoveOnSessionEnd;
            public PlayerAssociatedEntityRecord(AssociatedEntityRefBase entityRef, bool removeOnSessionEnd)
            {
                EntityRef = entityRef;
                RemoveOnSessionEnd = removeOnSessionEnd;
            }
        }
        MetaDictionary<ClientSlot, PlayerAssociatedEntityRecord> _associatedEntities = new();

        /// <summary>
        /// The associations that have RemoveOnSessionEnd that cleared in the latest SessionStart that
        /// ended in a refusal.
        /// <para>
        /// Normally <see cref="_associatedEntities"/> contains the currently active association, and looking here
        /// allows one to quickly see if a given association is stale for the purposes of handling association rejection.
        /// But in the case a session start is refused, all <c>RemoveOnSessionEnd</c> associations are dropped and
        /// these entities are no longer available. No matter if a session start succeeded or was refused, Session
        /// may still want to correct out declared associations. So for refusal, the dropped associations are then
        /// placed in this list and used for filtering stale corrections.
        /// </para>
        /// <para>
        /// null if empty.
        /// </para>
        /// </summary>
        List<AssociatedEntityRefBase> _sessionStartRefusalAssociatedEntities;

        /// <summary>
        /// Adds an association with an entity to the session. The associated entity will
        /// be subscribed-to by the session and the state and updates will be delivered to the client.
        /// If there is already a previous association on the <see cref="ClientSlot"/> set by this
        /// entity, the previous association is removed first. It is an error to replace an association
        /// on a slot set by some other Entity.
        /// <para>
        /// There are two ways to use AddEntityAssociation to mark associated entities. First way is:
        /// </para>
        /// <list type="bullet">
        /// <item> Use AddEntityAssociation with <c>removeOnSessionEnd = false</c> in Entity.Initialize() to setup the pre-existing associations, for example if the Player is supposed to be associated with a Chat Group. </item>
        /// <item> Whenever associations are changed by PlayerActor, call AddEntityAssociation and RemoveEntityAssociation with <c>removeOnSessionEnd = false</c>. </item>
        /// <item> Whenever associations are changed by SessionActor, SessionActor should inform PlayerActor to call AddEntityAssociation and RemoveEntityAssociation with <c>informSession = false</c>.
        /// This keeps player's state of the associations in sync with Session's. </item>
        /// </list>
        /// <para>
        /// The second way is:
        /// </para>
        /// <para>
        /// <list type="bullet">
        /// <item> Use AddEntityAssociation with <c>removeOnSessionEnd = true</c> in <see cref="PlayerActorBase{TModel, TPersisted}.OnClientSessionHandshakeAsync(PlayerSessionParamsBase)"/>
        /// to setup the pre-existing associations. </item>
        /// <item> Whenever associations are changed by PlayerActor, call AddEntityAssociation and RemoveEntityAssociation (with <c>removeOnSessionEnd = true</c>). </item>
        /// <item> Whenever associations are changed by SessionActor, PlayerActor doesn't need to do anything. This lets PlayerActors internal state of associations to desync with session,
        /// but this is harmless as the desynced associations are cleared when next session starts. </item>
        /// </list>
        /// </para>
        /// <para>
        /// The strategy chosen can vary from ClientSlot to ClientSlot. If the association is mostly managed by the PlayerActor, the former
        /// strategy is recommended. If the association is mostly managed by the Session, the latter approach is recommended.
        /// </para>
        /// </summary>
        /// <param name="removeOnSessionEnd">
        /// If true, the association is removed on session end automatically. This allows declaring all associations with
        /// <c>AddEntityAssociation(.., removeOnSessionEnd: true)</c> in <see cref="PlayerActorBase{TModel, TPersisted}.OnClientSessionHandshakeAsync(PlayerSessionParamsBase)"/>
        /// as the associations are removed before the next call to <c>OnClientSessionHandshakeAsync</c>.
        /// <br/>
        /// If false, you need to manually <see cref="RemoveEntityAssociation"/> the associations when appropriate.
        /// </param>
        /// <param name="informSession">
        /// If set, the information of the new association will be sent to Session (if any), and session will subscribe to the entity.
        /// This can be set to false if Session is manually made aware of the new association so it can update its book-keeping. For example,
        /// if Session EntityAsks this entity to associate to a new entity, and then uses SubscribeToEntityAsync to do so, this should be
        /// set to false. Otherwise, Session would first SubscribeToEntityAsync and then Re-subscribe when it received the notification.
        /// </param>
        protected void AddEntityAssociation(AssociatedEntityRefBase association, bool removeOnSessionEnd, bool informSession = true)
        {
            // Existing is overwritten.
            _associatedEntities.AddOrReplace(association.GetClientSlot(), new PlayerAssociatedEntityRecord(association, removeOnSessionEnd: removeOnSessionEnd));

            // Tell the session but only if subscription has been completely formed. Specifically
            // this skips the case where this methods is being called from OnNewSubscriber hook.
            EntitySubscriber owner = TryGetOwnerSession();
            if (owner != null && informSession)
                SendMessage(owner, new InternalSessionEntityAssociationUpdate(association.GetClientSlot(), association));
        }

        /// <summary>
        /// Removes association on the given slot from the current session. The associated entity will no longer visible to the
        /// client. It is an error to remove an association set by some other entity.
        /// </summary>
        /// <param name="informSession">
        /// If set, the information of the removed  association will be sent to Session (if any), and session will unsubscribe from the entity.
        /// This can be set to false if Session is manually made aware of the association removal so it can update its book-keeping. For example,
        /// if Session EntityAsks this entity to remove association, and then uses UnsubscribeFromEntityAsync manually, this should be
        /// set to false. Otherwise, Session would first UnsubscribeFromEntityAsync and then receive the unsubscribe-notification from this entity.
        /// </param>
        protected void RemoveEntityAssociation(ClientSlot slot, bool informSession = true)
        {
            // \note: Not a high log level as "unexpected" removals can happen easily since some associations are automatically removed on session end.
            if (!_associatedEntities.Remove(slot))
                _log.Information("Attempted to remove associated entity from slot {Slot}, but there were no entity associated. Ignored.", slot);

            // Tell the session but only if subscription has been completely formed. Specifically
            // this skips the case where this methods is being called from OnSubscriberLost hook
            EntitySubscriber owner = TryGetOwnerSession();
            if (owner != null && informSession)
                PublishMessage(EntityTopic.Owner, new InternalSessionEntityAssociationUpdate(slot, null));
        }

        /// <summary>
        /// Clears associations that should be cleared on session end. For session refusals, the removed associations are kept
        /// in internal list in case an Association is refused. See <see cref="_sessionStartRefusalAssociatedEntities"/> for details.
        /// </summary>
        /// <param name="isSessionStartRefusal">true, if the Session End is due to session start being refused</param>
        void RemoveEntityAssociationsOnSessionEnd(bool isSessionStartRefusal)
        {
            _sessionStartRefusalAssociatedEntities = null;
            if (isSessionStartRefusal)
            {
                foreach (PlayerAssociatedEntityRecord record in _associatedEntities.Values)
                {
                    if (record.RemoveOnSessionEnd)
                    {
                        _sessionStartRefusalAssociatedEntities ??= new List<AssociatedEntityRefBase>();
                        _sessionStartRefusalAssociatedEntities.Add(record.EntityRef);
                    }
                }
            }

            _associatedEntities.RemoveWhere(kv => kv.Value.RemoveOnSessionEnd);
        }

        List<AssociatedEntityRefBase> GetAssociatedEntityEntityRef()
        {
            return _associatedEntities.Select(kv => kv.Value.EntityRef).ToList();
        }

        [EntityAskHandler]
        async Task<EntityAskOk> HandleInternalEntityAssociatedEntityRefusedRequest(EntityId sessionId, InternalEntityAssociatedEntityRefusedRequest request)
        {
            _log.Debug("Handling InternalEntityAssociatedEntityRefusedRequest from {Entity} with {ReasonType}.", request.AssociationRef.AssociatedEntity, request.Refusal.GetType().ToGenericTypeString());

            // Filter out stale requests. Check the currently active associations
            // and the temporary associations that were added in the last session start
            // that we refused.
            bool isValidAssociation = false;
            foreach (PlayerAssociatedEntityRecord record in _associatedEntities.Values)
            {
                if (record.EntityRef.AssociatedEntity != request.AssociationRef.AssociatedEntity)
                    continue;
                // \todo: check MemberInstanceId. If request is less, request was stale. If more, desynced and need to remove. For equal need to remove.
                isValidAssociation = true;
                break;
            }
            if (_sessionStartRefusalAssociatedEntities != null)
            {
                foreach (AssociatedEntityRefBase association in _sessionStartRefusalAssociatedEntities)
                {
                    if (association.AssociatedEntity != request.AssociationRef.AssociatedEntity)
                        continue;
                    isValidAssociation = true;
                    break;
                }
            }

            if (isValidAssociation)
            {
                bool handled = await OnAssociatedEntityRefusalAsync(request.AssociationRef, request.Refusal);
                if (!handled)
                {
                    _log.Warning("Unhandled association refusal from {Entity}. Actor.OnAssociatedEntityRefusalAsync", request.AssociationRef.AssociatedEntity);

                    // If we cannot remove the association, we refuse the call. This prevents futile retries.
                    throw new InvalidEntityAsk($"Entity association refusal was not handled. Entity {_entityId} OnAssociatedEntityRefusalAsync must handle refusal \"{request.Refusal.Message}\" from {request.AssociationRef.AssociatedEntity}.");
                }

                await PersistStateIntermediate();
                return EntityAskOk.Instance;
            }

            _log.Warning("Ignoring stale InternalEntityAssociatedEntityRefusedRequest from {Entity}. There was no such entity associated.", request.AssociationRef.AssociatedEntity);
            return EntityAskOk.Instance;
        }

        /// <inheritdoc cref="Metaplay.Server.MultiplayerEntity.MultiplayerEntityActorBase{TModel, TAction}.OnAssociatedEntityRefusalAsync(EntityId, AssociatedEntityRefBase, InternalEntitySubscribeRefusedBase)"/>
        protected virtual async Task<bool> OnAssociatedEntityRefusalAsync(AssociatedEntityRefBase association, InternalEntitySubscribeRefusedBase refusal)
        {
            #if !METAPLAY_DISABLE_GUILDS
            if (association.GetClientSlot() == ClientSlotCore.Guild)
                return await Guilds.OnAssociatedEntityRefusalAsync(association, refusal);
            #else
            // Silence compiler warning for async method without await
            await Task.CompletedTask;
            #endif

            return false;
        }
    }
}
