import { DateTime } from 'luxon'

import { expect, waitFor, within } from '@storybook/test'
import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../../composables/usePermissions'
import StorybookEventTimelineWrapper from './StorybookEventTimelineWrapper.vue'
import { TimelineDataFetchHandlerMock } from './timelineDataFetcherMocks'

usePermissions().setPermissions([]) //['api.liveops_events.view', 'api.liveops_events.edit'])

const meta: Meta<typeof StorybookEventTimelineWrapper> = {
  component: StorybookEventTimelineWrapper,
  parameters: {
    docs: {
      description: {
        component:
          'A subcomponent of the timeline that displays the actual timeline container and handles user input like scrolling and zooming.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof StorybookEventTimelineWrapper>

/**
 * Timeline starts at two days before UTC now.
 */
export const Default: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {
          'root:0': {
            itemType: 'root',
            version: 0,
            hierarchy: {
              childIds: [],
            },
          },
        },
      }),
  },
  play: async ({ canvasElement }) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const canvas = within(canvasElement)

    // Wait for the timeline to load.
    await waitFor(async () => {
      await expect(canvas.getByTestId('timeline-container')).toBeInTheDocument()
    })

    // Assert that the timeline started at the correct date.
    await expect(canvas.getByTestId('current-month-label')).toHaveTextContent('December 1999')
  },
}

/**
 * Timeline can be started at a specific instant. The second day of January 2000 is used in this example.
 */
export const ManualStartInstant: Story = {
  args: {
    initialVisibleTimelineStartInstant: DateTime.fromISO('2000-01-02T00:00:00.000Z'),
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {
          'root:0': {
            itemType: 'root',
            version: 0,
            hierarchy: {
              childIds: [],
            },
          },
        },
      }),
  },
  play: async ({ canvasElement }) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const canvas = within(canvasElement)

    // Wait for the timeline to load.
    await waitFor(async () => {
      await expect(canvas.getByTestId('timeline-container')).toBeInTheDocument()
    })

    // Assert that the timeline started at the correct date.
    await expect(canvas.getByTestId('current-month-label')).toHaveTextContent('January 2000')
  },
}

/**
 * Timeline zoomed to hours.
 */
/** Temporarily disabled to reduce noise during development
export const ZoomedToHours: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {},
      }),
    initialZoomLevel: 'hours',
    debug: true,
  },
}
*/

/**
 * Timeline zoomed to days.
 */
/** Temporarily disabled to reduce noise during development
export const ZoomedToDays: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {},
      }),
    initialZoomLevel: 'days',
    debug: true,
  },
}
*/

/**
 * Timeline zoomed to months.
 */
/** Temporarily disabled to reduce noise during development
export const ZoomedToMonths: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {},
      }),
    initialZoomLevel: 'months',
    debug: true,
  },
}
*/

/**
 * Timeline zoomed to years.
 */
/** Temporarily disabled to reduce noise during development
export const ZoomedToYears: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {},
      }),
    initialZoomLevel: 'years',
    debug: true,
  },
}
*/

/**
 * Pressing the today button scrolls the timeline to the current date.
 */
/** Temporarily disabled to reduce noise during development
export const ScrollToTodayWithButton: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {},
      }),
  },
  play: async ({ canvasElement, step }) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const canvas = within(canvasElement)

    // Wait for the timeline to load.
    await waitFor(async () => {
      await expect(canvas.getByTestId('timeline-container')).toBeInTheDocument()
    })

    // Click the today button.
    // eslint-disable-next-line @typescript-eslint/no-unsafe-call
    await step('Click the today button', async () => {
      await userEvent.click(canvas.getByTestId('today-button'))
    })

    // Wait for the timeline to scroll to today.
    await waitFor(async () => {
      await expect(canvas.getByTestId('current-month-label')).not.toHaveTextContent('December 1999')
    })

    // Assert that the timeline scrolled to today.
    await expect(canvas.getByTestId('current-month-label')).toHaveTextContent('January 2000')
  },
}
*/

/**
 * Pressing CTRL+T scrolls the timeline to the current date.
 */
/** Temporarily disabled to reduce noise during development
export const ScrollToTodayWithKeyboard: Story = {
  args: {
    timelineDataFetchHandler: () =>
      new TimelineDataFetchHandlerMock({
        startInstantIsoString: '1999-12-21T00:00:00.000Z',
        endInstantIsoString: '2000-01-29T00:00:00.000Z',
        items: {},
      }),
  },
  play: async ({ canvasElement, step }) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const canvas = within(canvasElement)

    // Wait for the timeline to load.
    await waitFor(async () => {
      await expect(canvas.getByTestId('timeline-container')).toBeInTheDocument()
    })

    // Enter the CTRL+T keyboard shortcut.
    // eslint-disable-next-line @typescript-eslint/no-unsafe-call
    await step('Click the today button', async () => {
      await userEvent.keyboard('{Control>}t', { document })
    })

    // Wait for the timeline to scroll to today.
    await waitFor(async () => {
      await expect(canvas.getByTestId('current-month-label')).not.toHaveTextContent('December 1999')
    })

    // Assert that the timeline scrolled to today.
    await expect(canvas.getByTestId('current-month-label')).toHaveTextContent('January 2000')
  },
}
*/

// TODO: data loading story, scrolling story, zooming story, etc.
