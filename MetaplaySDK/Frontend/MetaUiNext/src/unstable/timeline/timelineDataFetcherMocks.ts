// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { DateTime, type DateTimeUnit } from 'luxon'

import { ColorPickerPalette } from './MEventTimelineColorUtils'
import type { TimelineItemInstantEvent, TimelineItemDetailsInstantEvent } from './MEventTimelineItemInstantEventTypes'
import type { TimelineItemLiveopsEvent, TimelineItemDetailsLiveopsEvent } from './MEventTimelineItemLiveopsEventTypes'
import type {
  TimelineData,
  TimelineItemDetails,
  TimelineItemGroup,
  TimelineItemRoot,
  TimelineItemRow,
  TimelineItemSection,
} from './MEventTimelineTypes'
import { TimelineDataFetchHandler } from './timelineDataFetcher'

// --------------------------------------------------------------------------------------------------------------------

export class TimelineDataFetchHandlerMock extends TimelineDataFetchHandler {
  private readonly timelineData: TimelineData
  private readonly itemDetails: Record<string, TimelineItemDetails>
  private readonly loadTimeInMs: number

  constructor(timelineData: TimelineData, itemDetails: Record<string, TimelineItemDetails> = {}, loadTimeInMs = 0) {
    super()
    this.timelineData = timelineData
    this.itemDetails = itemDetails
    this.loadTimeInMs = loadTimeInMs
  }

  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  requestTimelineData(_firstInstant: DateTime, _lastInstant: DateTime): void {
    if (this.loadTimeInMs) {
      setTimeout(() => {
        this.setTimelineData(this.timelineData)
      }, this.loadTimeInMs)
    } else {
      this.setTimelineData(this.timelineData)
    }
  }

  requestItemDetails(itemIds: string[]): void {
    const requestedItemDetails: Record<string, TimelineItemDetails | undefined> = {}
    for (const itemId of itemIds) {
      const details = this.itemDetails[itemId]
      // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
      if (details) {
        requestedItemDetails[itemId] = details
      } else {
        requestedItemDetails[itemId] = undefined
      }
    }

    const x = Object.entries(requestedItemDetails)
    const y = Object.fromEntries(
      x.map(([id, details]): [string, TimelineItemDetails | undefined] => {
        return [id, details]
      })
    )
    this.updateItemDetails(y)
  }
}

// --------------------------------------------------------------------------------------------------------------------

export class TimelineDataFetchHandlerInfiniteMock extends TimelineDataFetchHandler {
  private readonly dataFetchTimeInMs: number
  private readonly detailFetchTimeInMs: number

  constructor(dataFetchTimeInMs: number, detailFetchTimeInMs: number) {
    super()
    this.dataFetchTimeInMs = dataFetchTimeInMs
    this.detailFetchTimeInMs = detailFetchTimeInMs
  }

  private makeLiveOpsEventItemDataAndDetails(
    parentId: string,
    itemStartInstant: DateTime,
    interval: DateTimeUnit
  ): { id: string; item: TimelineItemLiveopsEvent; details: TimelineItemDetailsLiveopsEvent } {
    const itemEndInstant = itemStartInstant.plus(1).endOf(interval)
    const randomSelector = Math.floor(itemStartInstant.toUnixInteger() / 12345 + interval.length * 12345)
    const colors: string[] = Object.values(ColorPickerPalette)
    const colorId = randomSelector % colors.length
    return {
      id: `liveopsEvent-${interval}-${itemStartInstant.toUnixInteger()}`,
      item: {
        itemType: 'liveopsEvent',
        version: 0,
        hierarchy: {
          parentId,
        },
        metadata: {
          displayName: `Event ${interval} ${itemStartInstant.toISODate()}`,
          color: colors[colorId],
        },
        renderData: {
          timelinePosition: {
            startInstantIsoString: itemStartInstant.toISO() ?? '?',
            endInstantIsoString: itemEndInstant.toISO() ?? '?',
          },
          state: 'concluded',
          schedule: {
            timeMode: 'utc',
          },
          isLocked: !!(randomSelector & 1),
          isTargeted: !!(randomSelector & 2),
          isRecurring: !!(randomSelector & 4),
          isImmutable: !!(randomSelector & 8),
          participantCount: itemStartInstant.toUnixInteger(),
        },
      },
      details: {
        itemType: 'liveopsEvent',
        displayName: `Event ${interval} ${itemStartInstant.toISODate()}`,
        description: `A liveops event that occurred on ${itemStartInstant.toISODate()}.`,
        details: {
          eventId: 'eventId',
          eventTypeName: 'EventTypeName',
          eventParams: {
            todo: 'TODO use more realistic eventParams',
          },
          currentPhase: 'Active',
          nextPhase: 'Active',
          nextPhaseTime: null,
          participantCount: 123,
        },
      },
    }
  }

  private makeLiveOpsEventItemsData(
    parentId: string,
    firstInstant: DateTime,
    lastInstant: DateTime,
    interval: DateTimeUnit
  ): Record<string, TimelineItemLiveopsEvent> {
    const items: Record<string, TimelineItemLiveopsEvent> = {}
    for (
      let itemStartInstant = firstInstant.startOf(interval);
      itemStartInstant < lastInstant;
      itemStartInstant = itemStartInstant.endOf(interval).plus(1).startOf(interval)
    ) {
      const item = this.makeLiveOpsEventItemDataAndDetails(parentId, itemStartInstant, interval)
      items[item.id] = item.item
    }
    return items
  }

  private makeInstantEventItemDataAndDetails(
    parentId: string,
    itemStartInstant: DateTime,
    interval: DateTimeUnit
  ): { id: string; item: TimelineItemInstantEvent; details: TimelineItemDetailsInstantEvent } {
    const colors: string[] = Object.values(ColorPickerPalette)
    const colorId = Math.floor(itemStartInstant.toUnixInteger() / 12345 + interval.length * 12345) % colors.length
    return {
      id: `instantEvent-${interval}-${itemStartInstant.toUnixInteger()}`,
      item: {
        itemType: 'instantEvent',
        version: 0,
        hierarchy: {
          parentId,
        },
        renderData: {
          color: colors[colorId],
          instantIsoString: itemStartInstant.toISO() ?? '?',
        },
      },
      details: {
        itemType: 'instantEvent',
        displayName: `Event ${interval} ${itemStartInstant.toISODate()}`,
        description: `An instant event that occurred on ${itemStartInstant.toISODate()}.`,
        details: {
          timestamp: itemStartInstant.toISO() ?? '?',
          message: 'message',
          logEventId: 'logEventId',
          source: 'source',
          sourceType: 'sourceType',
          exception: 'exception',
          stackTrace: 'stackTrace',
          id: 'id',
        },
      },
    }
  }

  private makeInstantEventItemsData(
    parentId: string,
    firstInstant: DateTime,
    lastInstant: DateTime,
    interval: DateTimeUnit
  ): Record<string, TimelineItemInstantEvent> {
    const items: Record<string, TimelineItemInstantEvent> = {}
    for (
      let itemStartInstant = firstInstant.startOf(interval);
      itemStartInstant < lastInstant;
      itemStartInstant = itemStartInstant.endOf(interval).plus(1).startOf(interval)
    ) {
      const item = this.makeInstantEventItemDataAndDetails(parentId, itemStartInstant, interval)
      items[item.id] = item.item
    }
    return items
  }

  requestTimelineData(firstInstant: DateTime, lastInstant: DateTime): void {
    setTimeout(() => {
      const dailyLiveOpsEvents = this.makeLiveOpsEventItemsData(
        'row:daily-liveops-events',
        firstInstant,
        lastInstant,
        'day'
      )
      const weeklyLiveOpsEvents = this.makeLiveOpsEventItemsData(
        'row:weekly-liveops-events',
        firstInstant,
        lastInstant,
        'week'
      )
      const monthlyLiveOpsEvents = this.makeLiveOpsEventItemsData(
        'row:monthly-liveops-events',
        firstInstant,
        lastInstant,
        'month'
      )

      const dailyInstantEvents = this.makeInstantEventItemsData(
        'row:daily-instant-events',
        firstInstant,
        lastInstant,
        'day'
      )
      const weeklyInstantEvents = this.makeInstantEventItemsData(
        'row:weekly-instant-events',
        firstInstant,
        lastInstant,
        'week'
      )

      const timelineData: TimelineData = {
        startInstantIsoString: firstInstant.toISO() ?? '?',
        endInstantIsoString: lastInstant.toISO() ?? '?',
        items: {
          'root:0': {
            itemType: 'root',
            version: 0,
            hierarchy: {
              childIds: ['section:0'],
            },
          } satisfies TimelineItemRoot,
          'section:0': {
            itemType: 'section',
            version: 0,
            hierarchy: {
              parentId: 'root:0',
              childIds: ['group:liveops-events', 'group:instant-events'],
            },
            metadata: {
              displayName: 'Infinitely Scrolling Timeline',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemSection,
          'group:liveops-events': {
            itemType: 'group',
            version: 0,
            hierarchy: {
              parentId: 'section:0',
              childIds: ['row:daily-liveops-events', 'row:weekly-liveops-events', 'row:monthly-liveops-events'],
            },
            metadata: {
              displayName: 'Liveops Events',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemGroup,
          'group:instant-events': {
            itemType: 'group',
            version: 0,
            hierarchy: {
              parentId: 'section:0',
              childIds: ['row:daily-instant-events', 'row:weekly-instant-events'],
            },
            metadata: {
              displayName: 'Instant Events',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemGroup,
          'row:daily-liveops-events': {
            itemType: 'row',
            version: 0,
            hierarchy: {
              parentId: 'group:liveops-events',
              childIds: [...Object.keys(dailyLiveOpsEvents)],
            },
            metadata: {
              displayName: 'Daily',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemRow,
          'row:weekly-liveops-events': {
            itemType: 'row',
            version: 0,
            hierarchy: {
              parentId: 'group:liveops-events',
              childIds: [...Object.keys(weeklyLiveOpsEvents)],
            },
            metadata: {
              displayName: 'Weekly',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemRow,
          'row:monthly-liveops-events': {
            itemType: 'row',
            version: 0,
            hierarchy: {
              parentId: 'group:liveops-events',
              childIds: [...Object.keys(monthlyLiveOpsEvents)],
            },
            metadata: {
              displayName: 'Monthly',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemRow,
          'row:daily-instant-events': {
            itemType: 'row',
            version: 0,
            hierarchy: {
              parentId: 'group:instant-events',
              childIds: [...Object.keys(dailyInstantEvents)],
            },
            metadata: {
              displayName: 'Daily',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemRow,
          'row:weekly-instant-events': {
            itemType: 'row',
            version: 0,
            hierarchy: {
              parentId: 'group:instant-events',
              childIds: [...Object.keys(weeklyInstantEvents)],
            },
            metadata: {
              displayName: 'Weekly',
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemRow,
          ...dailyLiveOpsEvents,
          ...weeklyLiveOpsEvents,
          ...monthlyLiveOpsEvents,
          ...dailyInstantEvents,
          ...weeklyInstantEvents,
        },
      }
      this.setTimelineData(timelineData)
    }, this.dataFetchTimeInMs)
  }

  requestItemDetails(itemIds: string[]): void {
    const itemDetails: Record<string, TimelineItemDetails> = Object.fromEntries(
      itemIds.map((id) => {
        let item: TimelineItemDetails = {} as unknown as TimelineItemDetails
        const [type, interval, unixTimeString] = id.split('-')
        const unixTime = DateTime.fromMillis(parseInt(unixTimeString) * 1_000)
        const dateTimeInterval = interval as DateTimeUnit
        if (type === 'liveopsEvent') {
          item = this.makeLiveOpsEventItemDataAndDetails('dont-care', unixTime, dateTimeInterval).details
        } else if (type === 'instantEvent') {
          item = this.makeInstantEventItemDataAndDetails('dont-care', unixTime, dateTimeInterval).details
        }
        return [id, item]
      })
    )
    setTimeout(() => {
      this.updateItemDetails(itemDetails)
    }, this.detailFetchTimeInMs)
  }
}
