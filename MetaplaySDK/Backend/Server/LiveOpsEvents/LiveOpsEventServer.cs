// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server.LiveOpsEvent
{
    [MetaSerializable]
    public class LiveOpsEventSettings
    {
        [MetaMember(1)] public MetaScheduleBase   ScheduleMaybe { get; private set; }
        [MetaMember(2)] public LiveOpsEventParams EventParams { get; private set; }

        LiveOpsEventSettings() { }
        public LiveOpsEventSettings(MetaScheduleBase scheduleMaybe, LiveOpsEventParams eventParams)
        {
            ScheduleMaybe = scheduleMaybe;
            EventParams = eventParams ?? throw new ArgumentNullException(nameof(eventParams));
        }
    }

    [MetaSerializable]
    public class LiveOpsEventParams : IPlayerFilter
    {
        [MetaMember(6)] public string DisplayName { get; private set; }
        [MetaMember(7)] public string Description { get; private set; }
        [MetaMember(8)] public string Color { get; private set; }

        [MetaMember(2)] public List<EntityId>  TargetPlayersMaybe { get; private set; }
        [MetaMember(3)] public PlayerCondition TargetConditionMaybe { get; private set; }

        [MetaMember(4)] public LiveOpsEventTemplateId TemplateIdMaybe { get; private set; }
        [MetaMember(5)] public LiveOpsEventContent Content { get; private set; }

        [JsonIgnore] public PlayerFilterCriteria PlayerFilter => new PlayerFilterCriteria(TargetPlayersMaybe, TargetConditionMaybe);

        LiveOpsEventParams() { }
        public LiveOpsEventParams(string displayName, string description, string color, List<EntityId> targetPlayersMaybe, PlayerCondition targetConditionMaybe, LiveOpsEventTemplateId templateIdMaybe, LiveOpsEventContent content)
        {
            DisplayName = displayName;
            Description = description;
            Color = color;
            TargetPlayersMaybe = targetPlayersMaybe;
            TargetConditionMaybe = targetConditionMaybe;
            TemplateIdMaybe = templateIdMaybe;
            Content = content ?? throw new ArgumentNullException(nameof(content));
        }
    }

    [MetaSerializable]
    [MetaBlockedMembers(5)]
    public class LiveOpsEventSpec
    {
        [MetaMember(1)] public MetaGuid SpecId { get; private set; }

        [MetaMember(4)] public int EditVersion { get; private set; }

        [MetaMember(2)] public LiveOpsEventSettings Settings  { get; private set; }
        [MetaMember(3)] public MetaTime             CreatedAt { get; private set; }

        LiveOpsEventSpec() { }

        public LiveOpsEventSpec(MetaGuid specId, int editVersion, LiveOpsEventSettings settings, MetaTime createdAt)
        {
            SpecId = specId;
            EditVersion = editVersion;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            CreatedAt = createdAt;
        }
    }

    [MetaSerializable]
    [MetaBlockedMembers(6)]
    public class LiveOpsEventOccurrence
    {
        [MetaMember(1)] public MetaGuid OccurrenceId { get; private set; }

        // \todo #liveops-event Keep edit version only in LiveOpsEventSpec? Currently in both spec and occurrence.
        // \todo #liveops-event Separate edit version for client-visible parts. For example if only audience targeting
        //       has been edited, then no update needs to be sent to the client. (The audience targeting change
        //       may affect the player, but the effect for the client will be other than an "event params update".)
        [MetaMember(7)] public int EditVersion { get; private set; }

        [MetaMember(2)] public MetaGuid DefiningSpecId { get; private set; }
        /// <summary>
        /// Determines whether the schedule is the same for all players (<see cref="MetaScheduleTimeMode.Utc"/>)
        /// or whether it depends on the player's local time (<see cref="MetaScheduleTimeMode.Local"/>).
        /// See <see cref="UtcScheduleOccasionMaybe"/>.
        /// </summary>
        [MetaMember(3)]
        public MetaScheduleTimeMode ScheduleTimeMode { get; private set; }
        /// <summary>
        /// Optional schedule for the occurrence, in UTC.
        /// <para>
        /// If not null: <br/>
        /// If <see cref="ScheduleTimeMode"/> is <see cref="MetaScheduleTimeMode.Utc"/>, this
        /// is the schedule that is used regardless of player's UTC offset. <br/>
        /// If <see cref="ScheduleTimeMode"/> is <see cref="MetaScheduleTimeMode.Local"/>,
        /// the schedule that is used for the player is acquired by subtracting the player's UTC offset from this.
        /// In other words, this is the schedule that is used when the UTC offset is 0, and
        /// for other offsets, the schedule is adjusted such that the resulting local datetime
        /// is the same as the datetime in UTC when using the unadjusted schedule.
        /// See <see cref="LiveOpsEventScheduleOccasion.UtcOccasionToScheduleInfoForPlayer"/>.
        /// </para>
        /// </summary>
        [MetaMember(4)]
        public LiveOpsEventScheduleOccasion UtcScheduleOccasionMaybe { get; private set; }
        [MetaMember(5)] public LiveOpsEventParams EventParams { get; private set; }
        [MetaMember(9)] public MetaTime? ExplicitlyConcludedAt { get; private set; }

        [MetaMember(8)] public LiveOpsEventOccurrenceTimeState TimeState { get; private set; } = LiveOpsEventOccurrenceTimeState.Initial;

        LiveOpsEventOccurrence() { }

        public LiveOpsEventOccurrence(
            MetaGuid occurrenceId,
            int editVersion,
            MetaGuid definingSpecId,
            MetaScheduleTimeMode scheduleTimeMode,
            LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe,
            LiveOpsEventParams eventParams,
            MetaTime? explicitlyConcludedAt,
            LiveOpsEventOccurrenceTimeState timeState)
        {
            OccurrenceId             = occurrenceId;
            EditVersion              = editVersion;
            DefiningSpecId           = definingSpecId;
            ScheduleTimeMode         = scheduleTimeMode;
            UtcScheduleOccasionMaybe = utcScheduleOccasionMaybe;
            EventParams              = eventParams ?? throw new ArgumentNullException(nameof(eventParams));
            ExplicitlyConcludedAt    = explicitlyConcludedAt;

            if (timeState.IsDefaultValue)
                throw new ArgumentException($"Default-valued {nameof(timeState)} is not allowed (use {nameof(LiveOpsEventOccurrenceTimeState)}.{nameof(LiveOpsEventOccurrenceTimeState.Initial)} instead)", nameof(timeState));

            TimeState = timeState;
        }

        internal LiveOpsEventOccurrence CopyWithTimeState(LiveOpsEventOccurrenceTimeState newTimeState)
        {
            return new LiveOpsEventOccurrence(
                occurrenceId:               this.OccurrenceId,
                editVersion:                this.EditVersion,
                definingSpecId:             this.DefiningSpecId,
                scheduleTimeMode:           this.ScheduleTimeMode,
                utcScheduleOccasionMaybe:   this.UtcScheduleOccasionMaybe,
                eventParams:                this.EventParams,
                explicitlyConcludedAt:      this.ExplicitlyConcludedAt,
                timeState:                  newTimeState);
        }

        internal LiveOpsEventOccurrence CopyWithUpdatedTimeState(MetaTime currentTime)
        {
            LiveOpsEventOccurrenceTimeState newTimeState = LiveOpsEventOccurrenceTimeState.CalculateCurrentTimeState(this, currentTime, out bool _ /*wasChanged*/);
            return CopyWithTimeState(newTimeState);
        }
    }

    /// <summary>
    /// Describes the timing of phases in an event schedule.
    /// </summary>
    /// <remarks>
    /// On the shared-code side, there exists a counterpart type <see cref="LiveOpsEventScheduleInfo"/>.
    /// The two types are similar (and there may be some code duplication) but they are intentionally decoupled.
    /// </remarks>
    [MetaSerializable]
    public class LiveOpsEventScheduleOccasion
    {
        [MetaMember(1)] public MetaDictionary<LiveOpsEventPhase, MetaTime> PhaseSequence { get; private set; }

        LiveOpsEventScheduleOccasion() { }
        public LiveOpsEventScheduleOccasion(MetaDictionary<LiveOpsEventPhase, MetaTime> phaseSequence)
        {
            PhaseSequence = phaseSequence ?? throw new ArgumentNullException(nameof(phaseSequence));
        }

        /// <summary>
        /// Called on an occasion which represents the times of the occasion
        /// when it occurs in an UTC+0 zone, this returns an occasion which represents the times
        /// when it occurs for a player who has UTC offset <paramref name="playerUtcOffset"/>,
        /// with <paramref name="timeMode"/> controlling whether the occasion is the same for
        /// all players (<see cref="MetaScheduleTimeMode.Utc"/>) or depends on the player's
        /// local UTC offset (<see cref="MetaScheduleTimeMode.Local"/>).
        /// <para>
        /// When <paramref name="timeMode"/> is <see cref="MetaScheduleTimeMode.Utc"/>,
        /// the returned times are the same as in this original occasion.
        /// </para>
        /// <para>
        /// When <paramref name="timeMode"/> is <see cref="MetaScheduleTimeMode.Local"/>,
        /// the offset is subtracted from (<em>not</em> added to) the occasion's times
        /// Please see the example below.
        /// </para>
        /// <example>
        /// For example, if <c>utcOccasion.VisibilityStartTime</c> is <c>2024-02-20 12:00:00.000 Z</c>, then
        /// <c>GetUtcOccasionForPlayer(utcOccasion, MetaScheduleTimeMode.Local, MetaDuration.FromHours(2))</c>
        /// returns an occasion whose <c>VisibilityStartTime</c> is <c>2024-02-20 10:00:00.000 Z</c>
        /// (and the other time members are also similarly offset).
        /// This is because the UTC offset is 2 hours, meaning the player's local time is 2 hours
        /// ahead of UTC, meaning the occasion occurs 2 hours earlier than it does in UTC+0 zones.
        /// </example>
        /// </summary>
        /// <remarks>
        /// This returns the shared-code counterpart type <see cref="LiveOpsEventScheduleInfo"/>
        /// as this is intended for producing the schedule info that is sent to the client.
        /// </remarks>
        public LiveOpsEventScheduleInfo UtcOccasionToScheduleInfoForPlayer(MetaScheduleTimeMode timeMode, MetaDuration playerUtcOffset)
        {
            if (timeMode == MetaScheduleTimeMode.Local)
            {
                // Note the negation of the offset.
                return new LiveOpsEventScheduleInfo(CreatePhaseSequenceWithAddedOffset(-playerUtcOffset));
            }
            else
                return new LiveOpsEventScheduleInfo(PhaseSequence);
        }

        MetaDictionary<LiveOpsEventPhase, MetaTime> CreatePhaseSequenceWithAddedOffset(MetaDuration offset)
        {
            MetaDictionary<LiveOpsEventPhase, MetaTime> offsetPhaseSequence = new MetaDictionary<LiveOpsEventPhase, MetaTime>(capacity: PhaseSequence.Count);

            foreach ((LiveOpsEventPhase phase, MetaTime time) in PhaseSequence)
                offsetPhaseSequence.Add(phase, time + offset);

            return offsetPhaseSequence;
        }

        public MetaTime GetEnabledStartTime()
        {
            return PhaseSequence[LiveOpsEventPhase.NormalActive];
        }

        public MetaTime GetEnabledEndTime()
        {
            if (PhaseSequence.TryGetValue(LiveOpsEventPhase.Review, out MetaTime review))
                return review;
            else
                return PhaseSequence[LiveOpsEventPhase.Concluded];
        }

        public LiveOpsEventPhase GetPhaseAtTime(MetaTime currentTime)
        {
            LiveOpsEventPhase latestStartedPhase = LiveOpsEventPhase.NotStartedYet;
            foreach ((LiveOpsEventPhase phase, MetaTime phaseStartTime) in PhaseSequence)
            {
                if (currentTime < phaseStartTime)
                    break;

                latestStartedPhase = phase;
            }

            return latestStartedPhase;
        }

        public LiveOpsEventPhase GetPhaseAtTimeAdjustedForUtcOffset(MetaTime currentTime, MetaScheduleTimeMode timeMode, MetaDuration utcOffset)
        {
            if (timeMode == MetaScheduleTimeMode.Local)
                return GetPhaseAtTime(currentTime + utcOffset);
            else
                return GetPhaseAtTime(currentTime);
        }

        public IEnumerable<LiveOpsEventPhase> GetPhasesBetween(LiveOpsEventPhase startPhaseExclusive, LiveOpsEventPhase endPhaseExclusive)
        {
            return
                LiveOpsEventPhase.GetPhasesBetween(startPhaseExclusive: startPhaseExclusive, endPhaseExclusive: endPhaseExclusive)
                .Where(phase => PhaseSequence.ContainsKey(phase));
        }
    }

    /// <summary>
    /// Tracks the global time-based state of an event occurrence over its life;
    /// in particular, to which phase the event has progressed.
    /// <para>
    /// Among other things, this is used for restricting in what ways an event's
    /// schedule can be edited after it has been created.
    /// </para>
    /// </summary>
    [MetaSerializable]
    public struct LiveOpsEventOccurrenceTimeState
    {
        /// <summary>
        /// This indicates the phases the event has processed to over its life.
        /// Initially, this has 1 entry with phase <see cref="LiveOpsEventPhase.NotStartedYet"/>.
        /// <para>
        /// If the event is local-time, the phases are entered here when they start according to the most-advanced time zone.
        /// </para>
        /// </summary>
        [MetaMember(1)] PhaseRecord[] _enteredPhases;
        /// <summary>
        /// When this <see cref="LiveOpsEventOccurrenceTimeState"/> was last changed.
        /// This is used just for ensuring that evaluation time doesn't go backwards even if clock does.
        /// </summary>
        [MetaMember(2)] MetaTime _lastUpdatedAt;

        public bool IsDefaultValue => _enteredPhases == null;

        public static LiveOpsEventOccurrenceTimeState Initial = new LiveOpsEventOccurrenceTimeState(
            new PhaseRecord[]
            {
                new PhaseRecord(LiveOpsEventPhase.NotStartedYet, enteredAt: MetaTime.Epoch, wasEnteredDueToExplicitConclusion: false, nominalStartTimeInUtc: MetaTime.Epoch, closedAt: null, wasClosedDueToExplicitConclusion: false),
            },
            lastUpdatedAt: MetaTime.Epoch);

        LiveOpsEventOccurrenceTimeState(PhaseRecord[] enteredPhases, MetaTime lastUpdatedAt)
        {
            _enteredPhases = enteredPhases;
            _lastUpdatedAt = lastUpdatedAt;
        }

        [MetaSerializable]
        readonly struct PhaseRecord
        {
            [MetaMember(1)] public readonly LiveOpsEventPhase Phase;
            /// <summary>
            /// The actual time when the event manager recorded this phase.
            /// (Or <see cref="MetaTime.Epoch"/> for <see cref="LiveOpsEventPhase.NotStartedYet"/>.)
            /// </summary>
            [MetaMember(2)] public readonly MetaTime EnteredAt;
            /// <summary>
            /// The defined start time of the phase in UTC+0 time zones, according to the schedule
            /// at the time the event manager recorded this phase.
            /// (Or <see cref="MetaTime.Epoch"/> for <see cref="LiveOpsEventPhase.NotStartedYet"/>.)
            /// </summary>
            [MetaMember(3)] public readonly MetaTime NominalStartTimeInUtc;
            /// <summary>
            /// The actual time when the event manager declared this phase "closed",
            /// meaning that no player should move into this phase in this event anymore.
            /// Basically means the phase has ended in all time-zones.
            /// <para>
            /// This is null while the phase is still open.
            /// For <see cref="LiveOpsEventPhase.Concluded"/>, this is always null.
            /// </para>
            /// </summary>
            [MetaMember(4)] public readonly MetaTime? ClosedAt;
            /// <summary>
            /// Whether this phase was entered because the event was explicitly concluded,
            /// rather than "normally" by reaching the phase's scheduled start time.
            /// </summary>
            [MetaMember(5)] public readonly bool WasEnteredDueToExplicitConclusion;
            /// <summary>
            /// Whether this phase was closed because the event was explicitly concluded,
            /// rather than "normally" by reaching the phase's scheduled end time.
            /// </summary>
            [MetaMember(6)] public readonly bool WasClosedDueToExplicitConclusion;

            [MetaDeserializationConstructor]
            public PhaseRecord(LiveOpsEventPhase phase, MetaTime enteredAt, bool wasEnteredDueToExplicitConclusion, MetaTime nominalStartTimeInUtc, MetaTime? closedAt, bool wasClosedDueToExplicitConclusion)
            {
                Phase = phase;
                EnteredAt = enteredAt;
                WasEnteredDueToExplicitConclusion = wasEnteredDueToExplicitConclusion;
                NominalStartTimeInUtc = nominalStartTimeInUtc;
                ClosedAt = closedAt;
                WasClosedDueToExplicitConclusion = wasClosedDueToExplicitConclusion;
            }
        }

        internal IEnumerable<LiveOpsEventPhase> GetPhasesBetween(LiveOpsEventPhase startPhaseExclusive, LiveOpsEventPhase endPhaseExclusive)
        {
            bool foundStart = false;

            foreach (PhaseRecord phase in _enteredPhases)
            {
                if (phase.Phase == endPhaseExclusive)
                    break;

                if (!foundStart)
                {
                    if (phase.Phase == startPhaseExclusive)
                        foundStart = true;

                    continue;
                }

                yield return phase.Phase;
            }
        }

        internal LiveOpsEventPhase GetPhaseAtTimeWithOffset(MetaTime currentTime, MetaDuration utcOffset)
        {
            // Get the latest phase whose start time has been reached, with the following exceptions:
            //
            // Closed phases are ignored.
            //
            // If there is no non-closed phase with a suitable start time, then the first non-closed phase is returned.
            // This might happen if there is clock disagreement between the event manager (which called CalculateCurrentTimeState
            // which closed the phases it considered to have ended in all zones) and the current context
            // (which might think the closed phase actually hasn't ended yet). In that case we obey the manager
            // which closed the phase.
            // This can also happen if the event was explicitly concluded: the nominal start time of the Concluded phase
            // may not have been reached, but all other phases are closed, Concluded is the only available phase.

            // Find the latest suitable phase.
            LiveOpsEventPhase resultPhase = null;
            foreach (PhaseRecord phaseRecord in _enteredPhases)
            {
                if (phaseRecord.ClosedAt.HasValue)
                    continue;

                // Permit this phase if either it's the first non-closed phase (resultPhase == null)
                // or current time has reached its start time.
                if (resultPhase == null || currentTime >= phaseRecord.NominalStartTimeInUtc - utcOffset)
                    resultPhase = phaseRecord.Phase;
                else
                    break;
            }

            // resultPhase should always be non-null at this point, because at least the last of the phases in _enteredPhases should be non-closed.
            if (resultPhase == null)
                throw new MetaAssertException("Expected there to be at least 1 non-closed phase in event");

            return resultPhase;
        }

        internal static LiveOpsEventOccurrenceTimeState CalculateCurrentTimeState(LiveOpsEventOccurrence occurrence, MetaTime currentTimeParam, out bool wasChanged)
        {
            // If somehow time has gone backwards, clamp at the last update time to avoid problems.
            MetaTime effectiveCurrentTime = MetaTime.Max(currentTimeParam, occurrence.TimeState._lastUpdatedAt);

            // We do two things in this method:
            // - "Enter" new phases when they start (in the most-advanced time zone, for local events).
            // - "Close" those phases when they end (in the least-advanced time zone, for local events).

            PhaseRecord latestPhase = occurrence.TimeState._enteredPhases.Last();

            // Compute the most-advanced and least-advanced phases according to the schedule (if any).

            LiveOpsEventPhase scheduledMostAdvancedPhase = LiveOpsEventServerUtil.GetMostAdvancedPhase(occurrence.ScheduleTimeMode, occurrence.UtcScheduleOccasionMaybe, effectiveCurrentTime);
            if (LiveOpsEventPhase.PhasePrecedesOrIsEqual(scheduledMostAdvancedPhase, latestPhase.Phase))
            {
                // If phase has not changed from the latest recorded phase,
                // or has somehow gone backwards (likely an ill-behaving schedule edit that wasn't caught by validation),
                // clamp at the latest recorded phase.
                scheduledMostAdvancedPhase = latestPhase.Phase;
            }

            LiveOpsEventPhase scheduledLeastAdvancedPhase = LiveOpsEventServerUtil.GetLeastAdvancedPhase(occurrence.ScheduleTimeMode, occurrence.UtcScheduleOccasionMaybe, effectiveCurrentTime);
            if (LiveOpsEventPhase.PhasePrecedes(scheduledMostAdvancedPhase, scheduledLeastAdvancedPhase))
            {
                // If least advanced phase is somehow beyond most advanced, clamp the least advanced.
                scheduledLeastAdvancedPhase = scheduledMostAdvancedPhase;
            }

            // Normally, we use the schedule-based phases.
            // However, if the event has been explicitly concluded, we force the event to the Concluded phase regardless of the schedule.

            LiveOpsEventPhase effectiveLeastAdvancedPhase;
            LiveOpsEventPhase effectiveMostAdvancedPhase;
            if (occurrence.ExplicitlyConcludedAt.HasValue)
            {
                effectiveLeastAdvancedPhase = LiveOpsEventPhase.Concluded;
                effectiveMostAdvancedPhase  = LiveOpsEventPhase.Concluded;
            }
            else
            {
                effectiveLeastAdvancedPhase = scheduledLeastAdvancedPhase;
                effectiveMostAdvancedPhase  = scheduledMostAdvancedPhase;
            }

            // Before doing any List<PhaseRecord> allocations, check if there is anything to update.
            // - Are there new phases to enter?
            //   I.e. has the most-advanced phase changed from the last time?
            // - Are there phases that should be closed?
            //   I.e. there are currently unclosed phases that precede effectiveLeastAdvancedPhase
            //        (including phases that would be newly entered in this update - this is what the
            //        LiveOpsEventPhase.PhasePrecedes(latestPhase.Phase, effectiveLeastAdvancedPhase) check is for).

            bool hasNewPhasesToEnter = effectiveMostAdvancedPhase != latestPhase.Phase;

            bool hasPhasesToClose;
            if (LiveOpsEventPhase.PhasePrecedes(latestPhase.Phase, effectiveLeastAdvancedPhase))
            {
                // If there are phases that precede effectiveLeastAdvancedPhase that haven't yet
                // even been entered, then those phases will necessarily be entered in this update
                // (because mostAdvancedPhase is at least as advanced as effectiveLeastAdvancedPhase).
                // In that case, those phases don't yet exist in _enteredPhases (so we can't
                // do the `else` branch below), but we know that we will close them right away.
                hasPhasesToClose = true;
            }
            else
            {
                // All phases that precede effectiveLeastAdvancedPhase have already been entered.
                // Check if there are any unclosed phases among those.
                hasPhasesToClose = false;
                foreach (PhaseRecord phaseRecord in occurrence.TimeState._enteredPhases)
                {
                    // Stop when effectiveLeastAdvancedPhase is reached (i.e. only check its preceding phases).
                    if (phaseRecord.Phase == effectiveLeastAdvancedPhase)
                        break;

                    if (!phaseRecord.ClosedAt.HasValue)
                    {
                        hasPhasesToClose = true;
                        break;
                    }
                }
            }

            // Stop if there's nothing to update.
            if (!(hasNewPhasesToEnter || hasPhasesToClose))
            {
                wasChanged = false;
                return occurrence.TimeState;
            }

            // New copy of _enteredPhases, will be mutated below.
            List<PhaseRecord> enteredPhases = new(occurrence.TimeState._enteredPhases);

            // Enter any new phases if needed.
            if (hasNewPhasesToEnter)
            {
                if (occurrence.UtcScheduleOccasionMaybe == null)
                {
                    if (effectiveMostAdvancedPhase == LiveOpsEventPhase.Concluded
                        && LiveOpsEventPhase.PhasePrecedes(latestPhase.Phase, LiveOpsEventPhase.NormalActive))
                    {
                        enteredPhases.Add(new PhaseRecord(
                            LiveOpsEventPhase.NormalActive,
                            enteredAt: effectiveCurrentTime,
                            wasEnteredDueToExplicitConclusion: true,
                            nominalStartTimeInUtc: MetaTime.Epoch,
                            closedAt: null,
                            wasClosedDueToExplicitConclusion: false));
                    }

                    enteredPhases.Add(new PhaseRecord(
                        effectiveMostAdvancedPhase,
                        enteredAt: effectiveCurrentTime,
                        wasEnteredDueToExplicitConclusion: effectiveMostAdvancedPhase == LiveOpsEventPhase.Concluded,
                        nominalStartTimeInUtc: MetaTime.Epoch,
                        closedAt: null,
                        wasClosedDueToExplicitConclusion: false));
                }
                else
                {
                    IEnumerable<LiveOpsEventPhase> fastForwardedAndCurrentPhase =
                        occurrence.UtcScheduleOccasionMaybe.GetPhasesBetween(startPhaseExclusive: latestPhase.Phase, endPhaseExclusive: effectiveMostAdvancedPhase)
                        .Append(effectiveMostAdvancedPhase);

                    foreach (LiveOpsEventPhase phase in fastForwardedAndCurrentPhase)
                    {
                        MetaTime phaseStartTime = occurrence.UtcScheduleOccasionMaybe.PhaseSequence[phase];

                        enteredPhases.Add(new PhaseRecord(
                            phase,
                            enteredAt: effectiveCurrentTime,
                            wasEnteredDueToExplicitConclusion: LiveOpsEventPhase.PhasePrecedes(scheduledMostAdvancedPhase, phase),
                            nominalStartTimeInUtc: phaseStartTime,
                            closedAt: null,
                            wasClosedDueToExplicitConclusion: false));
                    }
                }
            }

            // Close any unclosed phases that precede leastAdvancedPhase.
            if (hasPhasesToClose)
            {
                for (int i = 0; i < enteredPhases.Count && enteredPhases[i].Phase != effectiveLeastAdvancedPhase; i++)
                {
                    PhaseRecord phase = enteredPhases[i];

                    if (!phase.ClosedAt.HasValue)
                    {
                        enteredPhases[i] = new PhaseRecord(
                            phase:                              phase.Phase,
                            enteredAt:                          phase.EnteredAt,
                            wasEnteredDueToExplicitConclusion:  phase.WasEnteredDueToExplicitConclusion,
                            nominalStartTimeInUtc:              phase.NominalStartTimeInUtc,
                            closedAt:                           effectiveCurrentTime,
                            wasClosedDueToExplicitConclusion:   LiveOpsEventPhase.PhasePrecedes(scheduledLeastAdvancedPhase, phase.Phase));
                    }
                }
            }

            LiveOpsEventOccurrenceTimeState newTimeState = new LiveOpsEventOccurrenceTimeState(
                enteredPhases.ToArray(),
                lastUpdatedAt: effectiveCurrentTime);

            wasChanged = true;
            return newTimeState;
        }

        internal LiveOpsEventPhase GetMostAdvancedPhase()
        {
            return _enteredPhases.Last().Phase;
        }

        internal LiveOpsEventPhase GetLeastAdvancedPhase()
        {
            // The first non-closed phase is the least-advanced.

            foreach (PhaseRecord phaseRecord in _enteredPhases)
            {
                if (phaseRecord.ClosedAt.HasValue)
                    continue;

                return phaseRecord.Phase;
            }

            // Shouldn't end up here, because at least the last of the phases in _enteredPhases should be non-closed.
            throw new MetaAssertException("Expected there to be at least 1 non-closed phase in event");
        }

        internal bool IsGloballyConcluded()
        {
            return GetLeastAdvancedPhase() == LiveOpsEventPhase.Concluded;
        }

        internal bool IsGloballyUpcoming()
        {
            return GetMostAdvancedPhase() == LiveOpsEventPhase.NotStartedYet;
        }

        internal MetaTime? TryGetNominalPhaseStartTimeInUtc(LiveOpsEventPhase phase)
        {
            foreach (PhaseRecord phaseRecord in _enteredPhases)
            {
                if (phaseRecord.Phase == phase)
                    return phaseRecord.NominalStartTimeInUtc;
            }

            return null;
        }

        internal MetaTime? TryGetPhaseEnteredAtTime(LiveOpsEventPhase phase)
        {
            foreach (PhaseRecord phaseRecord in _enteredPhases)
            {
                if (phaseRecord.Phase == phase)
                    return phaseRecord.EnteredAt;
            }

            return null;
        }

        internal bool EnteredPhaseNormally(LiveOpsEventPhase phase)
        {
            foreach (PhaseRecord phaseRecord in _enteredPhases)
            {
                if (phaseRecord.Phase == phase)
                    return !phaseRecord.WasEnteredDueToExplicitConclusion;
            }

            return false;
        }

        internal LiveOpsEventLifeStage GetLifeStage()
        {
            if (IsGloballyConcluded())
                return LiveOpsEventLifeStage.Concluded;
            else if (IsGloballyUpcoming())
                return LiveOpsEventLifeStage.Upcoming;
            else
                return LiveOpsEventLifeStage.Ongoing;
        }
    }

    internal enum LiveOpsEventLifeStage
    {
        /// <summary>
        /// Event is globally upcoming: hasn't started (or entered preview) in any time zone yet.
        /// I.e. most advanced phase is <see cref="LiveOpsEventPhase.NotStartedYet"/>.
        /// </summary>
        Upcoming,
        /// <summary>
        /// Event is neither globally upcoming nor globally concluded: it has started (or entered preview)
        /// in at least some time zone and also has not concluded in all time zones yet.
        /// I.e. most advanced phase is not <see cref="LiveOpsEventPhase.NotStartedYet"/> and least advanced phase is not <see cref="LiveOpsEventPhase.Concluded"/>.
        /// </summary>
        Ongoing,
        /// <summary>
        /// Event is globally concluded: has concluded in all time zones.
        /// I.e. least advanced phase is <see cref="LiveOpsEventPhase.Concluded"/>.
        /// </summary>
        Concluded,
    }

    internal static class LiveOpsEventServerUtil
    {
        internal static LiveOpsEventPhase TryGetNextPhaseAfterGivenPhase(LiveOpsEventScheduleOccasion occasion, LiveOpsEventPhase currentPhase)
        {
            LiveOpsEventPhase nextPhase;
            if (currentPhase == LiveOpsEventPhase.NotStartedYet)
                nextPhase = occasion.PhaseSequence.First().Key;
            else
            {
                IEnumerator<LiveOpsEventPhase> enumerator = occasion.PhaseSequence.Keys.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current == currentPhase)
                        break;
                }
                bool hasNextPhase = enumerator.MoveNext();
                if (!hasNextPhase)
                    return null;
                nextPhase = enumerator.Current;
            }

            return nextPhase;
        }

        internal static LiveOpsEventPhase GetMostAdvancedPhase(MetaScheduleTimeMode timeMode, LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe, MetaTime evaluationTime)
        {
            return utcScheduleOccasionMaybe?.GetPhaseAtTimeAdjustedForUtcOffset(evaluationTime, timeMode, PlayerTimeZoneInfo.MaximumUtcOffset)
                   ?? LiveOpsEventPhase.NormalActive;
        }

        internal static LiveOpsEventPhase GetLeastAdvancedPhase(MetaScheduleTimeMode timeMode, LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe, MetaTime evaluationTime)
        {
            return utcScheduleOccasionMaybe?.GetPhaseAtTimeAdjustedForUtcOffset(evaluationTime, timeMode, PlayerTimeZoneInfo.MinimumUtcOffset)
                   ?? LiveOpsEventPhase.NormalActive;
        }

        internal static LiveOpsEventPhase GetOccurrencePhase(LiveOpsEventOccurrence liveOpsEvent, MetaTime currentTime, MetaDuration utcOffset)
        {
            // Resolve the phase the event should be in for a player using the given utc offset.
            // - If there is no schedule, use the latest global phase for the event (from liveOpsEvent.TimeState.GetMostAdvancedPhase()).
            // - If there is a global-time ("utc") schedule, also then use the latest global phase for the event.
            // - If there is a local-time schedule, then the phase we should choose depends on the current time and the utc offset.
            //   We use liveOpsEvent.TimeState.GetPhaseAtTimeWithOffset, which returns one of the phases between (inclusive)
            //   the least-advanced and most-advanced phase, appropriate for the current time and the utc offset.
            // Note in particular that we never enter a phase that is not already recorded in liveOpsEvent.TimeState.

            if (liveOpsEvent.UtcScheduleOccasionMaybe == null)
                return liveOpsEvent.TimeState.GetMostAdvancedPhase();
            else if (liveOpsEvent.ScheduleTimeMode == MetaScheduleTimeMode.Utc)
                return liveOpsEvent.TimeState.GetMostAdvancedPhase();
            else
                return liveOpsEvent.TimeState.GetPhaseAtTimeWithOffset(currentTime, utcOffset);
        }

        /// <summary>
        /// Return the time range where the event should be considered visible. This is used
        /// for the liveops timeline visualization, and the specifics are dictated by those needs;
        /// this determines where the event is shown on the timeline.
        /// <para>
        /// Roughly speaking, this should be the time range when the event is visible
        /// (after <see cref="LiveOpsEventPhase.NotStartedYet"/> but before <see cref="LiveOpsEventPhase.Concluded"/>)
        /// at least in some time zones.
        /// </para>
        /// <para>
        /// The end time can be missing in case the event has no schedule and hasn't been
        /// concluded yet.
        /// </para>
        /// </summary>
        internal static (MetaTime StartTime, MetaTime? EndTime) GetVisibleTimeRange(LiveOpsEventOccurrence occurrence, MetaTime currentTime)
        {
            MetaDuration maxUtcOffset = occurrence.ScheduleTimeMode == MetaScheduleTimeMode.Local ? PlayerTimeZoneInfo.MaximumUtcOffset : MetaDuration.Zero;
            MetaDuration minUtcOffset = occurrence.ScheduleTimeMode == MetaScheduleTimeMode.Local ? PlayerTimeZoneInfo.MinimumUtcOffset : MetaDuration.Zero;

            MetaTime startTime;
            MetaTime? endTime;

            if (occurrence.TimeState.GetMostAdvancedPhase() == LiveOpsEventPhase.NotStartedYet)
            {
                // Event is nowhere visible yet, so report its earliest scheduled visibility start time.
                // But if it's either in the past, or there is no schedule, then clamp it to current time.
                // (It's possible for the event to spend a short time in a state where
                // it has not started yet even though it "should have started", because
                // the liveops event manager is the authority authority on when events
                // start and advance, and it can happen that the manager just hasn't
                // updated the event's state just yet.)

                if (occurrence.UtcScheduleOccasionMaybe != null)
                {
                    startTime = occurrence.UtcScheduleOccasionMaybe.PhaseSequence.First().Value - maxUtcOffset;
                    if (startTime < currentTime)
                        startTime = currentTime;
                }
                else
                    startTime = currentTime;
            }
            else
            {
                // Event has become visible, so report the actual time it happened.
                // Note that here we report the actual time (TryGetPhaseEnteredAtTime)
                // rather than the nominal schedule-based visibility start time.
                //
                // Note that this also covers the case where the event didn't actually start normally,
                // but was explicitly concluded before it event started. In that case, the event didn't
                // really *start* at all, but for bookkeeping purposes it still went through the active phase.

                LiveOpsEventPhase firstVisiblePhase = occurrence.UtcScheduleOccasionMaybe?.PhaseSequence.First().Key ?? LiveOpsEventPhase.NormalActive;
                startTime = occurrence.TimeState.TryGetPhaseEnteredAtTime(firstVisiblePhase).Value;
            }

            if (!occurrence.TimeState.GetLeastAdvancedPhase().IsEndedPhase())
            {
                // Event hasn't yet been concluded everywhere, so report its last scheduled visibility end time (if any).
                // Note the `?` - this is null if there's no schedule.
                endTime = occurrence.UtcScheduleOccasionMaybe?.PhaseSequence.Last().Value - minUtcOffset;
            }
            else
            {
                // Event has become concluded everywhere, so report the actual time it happened.
                // Note that here we report the actual time (TryGetPhaseEnteredAtTime)
                // rather than the nominal schedule-based visibility end time.
                //
                // Note that this also the explicit conclusion.

                endTime = occurrence.TimeState.TryGetPhaseEnteredAtTime(occurrence.TimeState.GetLeastAdvancedPhase()).Value;
            }

            return (startTime, endTime);
        }
    }

    public class LiveOpsEventsEnabledCondition : MetaplayFeatureEnabledConditionAttribute
    {
        public override bool IsEnabled => LiveOpsEventTypeRegistry.EventTypes.Any();
    }

    public static class LiveOpsEventExportImport
    {
        [MetaSerializable]
        public class Package
        {
            [JsonProperty(Required = Required.Always)]
            [MetaMember(1)] public int PackageFormatVersion { get; private set; }

            [MetaMember(2)] public List<ExportedEvent> Events { get; private set; }

            Package() { }
            public Package(int packageFormatVersion, List<ExportedEvent> events)
            {
                PackageFormatVersion = packageFormatVersion;
                Events = events;
            }
        }

        [MetaSerializable]
        public class ExportedEvent
        {
            // \todo #liveops-event Figure out: are both occurrences and specs included in export-import? Or just either?
            //       Separate entries for specs and occurrences (when no longer 1-to-1 correspondence).
            [MetaMember(1)] public MetaGuid OccurrenceId { get; private set; }
            [MetaMember(2)] public MetaGuid SpecId { get; private set; }
            /// <summary>
            /// Base64-encoded MetaSerialized <see cref="LiveOpsEventSettings"/>.
            /// </summary>
            [MetaMember(3)] public string SettingsBase64 { get; private set; }

            ExportedEvent() { }
            public ExportedEvent(MetaGuid occurrenceId, MetaGuid specId, string settingsBase64)
            {
                OccurrenceId = occurrenceId;
                SpecId = specId;
                SettingsBase64 = settingsBase64;
            }

            public static ExportedEvent Create(MetaGuid occurrenceId, MetaGuid specId, LiveOpsEventSettings settings)
            {
                return new ExportedEvent(occurrenceId: occurrenceId, specId: specId, settingsBase64: EncodeSettings(settings));
            }

            public static string EncodeSettings(LiveOpsEventSettings settings)
            {
                byte[] settingsSerialized = MetaSerialization.SerializeTagged(settings, SerializationFlags, logicVersion: null);
                string settingsBase64 = Convert.ToBase64String(settingsSerialized);
                return settingsBase64;
            }

            public LiveOpsEventSettings DecodeSettings()
            {
                if (SettingsBase64 == null)
                    throw new InvalidOperationException($"{nameof(SettingsBase64)} cannot be null");

                byte[] settingsSerialized = Convert.FromBase64String(SettingsBase64);
                LiveOpsEventSettings settings = MetaSerialization.DeserializeTagged<LiveOpsEventSettings>(settingsSerialized, SerializationFlags, resolver: null, logicVersion: null);
                return settings;
            }

            const MetaSerializationFlags SerializationFlags = MetaSerializationFlags.Persisted;
        }

        /// <summary>
        /// What to do when trying to import an event with an id that already exists.
        /// </summary>
        [MetaSerializable]
        public enum ImportConflictPolicy
        {
            /// <summary>
            /// Refuse to import if there is an existing event with the same id.
            /// </summary>
            Disallow,
            /// <summary>
            /// The existing event on the server will be overwritten with the imported event.
            /// </summary>
            Overwrite,
            /// <summary>
            /// The existing event on the server will be kept.
            /// </summary>
            KeepOld,
        }

        /// <summary>
        /// Describes what happened (or would happen - depending on the validateOnly flag in the request) as the result of importing a specific event.
        /// The outcome can depend on the <see cref="ImportConflictPolicy"/> given in the import request.
        /// </summary>
        [MetaSerializable]
        public enum EventImportOutcome
        {
            /// <summary>
            /// The event cannot be imported because of an event already exists with the same id.
            /// </summary>
            ConflictError,
            /// <summary>
            /// The event cannot be imported due to an error in the event (other than <see cref="ConflictError"/>), e.g. failure to deserialize the event's payload.
            /// </summary>
            GeneralError,
            /// <summary>
            /// The import will cause a new event to be created in this environment.
            /// </summary>
            CreateNew,
            /// <summary>
            /// The import will cause an existing event to be overwritten in this environment.
            /// </summary>
            OverwriteExisting,
            /// <summary>
            /// The event will not be imported, because an event already exists with the same id.
            /// </summary>
            IgnoreDueToExisting,
        }
    }
}
