// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { TargetConditionContent } from './components/mails/mailUtils'

/**
 *
 */
export type LiveOpsEventContent = Record<string, unknown>

/**
 *
 */
export interface LiveOpsEventTemplateInfo {
  content: LiveOpsEventContent
  defaultDisplayName: string
  defaultDescription: string
}

/**
 *
 */
export interface LiveOpsEventTypeInfo {
  /**
   * C# type.
   */
  contentClass: string
  /**
   * Title to display for this item.
   */
  eventTypeName: string
  /**
   *
   */
  templates: Record<string, LiveOpsEventTemplateInfo>
}

export interface LiveOpsEventScheduleInfo {
  isPlayerLocalTime: boolean
  previewDuration: string
  enabledStartTime: string
  endingSoonDuration: string
  enabledEndTime: string
  reviewDuration: string
}

export interface LiveOpsEventParams {
  displayName: string
  description: string
  color: string | null
  eventType: string
  templateId: string
  content: LiveOpsEventContent
  useSchedule: boolean
  schedule: LiveOpsEventScheduleInfo | null
  targetPlayers: string[] | null
  targetCondition: TargetConditionContent | null
}

export type LiveOpsEventPhase = 'NotYetStarted' | 'InPreview' | 'Active' | 'EndingSoon' | 'InReview' | 'Ended'

export interface LiveOpsEventBriefInfo {
  eventId: string
  createdAt: string
  eventTypeName: string
  displayName: string
  description: string
  // TODO: Implement these (not implemented server-side yet)
  // sequenceNumber: number
  // tags: string[] | null
  templateId: string
  useSchedule: boolean
  schedule: LiveOpsEventScheduleInfo | null
  currentPhase: LiveOpsEventPhase
  nextPhase: LiveOpsEventPhase | null
  nextPhaseTime: string | null
  hasEnded: boolean
  startTime: string | null
  endTime: string | null
}

export interface LiveOpsEventDetailsInfo {
  eventId: string
  eventParams: LiveOpsEventParams
  createdAt: string
  // TODO: Implement these (not implemented server-side yet)
  // sequenceNumber: number
  // tags: string[] | null
  relatedEvents: LiveOpsEventBriefInfo[]
  currentPhase: LiveOpsEventPhase
  nextPhase: LiveOpsEventPhase | null
  nextPhaseTime: string | null
  participantCount: number
}

export interface GetLiveOpsEventsListApiResult {
  upcomingEvents: LiveOpsEventBriefInfo[]
  ongoingAndPastEvents: LiveOpsEventBriefInfo[]
}

export type LiveOpsEventDiagnosticLevel = 'Error' | 'Warning' | 'Info' | 'Uneditable'

export interface LiveOpsEventDiagnostic {
  level: LiveOpsEventDiagnosticLevel
  /**
   * Note: null message is sometimes used for Uneditable-level diagnostics, when the relevant message is communicated by other means.
   * For example: when parts of a schedule are uneditable due to the event already being active, the uneditability explanation
   * is communicated as an Info-level message on the useSchedule component, instead of as individual messages on the schedule parts.
   */
  message: string | null
}

export type LiveOpsEventDiagnosticComponentPath =
  | 'eventType'
  | 'content'
  | 'displayName'
  | 'useSchedule'
  | 'schedule'
  | 'schedule.isPlayerLocalTime'
  | 'schedule.previewDuration'
  | 'schedule.enabledStartTime'
  | 'schedule.endingSoonDuration'
  | 'schedule.enabledEndTime'
  | 'schedule.reviewDuration'

export type LiveOpsDiagnostics = Record<LiveOpsEventDiagnosticComponentPath, LiveOpsEventDiagnostic[]>

export interface CreateLiveOpsEventRequest {
  validateOnly: boolean
  parameters: LiveOpsEventParams
}

export interface CreateLiveOpsEventResponse {
  isValid: boolean
  eventId: string
  relatedEvents: LiveOpsEventBriefInfo[]
  diagnostics: LiveOpsDiagnostics
}

export interface UpdateLiveOpsEventRequest {
  validateOnly: boolean
  occurrenceId: string
  parameters: LiveOpsEventParams
}

export interface UpdateLiveOpsEventResponse {
  isValid: boolean
  relatedEvents: LiveOpsEventBriefInfo[]
  diagnostics: LiveOpsDiagnostics
}

export interface ExportLiveOpsEventsRequest {
  eventIds: string[]
}

export interface LiveOpsEventExportResult {
  eventId: string
  isValid: boolean
  error: string | null
  eventInfo: LiveOpsEventBriefInfo | null
}

export interface ExportLiveOpsEventsResponse {
  isValid: boolean
  eventResults: LiveOpsEventExportResult[]
  package: string
}

/**
 * What to do when trying to import an event with an id that already exists.
 */
export enum LiveOpsEventImportConflictPolicy {
  /**
   * Refuse to import if there is an existing event with the same id.
   */
  Disallow = 'Disallow',
  /**
   * The existing event on the server will be overwritten with the imported event.
   */
  Overwrite = 'Overwrite',
  /**
   * The existing event on the server will be kept.
   */
  KeepOld = 'KeepOld',
}

/**
 * Describes what happened (or would happen - depending on the validateOnly flag in the request) as the result of importing a specific event.
 * The outcome can depend on the LiveOpsEventImportConflictPolicy given in the import request.
 */
export enum LiveOpsEventImportOutcome {
  /**
   * The event cannot be imported because of an event already exists with the same id.
   */
  ConflictError = 'ConflictError',
  /**
   * The event cannot be imported due to an error in the event (other than ConflictError), e.g. failure to deserialize the event's payload.
   */
  GeneralError = 'GeneralError',
  /**
   * The import will cause a new event to be created in this environment.
   */
  CreateNew = 'CreateNew',
  /**
   * The import will cause an existing event to be overwritten in this environment.
   */
  OverwriteExisting = 'OverwriteExisting',
  /**
   * The event will not be imported, because an event already exists with the same id.
   */
  IgnoreDueToExisting = 'IgnoreDueToExisting',
}

export interface ImportLiveOpsEventsRequest {
  validateOnly: boolean
  conflictPolicy: LiveOpsEventImportConflictPolicy
  package: string
}

export interface ImportLiveOpsEventsResponse {
  isValid: boolean
  generalDiagnostics: LiveOpsEventDiagnostic[]
  /**
   * Has entries corresponding to the request's package.events, in the same order
   */
  eventResults: LiveOpsEventImportResult[]
}

export interface LiveOpsEventImportResult {
  isValid: boolean
  outcome: LiveOpsEventImportOutcome
  diagnostics: Record<string, LiveOpsEventDiagnostic[]>
  eventId: string
  eventInfo: LiveOpsEventBriefInfo | null
}

export interface PlayerLiveOpsEventsInfo {
  lastRefreshedAt: string
  events: LiveOpsEventPerPlayerInfo[]
}

export interface LiveOpsEventPerPlayerInfo {
  eventId: string
  eventTypeName: string
  displayName: string
  description: string
  templateId: string
  /**
   * NOTE: In per-player data, phase is not guaranteed to be up to date with current time:
   * this data primarily comes from the player actor, where the phase is updated when player
   * logs in, as well as periodically. This means it can lag behind real time. In particular,
   * when looking at an offline player in the dashboard, the phase is not guaranteed to
   * update at all, as the player actor may not stay awake for long enough for the periodic
   * update to be run.
   * This concerns both currentTime as well as nextPhase and nextPhaseTime. So nextPhaseTime
   * may be in the past, in which case it means roughly "will enter next phase as soon as the
   * player  comes online" (or the periodic update happens to trigger).
   * This is not ideal, and we might change the server code in the future to run the update
   * more eagerly.
   */
  currentPhase: LiveOpsEventPhase
  nextPhase: LiveOpsEventPhase | null
  nextPhaseTime: string | null
}
