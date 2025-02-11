// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Metaplay.Server.LiveOpsEvent;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server
{
    public abstract partial class PlayerActorBase<TModel, TPersisted>
    {
        protected AtomicValueSubscriber<ActiveLiveOpsEventSet> _activeLiveOpsEventStateSubscriber = LiveOpsTimelineProxyActor.ActiveLiveOpsEventState.Subscribe();

        void RefreshLiveOpsEvents()
        {
            _activeLiveOpsEventStateSubscriber.Update();

            ActiveLiveOpsEventSet activeLiveOpsEvents = _activeLiveOpsEventStateSubscriber.Current;

            MetaTime currentTime = MetaTime.Now;

            // Here we handle ongoing and concluded events separately:
            // - Refresh globally ongoing events, because the player might join them,
            //   or if already participating then they might get updated.
            // - Refresh concluded events the player is already in, to conclude/remove them for the player.
            // This could be refactored more so that we don't run the same RefreshLiveOpsEvent in
            // both cases: for each case, there are parts of RefreshLiveOpsEvent we know will never run,
            // so it could be clearer to separate them at a higher level.

            // Join new or update existing ongoing events.
            foreach (LiveOpsEventOccurrence liveOpsEvent in activeLiveOpsEvents.OngoingEvents.Values)
                RefreshLiveOpsEvent(liveOpsEvent, currentTime);

            // Refresh concluded events that the player is already in.
            // We don't care about concluded events the player isn't already in.
            // \note We don't call RefreshLiveOpsEvent while iterating ServerOnly.EventModels,
            //       because calling it can mutate the collection. So we do it afterwards.
            List<LiveOpsEventOccurrence> concludedEventsToRefresh = null;
            foreach (MetaGuid eventId in Model.LiveOpsEvents.ServerOnly.EventModels.Keys)
            {
                if (activeLiveOpsEvents.ConcludedEvents.TryGetValue(eventId, out LiveOpsEventOccurrence liveOpsEvent))
                    (concludedEventsToRefresh ??= new()).Add(liveOpsEvent);
            }
            if (concludedEventsToRefresh != null)
            {
                foreach (LiveOpsEventOccurrence liveOpsEvent in concludedEventsToRefresh)
                    RefreshLiveOpsEvent(liveOpsEvent, currentTime);
            }

            Model.LiveOpsEvents.ServerOnly.LastRefreshedAt = currentTime;
        }

        void RefreshLiveOpsEvent(LiveOpsEventOccurrence liveOpsEvent, MetaTime currentTime)
        {
            MetaGuid eventId = liveOpsEvent.OccurrenceId;

            PlayerLiveOpsEventServerOnlyModel serverState = Model.LiveOpsEvents.ServerOnly.EventModels.GetValueOrDefault(eventId);

            // Add or update the event state, if certain checks pass.
            // The checks and updates we need to do here depend on whether the player already has the event
            // (i.e. whether serverState is null), so clearest to branch based on that here.

            if (serverState == null)
            {
                // Player doesn't yet have the event.

                // Don't add the event if it's already concluded globally.
                if (liveOpsEvent.TimeState.IsGloballyConcluded())
                    return;

                // Resolve the phase the event should be in for this player.
                LiveOpsEventPhase currentPhase = LiveOpsEventServerUtil.GetOccurrencePhase(liveOpsEvent, currentTime, Model.TimeZoneInfo.CurrentUtcOffset);

                // If event is not in a visible phase, don't add it for the player.
                // \note Defensive: the Concluded check should be unnecessary here since an earlier IsGloballyConcluded() already took care of it.
                //       The NotStartedYet check, however, is necessary here, as for local-time schedules it's possible the event started somewhere
                //       but not for this player's local time.
                if (currentPhase == LiveOpsEventPhase.NotStartedYet
                 || currentPhase == LiveOpsEventPhase.Concluded)
                {
                    return;
                }

                // If player is not in target audience, don't add the event.
                if (!Model.PassesFilter(liveOpsEvent.EventParams.PlayerFilter, out bool _))
                    return;

                if (!MetaSerializationUtil.IsInstanceCompatibleWithLogicVersion(liveOpsEvent, ClientLogicVersion, out int minimumLogicVersion))
                {
                    _log.Info("Could not enroll player {entityId} into event {eventId} as the player has an incompatible logic version ({clientLogicVersion}), {minimumLogicVersion} is required at minimum", _entityId, eventId, ClientLogicVersion, minimumLogicVersion);
                    return;
                }

                MetaDuration                   utcOffset              = Model.TimeZoneInfo.CurrentUtcOffset;
                LiveOpsEventScheduleInfo       scheduleForPlayerMaybe = liveOpsEvent.UtcScheduleOccasionMaybe?.UtcOccasionToScheduleInfoForPlayer(liveOpsEvent.ScheduleTimeMode, utcOffset);
                IEnumerable<LiveOpsEventPhase> fastForwardedPhases    = liveOpsEvent.TimeState.GetPhasesBetween(startPhaseExclusive: LiveOpsEventPhase.NotStartedYet, endPhaseExclusive: currentPhase);

                // Create the event info and EventModel here for validation purposes
                // This is not 100% waterproof but catches the normal use-cases.
                PlayerLiveOpsEventInfo eventInfo = new PlayerLiveOpsEventInfo(
                    eventId,
                    scheduleMaybe: MetaSerialization.CloneTagged(scheduleForPlayerMaybe, MetaSerializationFlags.IncludeAll, logicVersion: ClientLogicVersion, resolver: _specializedGameConfigResolver),
                    content: MetaSerialization.CloneTagged(liveOpsEvent.EventParams.Content, MetaSerializationFlags.IncludeAll, logicVersion: ClientLogicVersion, resolver: _specializedGameConfigResolver),
                    phase: LiveOpsEventPhase.NotStartedYet);

                PlayerLiveOpsEventModel eventModel = eventInfo.Content.CreateModel(eventInfo);

                if (!MetaSerializationUtil.IsInstanceCompatibleWithLogicVersion(eventModel, ClientLogicVersion, out minimumLogicVersion))
                {
                    _log.Info("Could not enroll player {entityId} into event {eventId} as the eventModel is not compatible with the player model, the player's logic version is {clientLogicVersion}, however, {minimumLogicVersion} is required at minimum", _entityId, eventId, ClientLogicVersion, minimumLogicVersion);
                    return;
                }

                // Checks passed. Will add the event to the player and fast-forward it to the current phase.

                // \todo #liveops-event User-definable hook controlling if starting the event is allowed.

                // Add the event for the player and advance it to currentPhase.
                serverState = new PlayerLiveOpsEventServerOnlyModel(
                    eventId: eventId,
                    playerUtcOffsetForEvent: utcOffset,
                    latestAssignedPhase: currentPhase,
                    editVersion: liveOpsEvent.EditVersion,
                    playerIsInTargetAudience: true);
                Model.LiveOpsEvents.ServerOnly.EventModels.Add(eventId, serverState);
                EnqueueServerAction(new PlayerAddLiveOpsEvent(eventId, scheduleForPlayerMaybe, liveOpsEvent.EventParams.Content, fastForwardedPhases.ToList(), currentPhase));

                Model.EventStream.Event(new PlayerEventLiveOpsEventAdded(eventId, liveOpsEvent.EventParams.DisplayName, liveOpsEvent.EditVersion, utcOffset, liveOpsEvent.ScheduleTimeMode, scheduleForPlayerMaybe, fastForwardedPhases.ToList(), currentPhase));

                RunAfterNextPersist(() => StatsCollectorProxy.IncreaseLiveOpsEventParticipantCount(eventId));
            }
            else
            {
                // Player already has the event.

                // If the event has already concluded for this player, do nothing,
                // except possibly remove it from this player's state if it has finally also concluded globally.
                //
                // Note that if the event has concluded globally but not yet for this player, we won't enter this branch,
                // but this will be the final update for the event for this player: it will become concluded below,
                // due to reaching the final phase.
                if (serverState.IsConcludedForPlayer())
                {
                    if (liveOpsEvent.TimeState.IsGloballyConcluded())
                        Model.LiveOpsEvents.ServerOnly.EventModels.Remove(eventId);
                    return;
                }

                // \note Since the player already has the event, we're using the per-event fixed serverState.PlayerUtcOffsetForEvent
                //       instead of Model.TimeZoneInfo.CurrentUtcOffset to avoid trouble if the player's utc offset changes during the event.
                MetaDuration utcOffset = serverState.PlayerUtcOffsetForEvent;

                // Unless audience membership is configured to be "sticky", inform shared code about audience membership changes.
                if (!liveOpsEvent.EventParams.Content.AudienceMembershipIsSticky)
                {
                    bool playerIsInTargetAudience = Model.PassesFilter(liveOpsEvent.EventParams.PlayerFilter, out bool _);

                    if (serverState.PlayerIsInTargetAudience != playerIsInTargetAudience)
                    {
                        serverState.PlayerIsInTargetAudience = playerIsInTargetAudience;
                        EnqueueServerAction(new PlayerSetLiveOpsEventAudienceMembershipFlag(eventId, playerIsInTargetAudience));
                        Model.EventStream.Event(new PlayerEventLiveOpsEventAudienceMembershipChanged(eventId, liveOpsEvent.EventParams.DisplayName, playerIsInTargetAudience));
                    }
                }

                // Inform shared code about event parameter changes.
                if (serverState.EditVersion != liveOpsEvent.EditVersion)
                {
                    serverState.EditVersion = liveOpsEvent.EditVersion;
                    LiveOpsEventScheduleInfo scheduleForPlayerMaybe = liveOpsEvent.UtcScheduleOccasionMaybe?.UtcOccasionToScheduleInfoForPlayer(liveOpsEvent.ScheduleTimeMode, utcOffset);
                    EnqueueServerAction(new PlayerUpdateEventLiveOpsEventParams(eventId, scheduleForPlayerMaybe, liveOpsEvent.EventParams.Content));
                    Model.EventStream.Event(new PlayerEventLiveOpsEventParamsChanged(eventId, liveOpsEvent.EventParams.DisplayName, liveOpsEvent.EditVersion, utcOffset, liveOpsEvent.ScheduleTimeMode, scheduleForPlayerMaybe));
                }

                // Resolve the phase the event should be in for this player.
                LiveOpsEventPhase currentPhase = LiveOpsEventServerUtil.GetOccurrencePhase(liveOpsEvent, currentTime, utcOffset);

                // If phase has not changed since last update, do nothing.
                if (currentPhase == serverState.LatestAssignedPhase)
                    return;

                // If the current scheduled phase *precedes* the phase that was previously known to the player, do nothing.
                // This ensures the phases only ever advance forwards.
                // \note This should only be possible if currentTime (coming from MetaTime.Now) went backwards compared to a previous update.
                if (LiveOpsEventPhase.PhasePrecedes(currentPhase, serverState.LatestAssignedPhase))
                    return;

                // Phase has gone forward. Update player state.

                // Advance the event to currentPhase.
                IEnumerable<LiveOpsEventPhase> fastForwardedPhases = liveOpsEvent.TimeState.GetPhasesBetween(startPhaseExclusive: serverState.LatestAssignedPhase, endPhaseExclusive: currentPhase);
                serverState.LatestAssignedPhase = currentPhase;
                EnqueueServerAction(new PlayerRunLiveOpsPhaseSequence(eventId, fastForwardedPhases.ToList(), currentPhase));
                Model.EventStream.Event(new PlayerEventLiveOpsEventPhaseChanged(eventId, liveOpsEvent.EventParams.DisplayName, fastForwardedPhases.ToList(), currentPhase));

                // If the event has concluded globally (reached the final phase, Concluded, everywhere),
                // remove the server-side record of it.
                // \note If the event concluded for this player, but not yet globally (when it's a local-time event),
                //       we keep it around in concluded state.
                //       This way we prevent a scenario where the system forgets that the player has already had
                //       the event and re-enters it later (which could otherwise happen if the player's UTC offset
                //       changed to a lower value).
                // \note This concerns only the server-side record in Model.LiveOpsEvents.ServerOnly.
                //       The shared-code model in Model.LiveOpsEvents.EventModels can be removed before the server-side
                //       record is removed, and it can also be retained after the server-side record has been removed
                //       (according to user's PlayerLiveOpsEventModel.AllowRemove hook).
                if (serverState.IsConcludedForPlayer())
                {
                    if (liveOpsEvent.TimeState.IsGloballyConcluded())
                        Model.LiveOpsEvents.ServerOnly.EventModels.Remove(eventId);
                }
            }
        }
    }
}
