import { defaultsDeep } from 'lodash-es'
import { DateTime } from 'luxon'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../../composables/usePermissions'
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
        component: 'Stories for the `MEventTimelineItemLiveopsEvent` component player local schedule states.',
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
          displayName: 'Section 0',
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
          displayName: 'Group 0',
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
          displayName: 'Row 0',
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
                state: 'concluded',
                timelinePosition: {
                  startInstantIsoString: '1999-12-28T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-29T00:00:00.000Z',
                  plainTimeEndInstantIsoString: '1999-12-30T12:00:00.000Z',
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
                  startInstantIsoString: '1999-12-28T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-29T00:00:00.000Z',
                  plainTimeEndInstantIsoString: '1999-12-30T12:00:00.000Z',
                  previewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '1999-12-28T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-29T00:00:00.000Z',
                  plainTimeEndInstantIsoString: '1999-12-30T12:00:00.000Z',
                  reviewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '1999-12-28T00:00:00.000Z',
                  endInstantIsoString: '1999-12-31T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-29T00:00:00.000Z',
                  plainTimeEndInstantIsoString: '1999-12-30T12:00:00.000Z',
                  previewDurationIsoString: 'PT12H',
                  reviewDurationIsoString: 'PT12H',
                },
              },
            },
          },
        ])
      ),
  },
}

// Active -------------------------------------------------------------------------------------------------------------

/* currentPhase does not exist in TimelineItemPlayerLocalSchedule
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
                  startInstantIsoString: '1999-12-30T12:00:00.000Z',
                  endInstantIsoString: '2000-01-03T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-31T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-02T12:00:00.000Z',
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
                  startInstantIsoString: '1999-12-30T12:00:00.000Z',
                  endInstantIsoString: '2000-01-03T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-31T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-02T12:00:00.000Z',
                  currentPhase: 'active',
                  previewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '1999-12-30T12:00:00.000Z',
                  endInstantIsoString: '2000-01-03T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-31T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-02T12:00:00.000Z',
                  currentPhase: 'active',
                  reviewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '1999-12-30T12:00:00.000Z',
                  endInstantIsoString: '2000-01-03T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '1999-12-31T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-02T12:00:00.000Z',
                  currentPhase: 'active',
                  reviewDurationIsoString: 'PT12H',
                  previewDurationIsoString: 'PT12H',
                },
              },
            },
          },
        ])
      ),
  },
}
*/

const mockTimelineForActiveLocalEventWithoutEndInstant: TimelineData = createMockTimelineForLiveopsEvent([
  {
    id: 'active-event',
    payload: {
      metadata: {
        displayName: 'Example Event',
        color: '#3f6730',
      },
      renderData: {
        isImmutable: false,
        isLocked: false,
        state: 'active',
        timelinePosition: {
          startInstantIsoString: '1999-12-30T12:00:00.000Z',
        },
        schedule: {
          timeMode: 'playerLocal',
          plainTimeStartInstantIsoString: '1999-12-31T12:00:00.000Z',
        },
        isRecurring: false,
        participantCount: 123456789,
      },
    },
  },
])

// @ts-expect-error -- Types are getting in the way here.
mockTimelineForActiveLocalEventWithoutEndInstant.items['active-event'].renderData.timelinePosition.endInstantIsoString =
  undefined

export const ActiveWithoutEndInstant: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () => new TimelineDataFetchHandlerMock(mockTimelineForActiveLocalEventWithoutEndInstant),
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
                  previewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
                  reviewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
                  previewDurationIsoString: 'PT12H',
                  reviewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
                  previewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
                  reviewDurationIsoString: 'PT12H',
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
                  startInstantIsoString: '2000-01-01T12:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'playerLocal',
                  plainTimeStartInstantIsoString: '2000-01-02T12:00:00.000Z',
                  plainTimeEndInstantIsoString: '2000-01-04T12:00:00.000Z',
                  previewDurationIsoString: 'PT12H',
                  reviewDurationIsoString: 'PT12H',
                },
              },
            },
          },
        ])
      ),
  },
}
