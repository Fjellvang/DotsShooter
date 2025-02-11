import { DateTime, Duration } from 'luxon'

import { defaultItemColors } from './MEventTimelineColorUtils'
import type {
  TimelineItemRenderInfoInstantEvent,
  TimelineItemInstantEvent,
} from './MEventTimelineItemInstantEventTypes'
import type {
  TimelineItemRenderInfoLiveopsEvent,
  TimelineItemLiveopsEvent,
} from './MEventTimelineItemLiveopsEventTypes'
import type { TimelineData, TimelineItem, TimelineItemRenderInfo } from './MEventTimelineTypes'
import {
  findRoot,
  calculateGroupsFromSection,
  calculateItemsFromRow,
  calculateRowsFromGroup,
  calculateSectionsFromRoot,
} from './MEventTimelineUtils'

export const sectionHeightInRem = 2
export const groupHeightInRem = 1.5
export const rowHeightInRem = 2.75

/**
 * Convert REM units to pixels.
 */
export function remToPixels(rem: number): number {
  return rem * parseFloat(getComputedStyle(document.documentElement).fontSize)
}

/**
 * Convert pixels to REM units.
 */
export function pixelsToRem(pixels: number): number {
  return pixels / parseFloat(getComputedStyle(document.documentElement).fontSize)
}

/**
 * Utility function to brighten and de-saturate a color like it had a white, variable opacity overlay.
 * @param colorHex The color in hexadecimal format.
 * @param strength The strength of the washed out effect, ranging from 0 to 1. Default is 0.5.
 * @returns The washed out color in RGB format.
 */
export function getWashedColor(colorHex: string, strength = 0.5): string {
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const [r, g, b] = colorHex.match(/\w\w/g)!.map((x) => parseInt(x, 16))
  const r2 = Math.round((255 - r) * strength + r)
  const g2 = Math.round((255 - g) * strength + g)
  const b2 = Math.round((255 - b) * strength + b)
  return `rgb(${r2}, ${g2}, ${b2})`
}

/**
 * Function that takes in the timeline data object and returns the same thing but with only the items that are visible in the current timeline view.
 * Also sorts the data in individual rows by start time while at it.
 */
export function filterTimelineDataForVisibleRange(
  timelineData: TimelineData,
  firstVisibleInstant: DateTime,
  lastVisibleInstant: DateTime
): TimelineData {
  return timelineData
  /*
  return {
    ...timelineData,
    sections: timelineData.sections.map((section) => {
      return {
        ...section,
        groups: section.groups.map((group) => {
          return {
            ...group,
            rows: group.rows.map((row) => {
              return {
                ...row,
                items: row.items
                  .filter((item) => {
                    let itemStart
                    let itemEnd

                    if (item.timelineItemType === 'liveopsEvent') {
                      itemStart = getTimelineLiveopsEventStartInstant(item)
                      itemEnd = getTimelineLiveopsEventEndInstant(
                        item,
                        DateTime.fromISO(timelineData.endInstantIsoString)
                      )
                      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                    } else if (item.timelineItemType === 'instantEvent') {
                      const instantEvent: TimelineInstantEventData = item
                      itemStart = DateTime.fromISO(instantEvent.data.instantIsoString)
                      itemEnd = DateTime.fromISO(instantEvent.data.instantIsoString)
                    } else {
                      throw new Error('Unknown timeline item type.')
                    }

                    return itemStart <= lastVisibleInstant && itemEnd >= firstVisibleInstant
                  })
                  .sort((a, b) => {
                    // Sort items by start time. This makes it easier to render overlapping events.
                    // TODO: should this sorting happen in source data? Probably?
                    let startInstantInMsA = 0
                    let startInstantInMsB = 0

                    if (a.timelineItemType === 'liveopsEvent') {
                      startInstantInMsA = a.data.timelinePosition.startInstantIsoString
                        ? DateTime.fromISO(a.data.timelinePosition.startInstantIsoString).toMillis()
                        : 0
                      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                    } else if (a.timelineItemType === 'instantEvent') {
                      startInstantInMsA = a.data.instantIsoString
                        ? DateTime.fromISO(a.data.instantIsoString).toMillis()
                        : 0
                    }

                    if (b.timelineItemType === 'liveopsEvent') {
                      startInstantInMsB = b.data.timelinePosition.startInstantIsoString
                        ? DateTime.fromISO(b.data.timelinePosition.startInstantIsoString).toMillis()
                        : 0
                      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                    } else if (b.timelineItemType === 'instantEvent') {
                      startInstantInMsB = b.data.instantIsoString
                        ? DateTime.fromISO(b.data.instantIsoString).toMillis()
                        : 0
                    }

                    return startInstantInMsA - startInstantInMsB
                  }),
              }
            }),
          }
        }),
      }
    }),
  }
    */
}

// eslint-disable-next-line @typescript-eslint/max-params
export function getRenderInfosForVisibleRange(
  visibleTimelineData: TimelineData,
  timelineStartInstant: DateTime,
  timelineEndInstant: DateTime,
  firstVisibleInstant: DateTime,
  lastVisibleInstant: DateTime,
  timelineDayWidthInRem: number,
  expandedGroups: string[]
): Record<string, TimelineItemRenderInfo> {
  const allVisibleRenderInfos: Record<string, TimelineItemRenderInfo> = {}

  const root = findRoot(visibleTimelineData.items)

  for (const [, section] of calculateSectionsFromRoot(visibleTimelineData.items, root)) {
    for (const [groupId, group] of calculateGroupsFromSection(visibleTimelineData.items, section)) {
      const isGroupExpanded = expandedGroups.includes(groupId)
      for (const [, row] of calculateRowsFromGroup(visibleTimelineData.items, group)) {
        for (const [itemId, item] of calculateItemsFromRow(visibleTimelineData.items, row)) {
          if (item.itemType === 'liveopsEvent') {
            const liveopsEvent: TimelineItemLiveopsEvent = item as TimelineItemLiveopsEvent

            const eventStartInstant = getTimelineLiveopsEventStartInstant(liveopsEvent)
            const eventEndInstant = getTimelineLiveopsEventEndInstant(liveopsEvent, timelineEndInstant)

            // Visibility culling.
            if (eventEndInstant < firstVisibleInstant || eventStartInstant > lastVisibleInstant) continue

            const eventPreviewEndInstant = getTimelineLiveopsEventPreviewEndInstant(
              itemId,
              liveopsEvent,
              eventStartInstant
            )
            const eventReviewStartInstant = getTimelineLiveopsEventReviewStartInstant(
              itemId,
              liveopsEvent,
              eventEndInstant
            )

            const renderInfo: TimelineItemRenderInfoLiveopsEvent = {
              id: itemId,
              eventState: liveopsEvent.renderData.state,
              eventStartInstant,
              eventPreviewEndInstant,
              eventReviewStartInstant,
              eventEndInstant,
              plainTimeContainerStyles:
                liveopsEvent.renderData.schedule?.timeMode === 'playerLocal'
                  ? getTimelineLiveopsEventPlainTimeContainerStyles(
                      liveopsEvent,
                      timelineStartInstant,
                      timelineDayWidthInRem,
                      eventStartInstant,
                      eventEndInstant,
                      isGroupExpanded
                    )
                  : undefined,
              containerStyles: getTimelineLiveopsEventContainerStyles(
                liveopsEvent,
                timelineStartInstant,
                timelineDayWidthInRem,
                eventStartInstant,
                eventEndInstant,
                isGroupExpanded
              ),
              previewStyles: getEventTimelinePreviewStyles(
                liveopsEvent,
                timelineStartInstant,
                timelineDayWidthInRem,
                eventStartInstant,
                eventPreviewEndInstant
              ),
              activePhasesStyles: getEventTimelineActivePhasesStyles(
                liveopsEvent,
                !!eventReviewStartInstant,
                !!eventPreviewEndInstant
              ),

              textContainerStyles: getEventTimelineTextContainerStyles(
                liveopsEvent,
                timelineStartInstant,
                timelineEndInstant,
                DateTime.fromISO(visibleTimelineData.endInstantIsoString, { zone: 'utc' }),
                firstVisibleInstant,
                lastVisibleInstant,
                timelineDayWidthInRem,
                eventStartInstant,
                eventPreviewEndInstant,
                eventReviewStartInstant,
                eventEndInstant,
                isGroupExpanded,
                [] // row.items
              ),
              reviewStyles: getEventTimelineReviewStyles(
                liveopsEvent,
                timelineStartInstant,
                timelineDayWidthInRem,
                eventReviewStartInstant,
                eventEndInstant
              ),
              currentPhase:
                liveopsEvent.renderData.schedule?.timeMode === 'utc'
                  ? liveopsEvent.renderData.schedule.currentPhase
                  : undefined, // Ignore phases for player local schedules.
              displayName: liveopsEvent.metadata.displayName,
              colorInHex: liveopsEvent.metadata.color ?? defaultItemColors.liveopsEventItem,
              isLocked: liveopsEvent.renderData.isLocked || liveopsEvent.renderData.isImmutable,
              isTargeted: !!liveopsEvent.renderData.isTargeted,
              isRecurring: liveopsEvent.renderData.isRecurring,
              isGroupExpanded,
              participantCount: liveopsEvent.renderData.participantCount,
            }
            allVisibleRenderInfos[itemId] = renderInfo
          } else if (item.itemType === 'instantEvent') {
            const instantEvent: TimelineItemInstantEvent = item as TimelineItemInstantEvent
            const renderInfo: TimelineItemRenderInfoInstantEvent = getTimelineItemRenderInfoInstantEvent(
              instantEvent,
              timelineStartInstant,
              timelineDayWidthInRem,
              DateTime.fromISO(instantEvent.renderData.instantIsoString),
              isGroupExpanded
            )
            allVisibleRenderInfos[itemId] = renderInfo
          }
        }
      }
    }
  }

  return allVisibleRenderInfos
}

// Times --------------------------------------------------------------------------------------------------------------

function getTimelineLiveopsEventStartInstant(event: TimelineItemLiveopsEvent): DateTime {
  // If the event has not started, set the start time to UTC now.
  if (!event.renderData.timelinePosition.startInstantIsoString) {
    return DateTime.utc()
  } else {
    // Return the earliest phase start instant.
    return DateTime.fromISO(event.renderData.timelinePosition.startInstantIsoString)
  }
}

function getTimelineLiveopsEventPreviewEndInstant(
  eventId: string,
  event: TimelineItemLiveopsEvent,
  eventStartInstant: DateTime
): DateTime | undefined {
  // If the event has no schedule or preview duration, return undefined.
  if (!event.renderData.schedule?.previewDurationIsoString) {
    return undefined
    // If the event is in player local time mode, return undefined.
  } else {
    const previewDuration = Duration.fromISO(event.renderData.schedule.previewDurationIsoString)

    if (!previewDuration.isValid) {
      throw new Error(
        `Invalid preview duration: ${event.renderData.schedule.previewDurationIsoString} for event ${eventId} (${previewDuration.invalidReason} - ${previewDuration.invalidExplanation})`
      )
    }

    // Double the preview duration if the event is in player local time to communicate uncertainty.
    // if (event.data.schedule.timeMode === 'playerLocal') {
    //   return eventStartInstant.plus(previewDuration.plus(previewDuration))
    // }

    // Return event start time + preview duration.
    return eventStartInstant.plus(previewDuration)
  }
}

function getTimelineLiveopsEventReviewStartInstant(
  eventId: string,
  event: TimelineItemLiveopsEvent,
  eventEndInstant: DateTime
): DateTime | undefined {
  // If the event has no schedule or review duration, return undefined.
  if (!event.renderData.schedule?.reviewDurationIsoString) {
    return undefined
    // If the event is in player local time mode, return undefined.
  } else {
    const reviewDuration = Duration.fromISO(event.renderData.schedule.reviewDurationIsoString)

    if (!reviewDuration.isValid) {
      throw new Error(
        `Invalid review duration: ${event.renderData.schedule.reviewDurationIsoString} for event ${eventId} (${reviewDuration.invalidReason} - ${reviewDuration.invalidExplanation})`
      )
    }

    // Double the preview duration if the event is in player local time to communicate uncertainty.
    // if (event.data.schedule.timeMode === 'playerLocal') {
    //   return eventEndInstant.minus(reviewDuration.plus(reviewDuration))
    // }

    // Return event end time - review duration.
    return eventEndInstant.minus(reviewDuration)
  }
}

function getTimelineLiveopsEventEndInstant(event: TimelineItemLiveopsEvent, lastPossibleInstant: DateTime): DateTime {
  // If the event has not ended, set the end time to the end of the timeline.
  if (!event.renderData.timelinePosition.endInstantIsoString) {
    return lastPossibleInstant
  } else {
    // Return the latest phase end instant.
    return DateTime.fromISO(event.renderData.timelinePosition.endInstantIsoString)
  }
}

// Styles -------------------------------------------------------------------------------------------------------------

// eslint-disable-next-line @typescript-eslint/max-params
function getTimelineLiveopsEventPlainTimeContainerStyles(
  event: TimelineItemLiveopsEvent,
  timelineStartInstant: DateTime,
  selectedWidthOfTimelineDaysInRem: number,
  eventStartInstant: DateTime,
  eventEndInstant: DateTime,
  isGroupExpanded: boolean
): TimelineItemRenderInfoLiveopsEvent['plainTimeContainerStyles'] {
  const leftEdgePositionInPixels =
    eventStartInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
  const rightEdgePositionInPixels =
    eventEndInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)

  return {
    left: `${leftEdgePositionInPixels}px`,
    width: `${rightEdgePositionInPixels - leftEdgePositionInPixels}px`,
    height: isGroupExpanded ? '42px' : '22px',
    backgroundColor: getWashedColor(event.metadata.color ?? defaultItemColors.liveopsEventItem),
    border: `2px dotted rgba(255, 255, 255, 0.7)`,
  }
}

// eslint-disable-next-line @typescript-eslint/max-params
function getTimelineLiveopsEventContainerStyles(
  event: TimelineItemLiveopsEvent,
  timelineStartInstant: DateTime,
  selectedWidthOfTimelineDaysInRem: number,
  eventStartInstant: DateTime,
  eventEndInstant: DateTime,
  isGroupExpanded: boolean,
  zIndex?: number
): TimelineItemRenderInfoLiveopsEvent['containerStyles'] {
  let leftEdgePositionInPixels =
    eventStartInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
  let rightEdgePositionInPixels =
    eventEndInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)

  if (event.renderData.schedule?.timeMode === 'playerLocal') {
    if (event.renderData.schedule.plainTimeStartInstantIsoString) {
      const plainTimeStartInstant = DateTime.fromISO(event.renderData.schedule.plainTimeStartInstantIsoString)
      leftEdgePositionInPixels =
        plainTimeStartInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
    }

    if (event.renderData.schedule.plainTimeEndInstantIsoString) {
      const plainTimeEndInstant = DateTime.fromISO(event.renderData.schedule.plainTimeEndInstantIsoString)
      rightEdgePositionInPixels =
        plainTimeEndInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
    }
  }

  // If the event is in draft mode, show a dashed border with the same color as the item.
  const border = event.renderData.state === 'draft' ? `3px dashed rgba(255, 255, 255, 0.7)` : undefined

  // If the event is concluded, set the filter to desaturated.
  const filter =
    event.renderData.state === 'concluded'
      ? 'grayscale(20%) brightness(90%)'
      : event.renderData.state === 'draft'
        ? 'grayscale(40%)'
        : undefined

  const height = isGroupExpanded ? '42px' : '22px'

  const backgroundColor = event.metadata.color ?? defaultItemColors.liveopsEventItem

  // TODO: Hide if width is less than 2px?

  return {
    left: `${leftEdgePositionInPixels}px`,
    width: `${rightEdgePositionInPixels - leftEdgePositionInPixels}px`,
    height,
    border,
    filter,
    zIndex,
    backgroundColor,
  }
}

// eslint-disable-next-line @typescript-eslint/max-params
function getEventTimelinePreviewStyles(
  event: TimelineItemLiveopsEvent,
  timelineStartInstant: DateTime,
  selectedWidthOfTimelineDaysInRem: number,
  eventStartInstant: DateTime,
  previewEndInstant: DateTime | undefined
): TimelineItemRenderInfoLiveopsEvent['previewStyles'] {
  if (!previewEndInstant) {
    return undefined
  }

  const previewStartPosition = eventStartInstant.diff(timelineStartInstant, 'days').days
  const previewEndPosition = previewEndInstant.diff(timelineStartInstant, 'days').days

  const washedColor = getWashedColor(event.metadata.color ?? defaultItemColors.liveopsEventItem)

  // Gradient from left to right that fades from washed color to transparent.
  const background =
    event.renderData.schedule?.timeMode === 'playerLocal'
      ? `linear-gradient(to right, ${washedColor} 60%, ${washedColor.slice(0, -1)}, 0))`
      : washedColor

  return {
    flexBasis: `${(previewEndPosition - previewStartPosition) * remToPixels(selectedWidthOfTimelineDaysInRem)}px`,
    background,
  }
}

function getEventTimelineActivePhasesStyles(
  event: TimelineItemLiveopsEvent,
  hasPreview: boolean,
  hasReview: boolean
): TimelineItemRenderInfoLiveopsEvent['activePhasesStyles'] {
  const backgroundColor = event.metadata.color ?? defaultItemColors.liveopsEventItem

  // If the event has no preview, set the left border radius to 0.375rem.
  const borderRadiusLeft = hasPreview ? '0' : '0.375rem'

  // If the event has no review, set the right border radius to 0.375rem.
  const borderRadiusRight = hasReview ? '0' : '0.375rem'

  return {
    backgroundColor,
    borderRadius: `${borderRadiusRight} ${borderRadiusLeft} ${borderRadiusLeft} ${borderRadiusRight}`,
  }
}

// eslint-disable-next-line @typescript-eslint/max-params
function getEventTimelineTextContainerStyles(
  event: TimelineItemLiveopsEvent,
  timelineStartInstant: DateTime,
  timelineEndInstant: DateTime,
  timelineDataRangeEndInstant: DateTime,
  firstVisibleInstant: DateTime,
  lastVisibleInstant: DateTime,
  selectedWidthOfTimelineDaysInRem: number,
  eventStartInstant: DateTime,
  previewEndInstant: DateTime | undefined,
  reviewStartInstant: DateTime | undefined,
  eventEndInstant: DateTime,
  isGroupExpanded: boolean,
  rowItems?: TimelineItem[],
  zIndex?: number
): TimelineItemRenderInfoLiveopsEvent['textContainerStyles'] {
  let startInstant = eventStartInstant
  let endInstant = eventEndInstant

  // Clamp to the first visible instant.
  if (startInstant < firstVisibleInstant) {
    startInstant = firstVisibleInstant
  }

  // Clamp to the last visible instant.
  if (endInstant > lastVisibleInstant) {
    endInstant = lastVisibleInstant
  }

  // Clamp right edge to data range end.
  if (endInstant > timelineDataRangeEndInstant) {
    endInstant = timelineDataRangeEndInstant
  }

  // If we got an array of events as a reference, find all events that have starting time before this event and an ending time after this event starts, so events that overlap.
  // This is relevant for collapsed rows where we need to push the text label to the end of the last overlapping event.
  // if (rowItems) {
  //   const eventsThatPushThisEventToLater = rowItems.filter(item => {
  //     if (item.timelineItemType !== 'liveopsEvent') return false

  //     const referenceEventStartInstant = getTimelineLiveopsEventStartInstant(item)
  //     if (referenceEventStartInstant < startInstant) {
  //       const referenceEventEndInstant = getTimelineLiveopsEventEndInstant(item, timelineEndInstant)
  //       return referenceEventEndInstant > startInstant
  //     }

  //     return false
  //   }) as TimelineLiveopsEvent[]

  //   // Push the current event text label to the end of the last overlapping event.
  //   if (eventsThatPushThisEventToLater.length > 0) {
  //     const lastEvent = eventsThatPushThisEventToLater[eventsThatPushThisEventToLater.length - 1]
  //     endInstant = getTimelineLiveopsEventEndInstant(lastEvent, timelineEndInstant)
  //   }
  // }

  let leftEdgePositionInPixels =
    startInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
  let rightEdgePositionInPixels =
    endInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)

  if (event.renderData.schedule?.timeMode === 'playerLocal') {
    if (event.renderData.schedule.plainTimeStartInstantIsoString) {
      let plainTimeStartInstant = DateTime.fromISO(event.renderData.schedule.plainTimeStartInstantIsoString)

      // Clamp to the first visible instant.
      if (plainTimeStartInstant < firstVisibleInstant) {
        plainTimeStartInstant = firstVisibleInstant
      }

      leftEdgePositionInPixels =
        plainTimeStartInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
    }

    if (event.renderData.schedule.plainTimeEndInstantIsoString) {
      let plainTimeEndInstant = DateTime.fromISO(event.renderData.schedule.plainTimeEndInstantIsoString)

      // Clamp to the last visible instant.
      if (plainTimeEndInstant > lastVisibleInstant) {
        plainTimeEndInstant = lastVisibleInstant
      }

      // Clamp right edge to data range end.
      if (plainTimeEndInstant > timelineDataRangeEndInstant) {
        plainTimeEndInstant = timelineDataRangeEndInstant
      }

      rightEdgePositionInPixels =
        plainTimeEndInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)
    }
  }

  // Set a dynamic color based on the event color.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const [r, g, b] = (event.metadata.color ?? defaultItemColors.liveopsEventItem)
    .match(/\w\w/g)!
    .map((x: string) => parseInt(x, 16))

  // YIQ (luma) color space is a simple way to determine the contrast of a color.
  const yiq = (r * 299 + g * 587 + b * 114) / 1000

  const color = yiq >= 128 ? 'black' : 'white'

  const height = isGroupExpanded ? '42px' : '22px'

  const paddingTop = isGroupExpanded ? '0.25rem' : '0.125rem'
  const paddingBottom = isGroupExpanded ? '0.25rem' : '0.125rem'

  return {
    left: `${leftEdgePositionInPixels}px`,
    width: `${rightEdgePositionInPixels - leftEdgePositionInPixels}px`,
    height,
    color,
    fill: color,
    paddingTop,
    paddingBottom,
    zIndex,
  }
}

// eslint-disable-next-line @typescript-eslint/max-params
function getEventTimelineReviewStyles(
  event: TimelineItemLiveopsEvent,
  timelineStartInstant: DateTime,
  selectedWidthOfTimelineDaysInRem: number,
  reviewStartInstant: DateTime | undefined,
  eventEndInstant: DateTime
): TimelineItemRenderInfoLiveopsEvent['reviewStyles'] {
  if (!reviewStartInstant) {
    return undefined
  }

  const reviewStartPosition = reviewStartInstant.diff(timelineStartInstant, 'days').days
  const reviewEndPosition = eventEndInstant.diff(timelineStartInstant, 'days').days

  const washedColor = getWashedColor(event.metadata.color ?? defaultItemColors.liveopsEventItem)

  // Gradient from left to right that fades from washed color to transparent.
  const background =
    event.renderData.schedule?.timeMode === 'playerLocal'
      ? `linear-gradient(to left, ${washedColor} 60%, ${washedColor.slice(0, -1)}, 0))`
      : washedColor

  return {
    flexBasis: `${(reviewEndPosition - reviewStartPosition) * remToPixels(selectedWidthOfTimelineDaysInRem)}px`,
    background,
  }
}

// function getTimelineEventMinimapStyles (event: TimelineLiveopsEvent) {
//   const startTime = event.timelinePosition.startInstantIsoString ? DateTime.fromISO(event.timelinePosition.startInstantIsoString) : DateTime.fromISO(props.timelineData.startInstantIsoString)
//   const endTime = event.timelinePosition.endInstantIsoString ? DateTime.fromISO(event.timelinePosition.endInstantIsoString) : DateTime.fromISO(props.timelineData.endInstantIsoString)

//   const leftEdgePositionInPixels = startTime.diff(dataRangeToFetch.value.start, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem.value)
//   const rightEdgePositionInPixels = endTime.diff(dataRangeToFetch.value.start, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem.value)

//   return {
//     left: `${leftEdgePositionInPixels}px`,
//     width: `${rightEdgePositionInPixels - leftEdgePositionInPixels}px`,
//     backgroundColor: event.colorInHex,
//   }
// }

// eslint-disable-next-line @typescript-eslint/max-params
function getTimelineItemRenderInfoInstantEvent(
  event: TimelineItemInstantEvent,
  timelineStartInstant: DateTime,
  selectedWidthOfTimelineDaysInRem: number,
  eventStartInstant: DateTime,
  isGroupExpanded: boolean
): TimelineItemRenderInfoInstantEvent {
  const leftEdgePositionInPixels =
    eventStartInstant.diff(timelineStartInstant, 'days').days * remToPixels(selectedWidthOfTimelineDaysInRem)

  const widthHeight = isGroupExpanded ? 42 : 22
  return {
    styles: {
      backgroundColor: event.renderData.color ?? defaultItemColors.instantItem,
      left: `${leftEdgePositionInPixels - widthHeight / 2}px`,
      width: `${widthHeight}px`,
      height: `${widthHeight}px`,
    },
  }
}
