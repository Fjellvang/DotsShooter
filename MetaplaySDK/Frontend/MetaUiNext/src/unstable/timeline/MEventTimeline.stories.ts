import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../../composables/usePermissions'
import { DisplayError } from '../../utils/DisplayErrorHandler'
import MEventTimeline from './MEventTimeline.vue'
import type { TimelineItemLiveopsEvent } from './MEventTimelineItemLiveopsEventTypes'
// import { liveOpsScenario1 } from './MEventTimelineMockData'
import type {
  TimelineData,
  TimelineItemGroup,
  TimelineItemRoot,
  TimelineItemRow,
  TimelineItemSection,
} from './MEventTimelineTypes'
import StorybookEventTimelineWrapper from './StorybookEventTimelineWrapper.vue'
import { TimelineDataFetchHandlerInfiniteMock, TimelineDataFetchHandlerMock } from './timelineDataFetcherMocks'

usePermissions().setPermissions([]) //['api.liveops_events.view', 'api.liveops_events.edit'])

const meta: Meta<typeof StorybookEventTimelineWrapper> = {
  component: StorybookEventTimelineWrapper,
  parameters: {
    docs: {
      description: {
        component: 'A component that displays a timeline of events.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof StorybookEventTimelineWrapper>

export const Default: Story = {
  args: {
    startExpanded: true,
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock(
        {
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
                childIds: ['liveopsEvent:past-event', 'liveopsEvent:hello-world', 'liveopsEvent:active-event'],
              },
              metadata: {
                displayName: 'Row 0',
              },
              renderData: {
                cannotRemoveReason: null,
              },
            } satisfies TimelineItemRow,
            'liveopsEvent:past-event': {
              itemType: 'liveopsEvent',
              version: 0,
              hierarchy: {
                parentId: 'row:0',
              },
              metadata: {
                displayName: 'Scheduled Past Event',
                color: '#123456',
              },
              renderData: {
                isTargeted: false,
                isLocked: false,
                isImmutable: true,
                state: 'concluded',
                isRecurring: false,
                timelinePosition: {
                  startInstantIsoString: '1999-12-26T00:00:00.000Z',
                  endInstantIsoString: '1999-12-29T00:00:00.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  previewDurationIsoString: 'P1D',
                  reviewDurationIsoString: 'P1D',
                },
              },
            } satisfies TimelineItemLiveopsEvent,
            'liveopsEvent:hello-world': {
              itemType: 'liveopsEvent',
              version: 0,
              hierarchy: {
                parentId: 'row:0',
              },
              metadata: {
                displayName: 'Scheduled Future Event',
                color: '#123456',
              },
              renderData: {
                isTargeted: false,
                isLocked: true,
                isImmutable: true,
                state: 'scheduled',
                isRecurring: false,
                timelinePosition: {
                  startInstantIsoString: '2000-01-02T00:00:00.000Z',
                  endInstantIsoString: '2000-01-05T24:00:00.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  reviewDurationIsoString: 'P1D',
                },
              },
            } satisfies TimelineItemLiveopsEvent,
            'liveopsEvent:active-event': {
              itemType: 'liveopsEvent',
              version: 0,
              hierarchy: {
                parentId: 'row:0',
              },
              metadata: {
                displayName: 'Scheduled Active Event',
                color: '#123456',
              },
              renderData: {
                isTargeted: false,
                isLocked: true,
                isImmutable: true,
                state: 'active',
                isRecurring: false,
                timelinePosition: {
                  startInstantIsoString: '1999-12-30T07:00:00.000Z',
                  endInstantIsoString: '2000-01-01T12:00:00.000Z',
                },
                schedule: {
                  timeMode: 'utc',
                  currentPhase: 'active',
                },
              },
            } satisfies TimelineItemLiveopsEvent,
          },
        },
        {
          'liveopsEvent:past-event': {
            itemType: 'liveopsEvent',
            displayName: 'Past Event',
            description: 'Past event. Lame.',
            details: {
              parameters: {
                some: 'data',
                other: 'data',
              },
            },
          },
          'liveopsEvent:hello-world': {
            itemType: 'liveopsEvent',
            displayName: 'Hello World',
            description:
              'This is the first event ever created in the timeline. It was a very important event. A tremendous event. Maybe the best event of all time. Just wow.',
            details: {
              parameters: {
                some: 'data',
                other: 'data',
              },
            },
          },
          'liveopsEvent:active-event': {
            itemType: 'liveopsEvent',
            displayName: 'Active Event',
            details: {
              parameters: {
                some: 'data',
                other: 'data',
              },
            },
          },
        }
      ),
  },
}

// Oh god I cannot face fixing this right now...
// export const Scenario1: Story = {
//   args: {
//     debug: true,
//     startExpanded: true,
//     timelineDataFetchHandler: () => new TimelineDataFetchHandlerMock(liveOpsScenario1),
//   },
// }

export const InfiniteEvents: Story = {
  args: {
    debug: true,
    startExpanded: true,
    timelineDataFetchHandler: () => new TimelineDataFetchHandlerInfiniteMock(100, 500),
  },
}

export const Loading: StoryObj<typeof MEventTimeline> = {
  render: (args) => ({
    components: { MEventTimeline },
    setup: () => ({ args }),
    template: `<MEventTimeline v-bind="args"></MEventTimeline>`,
  }),
  args: {},
}

export const EmptyData: StoryObj<typeof MEventTimeline> = {
  render: (args: unknown) => ({
    components: { MEventTimeline },
    setup: () => ({ args }),
    template: `<MEventTimeline v-bind="args"></MEventTimeline>`,
  }),
  args: {
    timelineData: {
      startInstantIsoString: '1999-12-21T00:00:00.000Z',
      endInstantIsoString: '2000-01-29T00:00:00.000Z',
      items: {},
    } satisfies TimelineData,
  },
}

export const LoadingErrorState: StoryObj<typeof MEventTimeline> = {
  render: (args) => ({
    components: { MEventTimeline },
    setup: () => ({ args }),
    template: `<MEventTimeline v-bind="args"></MEventTimeline>`,
  }),
  args: {
    loadingError: new DisplayError('Failed to load timeline data.', 'Please try again later.'),
  },
}
