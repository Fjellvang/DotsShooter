// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.Json;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Metaplay.Core.TypeCodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using static System.FormattableString;

namespace Metaplay.Core.LiveOpsEvent
{
    public interface ILiveOpsEventValidationLog
    {
        public void Error(string msg, string memberNameOrNull = null);
        public void Warning(string msg, string memberNameOrNull = null);
    }

    [MetaSerializable]
    [MetaReservedMembers(100, 200)]
    public abstract class LiveOpsEventContent : IMetaIntegration<LiveOpsEventContent>
    {
        [IgnoreDataMember]
        public virtual bool AudienceMembershipIsSticky => true;
        public virtual bool ShouldWarnAboutOverlapWith(LiveOpsEventContent otherContent) => false;
        public virtual void Validate(ILiveOpsEventValidationLog log, FullGameConfig activeGameConfig) { }

        public virtual PlayerLiveOpsEventModel CreateModel(PlayerLiveOpsEventInfo info)
        {
            EventTypeStaticInfo eventTypeInfo = LiveOpsEventTypeRegistry.GetEventTypeInfo(GetType());

            if (eventTypeInfo.UniqueModelClassMaybe == null)
            {
                throw new InvalidOperationException(
                    $"The base implementation of {nameof(LiveOpsEventContent)}.{nameof(CreateModel)} cannot be used " +
                    $"because the SDK could not find a unique {nameof(PlayerLiveOpsEventModel)} subclass corresponding to event {GetType()}. " +
                    $"Your {nameof(CreateModel)} override should construct the correct model type instead of calling the base method.");
            }

            PlayerLiveOpsEventModel eventModel = (PlayerLiveOpsEventModel)Activator.CreateInstance(eventTypeInfo.UniqueModelClassMaybe, nonPublic: true);
            eventModel.BaseInitialize(info);
            return eventModel;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class LiveOpsEventAttribute : Attribute, ISerializableTypeCodeProvider
    {
        public          int    TypeCode { get; }
        public readonly string Name;
        public LiveOpsEventAttribute(int typeCode, string name = null)
        {
            TypeCode = typeCode;
            Name     = name;
        }
    }

    public interface ILiveOpsEventTemplate
    {
        LiveOpsEventTemplateId TemplateId         { get; }
        LiveOpsEventContent    ContentBase        { get; }
        string                 DefaultDisplayName => TemplateId.ToString();
        string                 DefaultDescription => string.Empty;
    }

    public interface ILiveOpsEventTemplate<out TContentClass> : ILiveOpsEventTemplate where TContentClass : LiveOpsEventContent
    {
        TContentClass Content { get; }
        LiveOpsEventContent ILiveOpsEventTemplate.ContentBase => Content;
    }

    [MetaSerializable]
    public class LiveOpsEventTemplateConfigData<TContentClass> : IGameConfigData<LiveOpsEventTemplateId>, ILiveOpsEventTemplate<TContentClass> where TContentClass : LiveOpsEventContent
    {
        [MetaMember(1)]
        public LiveOpsEventTemplateId TemplateId { get; protected set; }
        [MetaMember(2)]
        public TContentClass          Content    { get; protected set; }
        [IgnoreDataMember]
        public LiveOpsEventTemplateId ConfigKey => TemplateId;
    }

    public class PlayerLiveOpsEventInfo
    {
        public MetaGuid Id { get; }
        public LiveOpsEventScheduleInfo ScheduleMaybe { get; }
        public LiveOpsEventContent Content { get; }
        public LiveOpsEventPhase Phase { get; }

        public PlayerLiveOpsEventInfo(MetaGuid id, LiveOpsEventScheduleInfo scheduleMaybe, LiveOpsEventContent content, LiveOpsEventPhase phase)
        {
            Id = id;
            ScheduleMaybe = scheduleMaybe;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Phase = phase ?? throw new ArgumentNullException(nameof(phase));
        }
    }

    [MetaSerializable]
    public class LiveOpsEventScheduleInfo
    {
        /// <summary>
        /// Start time of each phase in this schedule. Does not include the phases that this schedule
        /// does not have: for example a schedule might not have a <see cref="LiveOpsEventPhase.Preview"/>
        /// phase at all.
        /// The phases here are ordered from first to last.
        /// This does not include <see cref="LiveOpsEventPhase.NotStartedYet"/> but does include <see cref="LiveOpsEventPhase.Concluded"/>.
        /// </summary>
        [MetaMember(1)] public MetaDictionary<LiveOpsEventPhase, MetaTime> PhaseStartTimes { get; private set; }

        LiveOpsEventScheduleInfo() { }
        public LiveOpsEventScheduleInfo(MetaDictionary<LiveOpsEventPhase, MetaTime> phaseStartTimes)
        {
            PhaseStartTimes = phaseStartTimes ?? throw new ArgumentNullException(nameof(phaseStartTimes));
        }

        public MetaTime GetEnabledStartTime()
        {
            return PhaseStartTimes[LiveOpsEventPhase.NormalActive];
        }

        public MetaTime GetEnabledEndTime()
        {
            if (PhaseStartTimes.TryGetValue(LiveOpsEventPhase.Review, out MetaTime review))
                return review;
            else
                return PhaseStartTimes[LiveOpsEventPhase.Concluded];
        }

        public MetaTime GetConcludedTime()
        {
            return PhaseStartTimes[LiveOpsEventPhase.Concluded];
        }

        public LiveOpsEventPhase GetPhaseAtTime(MetaTime currentTime)
        {
            LiveOpsEventPhase latestStartedPhase = LiveOpsEventPhase.NotStartedYet;
            foreach ((LiveOpsEventPhase phase, MetaTime phaseStartTime) in PhaseStartTimes)
            {
                if (currentTime < phaseStartTime)
                    break;

                latestStartedPhase = phase;
            }

            return latestStartedPhase;
        }
    }

    [MetaSerializable]
    [MetaReservedMembers(100, 200)]
    public abstract class PlayerLiveOpsEventModel : IMetaIntegration<PlayerLiveOpsEventModel>
    {
        [MetaMember(100)] public MetaGuid Id { get; private set; }
        [MetaMember(101)] public LiveOpsEventScheduleInfo ScheduleMaybe { get; set; }
        [MetaMember(102)] LiveOpsEventContent _content;
        [MetaMember(103)] public LiveOpsEventPhase Phase { get; set; }
        [MetaMember(104)] public MetaTime? LatestUnacknowledgedUpdate { get; private set; }
        /// <summary>
        /// Whether the player currently belongs in the event's target audience.
        /// Note that this can only change during the event if <see cref="LiveOpsEventContent.AudienceMembershipIsSticky"/> is false.
        /// If it is true, then this is not updated during the event, even if the player stops being in a targeted player segment.
        /// </summary>
        [MetaMember(105)] public bool PlayerIsInTargetAudience { get; internal set; } = true;

        public         LiveOpsEventContent Content     { get => _content; set { _content = value; } }
        /// <summary>
        /// Can the event model can be removed from the player state? This is only read when event is in phase
        /// Concluded. Allows extending the lifetime of the model for operations on the Concluded event, such as
        /// claiming rewards.
        /// </summary>
        public virtual bool                AllowRemove => true;

        public PlayerLiveOpsEventInfo GetEventInfo() => new PlayerLiveOpsEventInfo(Id, ScheduleMaybe, Content, Phase);

        protected PlayerLiveOpsEventModel() { }
        protected PlayerLiveOpsEventModel(PlayerLiveOpsEventInfo info)
        {
            BaseInitialize(info);
        }

        internal void BaseInitialize(PlayerLiveOpsEventInfo info)
        {
            Id = info.Id;
            ScheduleMaybe = info.ScheduleMaybe;
            Content = info.Content;
            Phase = info.Phase;

            Initialize();
        }

        protected virtual void Initialize() { }

        public void RecordUpdate(IPlayerModelBase player)
        {
            LatestUnacknowledgedUpdate = player.CurrentTime;
            player.ClientListenerCore.GotLiveOpsEventUpdate(this);
        }

        public void AcknowledgeLatestUpdate()
        {
            LatestUnacknowledgedUpdate = null;
        }

        public virtual void OnPhaseChanged(IPlayerModelBase player, LiveOpsEventPhase oldPhase, LiveOpsEventPhase[] fastForwardedPhases, LiveOpsEventPhase newPhase) { }
        public virtual void OnParamsUpdated(IPlayerModelBase player, PlayerLiveOpsEventInfo oldInfo, PlayerLiveOpsEventInfo newInfo) { }
        public virtual void OnLatestUpdateAcknowledged(IPlayerModelBase player) { }
        public virtual void OnAudienceMembershipChanged(IPlayerModelBase player, bool playerIsInTargetAudience) { }
    }

    public abstract class PlayerLiveOpsEventModel<TEventContent> : PlayerLiveOpsEventModel
        where TEventContent : LiveOpsEventContent
    {
        public new TEventContent Content => (TEventContent)base.Content;

        protected PlayerLiveOpsEventModel() { }
        protected PlayerLiveOpsEventModel(PlayerLiveOpsEventInfo info) : base(info) { }
    }

    public abstract class PlayerLiveOpsEventModel<TEventContent, TPlayerModel> : PlayerLiveOpsEventModel<TEventContent>
        where TEventContent : LiveOpsEventContent
        where TPlayerModel : IPlayerModelBase
    {
        protected PlayerLiveOpsEventModel() { }
        protected PlayerLiveOpsEventModel(PlayerLiveOpsEventInfo info) : base(info) { }

        protected virtual void OnPhaseChanged(TPlayerModel player, LiveOpsEventPhase oldPhase, LiveOpsEventPhase[] fastForwardedPhases, LiveOpsEventPhase newPhase) { }
        public override sealed void OnPhaseChanged(IPlayerModelBase player, LiveOpsEventPhase oldPhase, LiveOpsEventPhase[] fastForwardedPhases, LiveOpsEventPhase newPhase)
        {
            OnPhaseChanged((TPlayerModel)player, oldPhase, fastForwardedPhases, newPhase);
        }

        protected virtual void OnLatestUpdateAcknowledged(TPlayerModel player) { }
        public override sealed void OnLatestUpdateAcknowledged(IPlayerModelBase player)
        {
            OnLatestUpdateAcknowledged((TPlayerModel)player);
        }

        protected virtual void OnParamsUpdated(TPlayerModel player, PlayerLiveOpsEventInfo oldInfo, PlayerLiveOpsEventInfo newInfo) { }
        public override sealed void OnParamsUpdated(IPlayerModelBase player, PlayerLiveOpsEventInfo oldInfo, PlayerLiveOpsEventInfo newInfo)
        {
            OnParamsUpdated((TPlayerModel)player, oldInfo, newInfo);
        }

        protected virtual void OnAudienceMembershipChanged(TPlayerModel player, bool playerIsInTargetAudience) { }
        public override sealed void OnAudienceMembershipChanged(IPlayerModelBase player, bool playerIsInTargetAudience)
        {
            OnAudienceMembershipChanged((TPlayerModel)player, playerIsInTargetAudience);
        }
    }

    /// <summary>
    /// Describes the separate phases of an event's lifecycle.
    /// The phases are based on the schedule that is set when creating the event in the LiveOps Dashboard.
    /// An event that does not have a schedule becomes immediately <see cref="NormalActive"/> after being created,
    /// and goes to the <see cref="Concluded"/> phase after being manually concluded via the Dashboard.
    /// Note that an event can be in different phases for different players if the event uses player-local scheduling.
    /// </summary>
    [MetaSerializable]
    public class LiveOpsEventPhase : DynamicEnum<LiveOpsEventPhase>
    {
        /// <summary>
        /// Event has not started yet.
        /// Players cannot join the event yet.
        /// </summary>
        public static readonly LiveOpsEventPhase NotStartedYet  = new LiveOpsEventPhase(1, nameof(NotStartedYet),   indexInPhaseSequence: 0);
        /// <summary>
        /// Event has entered the preview phase.
        /// Players can already join the event in this phase, but typically the gameplay functionality is not enabled yet.
        /// </summary>
        /// <remarks>
        /// This is an optional phase in the event schedule.
        /// </remarks>
        public static readonly LiveOpsEventPhase Preview        = new LiveOpsEventPhase(2, nameof(Preview),         indexInPhaseSequence: 1);
        /// <summary>
        /// Event is active and gameplay functionality is enabled.
        /// </summary>
        public static readonly LiveOpsEventPhase NormalActive   = new LiveOpsEventPhase(3, nameof(NormalActive),    indexInPhaseSequence: 2);
        /// <summary>
        /// Event is still active, but is considered to be 'ending soon'.
        /// You may want to indicate this to the player in the game UI.
        /// </summary>
        /// <remarks>
        /// This is an optional phase in the event schedule.
        /// </remarks>
        public static readonly LiveOpsEventPhase EndingSoon     = new LiveOpsEventPhase(4, nameof(EndingSoon),      indexInPhaseSequence: 3);
        /// <summary>
        /// The event has ended and is in review phase.
        /// Typically the gameplay functionality becomes disabled at this point, but the player can still view the event's results.
        /// </summary>
        /// <remarks>
        /// This is an optional phase in the event schedule.
        /// </remarks>
        public static readonly LiveOpsEventPhase Review         = new LiveOpsEventPhase(5, nameof(Review),          indexInPhaseSequence: 4);
        /// <summary>
        /// The event has ended and exited its review phase (if any).
        /// By default, the event is removed from the player's state when it reaches this phase;
        /// optionally, you can customize this behavior by overriding <see cref="PlayerLiveOpsEventModel.AllowRemove"/>,
        /// for example if you want to retain the event's state until the player has claimed the event's rewards.
        /// </summary>
        public static readonly LiveOpsEventPhase Concluded      = new LiveOpsEventPhase(6, nameof(Concluded),       indexInPhaseSequence: 5);

        int _indexInPhaseSequence;

        public LiveOpsEventPhase(int id, string name, int indexInPhaseSequence)
            : base(id, name, isValid: true)
        {
            _indexInPhaseSequence = indexInPhaseSequence;
        }

        public bool IsActivePhase()
        {
            return PhasePrecedesOrIsEqual(NormalActive, this)
                && PhasePrecedes(this, Review);
        }

        public bool IsEndedPhase()
        {
            return PhasePrecedesOrIsEqual(Review, this);
        }

        /// <summary>
        /// The sequence of all the possible phases, in the order they occur.
        /// This is the "full" sequence, and a specific schedule might contain only
        /// a subset of these phases, e.g. might not have a Preview phase.
        /// </summary>
        static readonly Lazy<LiveOpsEventPhase[]> s_fullPhaseSequence = new Lazy<LiveOpsEventPhase[]>(() =>
        {
            List<LiveOpsEventPhase> allValues = AllValues;

            LiveOpsEventPhase[] sequence = new LiveOpsEventPhase[allValues.Count];
            foreach (LiveOpsEventPhase phase in allValues)
            {
                if (phase._indexInPhaseSequence < 0 || phase._indexInPhaseSequence >= sequence.Length)
                    throw new InvalidOperationException($"{nameof(_indexInPhaseSequence)} out of range for {nameof(LiveOpsEventPhase)} {phase}: {phase._indexInPhaseSequence}, must be in [0 .. {sequence.Length})");

                if (sequence[phase._indexInPhaseSequence] != null)
                    throw new InvalidOperationException($"Duplicate {nameof(_indexInPhaseSequence)} among {nameof(LiveOpsEventPhase)}: {sequence[phase._indexInPhaseSequence]} vs {phase}");

                sequence[phase._indexInPhaseSequence] = phase;
            }

            return sequence;
        });

        public static IEnumerable<LiveOpsEventPhase> GetPhasesBetween(LiveOpsEventPhase startPhaseExclusive, LiveOpsEventPhase endPhaseExclusive)
        {
            bool foundStart = startPhaseExclusive == LiveOpsEventPhase.NotStartedYet;

            foreach (LiveOpsEventPhase phase in s_fullPhaseSequence.Value)
            {
                if (phase == endPhaseExclusive)
                    break;

                if (!foundStart)
                {
                    if (phase == startPhaseExclusive)
                        foundStart = true;

                    continue;
                }

                yield return phase;
            }
        }

        /// <summary>
        /// Whether phase <paramref name="first"/> comes before phase <paramref name="second"/>
        /// in the full phase sequence (<see cref="s_fullPhaseSequence"/>).
        /// </summary>
        public static bool PhasePrecedes(LiveOpsEventPhase first, LiveOpsEventPhase second)
        {
            return first._indexInPhaseSequence < second._indexInPhaseSequence;
        }

        public static bool PhasePrecedesOrIsEqual(LiveOpsEventPhase first, LiveOpsEventPhase second)
        {
            return first == second || PhasePrecedes(first, second);
        }
    }

    [MetaSerializable]
    [MetaBlockedMembers(2)]
    public class PlayerLiveOpsEventsModel
    {
        [MetaMember(1)] public MetaDictionary<MetaGuid, PlayerLiveOpsEventModel> EventModels { get; private set; } = new MetaDictionary<MetaGuid, PlayerLiveOpsEventModel>();
        [MetaMember(3), ServerOnly] public PlayerLiveOpsEventsServerOnlyModel ServerOnly { get; private set; } = new PlayerLiveOpsEventsServerOnlyModel();

        public PlayerLiveOpsEventModel TryGetEarliestUpdate(Func<PlayerLiveOpsEventModel, bool> filter = null)
        {
            PlayerLiveOpsEventModel earliestUpdate = null;
            foreach (PlayerLiveOpsEventModel model in EventModels.Values)
            {
                if (!model.LatestUnacknowledgedUpdate.HasValue)
                    continue;
                if (filter != null && !filter(model))
                    continue;

                if (earliestUpdate == null || model.LatestUnacknowledgedUpdate.Value < earliestUpdate.LatestUnacknowledgedUpdate.Value)
                    earliestUpdate = model;
            }

            return earliestUpdate;
        }

        public PlayerLiveOpsEventModel TryGetEarliestUpdate<TEventContent>()
            where TEventContent : LiveOpsEventContent
        {
            return TryGetEarliestUpdate(update => update.Content is TEventContent);
        }
    }

    [MetaSerializable]
    public class PlayerLiveOpsEventsServerOnlyModel
    {
        [MetaMember(1)] public MetaDictionary<MetaGuid, PlayerLiveOpsEventServerOnlyModel> EventModels { get; private set; } = new MetaDictionary<MetaGuid, PlayerLiveOpsEventServerOnlyModel>();
        [MetaMember(2)] public MetaTime LastRefreshedAt { get; set; } = MetaTime.Epoch;
    }

    [MetaSerializable]
    public class PlayerLiveOpsEventServerOnlyModel
    {
        [MetaMember(1)] public MetaGuid EventId { get; private set; }
        /// <summary>
        /// Player's UTC offset when this event started for the player.
        /// This is relevant for local-time events. We use the same offset for the event
        /// through its lifetime, so that if the player's UTC offset changes,
        /// it won't mess up the local-time scheduling of the event.
        /// </summary>
        [MetaMember(4)] public MetaDuration PlayerUtcOffsetForEvent { get; private set; }
        [MetaMember(2)] public LiveOpsEventPhase LatestAssignedPhase { get; set; }
        [MetaMember(3)] public int EditVersion { get; set; }
        [MetaMember(5)] public bool PlayerIsInTargetAudience { get; set; }

        public bool IsConcludedForPlayer() => LatestAssignedPhase == LiveOpsEventPhase.Concluded;

        PlayerLiveOpsEventServerOnlyModel() { }
        public PlayerLiveOpsEventServerOnlyModel(MetaGuid eventId, MetaDuration playerUtcOffsetForEvent, LiveOpsEventPhase latestAssignedPhase, int editVersion, bool playerIsInTargetAudience)
        {
            EventId = eventId;
            PlayerUtcOffsetForEvent = playerUtcOffsetForEvent;
            LatestAssignedPhase = latestAssignedPhase;
            EditVersion = editVersion;
            PlayerIsInTargetAudience = playerIsInTargetAudience;
        }
    }

    [ModelAction(ActionCodesCore.PlayerAddLiveOpsEvent)]
    public class PlayerAddLiveOpsEvent : PlayerSynchronizedServerActionCore<IPlayerModelBase>
    {
        [MetaMember(1)] public MetaGuid EventId { get; private set; }
        [MetaMember(2)] public LiveOpsEventScheduleInfo ScheduleMaybe { get; private set; }
        [MetaMember(3)] public LiveOpsEventContent Content { get; private set; }
        [MetaMember(4)] public List<LiveOpsEventPhase> FastForwardedPhases { get; private set; }
        [MetaMember(5)] public LiveOpsEventPhase Phase { get; private set; }

        PlayerAddLiveOpsEvent() { }
        public PlayerAddLiveOpsEvent(MetaGuid eventId, LiveOpsEventScheduleInfo scheduleMaybe, LiveOpsEventContent content, List<LiveOpsEventPhase> fastForwardedPhases, LiveOpsEventPhase phase)
        {
            EventId = eventId;
            ScheduleMaybe = scheduleMaybe;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            FastForwardedPhases = fastForwardedPhases ?? throw new ArgumentNullException(nameof(fastForwardedPhases));
            Phase = phase ?? throw new ArgumentNullException(nameof(phase));
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (player.LiveOpsEvents.EventModels.ContainsKey(EventId))
                return MetaActionResult.AlreadyHasEvent;

            if (commit)
            {
                // Add new event (initially in NotStartedYet phase, but will be updated right after)

                PlayerLiveOpsEventInfo eventInfo = new PlayerLiveOpsEventInfo(
                    EventId,
                    scheduleMaybe: MetaSerialization.CloneTagged(ScheduleMaybe, MetaSerializationFlags.IncludeAll, logicVersion: player.LogicVersion, resolver: player.GetDataResolver()),
                    content: MetaSerialization.CloneTagged(Content, MetaSerializationFlags.IncludeAll, logicVersion: player.LogicVersion, resolver: player.GetDataResolver()),
                    phase: LiveOpsEventPhase.NotStartedYet);

                PlayerLiveOpsEventModel eventModel = eventInfo.Content.CreateModel(eventInfo);
                if (!eventModel.Id.IsValid || eventModel.Phase == null || eventModel.Content == null)
                    throw new InvalidOperationException($"Initial base properties of {nameof(PlayerLiveOpsEventModel)} (type {eventModel.GetType()}) remain unassigned after creation! Did you forget to call the constructor which takes the `info` parameter?");

                player.LiveOpsEvents.EventModels.Add(EventId, eventModel);

                // Advance to current phase

                LiveOpsEventPhase[] fastForwardedPhasesCopy = FastForwardedPhases.ToArray();

                LiveOpsEventPhase oldPhase = eventModel.Phase;
                eventModel.Phase = Phase;

                eventModel.OnPhaseChanged(
                    player,
                    oldPhase: oldPhase,
                    fastForwardedPhases: fastForwardedPhasesCopy,
                    newPhase: Phase);
                eventModel.RecordUpdate(player);
            }

            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerRunLiveOpsPhaseSequence)]
    public class PlayerRunLiveOpsPhaseSequence : PlayerSynchronizedServerActionCore<IPlayerModelBase>
    {
        [MetaMember(1)] public MetaGuid EventId { get; private set; }
        [MetaMember(2)] public List<LiveOpsEventPhase> FastForwardedPhases { get; private set; }
        [MetaMember(3)] public LiveOpsEventPhase NewPhase { get; private set; }

        PlayerRunLiveOpsPhaseSequence() { }
        public PlayerRunLiveOpsPhaseSequence(MetaGuid eventId, List<LiveOpsEventPhase> fastForwardedPhases, LiveOpsEventPhase newPhase)
        {
            EventId = eventId;
            FastForwardedPhases = fastForwardedPhases ?? throw new ArgumentNullException(nameof(fastForwardedPhases));
            NewPhase = newPhase ?? throw new ArgumentNullException(nameof(newPhase));
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (!player.LiveOpsEvents.EventModels.ContainsKey(EventId))
                return MetaActionResult.NoSuchEvent;

            if (commit)
            {
                PlayerLiveOpsEventModel eventModel = player.LiveOpsEvents.EventModels[EventId];
                LiveOpsEventPhase[] fastForwardedPhasesCopy = FastForwardedPhases.ToArray();

                LiveOpsEventPhase oldPhase = eventModel.Phase;
                eventModel.Phase = NewPhase;

                eventModel.OnPhaseChanged(
                    player,
                    oldPhase: oldPhase,
                    fastForwardedPhases: fastForwardedPhasesCopy,
                    newPhase: NewPhase);
                eventModel.RecordUpdate(player);

                // If event became concluded, try to remove it.
                if (eventModel.Phase == LiveOpsEventPhase.Concluded && eventModel.AllowRemove)
                    player.LiveOpsEvents.EventModels.Remove(EventId);
            }

            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerUpdateEventLiveOpsEventParams)]
    public class PlayerUpdateEventLiveOpsEventParams : PlayerSynchronizedServerActionCore<IPlayerModelBase>
    {
        [MetaMember(1)] public MetaGuid EventId { get; private set; }
        [MetaMember(2)] public LiveOpsEventScheduleInfo ScheduleMaybe { get; private set; }
        [MetaMember(3)] public LiveOpsEventContent Content { get; private set; }

        PlayerUpdateEventLiveOpsEventParams() { }
        public PlayerUpdateEventLiveOpsEventParams(MetaGuid eventId, LiveOpsEventScheduleInfo scheduleMaybe, LiveOpsEventContent content)
        {
            EventId = eventId;
            ScheduleMaybe = scheduleMaybe;
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (!player.LiveOpsEvents.EventModels.ContainsKey(EventId))
                return MetaActionResult.NoSuchEvent;

            if (commit)
            {
                PlayerLiveOpsEventModel eventModel = player.LiveOpsEvents.EventModels[EventId];

                PlayerLiveOpsEventInfo oldInfo = eventModel.GetEventInfo();
                eventModel.ScheduleMaybe = MetaSerialization.CloneTagged(ScheduleMaybe, MetaSerializationFlags.IncludeAll, logicVersion: player.LogicVersion, resolver: player.GetDataResolver());
                eventModel.Content = MetaSerialization.CloneTagged(Content, MetaSerializationFlags.IncludeAll, logicVersion: player.LogicVersion, resolver: player.GetDataResolver());
                PlayerLiveOpsEventInfo newInfo = eventModel.GetEventInfo();

                eventModel.OnParamsUpdated(player, oldInfo: oldInfo, newInfo: newInfo);
                eventModel.RecordUpdate(player);
            }

            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerSetLiveOpsEventAudienceMembershipFlag)]
    public class PlayerSetLiveOpsEventAudienceMembershipFlag : PlayerSynchronizedServerActionCore<IPlayerModelBase>
    {
        [MetaMember(1)] public MetaGuid EventId { get; private set; }
        [MetaMember(2)] public bool PlayerIsInTargetAudience { get; private set; }

        PlayerSetLiveOpsEventAudienceMembershipFlag() { }
        public PlayerSetLiveOpsEventAudienceMembershipFlag(MetaGuid eventId, bool playerIsInTargetAudience)
        {
            EventId = eventId;
            PlayerIsInTargetAudience = playerIsInTargetAudience;
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (!player.LiveOpsEvents.EventModels.ContainsKey(EventId))
                return MetaActionResult.NoSuchEvent;

            if (commit)
            {
                PlayerLiveOpsEventModel eventModel = player.LiveOpsEvents.EventModels[EventId];

                eventModel.PlayerIsInTargetAudience = PlayerIsInTargetAudience;

                eventModel.OnAudienceMembershipChanged(player, PlayerIsInTargetAudience);
                eventModel.RecordUpdate(player);
            }

            return MetaActionResult.Success;
        }
    }

    [ModelAction(ActionCodesCore.PlayerClearLiveOpsEventUpdates)]
    public class PlayerClearLiveOpsEventUpdates : PlayerActionCore<IPlayerModelBase>
    {
        [MetaMember(1)] public List<MetaGuid> UpdatesToClear { get; private set; }

        PlayerClearLiveOpsEventUpdates() { }
        public PlayerClearLiveOpsEventUpdates(List<MetaGuid> updatesToClear)
        {
            UpdatesToClear = updatesToClear ?? throw new ArgumentNullException(nameof(updatesToClear));
        }

        public override MetaActionResult Execute(IPlayerModelBase player, bool commit)
        {
            if (commit)
            {
                foreach (MetaGuid eventId in UpdatesToClear)
                {
                    if (player.LiveOpsEvents.EventModels.TryGetValue(eventId, out PlayerLiveOpsEventModel model))
                    {
                        model.AcknowledgeLatestUpdate();
                        model.OnLatestUpdateAcknowledged(player);

                        // Try to remove concluded model
                        if (model.Phase == LiveOpsEventPhase.Concluded && model.AllowRemove)
                            player.LiveOpsEvents.EventModels.Remove(eventId);
                    }
                }
            }

            return MetaActionResult.Success;
        }
    }

    public struct EventTypeStaticInfo
    {
        public Type                         ContentClass { get; }
        public string                       EventTypeName { get; }
        public Func<FullGameConfig, object> ConfigTemplateLibraryGetter { get; }
        public Type                         UniqueModelClassMaybe { get; }

        public EventTypeStaticInfo(Type contentClass, string eventTypeName, Func<FullGameConfig, object> configTemplateLibraryGetterMaybe, Type uniqueModelClassMaybe)
        {
            ContentClass = contentClass ?? throw new ArgumentNullException(nameof(contentClass));
            EventTypeName = eventTypeName ?? throw new ArgumentNullException(nameof(eventTypeName));
            ConfigTemplateLibraryGetter = configTemplateLibraryGetterMaybe;
            UniqueModelClassMaybe = uniqueModelClassMaybe;
        }
    }

    public class LiveOpsEventTypeRegistry
    {
        readonly MetaDictionary<Type, EventTypeStaticInfo> _types;

        static EventTypeStaticInfo ResolveStaticInfo(Type contentType, List<Type> modelTypes, Func<FullGameConfig, object> templateAccessorMaybe)
        {
            LiveOpsEventAttribute eventAttr = contentType.GetCustomAttribute<LiveOpsEventAttribute>();

            MethodInfo createModelMethod = contentType.GetMethod(nameof(LiveOpsEventContent.CreateModel), new Type[] { typeof(PlayerLiveOpsEventInfo) });
            bool userOverridesCreateModelMethod = createModelMethod.GetBaseDefinition().DeclaringType == typeof(LiveOpsEventContent)
                                                  && createModelMethod.DeclaringType.Namespace != typeof(LiveOpsEventContent).Namespace; // \note Checking namespace, not just type, to allow intermediate base classes in SDK side

            Type uniqueModelClassMaybe = modelTypes.Count == 1 ? modelTypes.Single() : null;

            // If user overrides CreateModel, then we always use that override to create the event model.
            // If the user doesn't override it, then there's additional requirements regarding the model class:
            // there must be a unique known model class for the this content type (matched based on the content type parameter to PlayerLiveOpsEventModel<>),
            // and that model type must have a parameterless constructor.
            if (!userOverridesCreateModelMethod)
            {
                if (modelTypes.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The SDK cannot find a {nameof(PlayerLiveOpsEventModel)}-derived class corresponding to {contentType}. If it doesn't exist, you should define it.\n" +
                        $" - It should derive from the generic {nameof(PlayerLiveOpsEventModel)}<{contentType.Name}> class, so that the SDK knows it corresponds to this event type.\n" +
                        $" - Alternatively, {contentType.Name} must override the method {nameof(LiveOpsEventContent.CreateModel)} to create the model of the correct type.");
                }
                else if (modelTypes.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"The SDK found multiple {nameof(PlayerLiveOpsEventModel)} classes corresponding to {contentType}: {string.Join(", ", modelTypes)}.\n" +
                        $" - If it is intentional that {contentType.Name} has multiple model classes, it must override the method {nameof(LiveOpsEventContent.CreateModel)} to create the model of the correct type.\n" +
                        $" - Alternatively, adjust the type arguments for the base classes of these model classes, so that there's only 1 model for the event type {contentType.Name}.");
                }

                ConstructorInfo parameterlessConstructor = uniqueModelClassMaybe.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null);
                if (parameterlessConstructor == null)
                {
                    throw new InvalidOperationException(
                        $"LiveOps Event model class {uniqueModelClassMaybe} must have a parameterless constructor.\n" +
                        $" - Note that you can override the Initialize method, which gets called right after the base members have been assigned.\n" +
                        $" - Alternatively, if you don't want a parameterless constructor, {contentType} must override the method {nameof(LiveOpsEventContent.CreateModel)} to construct the model appropriately.");
                }
            }

            return new EventTypeStaticInfo(
                contentClass:                     contentType,
                eventTypeName:                    eventAttr?.Name ?? contentType.Name,
                configTemplateLibraryGetterMaybe: templateAccessorMaybe,
                uniqueModelClassMaybe:            uniqueModelClassMaybe);
        }

        IEnumerable<(Type, Func<FullGameConfig, object>)> EnumerateTemplateAccessors(Type configType, Func<FullGameConfig, IGameConfig> configTypeAccessor)
        {
            foreach (MemberInfo configEntryMember in GameConfigRepository.Instance.GetGameConfigTypeInfo(configType).Entries.Values.Select(entry => entry.MemberInfo))
            {
                Type configEntryType = configEntryMember.GetDataMemberType();
                if (!configEntryType.IsGameConfigLibrary())
                    continue;
                Type configDataType = configEntryType.GenericTypeArguments[1];
                if (configDataType.ImplementsGenericInterface(typeof(ILiveOpsEventTemplate<>)))
                {
                    Type                 eventTypeForConfigEntry = configDataType.GetGenericInterfaceTypeArguments(typeof(ILiveOpsEventTemplate<>))[0];
                    Func<object, object> libraryGetter           = configEntryMember.GetDataMemberGetValueOnDeclaringType();
                    yield return (eventTypeForConfigEntry, x => libraryGetter(configTypeAccessor(x)));
                }
            }
        }

        public LiveOpsEventTypeRegistry()
        {
            Dictionary<Type, Func<FullGameConfig, object>> templateAccessors =
                EnumerateTemplateAccessors(GameConfigRepository.Instance.ServerGameConfigType, x => x.ServerConfig)
                    .Concat(EnumerateTemplateAccessors(GameConfigRepository.Instance.SharedGameConfigType, x => x.SharedConfig))
                    .ToDictionary(x => x.Item1, x => x.Item2);

            // Create mapping from each LiveOpsEventContent type to its corresponding known PlayerLiveOpsEventModel types,
            // based on the event content type parameter given in PlayerLiveOpsEventModel<>.
            Dictionary<Type, List<Type>> modelTypesForContentType =
                IntegrationRegistry.GetIntegrationClasses(typeof(PlayerLiveOpsEventModel))
                .Where(type => type.HasGenericAncestor(typeof(PlayerLiveOpsEventModel<>)))
                .GroupBy(type => type.GetGenericAncestorTypeArguments(typeof(PlayerLiveOpsEventModel<>))[0])
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            _types = IntegrationRegistry.GetIntegrationClasses(typeof(LiveOpsEventContent))
                .ToMetaDictionary(
                    contentType => contentType,
                    contentType => ResolveStaticInfo(
                        contentType,
                        modelTypesForContentType.TryGetValue(contentType, out List<Type> modelTypes) ? modelTypes : new List<Type>(),
                        templateAccessors.TryGetValue(contentType, out Func<FullGameConfig, object> accessor) ? accessor : null));
        }

        static LiveOpsEventTypeRegistry _instance => MetaplayServices.Get<LiveOpsEventTypeRegistry>();
        public static IEnumerable<EventTypeStaticInfo> EventTypes => _instance._types.Values;
        public static EventTypeStaticInfo GetEventTypeInfo(Type contentClass) => _instance._types[contentClass];
    }

    #region Id types that should fundamentally only really be needed on the server, but are currently in shared code if only because ServerGameConfig is.

    [MetaSerializable]
    public class LiveOpsEventTemplateId : StringId<LiveOpsEventTemplateId> { }

    #endregion
}
