/**
 * Types for instant events in the timeline.
 */
import type { TimelineItem, TimelineItemDetails, TimelineItemRenderInfo } from './MEventTimelineTypes'

// Data ---------------------------------------------------------------------------------------------------------------

/**
 * Timeline item.
 */
export interface TimelineItemInstantEvent extends TimelineItem {
  itemType: 'instantEvent'
  hierarchy: {
    parentId: string
  }
  renderData: {
    /**
     * The exact date and time of the event in ISO format.
     */
    instantIsoString: string
    color?: string
  }
}

// Render Info --------------------------------------------------------------------------------------------------------

/**
 * Render info.
 */
export interface TimelineItemRenderInfoInstantEvent extends TimelineItemRenderInfo {
  styles: {
    backgroundColor: string
    left: string
    width: string
    height: string
  }
}

// Details -------  -----------------------------------------------------------------------------------------------------

/**
 * Details.
 */
export interface TimelineItemDetailsInstantEvent extends TimelineItemDetails {
  /**
   * Set the type of the event for easier type narrowing.
   */
  itemType: 'instantEvent'

  /**
   * Type specific data for instant events.
   */
  details: {
    timestamp: string
    message: string
    logEventId: string
    source: string
    sourceType: string
    exception: string
    stackTrace: string
    id: string
  }
}
