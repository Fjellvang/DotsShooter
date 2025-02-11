// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Json;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Core.Serialization;
using Metaplay.Server.LiveOpsEvent;
using Metaplay.Server.LiveOpsTimeline;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static Metaplay.Server.AdminApi.Controllers.Exceptions;

namespace Metaplay.Server.AdminApi.Controllers
{
    [LiveOpsEventsEnabledCondition]
    public partial class LiveOpsEventController : GameAdminApiController
    {
        #region Audit log

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsEventCreated)]
        public class LiveOpsEventCreated : LiveOpsEventAuditLogEventPayloadBase
        {
            [MetaMember(1)] MetaGuid _occurrenceId;
            [MetaMember(2)] MetaGuid _specId;
            [MetaMember(3)] LiveOpsEventSettings _eventSettings;

            LiveOpsEventCreated() { }
            public LiveOpsEventCreated(MetaGuid occurrenceId, MetaGuid specId, LiveOpsEventSettings eventSettings)
            {
                _occurrenceId = occurrenceId;
                _specId = specId;
                _eventSettings = eventSettings;
            }

            public override string EventTitle => "Event created";
            public override string EventDescription => "The event was created.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsEventUpdated)]
        public class LiveOpsEventUpdated : LiveOpsEventAuditLogEventPayloadBase
        {
            [MetaMember(1)] MetaGuid _occurrenceId;
            [MetaMember(3)] LiveOpsEventSettings _eventSettings;
            [MetaMember(4)] int _editVersion;

            LiveOpsEventUpdated() { }
            public LiveOpsEventUpdated(MetaGuid occurrenceId, LiveOpsEventSettings eventSettings, int editVersion)
            {
                _occurrenceId = occurrenceId;
                _eventSettings = eventSettings;
                _editVersion = editVersion;
            }

            public override string EventTitle => "Event updated";
            public override string EventDescription => "The event was updated.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsEventExplicitlyConcluded)]
        public class LiveOpsEventExplicitlyConcluded : LiveOpsEventAuditLogEventPayloadBase
        {
            [MetaMember(1)] MetaGuid _occurrenceId;

            LiveOpsEventExplicitlyConcluded() { }
            public LiveOpsEventExplicitlyConcluded(MetaGuid occurrenceId)
            {
                _occurrenceId = occurrenceId;
            }

            public override string EventTitle => "Event explicitly concluded";
            public override string EventDescription => "The event was explicitly concluded via the LiveOps Dashboard.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsEventExported)]
        public class LiveOpsEventExported : LiveOpsEventAuditLogEventPayloadBase
        {
            [MetaMember(1)] MetaGuid _occurrenceId;

            LiveOpsEventExported() { }
            public LiveOpsEventExported(MetaGuid occurrenceId)
            {
                _occurrenceId = occurrenceId;
            }

            public override string EventTitle => "Event exported";
            public override string EventDescription => "The event was exported.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.LiveOpsEventImported)]
        public class LiveOpsEventImported : LiveOpsEventAuditLogEventPayloadBase
        {
            [MetaMember(1)] MetaGuid _occurrenceId;
            [MetaMember(2)] MetaGuid _specId;
            [MetaMember(3)] LiveOpsEventExportImport.EventImportOutcome _outcome;
            [MetaMember(4)] LiveOpsEventSettings _settings;

            LiveOpsEventImported() { }
            public LiveOpsEventImported(MetaGuid occurrenceId, MetaGuid specId, LiveOpsEventExportImport.EventImportOutcome outcome, LiveOpsEventSettings settings)
            {
                _occurrenceId = occurrenceId;
                _specId = specId;
                _outcome = outcome;
                _settings = settings;
            }

            public override string EventTitle => "Event imported";
            public override string EventDescription => "The event was imported from a previously exported package.";
        }

        #endregion

        #region Some common types

        public enum EventPhase
        {
            NotYetStarted,
            InPreview,
            Active,
            EndingSoon,
            InReview,
            Ended,
        }

        static bool IsUpcomingPhase(EventPhase phase)
        {
            return phase == EventPhase.NotYetStarted
                || phase == EventPhase.InPreview;
        }

        [MetaSerializable, MetaAllowNoSerializedMembers]
        public class ScheduleInfo
        {
            public bool IsPlayerLocalTime;
            public MetaCalendarPeriod PreviewDuration;
            /// <summary>
            /// UTC date-time (including the Z at the end).
            /// For example: 2024-04-29T12:34:56.789Z <br/>
            /// When <see cref="IsPlayerLocalTime"/> is <c>false</c>, this is the global date-time when the event starts, regardless of user's local time offset.<br/>
            /// When <see cref="IsPlayerLocalTime"/> is <c>true</c>, this is the global date-time when the event starts specifically for users with +0 UTC offset;
            /// in other words, by ignoring the Z suffix, you get the verbatim date and time when the event starts in each user's local time.<br/>
            /// </summary>
            [JsonProperty("enabledStartTime")]
            public string EnabledStartTimeString;
            public MetaCalendarPeriod EndingSoonDuration;
            /// <summary>
            /// See <see cref="EnabledStartTimeString"/> for format information.
            /// This is like that one, but for the event's end time instead of start.
            /// </summary>
            [JsonProperty("enabledEndTime")]
            public string EnabledEndTimeString;
            public MetaCalendarPeriod ReviewDuration;

            public static bool TryParseDateTime(string str, out DateTime result, out string error)
            {
                try
                {
                    result = ParseDateTime(str);
                    error = null;
                    return true;
                }
                catch (Exception ex)
                {
                    result = default;

                    // \note Produce bespoke error message for FormatException, for two reasons:
                    //       - On some setups (seen on a Mac in 2024-05) DateTime.ParseExact produces a bad error message (note the bogus empty string even if the input wasn't empty):
                    //           Cannot parse start time: String '' was not recognized as a valid DateTime.
                    //       - We can include an example date-time string illustrating the correct format.
                    if (ex is FormatException formatEx)
                    {
                        // \note Ugly string content based logic to distinguish different cases, because DateTime.ParseExact uses FormatException for both of these.
                        if (formatEx.Message.Contains("was not recognized as a valid DateTime"))
                            error = $"Date-time must be formatted like (example) {DateTimeStringExample} (fractional seconds are optional).";
                        else if (formatEx.Message.Contains("is not supported in calendar"))
                            error = $"Date or time values are not valid.";
                        else
                            error = $"Expected a valid date-time formatted like (example) {DateTimeStringExample} (fractional seconds are optional).";
                    }
                    else
                        error = ex.Message;

                    return false;
                }
            }

            public static DateTime ParseDateTime(string str)
            {
                DateTime dateTime = DateTime.ParseExact(str, DateTimeFormatString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (dateTime.Kind != DateTimeKind.Utc)
                    throw new MetaAssertException($"Expected kind {nameof(DateTimeKind)}.{nameof(DateTimeKind.Utc)}, got {dateTime.Kind}");
                dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
                return dateTime;
            }

            public static string DateTimeToString(DateTime dateTime)
            {
                if (dateTime.Kind != DateTimeKind.Unspecified)
                    throw new ArgumentException($"Given {nameof(DateTime)} must have kind {DateTimeKind.Unspecified}, got {dateTime.Kind}", nameof(dateTime));
                return dateTime.ToString(DateTimeFormatString, CultureInfo.InvariantCulture);
            }

            const string DateTimeFormatString = "yyyy-MM-ddTHH\\:mm\\:ss.FFFFFFFZ";
            static readonly string DateTimeStringExample = new DateTime(2024, 5, 15, 12, 34, 56, 789).ToString(DateTimeFormatString, CultureInfo.InvariantCulture);
        }

        #endregion

        #region Get list of events

        [HttpGet("liveOpsEvents")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsView)]
        public async Task<ActionResult<GetEventsListApiResult>> GetLiveOpsEventsList()
        {
            GetLiveOpsEventsResponse events = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsEventsRequest());

            MetaDictionary<MetaGuid, LiveOpsEventSpec> specs = events.Specs.ToMetaDictionary(spec => spec.SpecId);

            List<EventBriefInfo> infos =
                events.Occurrences
                .Select(occurrence => CreateEventBriefInfo(occurrence, specs[occurrence.DefiningSpecId]))
                .ToList();

            return new GetEventsListApiResult
            {
                UpcomingEvents = infos.Where(info => IsUpcomingPhase(info.CurrentPhase)).ToList(),
                OngoingAndPastEvents = infos.Where(info => !IsUpcomingPhase(info.CurrentPhase)).ToList(),
            };
        }

        public class GetEventsListApiResult
        {
            // \todo Error per event, in case an individual event fails to deserialize
            public List<EventBriefInfo> UpcomingEvents;
            public List<EventBriefInfo> OngoingAndPastEvents;
        }

        public class EventBriefInfo
        {
            public required MetaGuid EventId;

            public required MetaTime CreatedAt;
            public required string EventTypeName;
            public required string DisplayName;
            public required string Description;
            // public required int SequenceNumber; \todo #liveops-event Implement
            // public required List<string> Tags; \todo #liveops-event Implement
            public required LiveOpsEventTemplateId TemplateId;
            public required bool UseSchedule;
            public required ScheduleInfo Schedule;
            public required EventPhase CurrentPhase;
            public required EventPhase? NextPhase;
            public required MetaTime? NextPhaseTime;
            public required bool HasEnded;
            public required MetaTime? StartTime;
            public required MetaTime? EndTime;
        }

        public static EventBriefInfo CreateEventBriefInfo(LiveOpsEventOccurrence occurrence, LiveOpsEventSpec spec)
        {
            (LiveOpsEventPhase currentPhase, LiveOpsEventPhase nextPhase, MetaTime? nextPhaseTime) = GetEventCurrentAndNextPhaseInfo(occurrence);

            bool hasEnded = LiveOpsEventPhase.PhasePrecedesOrIsEqual(LiveOpsEventPhase.Review, currentPhase);
            MetaTime? startTime;
            MetaTime? endTime;

            if (occurrence.TimeState.EnteredPhaseNormally(LiveOpsEventPhase.NormalActive))
            {
                startTime = occurrence.TimeState.TryGetNominalPhaseStartTimeInUtc(LiveOpsEventPhase.NormalActive);
                endTime = occurrence.ExplicitlyConcludedAt
                          ?? occurrence.TimeState.TryGetNominalPhaseStartTimeInUtc(LiveOpsEventPhase.Review)
                          ?? occurrence.TimeState.TryGetNominalPhaseStartTimeInUtc(LiveOpsEventPhase.Concluded)
                          ?? occurrence.UtcScheduleOccasionMaybe?.GetEnabledEndTime();
            }
            else if (!hasEnded)
            {
                startTime = occurrence.UtcScheduleOccasionMaybe?.GetEnabledStartTime();
                endTime = occurrence.UtcScheduleOccasionMaybe?.GetEnabledEndTime();
            }
            else
            {
                startTime = null;
                endTime = null;
            }

            return new EventBriefInfo
            {
                EventId = occurrence.OccurrenceId,

                CreatedAt = spec.CreatedAt,
                EventTypeName = LiveOpsEventTypeRegistry.GetEventTypeInfo(occurrence.EventParams.Content.GetType()).EventTypeName,
                DisplayName = occurrence.EventParams.DisplayName,
                Description = occurrence.EventParams.Description,
                // SequenceNumber  = ..., \todo #liveops-event Implement
                // Tags            = ..., \todo #liveops-event Implement
                TemplateId = occurrence.EventParams.TemplateIdMaybe,
                UseSchedule = occurrence.UtcScheduleOccasionMaybe != null,
                Schedule = TryCreateScheduleInfo(spec.Settings.ScheduleMaybe),
                CurrentPhase = TryConvertEventPhase(currentPhase).Value,
                NextPhase = TryConvertEventPhase(nextPhase),
                NextPhaseTime = nextPhaseTime,
                HasEnded = hasEnded,
                StartTime = startTime,
                EndTime = endTime,
            };
        }

        static ScheduleInfo TryCreateScheduleInfo(MetaScheduleBase scheduleBaseMaybe)
        {
            if (scheduleBaseMaybe == null)
                return null;

            MetaRecurringCalendarSchedule schedule = scheduleBaseMaybe as MetaRecurringCalendarSchedule
                                                     ?? throw new MetaAssertException($"Internal error: got schedule of type {scheduleBaseMaybe.GetType()}, expected {nameof(MetaRecurringCalendarSchedule)}");

            return new ScheduleInfo
            {
                IsPlayerLocalTime = schedule.TimeMode == MetaScheduleTimeMode.Local,
                PreviewDuration = schedule.Preview,
                EnabledStartTimeString = ScheduleInfo.DateTimeToString(schedule.Start.ToDateTime()),
                EndingSoonDuration = schedule.EndingSoon,
                EnabledEndTimeString = ScheduleInfo.DateTimeToString(schedule.Duration.AddToDateTime(schedule.Start.ToDateTime())),
                ReviewDuration = schedule.Review,
            };
        }

        /// <summary>
        /// Convert the given duration to a period which only uses constant-duration components.
        /// Days is the biggest possible non-zero unit in the returned period. As a consequence, the number of days may be greater than in any month.
        /// </summary>
        /// <remarks>
        /// Truncates away the sub-second part, as it is currently not supported by <see cref="MetaCalendarPeriod"/>.
        /// </remarks>
        static MetaCalendarPeriod DurationToConstantDurationPeriod(MetaDuration duration)
        {
            TimeSpan timeSpan = duration.ToTimeSpan();
            return new MetaCalendarPeriod
            {
                Years = 0,
                Months = 0,

                Days = timeSpan.Days,
                Hours = timeSpan.Hours,
                Minutes = timeSpan.Minutes,
                Seconds = timeSpan.Seconds,
            };
        }

        static LiveOpsEventPhase GetCurrentPhase(LiveOpsEventOccurrence occurrence)
        {
            // \note The below nontrivial logic is only relevant for local-scheduled events
            //       (for non-scheduled and global-scheduled events, the least and most advanced
            //       phases are equal).
            // For local-time events:
            // - If the event hasn't yet become "active" anywhere, report the most-advanced phase.
            // - If the event is no longer "active" anywhere, report the least-advanced phase.
            // - Otherwise, the event is "active" somewhere, and we report "active".

            LiveOpsEventPhase leastAdvancedPhase = occurrence.TimeState.GetLeastAdvancedPhase();
            LiveOpsEventPhase mostAdvancedPhase = occurrence.TimeState.GetMostAdvancedPhase();

            if (LiveOpsEventPhase.PhasePrecedes(mostAdvancedPhase, LiveOpsEventPhase.NormalActive))
                return mostAdvancedPhase;
            else if (LiveOpsEventPhase.PhasePrecedes(LiveOpsEventPhase.NormalActive, leastAdvancedPhase))
                return leastAdvancedPhase;
            else
                return LiveOpsEventPhase.NormalActive;
        }

        static EventPhase? TryConvertEventPhase(LiveOpsEventPhase phase)
        {
            if (phase == null)
                return null;

            if (phase == LiveOpsEventPhase.NotStartedYet)
                return EventPhase.NotYetStarted;
            else if (phase == LiveOpsEventPhase.Preview)
                return EventPhase.InPreview;
            else if (phase == LiveOpsEventPhase.NormalActive)
                return EventPhase.Active;
            else if (phase == LiveOpsEventPhase.EndingSoon)
                return EventPhase.EndingSoon;
            else if (phase == LiveOpsEventPhase.Review)
                return EventPhase.InReview;
            else if (phase == LiveOpsEventPhase.Concluded)
                return EventPhase.Ended;
            else
                return EventPhase.Ended;
        }

        #endregion

        #region Get event details

        [HttpGet("liveOpsEvent/{liveOpsEventIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsView)]
        public async Task<ActionResult<EventDetailsInfo>> GetLiveOpsEventDetails(string liveOpsEventIdStr)
        {
            MetaGuid occurrenceId = MetaGuid.Parse(liveOpsEventIdStr);

            GetLiveOpsEventResponse eventInfo;

            try
            {
                eventInfo = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsEventRequest(occurrenceId: occurrenceId));
            }
            catch (LiveOpsEventNotFound notFound)
            {
                throw new MetaplayHttpException(HttpStatusCode.NotFound, "Event not found.", notFound.Message);
            }

            LiveOpsEventOccurrence occurrence = eventInfo.Occurrence;
            List<LiveOpsEventOccurrence> relatedOccurrences = eventInfo.RelatedOccurrences;
            MetaDictionary<MetaGuid, LiveOpsEventSpec> specs = eventInfo.Specs.ToMetaDictionary(spec => spec.SpecId);

            (LiveOpsEventPhase currentPhase, LiveOpsEventPhase nextPhase, MetaTime? nextPhaseTime) = GetEventCurrentAndNextPhaseInfo(occurrence);

            StatsCollectorLiveOpsEventStatisticsRequest statisticsRequest = new StatsCollectorLiveOpsEventStatisticsRequest(new OrderedSet<MetaGuid> { occurrenceId });
            StatsCollectorLiveOpsEventStatisticsResponse statisticsResponse = await EntityAskAsync(StatsCollectorManager.EntityId, statisticsRequest);
            LiveOpsEventOccurrenceStatistics liveOpsEventStatistics = statisticsResponse.Statistics.Single().Value;

            return new EventDetailsInfo
            {
                EventId = occurrence.OccurrenceId,
                EventParams = EditableParamsFromOccurrenceAndSpec(occurrence, specs[occurrence.DefiningSpecId]),
                CreatedAt = specs[occurrence.DefiningSpecId].CreatedAt,
                // SequenceNumber = ..., \todo #liveops-event Implement
                // Tags = ..., \todo #liveops-event Implement
                CurrentPhase = TryConvertEventPhase(currentPhase).Value,
                NextPhase = TryConvertEventPhase(nextPhase),
                NextPhaseTime = nextPhaseTime,

                RelatedEvents =
                    relatedOccurrences
                    .Select(relatedOccurrence => CreateEventBriefInfo(relatedOccurrence, specs[relatedOccurrence.DefiningSpecId]))
                    .ToList(),

                ParticipantCount = liveOpsEventStatistics.ParticipantCount,
            };
        }

        static (LiveOpsEventPhase CurrentPhase, LiveOpsEventPhase NextPhaseMaybe, MetaTime? NextPhaseTime) GetEventCurrentAndNextPhaseInfo(LiveOpsEventOccurrence occurrence)
        {
            LiveOpsEventPhase currentPhase = GetCurrentPhase(occurrence);

            LiveOpsEventPhase nextPhaseMaybe;
            MetaTime? nextPhaseTime;
            if (occurrence.UtcScheduleOccasionMaybe != null && occurrence.ScheduleTimeMode == MetaScheduleTimeMode.Utc)
            {
                nextPhaseMaybe = LiveOpsEventServerUtil.TryGetNextPhaseAfterGivenPhase(occurrence.UtcScheduleOccasionMaybe, currentPhase);
                if (nextPhaseMaybe == null)
                    nextPhaseTime = null;
                else
                    nextPhaseTime = occurrence.UtcScheduleOccasionMaybe.PhaseSequence[nextPhaseMaybe];
            }
            else
            {
                nextPhaseMaybe = null;
                nextPhaseTime = null;
            }

            return (CurrentPhase: currentPhase, NextPhaseMaybe: nextPhaseMaybe, NextPhaseTime: nextPhaseTime);
        }

        static EditableEventParams EditableParamsFromOccurrenceAndSpec(LiveOpsEventOccurrence occurrence, LiveOpsEventSpec spec)
        {
            return new EditableEventParams()
            {
                EventType = occurrence.EventParams.Content.GetType(),
                DisplayName = occurrence.EventParams.DisplayName,
                Description = occurrence.EventParams.Description,
                Color = occurrence.EventParams.Color,
                TemplateId = occurrence.EventParams.TemplateIdMaybe?.Value,
                UseSchedule = occurrence.UtcScheduleOccasionMaybe != null,
                Schedule = TryCreateScheduleInfo(spec.Settings.ScheduleMaybe),
                TargetPlayers = occurrence.EventParams.TargetPlayersMaybe,
                TargetCondition = occurrence.EventParams.TargetConditionMaybe,
                ContentJObject = JObject.FromObject(occurrence.EventParams.Content, AdminApiJsonSerialization.Serializer),
            };
        }

        public class EventDetailsInfo
        {
            public required MetaGuid EventId;
            public required EditableEventParams EventParams;

            public required MetaTime CreatedAt; // \todo Separate "originally created at" and "imported at"? List of "updated at" times, or is that a job for the audit log?

            // Sequence number among similar events, starting from 1 (or 0?), so we can say "WeekendHappyHour #1", "WeekendHappyHour #2", etc.
            // This becomes fixed when the event starts, based on how many similar events have happened before it.
            // Before the event has started, this is a "tentative" number and can still change if scheduling is changed.
            // \todo #liveops-event Implement
            //public required int SequenceNumber;
            // Arbitrary user-defined tags for searching etc.
            // \todo #liveops-event Implement
            //public required List<string> Tags;
            public required EventPhase CurrentPhase;
            public required EventPhase? NextPhase;
            public required MetaTime? NextPhaseTime;
            public required List<EventBriefInfo> RelatedEvents;

            // \todo #liveops-event Audience estimate

            public required int ParticipantCount;
        }

        #endregion

        #region Create new event

        [HttpPost("createLiveOpsEvent")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsEdit)]
        public async Task<ActionResult<CreateEventApiResult>> CreateLiveOpsEvent()
        {
            CreateEventApiBody body = await ParseBodyAsync<CreateEventApiBody>();
            EditableEventParams eventParams = body.Parameters;

            LiveOpsEventDiagnostics diagnostics = new();

            ValidateEventParams(eventParams, diagnostics);

            if (diagnostics.HasErrors())
            {
                return new CreateEventApiResult(
                    isValid: false,
                    eventId: null,
                    relatedEvents: null,
                    diagnostics.DiagnosticsPerScope);
            }

            LiveOpsEventSettings eventSettings;
            try
            {
                eventSettings = CreateEventSettings(eventParams);
            }
            catch (Exception ex)
            {
                // \note This is considered an "internal error" because ideally ValidateEventParams should already catch these more cleanly.
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Internal error: {ex}");

                return new CreateEventApiResult(
                    isValid: false,
                    eventId: null,
                    relatedEvents: null,
                    diagnostics.DiagnosticsPerScope);
            }

            CreateLiveOpsEventResponse createEventResponse = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new CreateLiveOpsEventRequest(
                validateOnly: body.ValidateOnly,
                eventSettings));

            if (!body.ValidateOnly && createEventResponse.IsValid)
            {
                await WriteAuditLogEventAsync(new LiveOpsEventAuditLogEventBuilder(
                    occurrenceId: createEventResponse.InitialEventOccurrenceId.Value,
                    new LiveOpsEventCreated(occurrenceId: createEventResponse.InitialEventOccurrenceId.Value, specId: createEventResponse.EventSpecId.Value, eventSettings)));
            }

            diagnostics.AddAll(createEventResponse.Diagnostics);

            MetaDictionary<MetaGuid, LiveOpsEventSpec> specs = createEventResponse.Specs.ToMetaDictionary(spec => spec.SpecId);

            return new CreateEventApiResult(
                isValid: createEventResponse.IsValid,
                eventId: createEventResponse.InitialEventOccurrenceId,
                createEventResponse.RelatedOccurrences.Select(occurrence => CreateEventBriefInfo(occurrence, specs[occurrence.DefiningSpecId])).ToList(),
                diagnostics.DiagnosticsPerScope);
        }

        public class CreateEventApiBody
        {
            [JsonProperty(Required = Required.Always)] public bool ValidateOnly;
            public EditableEventParams Parameters;
        }

        public class CreateEventApiResult
        {
            public bool IsValid;
            public MetaGuid? EventId;
            public List<EventBriefInfo> RelatedEvents;
            public Dictionary<string, List<LiveOpsEventDiagnostic>> Diagnostics;

            public CreateEventApiResult(bool isValid, MetaGuid? eventId, List<EventBriefInfo> relatedEvents, Dictionary<string, List<LiveOpsEventDiagnostic>> diagnostics)
            {
                IsValid = isValid;
                EventId = eventId;
                RelatedEvents = relatedEvents;
                Diagnostics = diagnostics ?? new();
            }
        }

        [MetaSerializable, MetaAllowNoSerializedMembers]
        public struct EditableEventParams
        {
            public string DisplayName;
            public string Description;
            public string Color; // \note For liveops timeline

            public Type EventType;

            public string TemplateId; // \note LiveOpsEventTemplateId, but has a tiny bit of custom parsing (empty string produces null in this case).
            // \note JObject instead of LiveOpsEventContent; we parse it manually to produce nicer error messages.
            [JsonProperty("content")]
            public JObject ContentJObject;
            public bool UseSchedule;
            public ScheduleInfo Schedule;
            public List<EntityId> TargetPlayers;
            public PlayerCondition TargetCondition;

            public LiveOpsEventContent ParseContent() => ContentJObject.ToObject<LiveOpsEventContent>(AdminApiJsonSerialization.Serializer);
        }

        class ContentValidationLog : ILiveOpsEventValidationLog
        {
            LiveOpsEventDiagnostics _diagnostics;
            MetaSerializableType _type;

            public ContentValidationLog(LiveOpsEventDiagnostics diagnostics, MetaSerializableType type)
            {
                _diagnostics = diagnostics;
                _type = type;
            }

            string GetScope(string memberNameMaybe)
            {
                if (memberNameMaybe == null)
                    return LiveOpsEventDiagnosticScope.Content;

                if (_type.Members.Find(x => x.Name == memberNameMaybe) == null)
                    throw new InvalidOperationException($"MetaMember by name {memberNameMaybe} not found in LiveOpsEventContent class {_type.Name}");

                return LiveOpsEventDiagnosticScope.Content + "." + memberNameMaybe;
            }

            public void Error(string msg, string memberNameOrNull)
            {
                _diagnostics.AddError(GetScope(memberNameOrNull), msg);
            }

            public void Warning(string msg, string memberNameOrNull)
            {
                _diagnostics.AddWarning(GetScope(memberNameOrNull), msg);
            }
        }

        static void ValidateEventParams(EditableEventParams eventParams, LiveOpsEventDiagnostics diagnostics)
        {
            // \note EventType checks are just sanity checks, not strictly necessary for the server because the server only cares about Content.

            if (eventParams.EventType == null)
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.EventType, message: "Event type is required.");
            else if (eventParams.ContentJObject == null || eventParams.ContentJObject.Count == 0)
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Content, message: "Event content is required.");
            else
            {
                LiveOpsEventContent content;
                try
                {
                    content = eventParams.ParseContent();
                }
                catch (Exception ex)
                {
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Content, message: $"Cannot parse event content: {ex.Message}");
                    content = null;
                }

                if (content != null)
                {
                    if (content.GetType() != eventParams.EventType)
                        diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Content, message: "Event content's type must match the specified event type.");
                    else
                        content.Validate(new ContentValidationLog(diagnostics, MetaSerializerTypeRegistry.GetTypeSpec(content.GetType())), GlobalStateProxyActor.ActiveGameConfig.Get().BaselineGameConfig);
                }
            }

            // \note Not necessary for server, but we tend to require it for UX.
            if (string.IsNullOrEmpty(eventParams.DisplayName))
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.DisplayName, message: "Event name is required.");

            if (eventParams.UseSchedule)
            {
                if (eventParams.Schedule == null)
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Schedule, message: "Schedule cannot be null.");
                else
                    ValidateSchedule(eventParams.Schedule, diagnostics);
            }

            if (eventParams.EventType == null)
                diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.Content, message: "");

            if (!eventParams.UseSchedule)
                diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.Schedule, message: "The event is not using a schedule.");
        }

        static void ValidateSchedule(ScheduleInfo schedule, LiveOpsEventDiagnostics diagnostics)
        {
            DateTime? enabledStartTimeMaybe;
            if (string.IsNullOrEmpty(schedule.EnabledStartTimeString))
            {
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledStartTime, message: "Start time is required.");
                enabledStartTimeMaybe = null;
            }
            else
            {
                if (ScheduleInfo.TryParseDateTime(schedule.EnabledStartTimeString, out DateTime dateTime, out string error))
                    enabledStartTimeMaybe = dateTime;
                else
                {
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledStartTime, message: error);
                    enabledStartTimeMaybe = null;
                }
            }

            DateTime? enabledEndTimeMaybe;
            if (string.IsNullOrEmpty(schedule.EnabledEndTimeString))
            {
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledEndTime, message: "End time is required.");
                enabledEndTimeMaybe = null;
            }
            else
            {
                if (ScheduleInfo.TryParseDateTime(schedule.EnabledEndTimeString, out DateTime dateTime, out string error))
                    enabledEndTimeMaybe = dateTime;
                else
                {
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledEndTime, message: error);
                    enabledEndTimeMaybe = null;
                }
            }

            // \note Upper vs lower estimates here don't currently matter in practice
            //       because the dashboard does not produce year or month components.
            // \todo If year or month components ever start mattering, we should think
            //       here what is the appropriate check. Should we account for the actual
            //       dates of the schedule, instead of using lower/upper estimates?

            // \todo These checks are not completely equivalent to those that end up happening
            //       in the constructor of MetaRecurringCalendarSchedule, due to the fact that
            //       MetaCalendarDateTime and MetaCalendarPeriod have 1-second precision at the moment.
            //       The rounding/truncation can result in violating schedules (which will cause
            //       a different error later).

            if (enabledStartTimeMaybe.HasValue && enabledEndTimeMaybe.HasValue)
            {
                DateTime enabledStartTime = enabledStartTimeMaybe.Value;
                DateTime enabledEndTime = enabledEndTimeMaybe.Value;

                if (enabledEndTime <= enabledStartTime)
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledEndTime, message: "End time must be after start time.");
                else if (schedule.EndingSoonDuration.RoughLowerEstimatedDuration() > MetaDuration.FromTimeSpan(enabledEndTime - enabledStartTime))
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEndingSoonDuration, message: "Ending-soon duration cannot be longer than enabled duration.");
            }

            if (schedule.PreviewDuration.RoughUpperEstimatedDuration() < MetaDuration.Zero)
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.SchedulePreviewDuration, message: "Preview duration cannot be negative.");

            if (schedule.EndingSoonDuration.RoughUpperEstimatedDuration() < MetaDuration.Zero)
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEndingSoonDuration, message: "Ending-soon duration cannot be negative.");

            if (schedule.ReviewDuration.RoughUpperEstimatedDuration() < MetaDuration.Zero)
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleReviewDuration, message: "Review duration cannot be negative.");
        }

        static LiveOpsEventSettings CreateEventSettings(EditableEventParams eventParams)
        {
            MetaScheduleBase metaSchedule;
            if (eventParams.UseSchedule)
                metaSchedule = CreateMetaSchedule(eventParams.Schedule);
            else
                metaSchedule = null;

            return new LiveOpsEventSettings(
                metaSchedule,
                CreateEventParams(eventParams));
        }

        static MetaScheduleBase CreateMetaSchedule(ScheduleInfo schedule)
        {
            DateTime enabledStartTime = ScheduleInfo.ParseDateTime(schedule.EnabledStartTimeString);
            DateTime enabledEndTime = ScheduleInfo.ParseDateTime(schedule.EnabledEndTimeString);

            return new MetaRecurringCalendarSchedule(
                timeMode: schedule.IsPlayerLocalTime
                          ? MetaScheduleTimeMode.Local
                          : MetaScheduleTimeMode.Utc,
                start: MetaCalendarDateTime.FromDateTime(enabledStartTime),
                duration: DurationToConstantDurationPeriod(MetaDuration.FromTimeSpan(enabledEndTime - enabledStartTime)),
                endingSoon: schedule.EndingSoonDuration,
                preview: schedule.PreviewDuration,
                review: schedule.ReviewDuration,
                recurrence: null,
                numRepeats: null);
        }

        static LiveOpsEventParams CreateEventParams(EditableEventParams eventParams)
        {
            LiveOpsEventTemplateId templateId = eventParams.TemplateId == "" ? null : LiveOpsEventTemplateId.FromString(eventParams.TemplateId);

            LiveOpsEventContent content = eventParams.ParseContent();

            return new LiveOpsEventParams(
                displayName: eventParams.DisplayName,
                description: eventParams.Description,
                color: eventParams.Color,
                eventParams.TargetPlayers,
                eventParams.TargetCondition,
                templateId,
                content);
        }

        #endregion

        #region Update event

        [HttpPost("updateLiveOpsEvent")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsEdit)]
        public async Task<ActionResult<UpdateEventApiResult>> UpdateLiveOpsEvent()
        {
            UpdateEventApiBody body = await ParseBodyAsync<UpdateEventApiBody>();
            EditableEventParams eventParams = body.Parameters;

            GetLiveOpsEventResponse existingEventInfo;

            try
            {
                existingEventInfo = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsEventRequest(occurrenceId: body.OccurrenceId));
            }
            catch (LiveOpsEventNotFound notFound)
            {
                throw new MetaplayHttpException(HttpStatusCode.NotFound, "Event not found.", notFound.Message);
            }

            LiveOpsEventDiagnostics diagnostics = existingEventInfo.UneditableParamsDiagnostics; // \note Below code may mutate this part of the GetLiveOpsEventResponse, but that's harmless

            ValidateEventParams(eventParams, diagnostics);

            if (diagnostics.HasErrors())
            {
                return new UpdateEventApiResult(
                    isValid: false,
                    relatedEvents: null,
                    diagnostics.DiagnosticsPerScope);
            }

            LiveOpsEventSettings eventSettings;
            try
            {
                eventSettings = CreateEventSettings(eventParams);
            }
            catch (Exception ex)
            {
                // \note This is considered an "internal error" because ideally ValidateEventParams should already catch these more cleanly.
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Internal error: {ex}");

                return new UpdateEventApiResult(
                    isValid: false,
                    relatedEvents: null,
                    diagnostics.DiagnosticsPerScope);
            }

            UpdateLiveOpsEventResponse response = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new UpdateLiveOpsEventRequest(
                validateOnly: body.ValidateOnly,
                occurrenceId: body.OccurrenceId,
                eventSettings));

            if (!body.ValidateOnly && response.IsValid)
            {
                await WriteAuditLogEventAsync(new LiveOpsEventAuditLogEventBuilder(
                    occurrenceId: body.OccurrenceId,
                    new LiveOpsEventUpdated(occurrenceId: body.OccurrenceId, eventSettings, editVersion: response.NewEditVersion)));
            }

            diagnostics.AddAll(response.Diagnostics);

            MetaDictionary<MetaGuid, LiveOpsEventSpec> specs = response.Specs.ToMetaDictionary(spec => spec.SpecId);

            return new UpdateEventApiResult(
                isValid: response.IsValid,
                response.RelatedOccurrences.Select(occurrence => CreateEventBriefInfo(occurrence, specs[occurrence.DefiningSpecId])).ToList(),
                diagnostics.DiagnosticsPerScope);
        }

        public class UpdateEventApiBody
        {
            [JsonProperty(Required = Required.Always)] public bool ValidateOnly;
            public MetaGuid OccurrenceId;
            public EditableEventParams Parameters;
        }

        public class UpdateEventApiResult
        {
            public bool IsValid;
            public List<EventBriefInfo> RelatedEvents;
            public Dictionary<string, List<LiveOpsEventDiagnostic>> Diagnostics;

            public UpdateEventApiResult(bool isValid, List<EventBriefInfo> relatedEvents, Dictionary<string, List<LiveOpsEventDiagnostic>> diagnostics)
            {
                IsValid = isValid;
                RelatedEvents = relatedEvents;
                Diagnostics = diagnostics ?? new();
            }
        }

        #endregion

        #region Explicitly conclude an event

        [HttpPost("concludeLiveOpsEvent/{liveOpsEventIdStr}")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsEdit)]
        public async Task ConcludeLiveOpsEvent(string liveOpsEventIdStr)
        {
            MetaGuid occurrenceId = MetaGuid.Parse(liveOpsEventIdStr);

            ConcludeLiveOpsEventResponse response;
            try
            {
                response = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new ConcludeLiveOpsEventRequest(
                    occurrenceId: occurrenceId));
            }
            catch (LiveOpsEventNotFound notFound)
            {
                throw new MetaplayHttpException(HttpStatusCode.NotFound, "Event not found.", notFound.Message);
            }

            if (response.IsSuccess)
            {
                await WriteAuditLogEventAsync(new LiveOpsEventAuditLogEventBuilder(
                    occurrenceId: occurrenceId,
                    new LiveOpsEventExplicitlyConcluded(occurrenceId: occurrenceId)));
            }

            if (!response.IsSuccess)
                throw new MetaplayHttpException(HttpStatusCode.Conflict, "Failed to conclude event.", response.Error);
        }

        #endregion

        #region Get event types

        [HttpGet("liveOpsEventTypes")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsView)]
        public ActionResult GetLiveOpsEventTypes()
        {
            FullGameConfig fullGameConfig = GlobalStateProxyActor.ActiveGameConfig.Get().BaselineGameConfig;
            return Ok(LiveOpsEventTypeRegistry.EventTypes.Select(eventType =>
                new EventTypeInfo
                {
                    ContentClass = eventType.ContentClass,
                    EventTypeName = eventType.EventTypeName,
                    Templates = GetEventTemplatesForEventType(eventType, fullGameConfig).ToDictionary(
                        x => x.TemplateId,
                        x => new EventTemplateInfo() { Content = x.ContentBase, DefaultDisplayName = x.DefaultDisplayName, DefaultDescription = x.DefaultDescription }),
                }));
        }

        public struct EventTemplateInfo
        {
            public LiveOpsEventContent Content;
            public string DefaultDisplayName;
            public string DefaultDescription;
        }

        public struct EventTypeInfo
        {
            public Type ContentClass;
            public string EventTypeName;
            public Dictionary<LiveOpsEventTemplateId, EventTemplateInfo> Templates;
            // public bool   CanBeScheduled;
            // public bool   RequiresTemplate;
        }

        static IEnumerable<ILiveOpsEventTemplate> GetEventTemplatesForEventType(EventTypeStaticInfo eventType, FullGameConfig fullGameConfig)
        {
            if (eventType.ConfigTemplateLibraryGetter == null)
                return Enumerable.Empty<ILiveOpsEventTemplate>();
            IGameConfigLibrary templateLibrary = (IGameConfigLibrary)eventType.ConfigTemplateLibraryGetter(fullGameConfig);
            return templateLibrary.EnumerateAll().Select(x => (ILiveOpsEventTemplate)x.Value);
        }

        #endregion

        #region Export events

        [HttpPost("exportLiveOpsEvents")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsExport)]
        public async Task<ActionResult<ExportEventApiResult>> ExportLiveOpsEvents()
        {
            ExportEventsApiBody body = await ParseBodyAsync<ExportEventsApiBody>();

            // \note eventResults shall line up with body.EventIds.
            //       Here we initialize it with nulls, and later we populate it
            //       with either error messages or info about valid events.
            ExportEventApiResult.EventResult[] eventResults = new ExportEventApiResult.EventResult[body.EventIds.Count];
            List<(MetaGuid OccurrenceId, int IndexInRequest)> validOccurrenceIds = new();

            foreach ((string occurrenceIdStr, int indexInRequest) in body.EventIds.ZipWithIndex())
            {
                if (occurrenceIdStr == null)
                    throw new MetaplayHttpException(HttpStatusCode.BadRequest, "Null event ID.", "The list of events to export cannot contain null IDs.");

                if (!MetaGuid.TryParse(occurrenceIdStr, out MetaGuid occurrenceId, out string _/*error*/))
                {
                    eventResults[indexInRequest] = new ExportEventApiResult.EventResult { EventId = occurrenceIdStr, IsValid = false, Error = "Invalid event ID format." };
                    continue;
                }

                validOccurrenceIds.Add((occurrenceId, indexInRequest));
            }

            ExportLiveOpsEventsResponse exportResponse = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new ExportLiveOpsEventsRequest(occurrenceIds: validOccurrenceIds.Select(id => id.OccurrenceId).ToList()));

            if (exportResponse.EventResults.Count != validOccurrenceIds.Count)
                throw new MetaplayHttpException(HttpStatusCode.InternalServerError, "Event count mismatch.", $"LiveOps Event manager returned {exportResponse.EventResults.Count} event results, expected {validOccurrenceIds.Count}.");

            foreach ((ExportLiveOpsEventsResponse.EventResult eventResult, (MetaGuid occurrenceId, int indexInRequest)) in exportResponse.EventResults.Zip(validOccurrenceIds))
            {
                if (eventResult.OccurrenceId != occurrenceId)
                    throw new MetaplayHttpException(HttpStatusCode.InternalServerError, "Event id mismatch.", $"LiveOps Event manager returned event id {eventResult.OccurrenceId} where {occurrenceId} was expected.");

                eventResults[indexInRequest] = new ExportEventApiResult.EventResult
                {
                    EventId = eventResult.OccurrenceId.ToString(),
                    IsValid = eventResult.IsValid,
                    Error = eventResult.Error,
                    EventInfo = eventResult.OccurrenceMaybe != null && eventResult.SpecMaybe != null
                                ? CreateEventBriefInfo(eventResult.OccurrenceMaybe, eventResult.SpecMaybe)
                                : null,
                };
            }

            bool isValid = eventResults.All(res => res.IsValid);

            // \todo #liveops-event Enabled audit log for exporting - but avoid spamming it when
            //       the export request is done multiple times in the export form. Could have a "preview-only"
            //       flag for the request which doesn't return the actual package, and then only the form's
            //       download button would make a non-preview request, which would then audit log.
#if false
            if (isValid)
            {
                await WriteRelatedAuditLogEventsAsync(
                    eventResults.Select(eventResult =>
                    {
                        MetaGuid occurrenceId = MetaGuid.Parse(eventResult.EventId);
                        AuditLog.EventBuilder builder = new LiveOpsEventAuditLogEventBuilder(
                            occurrenceId: occurrenceId,
                            new LiveOpsEventExported(occurrenceId: occurrenceId));
                        return builder;
                    }).ToList());
            }
#endif

            string packageString = JsonSerialization.SerializeToString(exportResponse.Package, AdminApiJsonSerialization.Serializer);

            return new ExportEventApiResult
            {
                IsValid = isValid,
                EventResults = eventResults,
                Package = packageString,
            };
        }

        public class ExportEventsApiBody
        {
            public List<string> EventIds;
        }

        public class ExportEventApiResult
        {
            public bool IsValid;
            public IEnumerable<EventResult> EventResults;
            /// <summary>
            /// JSON-stringified <see cref="LiveOpsEventExportImport.Package"/>.
            /// </summary>
            public string Package;

            public class EventResult
            {
                public string EventId;
                public bool IsValid;
                public string Error;
                public EventBriefInfo EventInfo;
            }
        }

        #endregion

        #region Import events from an exported package

        [HttpPost("importLiveOpsEvents")]
        [RequirePermission(MetaplayPermissions.ApiLiveOpsEventsImport)]
        public async Task<ActionResult<ImportEventsApiResult>> ImportLiveOpsEvents()
        {
            ImportEventsApiBody body = await ParseBodyAsync<ImportEventsApiBody>();

            LiveOpsEventExportImport.Package package;
            try
            {
                package = JsonSerialization.Deserialize<LiveOpsEventExportImport.Package>(body.Package, AdminApiJsonSerialization.Serializer);
            }
            catch (Exception ex)
            {
                return new ImportEventsApiResult
                {
                    IsValid = false,
                    GeneralDiagnostics = new List<LiveOpsEventDiagnostic>
                    {
                        new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Error, $"Cannot parse package JSON: {ex.Message}"),
                    },
                    EventResults = new List<EventImportResult>(),
                };
            }

            ImportLiveOpsEventsResponse importResponse = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new ImportLiveOpsEventsRequest(
                validateOnly: body.ValidateOnly,
                body.ConflictPolicy,
                package));

            if (!body.ValidateOnly && importResponse.IsValid)
            {
                await WriteRelatedAuditLogEventsAsync(
                    importResponse.EventResults.Select(eventResult =>
                    {
                        AuditLog.EventBuilder builder = new LiveOpsEventAuditLogEventBuilder(
                            occurrenceId: eventResult.OccurrenceId,
                            new LiveOpsEventImported(occurrenceId: eventResult.OccurrenceId, specId: eventResult.SpecId, eventResult.Outcome, eventResult.SettingsMaybe));
                        return builder;
                    }).ToList());
            }

            return new ImportEventsApiResult
            {
                IsValid = importResponse.IsValid,
                GeneralDiagnostics = importResponse.GeneralDiagnostics,
                EventResults =
                    importResponse.EventResults.Select(eventResult =>
                    {
                        EventBriefInfo eventInfo;
                        if (eventResult.OccurrenceMaybe != null && eventResult.SpecMaybe != null)
                            eventInfo = CreateEventBriefInfo(eventResult.OccurrenceMaybe, eventResult.SpecMaybe);
                        else
                            eventInfo = null;

                        return new EventImportResult
                        {
                            IsValid = eventResult.IsValid,
                            Outcome = eventResult.Outcome,
                            Diagnostics = eventResult.Diagnostics.DiagnosticsPerScope,
                            EventId = eventResult.OccurrenceId,
                            EventInfo = eventInfo,
                        };
                    })
                    .ToList(),
            };
        }

        public class ImportEventsApiBody
        {
            [JsonProperty(Required = Required.Always)] public bool ValidateOnly;
            public LiveOpsEventExportImport.ImportConflictPolicy ConflictPolicy = LiveOpsEventExportImport.ImportConflictPolicy.Disallow;
            /// <summary>
            /// Server expects a JSON-stringified <see cref="LiveOpsEventExportImport.Package"/>.
            /// </summary>
            [JsonProperty(Required = Required.Always)] public string Package;
        }

        public class ImportEventsApiResult
        {
            public bool IsValid;
            public List<LiveOpsEventDiagnostic> GeneralDiagnostics;
            public List<EventImportResult> EventResults;
        }

        public class EventImportResult
        {
            public bool IsValid;
            public LiveOpsEventExportImport.EventImportOutcome Outcome;
            public Dictionary<string, List<LiveOpsEventDiagnostic>> Diagnostics;
            public MetaGuid EventId;
            /// <remarks>
            /// If <see cref="IsValid"/> is false, this may or may not be null.
            /// </remarks>
            public EventBriefInfo EventInfo;
        }

        #endregion

        #region Player-specific list of events

        [HttpGet("players/{playerIdStr}/liveOpsEvents")]
        [RequirePermission(MetaplayPermissions.ApiPlayersView)]
        public async Task<ActionResult<GetPlayerEventsApiResult>> GetPlayerLiveOpsEvents(string playerIdStr)
        {
            PlayerDetails playerDetails = await GetPlayerDetailsAsync(playerIdStr);
            IPlayerModelBase player = playerDetails.Model;

            GetLiveOpsEventsResponse events = await EntityAskAsync(LiveOpsTimelineManager.EntityId, new GetLiveOpsEventsRequest());

            MetaTime currentTime = MetaTime.Now;

            List<EventPerPlayerInfo> eventPerPlayerInfos = new();
            foreach (LiveOpsEventOccurrence occurrence in events.Occurrences)
            {
                PlayerLiveOpsEventModel eventModelMaybe = player.LiveOpsEvents.EventModels.GetValueOrDefault(occurrence.OccurrenceId);
                PlayerLiveOpsEventServerOnlyModel serverOnlyEventModelMaybe = player.LiveOpsEvents.ServerOnly.EventModels.GetValueOrDefault(occurrence.OccurrenceId);

                if (eventModelMaybe == null && serverOnlyEventModelMaybe == null && !player.PassesFilter(occurrence.EventParams.PlayerFilter, out bool _))
                {
                    // Skip events the player doesn't have and isn't eligible for.
                    continue;
                }

                LiveOpsEventPhase currentPhase;
                if (eventModelMaybe != null)
                    currentPhase = eventModelMaybe.Phase;
                else if (serverOnlyEventModelMaybe != null && serverOnlyEventModelMaybe.IsConcludedForPlayer())
                    currentPhase = LiveOpsEventPhase.Concluded;
                else if (occurrence.TimeState.IsGloballyConcluded())
                {
                    // Skip events that have ended and aren't present in the player's state (either never were, or no longer are).
                    // There's no interesting info to show since we don't know whether the player participated in the event.
                    continue;
                }
                else if (LiveOpsEventServerUtil.GetOccurrencePhase(occurrence, currentTime, player.TimeZoneInfo.CurrentUtcOffset) == LiveOpsEventPhase.Concluded)
                {
                    // Skip events that have ended according to the player's time offset, even if not globally concluded,
                    // if the event isn't already present in the player's state.
                    // The player won't be allowed to join in Concluded phase.
                    continue;
                }
                else
                    currentPhase = LiveOpsEventPhase.NotStartedYet;

                LiveOpsEventPhase nextPhase;
                MetaTime? nextPhaseTime;
                if (occurrence.UtcScheduleOccasionMaybe == null)
                {
                    nextPhase = null;
                    nextPhaseTime = null;
                }
                else
                {
                    nextPhase = LiveOpsEventServerUtil.TryGetNextPhaseAfterGivenPhase(occurrence.UtcScheduleOccasionMaybe, currentPhase);
                    if (nextPhase == null)
                        nextPhaseTime = null;
                    else
                    {

                        MetaTime nextPhaseTimeForUtc = occurrence.UtcScheduleOccasionMaybe.PhaseSequence[nextPhase];
                        MetaDuration utcOffset = occurrence.ScheduleTimeMode == MetaScheduleTimeMode.Local
                                                 ? serverOnlyEventModelMaybe?.PlayerUtcOffsetForEvent ?? player.TimeZoneInfo.CurrentUtcOffset
                                                 : MetaDuration.Zero;
                        nextPhaseTime = nextPhaseTimeForUtc - utcOffset;
                    }
                }

                eventPerPlayerInfos.Add(
                    new EventPerPlayerInfo
                    {
                        EventId = occurrence.OccurrenceId,

                        EventTypeName = LiveOpsEventTypeRegistry.GetEventTypeInfo((eventModelMaybe?.Content ?? occurrence.EventParams.Content).GetType()).EventTypeName,
                        DisplayName = occurrence.EventParams.DisplayName,
                        Description = occurrence.EventParams.Description,
                        TemplateId = occurrence.EventParams.TemplateIdMaybe,
                        CurrentPhase = TryConvertEventPhase(currentPhase).Value,
                        NextPhase = TryConvertEventPhase(nextPhase),
                        NextPhaseTime = nextPhaseTime,
                    });
            }

            // Report the latest refresh time. But if it's epoch (refresh hasn't been run
            // since the LastRefreshedAt member was added), fall back to last login time.
            MetaTime lastRefreshedAt = player.LiveOpsEvents.ServerOnly.LastRefreshedAt;
            if (lastRefreshedAt == MetaTime.Epoch)
                lastRefreshedAt = player.Stats.LastLoginAt;

            return new GetPlayerEventsApiResult
            {
                LastRefreshedAt = lastRefreshedAt,
                Events = eventPerPlayerInfos,
            };
        }

        public class GetPlayerEventsApiResult
        {
            /// <summary>
            /// When this player's event state was last refreshed.
            /// <para>
            /// Per-player event state is not guaranteed to be up to date
            /// because it is only updated at certain times; e.g. when a
            /// player session starts, and also periodically when the
            /// player actor is awake.
            /// When there is no player session, the event state can fall
            /// unboundedly "out of date"; this happens because when there
            /// is no subscriber to the player actor, the player actor
            /// shuts down fairly soon after being woken up for the admin
            /// api's request, and does not necessarily happen to run the
            /// periodic event update.
            /// This situation should be improved, but until then, this
            /// timestamp can be used to communicate the last refresh time
            /// to the dashboard user.
            /// </para>
            /// </summary>
            public required MetaTime LastRefreshedAt;
            public required List<EventPerPlayerInfo> Events;
        }

        public class EventPerPlayerInfo
        {
            public required MetaGuid EventId;

            public required string EventTypeName;
            public required string DisplayName;
            public required string Description;
            //public required int SequenceNumber; \todo #liveops-event Implement
            //public required List<string> Tags; \todo #liveops-event Implement
            public required LiveOpsEventTemplateId TemplateId;
            public required EventPhase CurrentPhase;
            public required EventPhase? NextPhase;
            public required MetaTime? NextPhaseTime;

            // \todo #liveops-event Show event content and user-defined event state
        }

        #endregion
    }
}
