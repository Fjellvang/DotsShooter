import { defaultsDeep } from 'lodash-es'

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
        component: 'A sub-component of the timeline that displays a single liveops event.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof StorybookEventTimelineWrapper>

function createMockLiveopsEvent(
  parentId: string,
  id: string | undefined,
  payload: Partial<TimelineItemLiveopsEvent>
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
    payload?: unknown
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

export const Default: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () => new TimelineDataFetchHandlerMock(createMockTimelineForLiveopsEvent([{}])),
  },
}

export const Locked: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                isLocked: true,
              },
            },
          },
        ])
      ),
  },
}

export const Immutable: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                isImmutable: true,
              },
            },
          },
        ])
      ),
  },
}

export const Targeted: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                isTargeted: true,
              },
            },
          },
        ])
      ),
  },
}

export const Recurring: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                isRecurring: true,
              },
            },
          },
        ])
      ),
  },
}

export const WithFewParticipants: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                participantCount: 123,
              },
            },
          },
        ])
      ),
  },
}

export const WithManyParticipants: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              renderData: {
                participantCount: 123456789,
              },
            },
          },
        ])
      ),
  },
}

export const VeryLongName: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              metadata: {
                displayName: 'This is a very long name that will be truncated',
              },
            },
          },
        ])
      ),
  },
}

export const ComplexAndShort: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              metadata: {
                displayName: 'This is a very long name that will be truncated',
              },
              renderData: {
                isLocked: true,
                timelinePosition: {
                  startInstantIsoString: '1999-12-31T23:00:00.000Z',
                  endInstantIsoString: '2000-01-01T22:00:00.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'active',
                  activateInstantIsoString: '1999-12-31T00:00:00.000Z',
                  concludeInstantIsoString: '2000-01-01T22:00:00.000Z',
                },
                isTargeted: true,
                isRecurring: true,
                participantCount: 123456789,
              },
            },
          },
        ])
      ),
  },
}

export const MaximumComplexity: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              metadata: {
                displayName: 'This is a very long name that will be truncated',
              },
              renderData: {
                isLocked: true,
                isRecurring: true,
                isTargeted: true,
                participantCount: 123456789,
                state: 'draft',
                schedule: {
                  timeMode: 'utc',
                  activateInstantIsoString: '1999-12-31T00:00:00.000Z',
                  concludeInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
              },
            },
          },
        ])
      ),
  },
}

export const MaximumComplexityAndCollapsed: Story = {
  args: {
    startExpanded: false,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            payload: {
              metadata: {
                displayName: 'This is a very long name that will be truncated',
              },
              renderData: {
                isLocked: true,
                isRecurring: true,
                isTargeted: true,
                participantCount: 123456789,
                state: 'draft',
                schedule: {
                  timeMode: 'utc',
                  activateInstantIsoString: '1999-12-31T00:00:00.000Z',
                  concludeInstantIsoString: '2000-01-02T23:59:59.000Z',
                },
              },
            },
          },
        ])
      ),
  },
}

// TODO: This story does not work.
export const SelectedItem: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        createMockTimelineForLiveopsEvent([
          {
            id: 'event-1',
          },
        ])
      ),
    preselectedItemId: 'event-1',
  },
}

export const OverlappingEventsOnOneRow: Story = {
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
                  endInstantIsoString: '2000-01-03T12:00:00.000Z',
                },
                schedule: undefined,
              },
            },
          },
          {
            payload: {
              renderData: {
                state: 'scheduled',
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-04T12:00:00.000Z',
                },
                schedule: undefined,
              },
            },
          },
          {
            payload: {
              renderData: {
                state: 'scheduled',
                timelinePosition: {
                  startInstantIsoString: '2000-01-03T00:00:00.000Z',
                  endInstantIsoString: '2000-01-05T12:00:00.000Z',
                },
                schedule: undefined,
              },
            },
          },
        ])
      ),
  },
}
