import { defaultsDeep } from 'lodash-es'
import { DateTime } from 'luxon'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../../composables/usePermissions'
import { ColorPickerPalette } from './MEventTimelineColorUtils'
import type { TimelineItemLiveopsEvent } from './MEventTimelineItemLiveopsEventTypes'
import type {
  TimelineData,
  TimelineItemGroup,
  TimelineItemRoot,
  TimelineItemRow,
  TimelineItemSection,
} from './MEventTimelineTypes'
import StorybookEventTimelineWrapper from './StorybookEventTimelineWrapper.vue'
import { TimelineDataFetchHandlerMock } from './timelineDataFetcherMocks'

usePermissions().setPermissions([]) //['api.liveops_events.view', 'api.liveops_events.edit'])

const meta: Meta<typeof StorybookEventTimelineWrapper> = {
  component: StorybookEventTimelineWrapper,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component: 'Stories for the `MEventTimelineItemLiveopsEvent` component UTC schedule states.',
      },
    },
  },
}

// Utility type to support recursive Partials.
type RecursivePartial<Type> = {
  [Prop in keyof Type]?: Type[Prop] extends Array<infer U>
    ? Array<RecursivePartial<U>>
    : Type[Prop] extends object | undefined
      ? RecursivePartial<Type[Prop]>
      : Type[Prop]
}

export default meta
type Story = StoryObj<typeof StorybookEventTimelineWrapper>

function createMockLiveopsEvent(
  parentId: string,
  id: string | undefined,
  payload: RecursivePartial<TimelineItemLiveopsEvent>
): [string, TimelineItemLiveopsEvent] {
  id = id ?? makeIntoUniqueKey('mockData')

  const defaultPayload: TimelineItemLiveopsEvent = {
    itemType: 'liveopsEvent',
    version: 0,
    hierarchy: {
      parentId,
    },
    metadata: {
      displayName: 'Example Event',
      color: '#3f6730',
    },
    renderData: {
      timelinePosition: {
        startInstantIsoString: '1999-12-31T00:00:00.000Z',
        endInstantIsoString: '2000-01-02T23:59:59.000Z',
      },
      state: 'concluded',
      schedule: {
        timeMode: 'utc',
      },
      isLocked: false,
      isTargeted: false,
      isRecurring: false,
      isImmutable: false,
    },
  }

  return [id, { ...defaultsDeep(payload, defaultPayload) }]
}

function createMockTimelineForLiveopsEvent(
  events: Array<{
    id?: string
    payload?: RecursivePartial<TimelineItemLiveopsEvent>
  }>
): TimelineData {
  const timelineData: TimelineData = {
    startInstantIsoString: '1999-12-21T00:00:00.000Z',
    endInstantIsoString: '2000-01-29T00:00:00.000Z',
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
          childIds: ['group:0'],
        },
        metadata: {
          displayName: 'Example Section',
        },
        renderData: {
          cannotRemoveReason: null,
        },
      } satisfies TimelineItemSection,
      'group:0': {
        itemType: 'group',
        version: 0,
        hierarchy: {
          parentId: 'section:0',
          childIds: ['row:0'],
        },
        metadata: {
          displayName: 'Example Group',
        },
        renderData: {
          cannotRemoveReason: null,
        },
      } satisfies TimelineItemGroup,
      'row:0': {
        itemType: 'row',
        version: 0,
        hierarchy: {
          parentId: 'group:0',
          childIds: [],
        },
        metadata: {
          displayName: 'Example Row',
        },
        renderData: {
          cannotRemoveReason: null,
        },
      } satisfies TimelineItemRow,
    },
  }

  const parentId = 'row:0'
  events.forEach(({ id, payload }) => {
    const [itemId, item] = createMockLiveopsEvent(parentId, id, payload ?? {})
    ;(timelineData.items[parentId] as TimelineItemRoot).hierarchy.childIds.push(itemId)
    timelineData.items[itemId] = item
  })

  return timelineData
}

/**
 * A story with a coverage of different liveops event schedule states in a variety of colors to make comparisons easy.
 */
export const ColorReview: Story = {
  args: {
    startExpanded: true,
    initialVisibleTimelineStartInstant: DateTime.fromISO('1999-12-24T12:00:00.000Z'),
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {
          'root:0': {
            itemType: 'root',
            version: 0,
            hierarchy: {
              childIds: ['section:1'],
            },
          } satisfies TimelineItemRoot,
          'section:1': {
            itemType: 'section',
            version: 0,
            metadata: {
              displayName: 'Top level section',
            },
            hierarchy: {
              parentId: 'root:0',
              childIds: ['group:color-comparison'],
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemSection,
          'group:color-comparison': {
            itemType: 'group',
            version: 0,
            metadata: {
              displayName: 'Color Comparison Group',
            },
            hierarchy: {
              parentId: 'section:1',
              childIds: Object.keys(ColorPickerPalette).map((colorName) => `row:${colorName}`),
            },
            renderData: {
              cannotRemoveReason: null,
            },
          } satisfies TimelineItemGroup,
          ...Object.fromEntries(
            Object.keys(ColorPickerPalette).map((colorName) => [
              `row:${colorName}`,
              {
                itemType: 'row',
                version: 0,
                metadata: {
                  displayName: `Row for ${colorName}`,
                },
                hierarchy: {
                  parentId: 'group:color-comparison',
                  childIds: [
                    `liveopsEvent:${colorName}-concluded`,
                    `liveopsEvent:${colorName}-active`,
                    `liveopsEvent:${colorName}-scheduled`,
                    `liveopsEvent:${colorName}-draft`,
                  ],
                },
                renderData: {
                  cannotRemoveReason: null,
                },
              } satisfies TimelineItemRow,
            ])
          ),
          ...Object.fromEntries(
            Object.entries(ColorPickerPalette).flatMap(([colorName, hexCode]) => [
              [
                `liveopsEvent:${colorName}-concluded`,
                {
                  itemType: 'liveopsEvent',
                  version: 0,
                  hierarchy: {
                    parentId: `row:${colorName}`,
                  },
                  metadata: {
                    displayName: `${colorName} Concluded Event`,
                    color: hexCode,
                  },
                  renderData: {
                    isTargeted: false,
                    isLocked: true,
                    isImmutable: true,
                    state: 'concluded',
                    isRecurring: false,
                    timelinePosition: {
                      startInstantIsoString: '1999-12-25T07:00:00.000Z',
                      endInstantIsoString: '1999-12-28T12:00:00.000Z',
                    },
                    schedule: {
                      timeMode: 'utc',
                      previewDurationIsoString: 'P1D',
                    },
                  },
                } satisfies TimelineItemLiveopsEvent,
              ],
              [
                `liveopsEvent:${colorName}-active`,
                {
                  itemType: 'liveopsEvent',
                  version: 0,
                  hierarchy: {
                    parentId: `row:${colorName}`,
                  },
                  metadata: {
                    displayName: `${colorName} Active Event`,
                    color: hexCode,
                  },
                  renderData: {
                    isTargeted: false,
                    isLocked: true,
                    isImmutable: true,
                    state: 'active',
                    isRecurring: false,
                    timelinePosition: {
                      startInstantIsoString: '1999-12-29T07:00:00.000Z',
                      endInstantIsoString: '2000-01-01T12:00:00.000Z',
                    },
                    schedule: {
                      timeMode: 'utc',
                      currentPhase: 'active',
                      previewDurationIsoString: 'P1D',
                    },
                  },
                } satisfies TimelineItemLiveopsEvent,
              ],
              [
                `liveopsEvent:${colorName}-scheduled`,
                {
                  itemType: 'liveopsEvent',
                  version: 0,
                  hierarchy: {
                    parentId: `row:${colorName}`,
                  },
                  metadata: {
                    displayName: `${colorName} Scheduled Event`,
                    color: hexCode,
                  },
                  renderData: {
                    isTargeted: false,
                    isLocked: true,
                    isImmutable: true,
                    state: 'scheduled',
                    isRecurring: false,
                    timelinePosition: {
                      startInstantIsoString: '2000-01-02T07:00:00.000Z',
                      endInstantIsoString: '2000-01-04T12:00:00.000Z',
                    },
                    schedule: {
                      timeMode: 'utc',
                      previewDurationIsoString: 'P1D',
                    },
                  },
                } satisfies TimelineItemLiveopsEvent,
              ],
              [
                `liveopsEvent:${colorName}-draft`,
                {
                  itemType: 'liveopsEvent',
                  version: 0,
                  hierarchy: {
                    parentId: `row:${colorName}`,
                  },
                  metadata: {
                    displayName: `${colorName} Draft Event`,
                    color: hexCode,
                  },
                  renderData: {
                    isTargeted: false,
                    isLocked: false,
                    isImmutable: false,
                    state: 'draft',
                    isRecurring: false,
                    timelinePosition: {
                      startInstantIsoString: '2000-01-05T07:00:00.000Z',
                      endInstantIsoString: '2000-01-08T12:00:00.000Z',
                    },
                    schedule: {
                      timeMode: 'utc',
                      previewDurationIsoString: 'P1D',
                    },
                  },
                } satisfies TimelineItemLiveopsEvent,
              ],
            ])
          ),
        },
      }),
  },
}

// Concluded ----------------------------------------------------------------------------------------------------------
export const Concluded: Story = {
  args: {
    startExpanded: true,
    initialVisibleTimelineStartInstant: DateTime.fromISO('1999-12-28T12:00:00.000Z'),
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                timelinePosition: {
                  startInstantIsoString: '1999-12-29T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ConcludedWithPreview: Story = {
  args: {
    startExpanded: true,
    initialVisibleTimelineStartInstant: DateTime.fromISO('1999-12-28T12:00:00.000Z'),
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'concluded',
                timelinePosition: {
                  startInstantIsoString: '1999-12-29T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ConcludedWithReview: Story = {
  args: {
    startExpanded: true,
    initialVisibleTimelineStartInstant: DateTime.fromISO('1999-12-28T12:00:00.000Z'),
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'concluded',
                timelinePosition: {
                  startInstantIsoString: '1999-12-29T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ConcludedWithPreviewAndReview: Story = {
  args: {
    startExpanded: true,
    initialVisibleTimelineStartInstant: DateTime.fromISO('1999-12-28T12:00:00.000Z'),
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'concluded',
                timelinePosition: {
                  startInstantIsoString: '1999-12-29T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

// Active -------------------------------------------------------------------------------------------------------------

export const Active: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'active',
                timelinePosition: {
                  startInstantIsoString: '1999-12-31T00:00:00.000Z',
                  endInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'active',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ActiveWithPreview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'active',
                timelinePosition: {
                  startInstantIsoString: '1999-12-31T00:00:00.000Z',
                  endInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'active',
                  previewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ActiveWithReview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'active',
                timelinePosition: {
                  startInstantIsoString: '1999-12-31T00:00:00.000Z',
                  endInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'active',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ActiveWithPreviewAndReview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'active',
                timelinePosition: {
                  startInstantIsoString: '1999-12-31T00:00:00.000Z',
                  endInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'active',
                  previewDurationIsoString: 'P1D',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ActiveInPreviewPhase: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'active',
                timelinePosition: {
                  startInstantIsoString: '1999-12-31T12:00:00.000Z',
                  endInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'preview',
                  previewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ActiveInReviewPhase: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'active',
                timelinePosition: {
                  startInstantIsoString: '1999-12-29T12:00:00.000Z',
                  endInstantIsoString: '2000-01-01T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'review',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

const tempActiveTimeline = createMockTimelineForLiveopsEvent([
  {
    id: 'activeWithoutEndInstant',
    payload: {
      renderData: {
        state: 'active',
        timelinePosition: {
          startInstantIsoString: '1999-12-31T12:00:00.000Z',
        },
        schedule: {
          timeMode: 'utc',
          currentPhase: 'active',
        },
      },
    },
  },
])

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Types getting in the way.
tempActiveTimeline.items.activeWithoutEndInstant.renderData!.timelinePosition = {
  startInstantIsoString: '1999-12-31T12:00:00.000Z',
}

export const ActiveWithoutEndInstant: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () => new TimelineDataFetchHandlerMock(tempActiveTimeline),
  },
}

// Scheduled ----------------------------------------------------------------------------------------------------------

export const Scheduled: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'scheduled',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ScheduledWithPreview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'scheduled',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ScheduledWithReview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'scheduled',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const ScheduledWithPreviewAndReview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'scheduled',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

// Draft -------------------------------------------------------------------------------------------------------------

export const Draft: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'draft',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                },
              },
            },
          },
        ])
      ),
  },
}

export const DraftWithPreview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'draft',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const DraftWithReview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'draft',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

export const DraftWithPreviewAndReview: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                state: 'draft',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T23:59:59.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                  reviewDurationIsoString: 'P1D',
                },
              },
            },
          },
        ])
      ),
  },
}

const tempTimeline = createMockTimelineForLiveopsEvent([
  {
    id: 'draftWithoutSchedule',
    payload: {
      renderData: {
        state: 'draft',
        timelinePosition: {},
        schedule: {
          timeMode: 'utc',
        },
      },
    },
  },
])

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Types getting in the way.
tempTimeline.items.draftWithoutSchedule.renderData!.schedule = {
  timeMode: 'utc',
}

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- Types getting in the way.
tempTimeline.items.draftWithoutSchedule.renderData!.timelinePosition = {}

export const DraftWithoutSchedule: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () => new TimelineDataFetchHandlerMock(tempTimeline),
  },
}
