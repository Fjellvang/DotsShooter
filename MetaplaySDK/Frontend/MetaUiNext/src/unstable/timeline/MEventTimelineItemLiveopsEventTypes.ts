/**
 * Types for liveops events in the timeline.
 */
import type { DateTime } from 'luxon'

import type { TimelineItem, TimelineItemDetails, TimelineItemRenderInfo } from './MEventTimelineTypes'

// Data ---------------------------------------------------------------------------------------------------------------

export interface TimelineItemLiveopsEvent extends TimelineItem {
  itemType: 'liveopsEvent'
  hierarchy: {
    parentId: string
  }
  metadata: {
    displayName: string
    color?: string // do we want color to be optional?
  }
  renderData: {
    /**
     * Server type of the event.
     */
    // liveOpsEventType: string

    /**
     * Where to render the event in the timeline.
     */
    timelinePosition: {
      /**
       * Optional: Start date and time of the event in ISO format. Either what has been scheduled or when the event became first active.
       * Draft events may not have a start time and need to be rendered differently in that case.
       */
      startInstantIsoString?: string
      /**
       * Optional: End date and time of the event in ISO format. Either what has been scheduled or when the event was concluded.
       * It's common for events to not have an end time, in which case they are considered to be active indefinitely.
       */
      endInstantIsoString?: string
    }

    /**
     * Top-level state of the event.
     */
    state: TimelineLiveopsEventState

    /**
     * Optional: Schedule options of the event.
     */
    schedule: TimelineItemUtcSchedule | TimelineItemPlayerLocalSchedule | undefined

    isLocked: boolean

    /**
     * Optional: Has targeting enabled.
     */
    isTargeted: boolean

    /**
     * Flag for if this event is a recurring instance.
     */
    isRecurring: boolean

    isImmutable: boolean

    /**
     * Optional: Number of unique players who have participated in the event.
     * Events that have not activated yet or are still in draft state may not have this data.
     */
    participantCount?: number
  }
}

export type TimelineLiveopsEventState = 'draft' | 'scheduled' | 'active' | 'concluded'

export type TimelineSchedulePhase = 'preview' | 'active' | 'endingSoon' | 'review'

/**
 * Schedule options for events that are scheduled in UTC time.
 *
 * Note: The event is always drawn from the start and end instants passed to the sibling `timelinePosition`.
 * We assume that those instants have any possible preview and review times baked in.
 */
export interface TimelineItemUtcSchedule {
  /**
   * Time mode of the schedule.
   */
  timeMode: 'utc'
  /**
   * Optional: Current phase of the event. Leave `undefined` if the event is not active.
   */
  currentPhase?: TimelineSchedulePhase
  /**
   * Optional: Duration of the preview phase in ISO format. Leave `undefined` if the event has no preview phase.
   */
  previewDurationIsoString?: string
  /**
   * Optional: Duration of the review phase in ISO format. Leave `undefined` if the event has no review phase.
   */
  reviewDurationIsoString?: string
}

/**
 * Schedule options for events that are scheduled in the local time of the player.
 *
 * Note: The event is always drawn from the start and end instants passed to the sibling `timelinePosition`.
 * We assume that the server has decided the earliest possible instant to activate the event and the latest possible instant to conclude it in UTC time.
 */
export interface TimelineItemPlayerLocalSchedule {
  /**
   * Time mode of the schedule.
   */
  timeMode: 'playerLocal'
  /**
   * Optional: The player local time when the event starts.
   */
  plainTimeStartInstantIsoString?: string
  /**
   * Optional: The player local time when the event ends.
   */
  plainTimeEndInstantIsoString?: string
  /**
   * Optional: Duration of the preview phase in ISO format. Leave `undefined` if the event has no preview phase.
   */
  previewDurationIsoString?: string
  /**
   * Optional: Duration of the review phase in ISO format. Leave `undefined` if the event has no review phase.
   */
  reviewDurationIsoString?: string
}

// Render Info --------------------------------------------------------------------------------------------------------

export interface TimelineItemRenderInfoLiveopsEvent extends TimelineItemRenderInfo {
  id: string
  eventState: TimelineLiveopsEventState
  eventStartInstant: DateTime
  eventPreviewEndInstant?: DateTime
  eventEndInstant: DateTime
  eventReviewStartInstant?: DateTime
  plainTimeContainerStyles?: {
    left: string
    width: string
    height: string
    backgroundColor: string
    border?: string
    filter?: string
    zIndex?: number
  }
  containerStyles: {
    left: string
    width: string
    height: string
    backgroundColor: string
    border?: string
    filter?: string
    zIndex?: number
  }
  previewStyles?: {
    flexBasis: string
    background: string
  }
  activePhasesStyles: {
    backgroundColor: string
    borderRadius: string
  }
  textContainerStyles: {
    left: string
    width: string
    height: string
    color: string
    fill: string
    paddingTop: string
    paddingBottom: string
    zIndex?: number
  }
  reviewStyles?: {
    flexBasis: string
    background: string
  }
  currentPhase?: TimelineSchedulePhase
  displayName: string
  colorInHex: string
  isLocked: boolean
  isTargeted: boolean
  isRecurring: boolean
  isGroupExpanded: boolean
  participantCount?: number
}

// Details ------------------------------------------------------------------------------------------------------------

export interface TimelineItemDetailsLiveopsEvent extends TimelineItemDetails {
  /**
   * Set the type of the event for easier type narrowing.
   */
  itemType: 'liveopsEvent'

  /**
   * Type specific data for liveops events.
   */
  details: {
    /**
     * Detailed parameters of the event.
     * TODO: this type should be LiveOpsEventParams, but that's in the Core module, which MetaUiNext doesn't want to depend on.
     */
    eventId: string
    eventTypeName: string
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    eventParams: any
    currentPhase: string
    nextPhase: string | null
    nextPhaseTime: string | null
    participantCount: number
  }
}
