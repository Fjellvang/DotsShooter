/**
 * @fileoverview This file contains types for the event timeline.
 * Please note that all times and durations are ISO strings instead of Luxon objects so that the data does not need to be duplicated and transformed after being received from the server.
 */
import { TimelineItemHelper } from './MEventTimelineUtils'

// --------------------------------------------------------------------------------------------------------------------

/**
 * Full set of data and options needed to render the timeline.
 */
export interface TimelineData {
  /**
   * Beginning of the included data range.
   * todo: is this even needed?
   */
  startInstantIsoString: string
  /**
   * End of the included data range.
   * todo: is this even needed?
   */
  endInstantIsoString: string
  /**
   * Actual data items to show.
   */
  items: Record<string, TimelineItem>
}

// --------------------------------------------------------------------------------------------------------------------

/**
 * Types for all items that can appear on the timeline.
 */

/**
 * Base type for all items on the timeline.
 */
export interface TimelineItem {
  itemType: string
  version: number
  hierarchy: Record<string, unknown>
  metadata?: Record<string, unknown>
  renderData?: Record<string, unknown>
  isImmutable?: boolean
}
export const timelineItemHelper = new TimelineItemHelper<TimelineItem>('')

/**
 */
export interface TimelineItemRoot extends TimelineItem {
  itemType: 'root'
  hierarchy: {
    childIds: string[]
  }
  /* Removed but may need to go back?
    editPermission?: string
  */
}
export const timelineItemRootHelper = new TimelineItemHelper<TimelineItemRoot>('root')

/**
 * A single section in the timeline that contains groups of rows.
 * Sections exist to make organization easier when you have a large amount of groups.
 */
export interface TimelineItemSection extends TimelineItem {
  itemType: 'section'
  hierarchy: {
    parentId: string
    childIds: string[]
  }
  metadata: {
    displayName: string
  }
  renderData: {
    cannotRemoveReason: string | null
  }
  /* Removed but may need to go back?
  isLocked: boolean
  isImmutable: boolean
  */
}
export const timelineItemSectionHelper = new TimelineItemHelper<TimelineItemSection>('section')

/**
 * A single group in the timeline that contains rows of events.
 * Groups can be collapsed (merging all contained rows onto one row) to save on vertical space and to make it easier to compare two groups side by side.
 */
export interface TimelineItemGroup extends TimelineItem {
  itemType: 'group'
  hierarchy: {
    parentId: string
    childIds: string[]
  }
  metadata: {
    displayName: string
    color?: string
  }
  renderData: {
    cannotRemoveReason: string | null
  }
  /* Removed but may need to go back?
  isLocked: boolean
  isImmutable: boolean
  */
}
export const timelineItemGroupHelper = new TimelineItemHelper<TimelineItemGroup>('group')

/**
 * A single row in the timeline that contains events.
 * Rows can be edited independently from other rows or any event data. This makes it possible to both move rows between groups and to move events between rows.
 */
export interface TimelineItemRow extends TimelineItem {
  itemType: 'row'
  hierarchy: {
    parentId: string
    childIds: string[]
  }
  metadata: {
    displayName: string
  }
  renderData: {
    cannotRemoveReason: string | null
  }
  /* Removed but may need to go back?
  isLocked: boolean
  isImmutable: boolean
  */
}
export const timelineItemRowHelper = new TimelineItemHelper<TimelineItemRow>('row')

// --------------------------------------------------------------------------------------------------------------------

/**
 * A single event as rendered in the timeline.
 */
export interface TimelineItemRenderInfo {
  _?: undefined
}

// --------------------------------------------------------------------------------------------------------------------

/**
 * Common properties for all timeline items.
 * These details are used to build the inspector view for the item.
 */
export interface TimelineItemDetails {
  /**
   * Type of the item.
   */
  itemType: string

  /**
   * Display name of the item.
   */
  displayName: string

  /**
   * Optional: Description of the item.
   */
  description?: string

  /*
   * Common data. Will be filled with type specific data.
   */
  details: Record<string, unknown>
}

/**
 * Details for a section.
 */
export interface TimelineItemDetailsSection extends TimelineItemDetails {
  /**
   * Set the type of the event for easier type narrowing.
   */
  itemType: 'section'

  /**
   * Type specific data for sections.
   */
  // details: {}
}

/**
 * Details for a group.
 */
export interface TimelineItemDetailsGroup extends TimelineItemDetails {
  /**
   * Set the type of the event for easier type narrowing.
   */
  itemType: 'group'

  /**
   * Type specific data for groups.
   */
  details: {
    color: string | undefined
  }
}

/**
 * Details for a row.
 */
export interface TimelineItemDetailsRow extends TimelineItemDetails {
  /**
   * Set the type of the event for easier type narrowing.
   */
  itemType: 'row'

  /**
   * Type specific data for rows.
   */
  // details: {}
}
