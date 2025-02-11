import { defaultsDeep } from 'lodash-es'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../../composables/usePermissions'
import type { TimelineItemInstantEvent } from './MEventTimelineItemInstantEventTypes'
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
  parameters: {
    docs: {
      description: {
        component: 'A subcomponent of the timeline that displays a single instant event.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof StorybookEventTimelineWrapper>

function createMockInstantEvent(
  parentId: string,
  id: string | undefined,
  payload: Partial<TimelineItemInstantEvent>
): [string, TimelineItemInstantEvent] {
  id = id ?? makeIntoUniqueKey('mockData')

  const defaultPayload: TimelineItemInstantEvent = {
    itemType: 'instantEvent',
    version: 0,
    hierarchy: {
      parentId,
    },
    metadata: {
      displayName: 'Example Event',
    },
    renderData: {
      instantIsoString: '1999-12-31T00:00:00.000Z',
      color: '#3f6730',
    },
  }

  return [id, { ...defaultsDeep(payload, defaultPayload) }]
}

function createMockTimelineForInstantEvent(
  events: Array<{
    id?: string
    payload?: Partial<TimelineItemInstantEvent>
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
    const [itemId, item] = createMockInstantEvent(parentId, id, payload ?? {})

    ;(timelineData.items[parentId] as TimelineItemRoot).hierarchy.childIds.push(itemId)
    timelineData.items[itemId] = item
  })

  return timelineData
}

export const Default: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(createMockTimelineForInstantEvent([{ id: 'event-1' }]), {
        'event-1': {
          itemType: 'instantEvent',
          displayName: 'Example Event',
          details: {
            rawPayload: { abc: 1 },
          },
        },
      }),
  },
}

export const SelectedItem: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(createMockTimelineForInstantEvent([{ id: 'event-1' }]), {
        'event-1': {
          itemType: 'instantEvent',
          displayName: 'Example Event',
          details: {
            rawPayload: { abc: 1 },
          },
        },
      }),
    preselectedItemId: 'event-1',
  },
}
