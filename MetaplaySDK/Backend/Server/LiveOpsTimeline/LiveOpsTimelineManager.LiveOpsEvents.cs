// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Akka.Actor;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Schedule;
using Metaplay.Core.TypeCodes;
using Metaplay.Server.LiveOpsEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server.LiveOpsTimeline
{
    [MetaSerializable]
    public class LiveOpsEventsGlobalState
    {
        public LiveOpsEventsGlobalState()
        {
            _eventSpecs = new MetaDictionary<MetaGuid, LiveOpsEventSpec>();
            _eventOccurrences = new MetaDictionary<MetaGuid, LiveOpsEventOccurrence>();

            InitializeLifeStageCollections();
        }

        // \note Funky parameter naming due to MetaDeserializationConstructor's name-based member-parameter matching.
        [MetaDeserializationConstructor]
        LiveOpsEventsGlobalState(MetaDictionary<MetaGuid, LiveOpsEventSpec> eventSpecs, MetaDictionary<MetaGuid, LiveOpsEventOccurrence> eventOccurrences)
        {
            _eventSpecs = eventSpecs;
            _eventOccurrences = eventOccurrences;

            InitializeLifeStageCollections();
        }

        // \note Here, we do not allow direct mutation of _eventSpecs and _eventOccurrences.
        //       Instead they're accessed via read-only EventSpecs and EventOccurrences,
        //       and added/updated via AddNewSpec/SetSpec and AddNewOccurrence/SetOccurrence.
        //
        //       This is because, for occurrences, we maintain an organization of the occurrences
        //       into separate collections (_upcomingOccurrenceIds, _ongoingOccurrenceIds, _concludedOccurrenceIds)
        //       according to which stage they're in. We want this to stay in sync with the occurrences'
        //       states.
        //
        //       For specs this would not be necessary but we do it for consistency.

        // \todo #liveops-event Tolerate individual events being broken and non-deserializable.
        //       Consider what kind of state storage is suitable for this.
        [MetaMember(1)] MetaDictionary<MetaGuid, LiveOpsEventSpec> _eventSpecs;
        [MetaMember(2)] MetaDictionary<MetaGuid, LiveOpsEventOccurrence> _eventOccurrences;

        public IReadOnlyDictionary<MetaGuid, LiveOpsEventSpec> EventSpecs => _eventSpecs;
        public IReadOnlyDictionary<MetaGuid, LiveOpsEventOccurrence> EventOccurrences => _eventOccurrences;

        public IEnumerable<MetaGuid> UpcomingOccurrenceIds => _upcomingOccurrenceIds;
        public IEnumerable<MetaGuid> OngoingOccurrenceIds => _ongoingOccurrenceIds;
        public IEnumerable<MetaGuid> ConcludedOccurrenceIds => _concludedOccurrenceIds;

        public void AddNewSpec(LiveOpsEventSpec spec)
        {
            _eventSpecs.Add(spec.SpecId, spec);
        }

        public void SetSpec(LiveOpsEventSpec spec)
        {
            _eventSpecs[spec.SpecId] = spec;
        }

        public void AddNewOccurrence(LiveOpsEventOccurrence occurrence)
        {
            MetaGuid id = occurrence.OccurrenceId;

            _eventOccurrences.Add(id, occurrence);

            LiveOpsEventLifeStage lifeStage = occurrence.TimeState.GetLifeStage();
            GetCollectionForLifeStage(lifeStage).Add(id);
        }

        public void SetOccurrence(LiveOpsEventOccurrence occurrence)
        {
            MetaGuid id = occurrence.OccurrenceId;

            if (!_eventOccurrences.ContainsKey(id))
            {
                AddNewOccurrence(occurrence);
                return;
            }

            LiveOpsEventOccurrence oldOccurrence = _eventOccurrences[id];
            _eventOccurrences[id] = occurrence;

            LiveOpsEventLifeStage oldLifeStage = oldOccurrence.TimeState.GetLifeStage();
            LiveOpsEventLifeStage newLifeStage = occurrence.TimeState.GetLifeStage();

            if (oldLifeStage != newLifeStage)
            {
                GetCollectionForLifeStage(oldLifeStage).Remove(id);
                GetCollectionForLifeStage(newLifeStage).Add(id);
            }
        }

        OrderedSet<MetaGuid> _upcomingOccurrenceIds;
        OrderedSet<MetaGuid> _ongoingOccurrenceIds;
        OrderedSet<MetaGuid> _concludedOccurrenceIds;

        OrderedSet<MetaGuid> GetCollectionForLifeStage(LiveOpsEventLifeStage lifeStage)
        {
            switch (lifeStage)
            {
                case LiveOpsEventLifeStage.Upcoming:    return _upcomingOccurrenceIds;
                case LiveOpsEventLifeStage.Ongoing:     return _ongoingOccurrenceIds;
                case LiveOpsEventLifeStage.Concluded:   return _concludedOccurrenceIds;
                default:
                    throw new MetaAssertException($"Invalid {nameof(LiveOpsEventLifeStage)}: {lifeStage}");
            }
        }

        void InitializeLifeStageCollections()
        {
            _upcomingOccurrenceIds = new OrderedSet<MetaGuid>();
            _ongoingOccurrenceIds = new OrderedSet<MetaGuid>();
            _concludedOccurrenceIds = new OrderedSet<MetaGuid>();

            foreach (LiveOpsEventOccurrence occurrence in _eventOccurrences.Values)
            {
                LiveOpsEventLifeStage lifeStage = occurrence.TimeState.GetLifeStage();
                GetCollectionForLifeStage(lifeStage).Add(occurrence.OccurrenceId);
            }
        }
    }

    [MetaMessage(MessageCodesCore.CreateLiveOpsEventRequest, MessageDirection.ServerInternal)]
    public class CreateLiveOpsEventRequest : EntityAskRequest<CreateLiveOpsEventResponse>
    {
        public bool ValidateOnly { get; private set; }
        public LiveOpsEventSettings Settings { get; private set; }

        CreateLiveOpsEventRequest() { }
        public CreateLiveOpsEventRequest(bool validateOnly, LiveOpsEventSettings settings)
        {
            ValidateOnly = validateOnly;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
    }
    [MetaMessage(MessageCodesCore.CreateLiveOpsEventResponse, MessageDirection.ServerInternal)]
    public class CreateLiveOpsEventResponse : EntityAskResponse
    {
        public bool IsValid { get; private set; }
        public LiveOpsEventDiagnostics Diagnostics { get; private set; }
        // \note EventSpecId and InitialEventOccurrenceId are null if IsValid is false, or request's ValidateOnly was true.
        public MetaGuid? EventSpecId { get; private set; }
        public MetaGuid? InitialEventOccurrenceId { get; private set; }
        public List<LiveOpsEventOccurrence> RelatedOccurrences { get; private set; }
        public List<LiveOpsEventSpec> Specs { get; private set; }

        public static CreateLiveOpsEventResponse CreateValid(
            LiveOpsEventDiagnostics diagnostics,
            MetaGuid? eventSpecId,
            MetaGuid? initialEventOccurrenceId,
            List<LiveOpsEventOccurrence> relatedOccurrences,
            List<LiveOpsEventSpec> specs)
            => new CreateLiveOpsEventResponse
            {
                IsValid = true,
                Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)),
                EventSpecId = eventSpecId,
                InitialEventOccurrenceId = initialEventOccurrenceId,
                RelatedOccurrences = relatedOccurrences ?? throw new ArgumentNullException(nameof(relatedOccurrences)),
                Specs = specs ?? throw new ArgumentNullException(nameof(specs)),
            };

        public static CreateLiveOpsEventResponse CreateInvalid(
            LiveOpsEventDiagnostics diagnostics,
            List<LiveOpsEventOccurrence> relatedOccurrences,
            List<LiveOpsEventSpec> specs)
            => new CreateLiveOpsEventResponse
            {
                IsValid = false,
                Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)),
                RelatedOccurrences = relatedOccurrences ?? throw new ArgumentNullException(nameof(relatedOccurrences)),
                Specs = specs ?? throw new ArgumentNullException(nameof(specs)),
            };
    }

    [MetaSerializable]
    public class LiveOpsEventDiagnostics
    {
        [MetaMember(1)] public Dictionary<string, List<LiveOpsEventDiagnostic>> DiagnosticsPerScope = new();

        public void AddError(string scope, string message)
        {
            AddDiagnostic(scope, new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Error, message));
        }

        public void AddWarning(string scope, string message)
        {
            AddDiagnostic(scope, new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Warning, message));
        }

        public void AddInfo(string scope, string message)
        {
            AddDiagnostic(scope, new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Info, message));
        }

        public void AddUneditable(string scope, string message)
        {
            AddDiagnostic(scope, new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Uneditable, message));
        }

        public void AddDiagnostic(string scope, LiveOpsEventDiagnostic diagnostic)
        {
            scope ??= LiveOpsEventDiagnosticScope.General;
            DiagnosticsPerScope.GetOrAddDefaultConstructed(scope).Add(diagnostic);
        }

        public void AddAll(LiveOpsEventDiagnostics other)
        {
            foreach ((string scope, List<LiveOpsEventDiagnostic> diagnostics) in other.DiagnosticsPerScope)
            {
                foreach (LiveOpsEventDiagnostic diagnostic in diagnostics)
                    AddDiagnostic(scope, diagnostic);
            }
        }

        public bool HasErrors()
        {
            return DiagnosticsPerScope.Values.Any(diags => diags.Any(diag => diag.Level == LiveOpsEventDiagnostic.DiagnosticLevel.Error));
        }
    }

    public static class LiveOpsEventDiagnosticScope
    {
        public const string General = "<general>";

        public const string EventType = "eventType";
        public const string Content = "content";

        public const string DisplayName = "displayName";

        public const string UseSchedule = "useSchedule";
        public const string Schedule = "schedule";
        public const string ScheduleIsPlayerLocalTime = "schedule.isPlayerLocalTime";
        public const string SchedulePreviewDuration = "schedule.previewDuration";
        public const string ScheduleEnabledStartTime = "schedule.enabledStartTime";
        public const string ScheduleEndingSoonDuration = "schedule.endingSoonDuration";
        public const string ScheduleEnabledEndTime = "schedule.enabledEndTime";
        public const string ScheduleReviewDuration = "schedule.reviewDuration";
    }

    [MetaSerializable]
    public struct LiveOpsEventDiagnostic
    {
        [MetaSerializable]
        public enum DiagnosticLevel
        {
            Warning,
            Error,
            Info,
            Uneditable,
        }

        [MetaMember(1)] public DiagnosticLevel Level { get; private set; }
        [MetaMember(2)] public string Message { get; private set; }

        public LiveOpsEventDiagnostic(DiagnosticLevel level, string message)
        {
            Level = level;
            Message = message;
        }
    }

    [MetaMessage(MessageCodesCore.UpdateLiveOpsEventRequest, MessageDirection.ServerInternal)]
    public class UpdateLiveOpsEventRequest : EntityAskRequest<UpdateLiveOpsEventResponse>
    {
        public bool ValidateOnly { get; private set; }
        public MetaGuid OccurrenceId { get; private set; } // \todo Should we use occurrence id or spec id?
        public LiveOpsEventSettings Settings { get; private set; }

        UpdateLiveOpsEventRequest() { }
        public UpdateLiveOpsEventRequest(bool validateOnly, MetaGuid occurrenceId, LiveOpsEventSettings settings)
        {
            ValidateOnly = validateOnly;
            OccurrenceId = occurrenceId;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
    }
    [MetaMessage(MessageCodesCore.UpdateLiveOpsEventResponse, MessageDirection.ServerInternal)]
    public class UpdateLiveOpsEventResponse : EntityAskResponse
    {
        public bool IsValid { get; private set; }
        // \todo Spec id?
        // \todo Ids of occurrences that were updated due to being defined by the same spec?
        public LiveOpsEventDiagnostics Diagnostics { get; private set; }
        public int NewEditVersion { get; private set; }
        public List<LiveOpsEventOccurrence> RelatedOccurrences { get; private set; }
        public List<LiveOpsEventSpec> Specs { get; private set; }

        UpdateLiveOpsEventResponse() { }
        public UpdateLiveOpsEventResponse(
            bool isValid,
            LiveOpsEventDiagnostics diagnostics,
            int newEditVersion,
            List<LiveOpsEventOccurrence> relatedOccurrences,
            List<LiveOpsEventSpec> specs)
        {
            IsValid = isValid;
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            NewEditVersion = newEditVersion;
            RelatedOccurrences = relatedOccurrences ?? throw new ArgumentNullException(nameof(relatedOccurrences));
            Specs = specs ?? throw new ArgumentNullException(nameof(specs));
        }
    }

    [MetaMessage(MessageCodesCore.GetLiveOpsEventsRequest, MessageDirection.ServerInternal)]
    public class GetLiveOpsEventsRequest : EntityAskRequest<GetLiveOpsEventsResponse>
    {
        public OrderedSet<MetaGuid> OccurrenceIdsMaybe { get; private set; } = null;
        public MetaTime? StartTimeMaybe { get; private set; } = null;
        public MetaTime? EndTimeMaybe { get; private set; } = null;
        public bool GetTimelineData { get; private set; } = false;

        public GetLiveOpsEventsRequest() { }
        public GetLiveOpsEventsRequest(bool getTimelineData)
        {
            GetTimelineData = getTimelineData;
        }
        public GetLiveOpsEventsRequest(OrderedSet<MetaGuid> occurrenceIds, bool getTimelineData = false)
        {
            OccurrenceIdsMaybe = occurrenceIds;
            GetTimelineData = getTimelineData;
        }
        public GetLiveOpsEventsRequest(MetaTime startTime, MetaTime endTime)
        {
            StartTimeMaybe = startTime;
            EndTimeMaybe = endTime;
            GetTimelineData = true;
        }
    }
    [MetaMessage(MessageCodesCore.GetLiveOpsEventsResponse, MessageDirection.ServerInternal)]
    public class GetLiveOpsEventsResponse : EntityAskResponse
    {
        public List<LiveOpsEventOccurrence> Occurrences { get; private set; }
        public List<LiveOpsEventSpec> Specs { get; private set; }
        public Timeline.TimelineState TimelineStateMaybe { get; private set; }

        GetLiveOpsEventsResponse() { }
        public GetLiveOpsEventsResponse(List<LiveOpsEventOccurrence> occurrences, List<LiveOpsEventSpec> specs, Timeline.TimelineState timelineStateMaybe)
        {
            Occurrences = occurrences ?? throw new ArgumentNullException(nameof(occurrences));
            Specs = specs ?? throw new ArgumentNullException(nameof(specs));
            TimelineStateMaybe = timelineStateMaybe;
        }
    }

    [MetaMessage(MessageCodesCore.GetLiveOpsEventRequest, MessageDirection.ServerInternal)]
    public class GetLiveOpsEventRequest : EntityAskRequest<GetLiveOpsEventResponse>
    {
        public MetaGuid OccurrenceId { get; private set; }

        GetLiveOpsEventRequest() { }
        public GetLiveOpsEventRequest(MetaGuid occurrenceId)
        {
            OccurrenceId = occurrenceId;
        }
    }
    [MetaMessage(MessageCodesCore.GetLiveOpsEventResponse, MessageDirection.ServerInternal)]
    public class GetLiveOpsEventResponse : EntityAskResponse
    {
        public LiveOpsEventOccurrence Occurrence { get; private set; }
        public List<LiveOpsEventOccurrence> RelatedOccurrences { get; private set; }
        public List<LiveOpsEventSpec> Specs { get; private set; }
        public LiveOpsEventDiagnostics UneditableParamsDiagnostics { get; private set; }

        GetLiveOpsEventResponse() { }
        public GetLiveOpsEventResponse(LiveOpsEventOccurrence occurrence, List<LiveOpsEventOccurrence> relatedOccurrences, List<LiveOpsEventSpec> specs, LiveOpsEventDiagnostics uneditableParamsDiagnostics)
        {
            Occurrence = occurrence ?? throw new ArgumentNullException(nameof(occurrence));
            RelatedOccurrences = relatedOccurrences ?? throw new ArgumentNullException(nameof(relatedOccurrences));
            Specs = specs ?? throw new ArgumentNullException(nameof(specs));
            UneditableParamsDiagnostics = uneditableParamsDiagnostics ?? throw new ArgumentNullException(nameof(uneditableParamsDiagnostics));
        }
    }

    [MetaMessage(MessageCodesCore.ExportLiveOpsEventsRequest, MessageDirection.ServerInternal)]
    public class ExportLiveOpsEventsRequest : EntityAskRequest<ExportLiveOpsEventsResponse>
    {
        public List<MetaGuid> OccurrenceIds { get; private set; }

        ExportLiveOpsEventsRequest() { }
        public ExportLiveOpsEventsRequest(List<MetaGuid> occurrenceIds)
        {
            OccurrenceIds = occurrenceIds ?? throw new ArgumentNullException(nameof(occurrenceIds));
        }
    }

    [MetaMessage(MessageCodesCore.ExportLiveOpsEventsResponse, MessageDirection.ServerInternal)]
    public class ExportLiveOpsEventsResponse : EntityAskResponse
    {
        public List<EventResult> EventResults { get; private set; }
        public LiveOpsEventExportImport.Package Package { get; private set; }

        ExportLiveOpsEventsResponse() { }
        public ExportLiveOpsEventsResponse(List<EventResult> eventResults, LiveOpsEventExportImport.Package package)
        {
            EventResults = eventResults ?? new();
            Package = package ?? throw new ArgumentNullException(nameof(package));
        }

        [MetaSerializable]
        public class EventResult
        {
            [MetaMember(1)] public MetaGuid OccurrenceId { get; private set; }
            [MetaMember(2)] public bool IsValid { get; private set; }
            [MetaMember(3)] public string Error { get; private set; }
            [MetaMember(4)] public LiveOpsEventOccurrence OccurrenceMaybe { get; private set; }
            [MetaMember(5)] public LiveOpsEventSpec SpecMaybe { get; private set; }

            EventResult() { }
            public EventResult(MetaGuid occurrenceId, bool isValid, string error, LiveOpsEventOccurrence occurrenceMaybe, LiveOpsEventSpec specMaybe)
            {
                OccurrenceId = occurrenceId;
                IsValid = isValid;
                Error = error;
                OccurrenceMaybe = occurrenceMaybe;
                SpecMaybe = specMaybe;
            }
        }
    }

    [MetaMessage(MessageCodesCore.ImportLiveOpsEventsRequest, MessageDirection.ServerInternal)]
    public class ImportLiveOpsEventsRequest : EntityAskRequest<ImportLiveOpsEventsResponse>
    {
        public bool ValidateOnly { get; private set; }
        public LiveOpsEventExportImport.ImportConflictPolicy ConflictPolicy { get; private set; }
        public LiveOpsEventExportImport.Package Package { get; private set; }

        ImportLiveOpsEventsRequest() { }
        public ImportLiveOpsEventsRequest(bool validateOnly, LiveOpsEventExportImport.ImportConflictPolicy conflictPolicy, LiveOpsEventExportImport.Package package)
        {
            ValidateOnly = validateOnly;
            ConflictPolicy = conflictPolicy;
            Package = package ?? throw new ArgumentNullException(nameof(package));
        }
    }

    [MetaMessage(MessageCodesCore.ImportLiveOpsEventsResponse, MessageDirection.ServerInternal)]
    public class ImportLiveOpsEventsResponse : EntityAskResponse
    {
        public bool IsValid { get; private set; }
        public List<LiveOpsEventDiagnostic> GeneralDiagnostics { get; private set; }
        public List<EventResult> EventResults { get; private set; }

        ImportLiveOpsEventsResponse() { }
        public ImportLiveOpsEventsResponse(bool isValid, List<LiveOpsEventDiagnostic> generalDiagnostics, List<EventResult> eventResults)
        {
            IsValid = isValid;
            GeneralDiagnostics = generalDiagnostics ?? throw new ArgumentNullException(nameof(generalDiagnostics));
            EventResults = eventResults ?? new();
        }

        [MetaSerializable]
        public class EventResult
        {
            [MetaMember(1)] public bool IsValid;
            [MetaMember(2)] public MetaGuid OccurrenceId;
            [MetaMember(3)] public MetaGuid SpecId;
            [MetaMember(4)] public LiveOpsEventDiagnostics Diagnostics;
            [MetaMember(8)] public LiveOpsEventSettings SettingsMaybe;
            [MetaMember(5)] public LiveOpsEventOccurrence OccurrenceMaybe;
            [MetaMember(6)] public LiveOpsEventSpec SpecMaybe;
            [MetaMember(7)] public LiveOpsEventExportImport.EventImportOutcome Outcome;

            EventResult() { }
            public EventResult(bool isValid, MetaGuid occurrenceId, MetaGuid specId, LiveOpsEventDiagnostics diagnostics, LiveOpsEventSettings settingsMaybe, LiveOpsEventOccurrence occurrenceMaybe, LiveOpsEventSpec specMaybe, LiveOpsEventExportImport.EventImportOutcome outcome)
            {
                IsValid = isValid;
                OccurrenceId = occurrenceId;
                SpecId = specId;
                Diagnostics = diagnostics;
                SettingsMaybe = settingsMaybe;
                OccurrenceMaybe = occurrenceMaybe;
                SpecMaybe = specMaybe;
                Outcome = outcome;
            }
        }
    }

    [MetaMessage(MessageCodesCore.SetLiveOpsEventsMessage, MessageDirection.ServerInternal)]
    public class SetLiveOpsEventsMessage : MetaMessage
    {
        public List<LiveOpsEventOccurrence> Occurrences { get; private set; }
        public List<LiveOpsEventSpec> Specs { get; private set; }

        SetLiveOpsEventsMessage() { }
        public SetLiveOpsEventsMessage(List<LiveOpsEventOccurrence> occurrences, List<LiveOpsEventSpec> specs)
        {
            Occurrences = occurrences ?? throw new ArgumentNullException(nameof(occurrences));
            Specs = specs ?? throw new ArgumentNullException(nameof(specs));
        }
    }

    [MetaMessage(MessageCodesCore.LiveOpsEventTimeStatesUpdated, MessageDirection.ServerInternal)]
    public class LiveOpsEventTimeStatesUpdatedMessage : MetaMessage
    {
        public List<LiveOpsEventTimeStateUpdate> Updates { get; }

        [MetaDeserializationConstructor]
        public LiveOpsEventTimeStatesUpdatedMessage(List<LiveOpsEventTimeStateUpdate> updates)
        {
            Updates = updates ?? throw new ArgumentNullException(nameof(updates));
        }
    }

    [MetaSerializable]
    public struct LiveOpsEventTimeStateUpdate
    {
        [MetaMember(1)] public readonly MetaGuid OccurrenceId;
        [MetaMember(2)] public readonly LiveOpsEventOccurrenceTimeState TimeState;

        [MetaDeserializationConstructor]
        public LiveOpsEventTimeStateUpdate(MetaGuid occurrenceId, LiveOpsEventOccurrenceTimeState timeState)
        {
            OccurrenceId = occurrenceId;
            TimeState = timeState;
        }
    }

    [MetaMessage(MessageCodesCore.ConcludeLiveOpsEventRequest, MessageDirection.ServerInternal)]
    public class ConcludeLiveOpsEventRequest : EntityAskRequest<ConcludeLiveOpsEventResponse>
    {
        public MetaGuid OccurrenceId { get; }

        [MetaDeserializationConstructor]
        public ConcludeLiveOpsEventRequest(MetaGuid occurrenceId)
        {
            OccurrenceId = occurrenceId;
        }
    }
    [MetaMessage(MessageCodesCore.ConcludeLiveOpsEventResponse, MessageDirection.ServerInternal)]
    public class ConcludeLiveOpsEventResponse : EntityAskResponse
    {
        public bool IsSuccess { get; private set; }
        public string Error { get; private set; }

        public static ConcludeLiveOpsEventResponse CreateSuccess()
            => new ConcludeLiveOpsEventResponse { IsSuccess = true, Error = null };

        public static ConcludeLiveOpsEventResponse CreateFailure(string error)
            => new ConcludeLiveOpsEventResponse { IsSuccess = false, Error = error ?? throw new ArgumentNullException(nameof(error)) };
    }

    [MetaSerializableDerived(MessageCodesCore.LiveOpsEventNotFound)]
    public class LiveOpsEventNotFound : EntityAskRefusal
    {
        [MetaMember(1)] public MetaGuid OccurrenceId { get; private set; }

        [MetaDeserializationConstructor]
        public LiveOpsEventNotFound(MetaGuid occurrenceId)
        {
            OccurrenceId = occurrenceId;
        }

        public override string Message => $"Event occurrence {OccurrenceId} not found.";
    }

    public partial class LiveOpsTimelineManager
    {
        class LiveOpsEventsTick { public static readonly LiveOpsEventsTick Instance = new LiveOpsEventsTick(); }
        class MigrateLiveOpsEventsFromGlobalStateCommand { public static readonly MigrateLiveOpsEventsFromGlobalStateCommand Instance = new(); }

        void LiveOpsEventsPreStart()
        {
            // \todo #liveops-event-time-state Smarter updating of event time states.
            if (new LiveOpsEventsEnabledCondition().IsEnabled)
                StartPeriodicTimer(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), LiveOpsEventsTick.Instance);
        }

        void LiveOpsEventsPostLoad()
        {
            // Migrate liveops events state from away from GlobalStateManager to LiveOpsTimelineManager.
            // \note This migration was added in R31, remove when not needed anymore. #r31
            if (!_state.HasMigratedLiveOpsEventsFromGlobalState)
                _self.Tell(MigrateLiveOpsEventsFromGlobalStateCommand.Instance);
            else
                CastMessage(GlobalStateManager.EntityId, new GlobalStateForgetLegacyLiveOpsEventsState());
        }

        [CommandHandler]
        async Task HandleMigrateLiveOpsEventsFromGlobalStateCommandAsync(MigrateLiveOpsEventsFromGlobalStateCommand _)
        {
            if (_state.LiveOpsEvents.EventSpecs.Count != 0 || _state.LiveOpsEvents.EventOccurrences.Count != 0)
                throw new InvalidOperationException($"Can't migrate liveops event state from {nameof(GlobalState)}, because {nameof(LiveOpsTimelineManager)} already has events ({_state.LiveOpsEvents.EventSpecs.Count} specs, {_state.LiveOpsEvents.EventOccurrences.Count} occurrences)");

            // \note We tolerate some amount of EntityAsk timeouts, because GlobalStateManager's PostLoad
            //       can take a somewhat long time if game config is big and has lots of experiments.
            //       Actually crashing here (due to ask timeout) would be mostly OK (LiveOpsTimelineManager
            //       would just restart and retry and eventually succeed), but this way we avoid failing
            //       integration tests due to error logging.
            TimeSpan askRetryTimeout = TimeSpan.FromMinutes(1);
            DateTime askRetryDeadline = DateTime.UtcNow + askRetryTimeout;
            GlobalStateSnapshot response = null;
            while (response == null)
            {
                try
                {
                    response = await EntityAskAsync(GlobalStateManager.EntityId, GlobalStateRequest.Instance);
                }
                catch (TimeoutException ex)
                {
                    if (DateTime.UtcNow < askRetryDeadline)
                        _log.Warning($"Got timeout when trying to ask state from {nameof(GlobalStateManager)}, will keep trying until {{Deadline}}: {{Exception}}", askRetryDeadline, ex);
                    else
                        throw;
                }
            }

            GlobalState globalState = response.GlobalState.Deserialize(resolver: null, logicVersion: null);
            if (globalState.LegacyLiveOpsEvents == null)
                throw new InvalidOperationException($"Tried to migrate liveops event state from {nameof(GlobalState)}, but it has null {nameof(globalState.LegacyLiveOpsEvents)}");

            _log.Info($"Initializing {nameof(LiveOpsTimelineManagerState)}'s liveops events state from {nameof(GlobalState)} ({{NumEvents}} events)", globalState.LegacyLiveOpsEvents.EventOccurrences.Count);

            foreach (LiveOpsEventSpec spec in globalState.LegacyLiveOpsEvents.EventSpecs.Values)
                _state.LiveOpsEvents.AddNewSpec(spec);

            foreach (LiveOpsEventOccurrence occurrence in globalState.LegacyLiveOpsEvents.EventOccurrences.Values)
                _state.LiveOpsEvents.AddNewOccurrence(occurrence);

            _state.HasMigratedLiveOpsEventsFromGlobalState = true;

            await PersistStateIntermediate();

            CastMessage(GlobalStateManager.EntityId, new GlobalStateForgetLegacyLiveOpsEventsState());
        }

        [CommandHandler]
        async Task HandleLiveOpsEventsTickAsync(LiveOpsEventsTick _)
        {
            List<LiveOpsEventTimeStateUpdate> updates;

            try
            {
                MetaTime currentTime = MetaTime.Now;

                updates = null;

                // \note We only consider upcoming and ongoing occurrences, not concluded ones (because we know they won't change).
                foreach (MetaGuid occurrenceId in _state.LiveOpsEvents.UpcomingOccurrenceIds.Concat(_state.LiveOpsEvents.OngoingOccurrenceIds))
                {
                    LiveOpsEventOccurrence occurrence = _state.LiveOpsEvents.EventOccurrences[occurrenceId];

                    LiveOpsEventOccurrenceTimeState newTimeState = LiveOpsEventOccurrenceTimeState.CalculateCurrentTimeState(occurrence, currentTime, out bool wasChanged);
                    if (!wasChanged)
                        continue;

                    (updates ??= new()).Add(new LiveOpsEventTimeStateUpdate(occurrence.OccurrenceId, newTimeState));
                }
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(LiveOpsEventsTick)}");
                return;
            }

            if (updates != null && updates.Count > 0)
            {
                foreach (LiveOpsEventTimeStateUpdate update in updates)
                {
                    LiveOpsEventOccurrence existingOccurrence = _state.LiveOpsEvents.EventOccurrences[update.OccurrenceId];
                    LiveOpsEventOccurrence updatedOccurrence = existingOccurrence.CopyWithTimeState(update.TimeState);
                    _state.LiveOpsEvents.SetOccurrence(updatedOccurrence);
                }

                await PersistStateIntermediate();
                TimelineRecordLiveOpsEventUpdates(updates.Select(update => update.OccurrenceId));

                PublishMessage(EntityTopic.Member, new LiveOpsEventTimeStatesUpdatedMessage(updates));
            }
        }

        [EntityAskHandler]
        async Task<CreateLiveOpsEventResponse> HandleCreateLiveOpsEventRequestAsync(CreateLiveOpsEventRequest request)
        {
            LiveOpsEventSpec spec;
            LiveOpsEventOccurrence occurrence;
            LiveOpsEventDiagnostics diagnostics;
            List<LiveOpsEventOccurrence> relatedOccurrences;
            List<LiveOpsEventSpec> specs;

            try
            {
                LiveOpsEventSettings settings = request.Settings;

                MetaTime currentTime = MetaTime.Now;

                diagnostics = new LiveOpsEventDiagnostics();
                ValidateEventSettings(diagnostics, settings);
                CheckEventOverlaps(diagnostics, settings, currentTime, _state.LiveOpsEvents.EventOccurrences.Values);

                relatedOccurrences = GetRelatedOccurrences(settings.EventParams.Content);
                specs = GetSpecsForOccurrences(relatedOccurrences);

                // \todo #liveops-event Option/parameter for "treat warnings as errors"?
                if (diagnostics.HasErrors())
                {
                    return CreateLiveOpsEventResponse.CreateInvalid(diagnostics, relatedOccurrences, specs);
                }

                if (request.ValidateOnly)
                {
                    return CreateLiveOpsEventResponse.CreateValid(diagnostics, eventSpecId: null, initialEventOccurrenceId: null, relatedOccurrences, specs);
                }

                spec = new LiveOpsEventSpec(
                    specId: MetaGuid.NewWithTime(currentTime.ToDateTime()),
                    editVersion: 0,
                    settings,
                    createdAt: currentTime);

                {
                    (MetaScheduleTimeMode timeMode, LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe) = CreateSingleOccasionScheduleAssumeNoRecurrence(settings);

                    occurrence = new LiveOpsEventOccurrence(
                        occurrenceId: MetaGuid.NewWithTime(currentTime.ToDateTime()),
                        editVersion: 0,
                        definingSpecId: spec.SpecId,
                        timeMode,
                        utcScheduleOccasionMaybe,
                        spec.Settings.EventParams,
                        explicitlyConcludedAt: null,
                        LiveOpsEventOccurrenceTimeState.Initial);
                }
                occurrence = occurrence.CopyWithUpdatedTimeState(currentTime);
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleCreateLiveOpsEventRequestAsync)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }

            _state.LiveOpsEvents.AddNewSpec(spec);
            _state.LiveOpsEvents.AddNewOccurrence(occurrence);
            await PersistStateIntermediate();
            TimelineRecordLiveOpsEventUpdates([ occurrence.OccurrenceId ]);

            PublishMessage(EntityTopic.Member, new SetLiveOpsEventsMessage(
                new List<LiveOpsEventOccurrence>{ occurrence },
                new List<LiveOpsEventSpec>{ spec }));

            return CreateLiveOpsEventResponse.CreateValid(
                diagnostics,
                eventSpecId: spec.SpecId,
                initialEventOccurrenceId: occurrence.OccurrenceId,
                relatedOccurrences,
                specs);
        }

        [EntityAskHandler]
        async Task<UpdateLiveOpsEventResponse> HandleUpdateLiveOpsEventRequestAsync(UpdateLiveOpsEventRequest request)
        {
            LiveOpsEventOccurrence existingOccurrence;
            LiveOpsEventSpec existingSpec;
            LiveOpsEventDiagnostics diagnostics;
            List<LiveOpsEventOccurrence> relatedOccurrences;
            List<LiveOpsEventSpec> specs;
            LiveOpsEventOccurrence updatedOccurrence;
            LiveOpsEventSpec updatedSpec;

            try
            {
                MetaGuid occurrenceId = request.OccurrenceId;
                LiveOpsEventSettings updatedSettings = request.Settings;

                MetaTime currentTime = MetaTime.Now;

                if (!_state.LiveOpsEvents.EventOccurrences.TryGetValue(occurrenceId, out existingOccurrence))
                    throw new LiveOpsEventNotFound(occurrenceId);

                existingSpec = _state.LiveOpsEvents.EventSpecs[existingOccurrence.DefiningSpecId];

                diagnostics = new LiveOpsEventDiagnostics();
                ValidateEventUpdate(diagnostics, existingOccurrence, existingSpec, updatedSettings, currentTime);
                CheckEventOverlaps(diagnostics, updatedSettings, currentTime, _state.LiveOpsEvents.EventOccurrences.Values.Where(occ => occ.OccurrenceId != existingOccurrence.OccurrenceId));

                relatedOccurrences = GetRelatedOccurrences(updatedSettings.EventParams.Content);
                specs = GetSpecsForOccurrences(relatedOccurrences);

                // \todo #liveops-event Option/parameter for "treat warnings as errors"?
                if (diagnostics.HasErrors())
                {
                    return new UpdateLiveOpsEventResponse(isValid: false, diagnostics, newEditVersion: existingOccurrence.EditVersion + 1, relatedOccurrences, specs);
                }

                if (request.ValidateOnly)
                {
                    return new UpdateLiveOpsEventResponse(isValid: true, diagnostics, newEditVersion: existingOccurrence.EditVersion + 1, relatedOccurrences, specs);
                }

                updatedSpec = new LiveOpsEventSpec(
                    specId: existingSpec.SpecId,
                    editVersion: existingSpec.EditVersion + 1,
                    updatedSettings,
                    existingSpec.CreatedAt);

                {
                    (MetaScheduleTimeMode timeMode, LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe) = CreateSingleOccasionScheduleAssumeNoRecurrence(updatedSettings);

                    updatedOccurrence = new LiveOpsEventOccurrence(
                        occurrenceId: existingOccurrence.OccurrenceId,
                        editVersion: existingOccurrence.EditVersion + 1,
                        definingSpecId: existingOccurrence.DefiningSpecId,
                        timeMode,
                        utcScheduleOccasionMaybe,
                        updatedSpec.Settings.EventParams,
                        explicitlyConcludedAt: existingOccurrence.ExplicitlyConcludedAt,
                        existingOccurrence.TimeState);
                }
                updatedOccurrence = updatedOccurrence.CopyWithUpdatedTimeState(currentTime);
                // \todo #liveops-event-time-state #update-restrictions Sanity check that the current phase (in any time zone) did not go backwards, comparing old vs new UtcScheduleOccasionMaybe.
                // \todo #liveops-event #update-restrictions Restrictions for updating existing occurrences:
                // \todo #liveops-event Update existing occurrence(s) as appropriate:
                //       - if occurrence is fully in past, don't update
                //       - if occurrence has started, don't update start time, but allow updating other things
                //       - otherwise, allow updating anything
                //       Offer some user controls for whether to update ongoing events or just future?
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleUpdateLiveOpsEventRequestAsync)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }

            _state.LiveOpsEvents.SetSpec(updatedSpec);
            _state.LiveOpsEvents.SetOccurrence(updatedOccurrence);
            await PersistStateIntermediate();
            TimelineRecordLiveOpsEventUpdates([ updatedOccurrence.OccurrenceId ]);

            PublishMessage(EntityTopic.Member, new SetLiveOpsEventsMessage(
                new List<LiveOpsEventOccurrence>{ updatedOccurrence },
                new List<LiveOpsEventSpec>{ updatedSpec }));

            return new UpdateLiveOpsEventResponse(isValid: true, diagnostics, newEditVersion: updatedOccurrence.EditVersion, relatedOccurrences, specs);
        }

        static void ValidateEventUpdate(LiveOpsEventDiagnostics diagnostics, LiveOpsEventOccurrence existingOccurrence, LiveOpsEventSpec existingSpec, LiveOpsEventSettings updatedSettings, MetaTime currentTime)
        {
            ValidateEventSettings(diagnostics, updatedSettings);

            LiveOpsEventDiagnostics uneditabilityDiagnostics = ResolveUneditableEventParams(existingOccurrence);

            void AddDiagnosticIfUneditable(string scope)
            {
                if (uneditabilityDiagnostics.DiagnosticsPerScope.TryGetValue(scope, out List<LiveOpsEventDiagnostic> uneditabilityForThisScope)
                    && uneditabilityForThisScope.Any(diag => diag.Level == LiveOpsEventDiagnostic.DiagnosticLevel.Uneditable))
                {
                    LiveOpsEventDiagnostic diag = uneditabilityForThisScope.First();
                    diagnostics.AddError(scope: scope, message: diag.Message);
                }
            }

            if (updatedSettings.EventParams.Content != null // \note Null content will have already caused a diagnostic by ValidateEventSettings.
                && updatedSettings.EventParams.Content.GetType() != existingOccurrence.EventParams.Content.GetType())
            {
                AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.EventType);
            }

            {
                MetaRecurringCalendarSchedule existingSchedule = (MetaRecurringCalendarSchedule)existingSpec.Settings.ScheduleMaybe;
                MetaRecurringCalendarSchedule newSchedule = (MetaRecurringCalendarSchedule)updatedSettings.ScheduleMaybe;

                if (newSchedule == null && existingSchedule == null)
                {
                    // No schedule in old or new, not edited, OK
                }
                else if (newSchedule == null || existingSchedule == null)
                {
                    AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.UseSchedule);
                    AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.Schedule);

                    if (existingSchedule == null && newSchedule != null)
                    {
                        LiveOpsEventScheduleOccasion newScheduleUtcOccasion = CreateSingleOccasionScheduleAssumeNoRecurrence(updatedSettings).UtcScheduleOccasionMaybe;
                        LiveOpsEventPhase leastAdvancedPhase = LiveOpsEventServerUtil.GetLeastAdvancedPhase(newSchedule.TimeMode, newScheduleUtcOccasion, currentTime);

                        if (LiveOpsEventPhase.PhasePrecedes(leastAdvancedPhase, LiveOpsEventPhase.NormalActive))
                        {
                            bool isLocalTime = newSchedule.TimeMode == MetaScheduleTimeMode.Local;
                            string localTimeCaveatTextMaybe = isLocalTime ? " in all time zones" : "";

                            diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledStartTime, $"Start time must be in the past{localTimeCaveatTextMaybe} when adding a schedule to an event that didn't previously have it.");
                        }
                    }
                }
                else
                {
                    if (newSchedule.TimeMode != existingSchedule.TimeMode)
                        AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.ScheduleIsPlayerLocalTime);

                    if (newSchedule.Start != existingSchedule.Start)
                        AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.ScheduleEnabledStartTime);

                    if (newSchedule.Duration != existingSchedule.Duration)
                        AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.ScheduleEnabledEndTime);

                    if (newSchedule.EndingSoon != existingSchedule.EndingSoon)
                        AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.ScheduleEndingSoonDuration);

                    if (newSchedule.Preview != existingSchedule.Preview)
                        AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.SchedulePreviewDuration);

                    if (newSchedule.Review != existingSchedule.Review)
                        AddDiagnosticIfUneditable(LiveOpsEventDiagnosticScope.ScheduleReviewDuration);
                }
            }
        }

        static void ValidateEventSettings(LiveOpsEventDiagnostics diagnostics, LiveOpsEventSettings settings)
        {
            if (settings.ScheduleMaybe != null)
            {
                if (!(settings.ScheduleMaybe is MetaRecurringCalendarSchedule schedule))
                    diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Schedule, message: $"Got schedule of type {settings.ScheduleMaybe.GetType()}, expected {nameof(MetaRecurringCalendarSchedule)}.");
                else
                {
                    if (schedule.Recurrence.HasValue)
                        diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Schedule, message: $"Recurring schedules not yet supported by LiveOps Events.");
                }
            }

            if (settings.EventParams.Content == null)
            {
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.Content, message: "LiveOps Event must a have non-null template.");
            }

            if (settings.EventParams.DisplayName == null)
                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.DisplayName, message: "Event name is required.");
        }

        static void CheckEventOverlaps(LiveOpsEventDiagnostics diagnostics, LiveOpsEventSettings newEventSettings, MetaTime currentTime, IEnumerable<LiveOpsEventOccurrence> otherOccurrences)
        {
            // Warnings about overlapping events (when desired, according to user implementation of ShouldWarnAboutOverlapWith)
            // \todo #liveops-event Think about overlap check more carefully. Didn't think about this too much yet.
            //       - Local vs UTC
            //       - Is it enough to only look at enabled time range, or should we also look at visibility time range?
            //       - What else...

            LiveOpsEventScheduleOccasion scheduleOccasion = CreateSingleOccasionScheduleAssumeNoRecurrence(newEventSettings).UtcScheduleOccasionMaybe;
            foreach (LiveOpsEventOccurrence otherOccurrence in otherOccurrences)
            {
                if (!otherOccurrence.EventParams.Content.ShouldWarnAboutOverlapWith(newEventSettings.EventParams.Content))
                    continue;

                LiveOpsEventScheduleOccasion otherScheduleOccasion = otherOccurrence.UtcScheduleOccasionMaybe;

                if (scheduleOccasion == null && otherScheduleOccasion == null)
                {
                    diagnostics.AddWarning(scope: LiveOpsEventDiagnosticScope.Schedule, message: $"Event overlaps with other event {otherOccurrence.OccurrenceId}. Both events are always active (neither has a schedule).");
                }
                else if (scheduleOccasion == null && otherScheduleOccasion != null)
                {
                    if (currentTime < otherScheduleOccasion.GetEnabledEndTime())
                    {
                        diagnostics.AddWarning(scope: LiveOpsEventDiagnosticScope.Schedule, message: $"Event overlaps with other event {otherOccurrence.OccurrenceId}. This event is always active (does not have a schedule) and the other event does not end until {otherScheduleOccasion.GetEnabledEndTime()}.");
                    }
                }
                else if (scheduleOccasion != null && otherScheduleOccasion == null)
                {
                    if (currentTime < scheduleOccasion.GetEnabledEndTime())
                    {
                        diagnostics.AddWarning(scope: LiveOpsEventDiagnosticScope.Schedule, message: $"Event overlaps with other event {otherOccurrence.OccurrenceId}. The other event is always active (does not have a schedule) and this event does not end until {scheduleOccasion.GetEnabledEndTime()}.");
                    }
                }
                else
                {
                    MetaTime overlapStart = MetaTime.Max(scheduleOccasion.GetEnabledStartTime(),    otherScheduleOccasion.GetEnabledStartTime());
                    MetaTime overlapEnd   = MetaTime.Min(scheduleOccasion.GetEnabledEndTime(),      otherScheduleOccasion.GetEnabledEndTime());

                    if (overlapStart < overlapEnd)
                    {
                        diagnostics.AddWarning(scope: LiveOpsEventDiagnosticScope.Schedule, message: $"Event overlaps with other event {otherOccurrence.OccurrenceId}. The overlap is from {overlapStart} to {overlapEnd}. The other event is from {otherScheduleOccasion.GetEnabledStartTime()} to {otherScheduleOccasion.GetEnabledEndTime()}.");
                    }
                }
            }
        }

        [EntityAskHandler]
        async Task<ConcludeLiveOpsEventResponse> HandleConcludeLiveOpsEventRequestAsync(ConcludeLiveOpsEventRequest request)
        {
            LiveOpsEventOccurrence existingOccurrence;
            LiveOpsEventOccurrence updatedOccurrence;

            try
            {
                MetaTime currentTime = MetaTime.Now;

                MetaGuid occurrenceId = request.OccurrenceId;

                if (!_state.LiveOpsEvents.EventOccurrences.TryGetValue(occurrenceId, out existingOccurrence))
                    throw new LiveOpsEventNotFound(occurrenceId);

                if (existingOccurrence.ExplicitlyConcludedAt.HasValue)
                    return ConcludeLiveOpsEventResponse.CreateFailure("Event has already been explicitly concluded.");
                if (existingOccurrence.TimeState.GetLeastAdvancedPhase() == LiveOpsEventPhase.Concluded)
                    return ConcludeLiveOpsEventResponse.CreateFailure("Event has already conluded.");

                updatedOccurrence = new LiveOpsEventOccurrence(
                    occurrenceId: existingOccurrence.OccurrenceId,
                    editVersion: existingOccurrence.EditVersion,
                    definingSpecId: existingOccurrence.DefiningSpecId,
                    existingOccurrence.ScheduleTimeMode,
                    existingOccurrence.UtcScheduleOccasionMaybe,
                    existingOccurrence.EventParams,
                    explicitlyConcludedAt: currentTime,
                    existingOccurrence.TimeState);
                updatedOccurrence = updatedOccurrence.CopyWithUpdatedTimeState(currentTime);
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleConcludeLiveOpsEventRequestAsync)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }

            _state.LiveOpsEvents.SetOccurrence(updatedOccurrence);
            await PersistStateIntermediate();
            TimelineRecordLiveOpsEventUpdates([ updatedOccurrence.OccurrenceId ]);

            PublishMessage(EntityTopic.Member, new SetLiveOpsEventsMessage(
                new List<LiveOpsEventOccurrence>{ updatedOccurrence },
                new List<LiveOpsEventSpec>{ }));

            return ConcludeLiveOpsEventResponse.CreateSuccess();
        }

        [EntityAskHandler]
        GetLiveOpsEventsResponse HandleGetLiveOpsEventOccurrencesRequest(GetLiveOpsEventsRequest request)
        {
            try
            {
                MetaTime currentTime = MetaTime.Now;

                List<LiveOpsEventOccurrence> occurrences;

                if (request.OccurrenceIdsMaybe == null)
                    occurrences = _state.LiveOpsEvents.EventOccurrences.Values.ToList();
                else
                {
                    occurrences = new List<LiveOpsEventOccurrence>(capacity: request.OccurrenceIdsMaybe.Count);
                    foreach (MetaGuid occurrenceId in request.OccurrenceIdsMaybe)
                    {
                        if (!_state.LiveOpsEvents.EventOccurrences.TryGetValue(occurrenceId, out LiveOpsEventOccurrence occurrence))
                            throw new InvalidEntityAsk($"Event occurrence {occurrenceId} not found");

                        occurrences.Add(occurrence);
                    }
                }

                if (request.StartTimeMaybe.HasValue || request.EndTimeMaybe.HasValue)
                    occurrences.RemoveAll(occurrence => !OccurrenceVisibilityOverlapsWithTimeRange(occurrence, request.StartTimeMaybe, request.EndTimeMaybe, currentTime));

                List<LiveOpsEventSpec> specs = GetSpecsForOccurrences(occurrences);

                return new GetLiveOpsEventsResponse(
                    occurrences,
                    specs,
                    request.GetTimelineData ? _state.Timeline : null);
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleGetLiveOpsEventOccurrencesRequest)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }
        }

        [EntityAskHandler]
        GetLiveOpsEventResponse HandleGetLiveOpsEventRequest(GetLiveOpsEventRequest request)
        {
            try
            {
                MetaGuid occurrenceId = request.OccurrenceId;

                if (!_state.LiveOpsEvents.EventOccurrences.TryGetValue(occurrenceId, out LiveOpsEventOccurrence requestedOccurrence))
                    throw new LiveOpsEventNotFound(occurrenceId);

                List<LiveOpsEventOccurrence> relatedOccurrences = GetRelatedOccurrences(requestedOccurrence.EventParams.Content);
                List<LiveOpsEventSpec> specs = GetSpecsForOccurrences(relatedOccurrences.Prepend(requestedOccurrence));

                LiveOpsEventDiagnostics uneditableParamsDiagnostics = ResolveUneditableEventParams(requestedOccurrence);

                return new GetLiveOpsEventResponse(
                    requestedOccurrence,
                    relatedOccurrences,
                    specs,
                    uneditableParamsDiagnostics);
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleGetLiveOpsEventRequest)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }
        }

        [EntityAskHandler]
        ExportLiveOpsEventsResponse HandleExportLiveOpsEventsRequest(ExportLiveOpsEventsRequest request)
        {
            try
            {
                List<LiveOpsEventExportImport.ExportedEvent> exportedEvents = new();

                List<ExportLiveOpsEventsResponse.EventResult> eventResults = new();

                // Count number of each occurrence ids, for detecting duplicates
                Dictionary<MetaGuid, int> occurrenceIdCounts =
                    request.OccurrenceIds.GroupBy(id => id)
                    .ToDictionary(grouping => grouping.Key, grouping => grouping.Count());

                foreach (MetaGuid occurrenceId in request.OccurrenceIds)
                {
                    ExportLiveOpsEventsResponse.EventResult eventResult;

                    LiveOpsEventSpec specMaybe;
                    if (_state.LiveOpsEvents.EventOccurrences.TryGetValue(occurrenceId, out LiveOpsEventOccurrence occurrenceMaybe))
                    {
                        specMaybe = _state.LiveOpsEvents.EventSpecs[occurrenceMaybe.DefiningSpecId];

                        exportedEvents.Add(LiveOpsEventExportImport.ExportedEvent.Create(
                            occurrenceId: occurrenceMaybe.OccurrenceId,
                            specId: occurrenceMaybe.DefiningSpecId,
                            specMaybe.Settings));
                    }
                    else
                        specMaybe = null;

                    if (occurrenceMaybe == null)
                    {
                        eventResult = new ExportLiveOpsEventsResponse.EventResult(
                            occurrenceId: occurrenceId,
                            isValid: false,
                            error: $"Event does not exist.",
                            occurrenceMaybe: null,
                            specMaybe: null);
                    }
                    else if (occurrenceIdCounts[occurrenceId] != 1)
                    {
                        eventResult = new ExportLiveOpsEventsResponse.EventResult(
                            occurrenceId: occurrenceId,
                            isValid: false,
                            error: Invariant($"Duplicate event ID (seen {occurrenceIdCounts[occurrenceId]} times)."),
                            occurrenceMaybe,
                            specMaybe);
                    }
                    else
                    {
                        if (specMaybe == null)
                            throw new InvalidEntityAsk("Internal error: Spec must be non-null at this point");

                        eventResult = new ExportLiveOpsEventsResponse.EventResult(
                            occurrenceId: occurrenceId,
                            isValid: true,
                            error: null,
                            occurrenceMaybe,
                            specMaybe);
                    }

                    eventResults.Add(eventResult);
                }

                // Sanity check: must produce an EventResult entry for each requested event, valid and invalid alike.
                if (eventResults.Count != request.OccurrenceIds.Count)
                    throw new InvalidEntityAsk("Internal error: some event results are missing");

                // Sanity check: actual vs requested count should never mismatch if there are no errors.
                if (eventResults.All(res => res.IsValid) && exportedEvents.Count != request.OccurrenceIds.Count)
                    throw new InvalidEntityAsk("Internal error: exported count event mismatch");

                LiveOpsEventExportImport.Package package = new LiveOpsEventExportImport.Package(
                    packageFormatVersion: 1,
                    exportedEvents);

                return new ExportLiveOpsEventsResponse(
                    eventResults,
                    package);
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleExportLiveOpsEventsRequest)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }
        }

        [EntityAskHandler]
        async Task<ImportLiveOpsEventsResponse> HandleImportLiveOpsEventsRequestAsync(ImportLiveOpsEventsRequest request)
        {
            List<LiveOpsEventDiagnostic> generalDiagnostics;

            MetaDictionary<MetaGuid, LiveOpsEventOccurrence> importedOccurrences;
            MetaDictionary<MetaGuid, LiveOpsEventSpec> importedSpecs;

            List<ImportLiveOpsEventsResponse.EventResult> eventResults;

            try
            {
                LiveOpsEventExportImport.ImportConflictPolicy conflictPolicy = request.ConflictPolicy;
                LiveOpsEventExportImport.Package package = request.Package;
                MetaTime currentTime = MetaTime.Now;

                generalDiagnostics = new List<LiveOpsEventDiagnostic>();

                if (package.PackageFormatVersion != 1)
                {
                    generalDiagnostics.Add(new LiveOpsEventDiagnostic(
                        LiveOpsEventDiagnostic.DiagnosticLevel.Error,
                        Invariant($"Unsupported export-import package format version {package.PackageFormatVersion} (only version 1 is supported).")));
                    return new ImportLiveOpsEventsResponse(isValid: false, generalDiagnostics, new List<ImportLiveOpsEventsResponse.EventResult>());
                }

                if (conflictPolicy != LiveOpsEventExportImport.ImportConflictPolicy.Disallow
                    && conflictPolicy != LiveOpsEventExportImport.ImportConflictPolicy.Overwrite
                    && conflictPolicy != LiveOpsEventExportImport.ImportConflictPolicy.KeepOld)
                {
                    generalDiagnostics.Add(new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Error, $"Unknown conflict policy {conflictPolicy}."));
                    return new ImportLiveOpsEventsResponse(isValid: false, generalDiagnostics, new List<ImportLiveOpsEventsResponse.EventResult>());
                }

                if (package.Events == null)
                {
                    generalDiagnostics.Add(new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Error, $"Events array cannot be null."));
                    return new ImportLiveOpsEventsResponse(isValid: false, generalDiagnostics, new List<ImportLiveOpsEventsResponse.EventResult>());
                }

                if (package.Events.Contains(null))
                {
                    generalDiagnostics.Add(new LiveOpsEventDiagnostic(LiveOpsEventDiagnostic.DiagnosticLevel.Error, $"Events array cannot contain null events."));
                    return new ImportLiveOpsEventsResponse(isValid: false, generalDiagnostics, new List<ImportLiveOpsEventsResponse.EventResult>());
                }

                HashSet<MetaGuid> seenOccurrenceIds = new();
                HashSet<MetaGuid> seenSpecIds = new();

                importedOccurrences = new MetaDictionary<MetaGuid, LiveOpsEventOccurrence>();
                importedSpecs = new MetaDictionary<MetaGuid, LiveOpsEventSpec>();

                eventResults = new List<ImportLiveOpsEventsResponse.EventResult>();

                foreach (LiveOpsEventExportImport.ExportedEvent ev in package.Events)
                {
                    LiveOpsEventDiagnostics diagnostics = new();

                    if (!seenOccurrenceIds.Add(ev.OccurrenceId))
                        diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Duplicate entry for occurrence id {ev.OccurrenceId}.");

                    if (!seenSpecIds.Add(ev.SpecId))
                        diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Duplicate entry for spec id {ev.SpecId}.");

                    LiveOpsEventExportImport.EventImportOutcome importOutcome;

                    LiveOpsEventOccurrence existingOccurrenceMaybe = _state.LiveOpsEvents.EventOccurrences.GetValueOrDefault(ev.OccurrenceId);

                    bool specAlreadyExists = _state.LiveOpsEvents.EventSpecs.ContainsKey(ev.SpecId);

                    if (existingOccurrenceMaybe != null)
                    {
                        if (conflictPolicy == LiveOpsEventExportImport.ImportConflictPolicy.Disallow)
                        {
                            diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Event {ev.OccurrenceId} already exists in this environment (conflict policy is {conflictPolicy}).");
                            importOutcome = LiveOpsEventExportImport.EventImportOutcome.ConflictError;
                        }
                        else
                        {
                            if (ev.SpecId == existingOccurrenceMaybe.DefiningSpecId)
                            {
                                if (conflictPolicy == LiveOpsEventExportImport.ImportConflictPolicy.Overwrite)
                                    importOutcome = LiveOpsEventExportImport.EventImportOutcome.OverwriteExisting;
                                else if (conflictPolicy == LiveOpsEventExportImport.ImportConflictPolicy.KeepOld)
                                    importOutcome = LiveOpsEventExportImport.EventImportOutcome.IgnoreDueToExisting;
                                else
                                    throw new MetaAssertException($"Unhandled conflict policy {conflictPolicy}");
                            }
                            else
                            {
                                diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Event {ev.OccurrenceId} already exists in this environment, but with a different spec id ({existingOccurrenceMaybe.DefiningSpecId}) than imported event.");
                                importOutcome = LiveOpsEventExportImport.EventImportOutcome.ConflictError;
                            }
                        }
                    }
                    else
                    {
                        if (specAlreadyExists)
                        {
                            diagnostics.AddError(scope: LiveOpsEventDiagnosticScope.General, message: $"Spec {ev.SpecId} already exists in this environment, but for a different occurrence than the imported event.");
                            importOutcome = LiveOpsEventExportImport.EventImportOutcome.ConflictError;
                        }
                        else
                            importOutcome = LiveOpsEventExportImport.EventImportOutcome.CreateNew;
                    }

                    LiveOpsEventSettings settings;
                    try
                    {
                        settings = ev.DecodeSettings();
                    }
                    catch (Exception ex)
                    {
                        diagnostics.AddError(LiveOpsEventDiagnosticScope.General, $"Failed to deserialize event: {ex.Message}");
                        settings = null;
                    }

                    LiveOpsEventSpec specMaybe = null;
                    LiveOpsEventOccurrence occurrenceMaybe = null;

                    if (settings != null)
                    {
                        LiveOpsEventDiagnostics settingsDiagnostics = new();

                        if (existingOccurrenceMaybe == null || ev.SpecId != existingOccurrenceMaybe.DefiningSpecId)
                        {
                            ValidateEventSettings(settingsDiagnostics, settings);

                            if (!settingsDiagnostics.HasErrors())
                            {
                                specMaybe = new LiveOpsEventSpec(
                                    specId: ev.SpecId,
                                    editVersion: 0,
                                    settings,
                                    createdAt: currentTime);

                                (MetaScheduleTimeMode timeMode, LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe) = CreateSingleOccasionScheduleAssumeNoRecurrence(settings);

                                occurrenceMaybe = new LiveOpsEventOccurrence(
                                    occurrenceId: ev.OccurrenceId,
                                    editVersion: 0,
                                    definingSpecId: specMaybe.SpecId,
                                    timeMode,
                                    utcScheduleOccasionMaybe,
                                    specMaybe.Settings.EventParams,
                                    explicitlyConcludedAt: null,
                                    LiveOpsEventOccurrenceTimeState.Initial);
                                occurrenceMaybe = occurrenceMaybe.CopyWithUpdatedTimeState(currentTime);
                            }
                        }
                        else
                        {
                            // \todo #liveops-event #update-restrictions

                            LiveOpsEventSpec existingSpec = _state.LiveOpsEvents.EventSpecs[ev.SpecId];
                            ValidateEventUpdate(settingsDiagnostics, existingOccurrenceMaybe, existingSpec, settings, currentTime);

                            if (!settingsDiagnostics.HasErrors())
                            {
                                specMaybe = new LiveOpsEventSpec(
                                    specId: existingSpec.SpecId,
                                    editVersion: existingSpec.EditVersion + 1,
                                    settings,
                                    existingSpec.CreatedAt);

                                (MetaScheduleTimeMode timeMode, LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe) = CreateSingleOccasionScheduleAssumeNoRecurrence(settings);

                                occurrenceMaybe = new LiveOpsEventOccurrence(
                                    occurrenceId: existingOccurrenceMaybe.OccurrenceId,
                                    editVersion: existingOccurrenceMaybe.EditVersion + 1,
                                    definingSpecId: existingOccurrenceMaybe.DefiningSpecId,
                                    timeMode,
                                    utcScheduleOccasionMaybe,
                                    specMaybe.Settings.EventParams,
                                    explicitlyConcludedAt: existingOccurrenceMaybe.ExplicitlyConcludedAt,
                                    existingOccurrenceMaybe.TimeState);
                                occurrenceMaybe = occurrenceMaybe.CopyWithUpdatedTimeState(currentTime);
                                // \todo #liveops-event-time-state #update-restrictions Sanity check that the current phase (in any time zone) did not go backwards, comparing old vs new UtcScheduleOccasionMaybe.
                            }
                        }

                        diagnostics.AddAll(settingsDiagnostics);

                        bool wouldCreateOrUpdateEvent = importOutcome == LiveOpsEventExportImport.EventImportOutcome.CreateNew
                                                        || importOutcome == LiveOpsEventExportImport.EventImportOutcome.OverwriteExisting;

                        if (wouldCreateOrUpdateEvent)
                        {
                            if (occurrenceMaybe != null && !importedOccurrences.ContainsKey(occurrenceMaybe.OccurrenceId))
                                importedOccurrences.Add(occurrenceMaybe.OccurrenceId, occurrenceMaybe);

                            if (specMaybe != null && !importedSpecs.ContainsKey(specMaybe.SpecId))
                                importedSpecs.Add(specMaybe.SpecId, specMaybe);
                        }
                    }

                    // For erroneous events, always report an error outcome - either ConflictError if appropriate, or GeneralError otherwise.
                    if (diagnostics.HasErrors() && importOutcome != LiveOpsEventExportImport.EventImportOutcome.ConflictError)
                        importOutcome = LiveOpsEventExportImport.EventImportOutcome.GeneralError;

                    eventResults.Add(new ImportLiveOpsEventsResponse.EventResult(
                        isValid: !diagnostics.HasErrors(),
                        occurrenceId: ev.OccurrenceId,
                        specId: ev.SpecId,
                        diagnostics,
                        settings,
                        occurrenceMaybe,
                        specMaybe,
                        importOutcome));
                }

                {
                    IEnumerable<LiveOpsEventOccurrence> existingAndOverwrittenOccurrences = _state.LiveOpsEvents.EventOccurrences.Values.Select(occ => importedOccurrences.GetValueOrDefault(occ.OccurrenceId, occ));
                    IEnumerable<LiveOpsEventOccurrence> newlyAddedOccurrences = importedOccurrences.Values.Where(occ => !_state.LiveOpsEvents.EventOccurrences.ContainsKey(occ.OccurrenceId));
                    IEnumerable<LiveOpsEventOccurrence> allOccurrences = existingAndOverwrittenOccurrences.Concat(newlyAddedOccurrences);

                    foreach (ImportLiveOpsEventsResponse.EventResult eventResult in eventResults)
                    {
                        if (!importedOccurrences.ContainsKey(eventResult.OccurrenceId))
                            continue;

                        CheckEventOverlaps(eventResult.Diagnostics, eventResult.SpecMaybe.Settings, currentTime, allOccurrences.Where(occ => occ.OccurrenceId != eventResult.OccurrenceId));
                    }
                }

                if (generalDiagnostics.Any(diag => diag.Level == LiveOpsEventDiagnostic.DiagnosticLevel.Error)
                 || eventResults.Any(res => res.Diagnostics.HasErrors()))
                {
                    return new ImportLiveOpsEventsResponse(isValid: false, generalDiagnostics, eventResults);
                }

                // Sanity assertions (should never fail) - presence in importedSpecs and importedOccurrences must be consistent with event's prior existence and conflictPolicy.
                foreach (LiveOpsEventExportImport.ExportedEvent ev in package.Events)
                {
                    bool eventAlreadyExists = _state.LiveOpsEvents.EventOccurrences.ContainsKey(ev.OccurrenceId);
                    bool willKeepOld = eventAlreadyExists && conflictPolicy == LiveOpsEventExportImport.ImportConflictPolicy.KeepOld;

                    if (willKeepOld)
                    {
                        if (importedSpecs.ContainsKey(ev.SpecId))
                            throw new InvalidEntityAsk($"Internal error: expected imported spec {ev.SpecId} to be ignored due to conflict, but it wasn't");
                        if (importedOccurrences.ContainsKey(ev.OccurrenceId))
                            throw new InvalidEntityAsk($"Internal error: expected imported occurrence {ev.OccurrenceId} to be ignored due to conflict, but it wasn't");
                    }
                    else
                    {
                        if (!importedSpecs.ContainsKey(ev.SpecId))
                            throw new InvalidEntityAsk($"Internal error: expected spec {ev.SpecId} to get imported, but it wasn't");
                        if (!importedOccurrences.ContainsKey(ev.OccurrenceId))
                            throw new InvalidEntityAsk($"Internal error: expected occurrence {ev.OccurrenceId} to get imported, but it wasn't");
                    }
                }

                if (request.ValidateOnly)
                {
                    return new ImportLiveOpsEventsResponse(isValid: true, generalDiagnostics, eventResults);
                }
            }
            catch (EntityAskRefusal)
            {
                throw;
            }
            catch (Exception ex)
            {
                // \note Not expected to happen - being defensive.
                _log.Error(ex, $"Unexpected error in {nameof(HandleImportLiveOpsEventsRequestAsync)}");
                throw new InvalidEntityAsk("Internal error, see server logs");
            }

            // \note These are either overwrites or new additions, depending on whether each event already existed.
            //       The earlier loop that populated importedOccurrences and importedSpecs took care of doing
            //       the appropriate validations and adjustments (like bumping EditVersion).
            foreach (LiveOpsEventSpec spec in importedSpecs.Values)
                _state.LiveOpsEvents.SetSpec(spec);
            foreach (LiveOpsEventOccurrence occurrence in importedOccurrences.Values)
                _state.LiveOpsEvents.SetOccurrence(occurrence);

            await PersistStateIntermediate();
            TimelineRecordLiveOpsEventUpdates(importedOccurrences.Values.Select(occ => occ.OccurrenceId));

            PublishMessage(EntityTopic.Member, new SetLiveOpsEventsMessage(
                importedOccurrences.Values.ToList(),
                importedSpecs.Values.ToList()));

            return new ImportLiveOpsEventsResponse(isValid: true, generalDiagnostics, eventResults);
        }

        List<LiveOpsEventOccurrence> GetRelatedOccurrences(LiveOpsEventContent content)
        {
            return _state.LiveOpsEvents.EventOccurrences.Values
                .Where(occ => occ.EventParams.Content.ShouldWarnAboutOverlapWith(content))
                .ToList();
        }

        List<LiveOpsEventSpec> GetSpecsForOccurrences(IEnumerable<LiveOpsEventOccurrence> occurrences)
        {
            IEnumerable<MetaGuid> specIds =
                occurrences
                .Select(occ => occ.DefiningSpecId)
                .Distinct();

            return specIds
                .Select(specId => _state.LiveOpsEvents.EventSpecs[specId])
                .ToList();
        }

        static LiveOpsEventDiagnostics ResolveUneditableEventParams(LiveOpsEventOccurrence existingOccurrence)
        {
            LiveOpsEventDiagnostics diagnostics = new();

            diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.EventType, message: "An existing event's type cannot be changed.");

            LiveOpsEventPhase mostAdvancedPhase = existingOccurrence.TimeState.GetMostAdvancedPhase();

            if (existingOccurrence.UtcScheduleOccasionMaybe != null)
            {
                string schedulePartialUneditabilityDescription = null;

                bool isLocalTime = existingOccurrence.ScheduleTimeMode == MetaScheduleTimeMode.Local;
                string localTimeCaveatTextMaybe = isLocalTime ? " in some time zones" : "";

                if (LiveOpsEventPhase.PhasePrecedes(LiveOpsEventPhase.NotStartedYet, mostAdvancedPhase))
                {
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.ScheduleIsPlayerLocalTime, message: null);
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.SchedulePreviewDuration, message: null);
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledStartTime, message: null);

                    string phaseText = mostAdvancedPhase == LiveOpsEventPhase.Preview
                                       ? "is already in the preview phase"
                                       : "has already started";

                    schedulePartialUneditabilityDescription = $"Some parts of the schedule can no longer be edited, because the event {phaseText}{localTimeCaveatTextMaybe}.";
                }

                if (LiveOpsEventPhase.PhasePrecedes(LiveOpsEventPhase.NormalActive, mostAdvancedPhase))
                {
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.UseSchedule, message: null);

                    schedulePartialUneditabilityDescription = $"Some parts of the schedule can no longer be edited, because the event is already past the active phase{localTimeCaveatTextMaybe}.";
                }

                if (LiveOpsEventPhase.PhasePrecedesOrIsEqual(LiveOpsEventPhase.EndingSoon, mostAdvancedPhase))
                {
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.ScheduleEndingSoonDuration, message: null);
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.ScheduleEnabledEndTime, message: null);

                    string phaseText = mostAdvancedPhase == LiveOpsEventPhase.EndingSoon
                                       ? "is already in the ending-soon phase"
                                       : "has already ended";

                    schedulePartialUneditabilityDescription = $"Some parts of the schedule can no longer be edited, because the event {phaseText}{localTimeCaveatTextMaybe}.";
                }

                if (LiveOpsEventPhase.PhasePrecedes(LiveOpsEventPhase.Review, mostAdvancedPhase))
                {
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.ScheduleReviewDuration, message: null);
                    schedulePartialUneditabilityDescription = $"The schedule can no longer be edited, because the event has already ended{localTimeCaveatTextMaybe}.";
                }

                if (schedulePartialUneditabilityDescription != null)
                    diagnostics.AddInfo(scope: LiveOpsEventDiagnosticScope.UseSchedule, message: schedulePartialUneditabilityDescription);
            }
            else
            {
                if (LiveOpsEventPhase.PhasePrecedes(LiveOpsEventPhase.NormalActive, mostAdvancedPhase))
                {
                    diagnostics.AddUneditable(scope: LiveOpsEventDiagnosticScope.UseSchedule, message: null);
                    diagnostics.AddInfo(scope: LiveOpsEventDiagnosticScope.UseSchedule, message: "A schedule cannot be added, because this event is no longer active.");
                }
            }

            return diagnostics;
        }

        // \todo #liveops-event Needs to be rethought when recurring liveops events are supported.
        //       This is only good for MVP where each specs correspond 1-to-1 with occurrences.
        //
        //       Currently we're assuming a single-occasion schedule (or no schedule at all).
        //       In this case exactly 1 event occurrence is created from the spec, its schedule
        //       occasion matching the single occasion of the spec's schedule.
        static (MetaScheduleTimeMode TimeMode, LiveOpsEventScheduleOccasion UtcScheduleOccasionMaybe) CreateSingleOccasionScheduleAssumeNoRecurrence(LiveOpsEventSettings settings)
        {
            MetaScheduleTimeMode timeMode;
            LiveOpsEventScheduleOccasion utcScheduleOccasionMaybe;
            if (settings.ScheduleMaybe == null)
            {
                timeMode = MetaScheduleTimeMode.Utc;
                utcScheduleOccasionMaybe = null;
            }
            else
            {
                MetaScheduleBase schedule = settings.ScheduleMaybe;

                timeMode = settings.ScheduleMaybe.TimeMode;

                MetaScheduleOccasion? metaOccasionMaybe = schedule.TryGetNextOccasion(new PlayerLocalTime(
                    time: MetaTime.Epoch,
                    utcOffset: MetaDuration.Zero));

                if (!metaOccasionMaybe.HasValue)
                {
                    // Not expected to happen.
                    utcScheduleOccasionMaybe = null;
                }
                else
                {
                    MetaScheduleOccasion metaOccasion = metaOccasionMaybe.Value;

                    MetaDictionary<LiveOpsEventPhase, MetaTime> phaseSequence = new();

                    if (metaOccasion.VisibleRange.Start != metaOccasion.EnabledRange.Start)
                        phaseSequence.Add(LiveOpsEventPhase.Preview, metaOccasion.VisibleRange.Start);

                    phaseSequence.Add(LiveOpsEventPhase.NormalActive, metaOccasion.EnabledRange.Start);

                    if (metaOccasion.EndingSoonStartsAt != metaOccasion.EnabledRange.End)
                        phaseSequence.Add(LiveOpsEventPhase.EndingSoon, metaOccasion.EndingSoonStartsAt);

                    if (metaOccasion.EnabledRange.End != metaOccasion.VisibleRange.End)
                        phaseSequence.Add(LiveOpsEventPhase.Review, metaOccasion.EnabledRange.End);

                    phaseSequence.Add(LiveOpsEventPhase.Concluded, metaOccasion.VisibleRange.End);

                    utcScheduleOccasionMaybe = new LiveOpsEventScheduleOccasion(phaseSequence);
                }
            }

            return (timeMode, utcScheduleOccasionMaybe);
        }

        static bool OccurrenceVisibilityOverlapsWithTimeRange(LiveOpsEventOccurrence occurrence, MetaTime? requestedStartTime, MetaTime? requestedEndTime, MetaTime currentTime)
        {
            (MetaTime occurrenceStartTime, MetaTime? occurrenceEndTime) = LiveOpsEventServerUtil.GetVisibleTimeRange(occurrence, currentTime);

            occurrenceEndTime ??= MetaTime.FromMillisecondsSinceEpoch(long.MaxValue);

            requestedStartTime ??= MetaTime.FromMillisecondsSinceEpoch(long.MinValue);
            requestedEndTime ??= MetaTime.FromMillisecondsSinceEpoch(long.MaxValue);

            if (occurrenceEndTime.Value <= requestedStartTime.Value)
                return false;

            if (requestedEndTime.Value <= occurrenceStartTime)
                return false;

            return true;
        }
    }
}
