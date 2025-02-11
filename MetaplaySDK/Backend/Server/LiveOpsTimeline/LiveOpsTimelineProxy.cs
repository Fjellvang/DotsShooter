// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Server.LiveOpsEvent;
using Metaplay.Server.LiveOpsTimeline;
using System;
using System.Threading.Tasks;

namespace Metaplay.Server
{
    public class ActiveLiveOpsEventSet : IAtomicValue<ActiveLiveOpsEventSet>
    {
        public int AtomicValueVersion { get; }
        /// <summary>
        /// Events that have entered preview/active phase in at least one possible time zone (i.e. most-advanced phase is not <see cref="LiveOpsEventPhase.NotStartedYet"/>)
        /// and have not yet concluded in all time zones (i.e. least-advanced phase is not <see cref="LiveOpsEventPhase.Concluded"/>).
        /// Player actors will use this to join new events and to update their existing events.
        /// </summary>
        public MetaDictionary<MetaGuid, LiveOpsEventOccurrence> OngoingEvents { get; }
        /// <summary>
        /// Events that are concluded everywhere in all time zones (i.e. least-advanced phase is <see cref="LiveOpsEventPhase.Concluded"/>).
        /// Player actors will use this to conclude their existing events.
        /// A player will not join a concluded event the player isn't already participating in.
        /// </summary>
        public MetaDictionary<MetaGuid, LiveOpsEventOccurrence> ConcludedEvents { get; }

        public ActiveLiveOpsEventSet(int atomicValueVersion, MetaDictionary<MetaGuid, LiveOpsEventOccurrence> ongoingEvents, MetaDictionary<MetaGuid, LiveOpsEventOccurrence> concludedEvents)
        {
            AtomicValueVersion = atomicValueVersion;
            OngoingEvents = ongoingEvents;
            ConcludedEvents = concludedEvents;
        }

        public bool Equals(ActiveLiveOpsEventSet other)
        {
            if (other is null) return false;

            return AtomicValueVersion == other.AtomicValueVersion;
        }

        public override bool Equals(object obj) => obj is ActiveLiveOpsEventSet other && Equals(other);

        public override int GetHashCode()
        {
            return AtomicValueVersion.GetHashCode();
        }
    }

    [EntityConfig]
    internal sealed class LiveOpsTimelineProxyConfig : EphemeralEntityConfig
    {
        public override EntityKind          EntityKind              => EntityKindCloudCore.LiveOpsTimelineProxy;
        public override Type                EntityActorType         => typeof(LiveOpsTimelineProxyActor);
        public override EntityShardGroup    EntityShardGroup        => EntityShardGroup.ServiceProxies;
        public override NodeSetPlacement    NodeSetPlacement        => NodeSetPlacement.All;
        public override IShardingStrategy   ShardingStrategy        => ShardingStrategies.CreateDynamicService();
        public override TimeSpan            ShardShutdownTimeout    => TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Actor for proxying the <see cref="LiveOpsTimelineManagerState"/> on all nodes in the server cluster. Subscribes
    /// to <see cref="LiveOpsTimelineManager"/> to get the latest state and the following stream of updates.
    /// </summary>
    public class LiveOpsTimelineProxyActor : EphemeralEntityActor
    {
        protected override sealed AutoShutdownPolicy ShutdownPolicy => AutoShutdownPolicy.ShutdownNever();

        EntitySubscription              _subscription;
        LiveOpsTimelineManagerState     _state;

        static int s_runningActiveLiveOpsEventStatesVersion = 1;

        public static AtomicValuePublisher<ActiveLiveOpsEventSet> ActiveLiveOpsEventState = new AtomicValuePublisher<ActiveLiveOpsEventSet>();

        public LiveOpsTimelineProxyActor(EntityId entityId) : base(entityId) { }

        protected override async Task Initialize()
        {
            _log.Info("Subscribing to LiveOpsTimelineManager..");
            await SubscribeToManagerAsync();
            StartPeriodicTimer(TimeSpan.FromSeconds(1), ActorTick.Instance);
        }

        async Task SubscribeToManagerAsync()
        {
            (EntitySubscription subscription, LiveOpsTimelineManagerSubscribeResponse response) = await SubscribeToAsync<LiveOpsTimelineManagerSubscribeResponse>(LiveOpsTimelineManager.EntityId, EntityTopic.Member, new LiveOpsTimelineManagerSubscribeRequest());
            _subscription = subscription;

            LiveOpsTimelineManagerState state = response.State.Deserialize(resolver: null, logicVersion: null);
            UpdateLiveOpsTimelineManagerState(state);
        }

        protected override async Task OnShutdown()
        {
            if (_subscription != null)
            {
                await UnsubscribeFromAsync(_subscription);
                _subscription = null;
            }

            await base.OnShutdown();
        }

        protected override Task OnSubscriptionLostAsync(EntitySubscription subscription)
        {
            _log.Warning("Lost subscription to LiveOpsTimelineManager ({Actor})", subscription.ActorRef);
            _subscription = null;
            return Task.CompletedTask;
        }

        [CommandHandler]
        public async Task HandleActorTick(ActorTick _)
        {
            if (_subscription == null)
            {
                _log.Warning("No subscription to LiveOpsTimelineManager, retrying");
                try
                {
                    await SubscribeToManagerAsync();
                    _log.Info("Re-established subscription to LiveOpsTimelineManager.");
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to re-subscribe to LiveOpsTimelineManager: {Exception}", ex);
                }
            }
        }

        [MessageHandler]
        public void HandleSetLiveOpsEventsMessage(SetLiveOpsEventsMessage message)
        {
            foreach (LiveOpsEventOccurrence occurrence in message.Occurrences)
                _state.LiveOpsEvents.SetOccurrence(occurrence);
            foreach (LiveOpsEventSpec spec in message.Specs)
                _state.LiveOpsEvents.SetSpec(spec);

            UpdateActiveLiveOpsEventStates();
        }

        [MessageHandler]
        public void HandleLiveOpsEventTimeStatesUpdatedMessage(LiveOpsEventTimeStatesUpdatedMessage message)
        {
            foreach (LiveOpsEventTimeStateUpdate update in message.Updates)
            {
                LiveOpsEventOccurrence existingOccurrence = _state.LiveOpsEvents.EventOccurrences[update.OccurrenceId];
                LiveOpsEventOccurrence updatedOccurrence = existingOccurrence.CopyWithTimeState(update.TimeState);
                _state.LiveOpsEvents.SetOccurrence(updatedOccurrence);
            }

            UpdateActiveLiveOpsEventStates();
        }

        void UpdateActiveLiveOpsEventStates()
        {
            ActiveLiveOpsEventState.TryUpdate(new ActiveLiveOpsEventSet(
                ++s_runningActiveLiveOpsEventStatesVersion,
                ongoingEvents: _state.LiveOpsEvents.OngoingOccurrenceIds.ToMetaDictionary(id => id, id => _state.LiveOpsEvents.EventOccurrences[id]),
                concludedEvents: _state.LiveOpsEvents.ConcludedOccurrenceIds.ToMetaDictionary(id => id, id => _state.LiveOpsEvents.EventOccurrences[id])));
        }

        void UpdateLiveOpsTimelineManagerState(LiveOpsTimelineManagerState state)
        {
            _state = state;

            UpdateActiveLiveOpsEventStates();
        }
    }
}
