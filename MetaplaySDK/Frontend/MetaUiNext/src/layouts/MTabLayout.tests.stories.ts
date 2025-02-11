// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { vueRouter } from 'storybook-vue3-router'

import { within, userEvent, expect, waitFor, fireEvent } from '@storybook/test'
import type { Meta, StoryContext } from '@storybook/vue3'

import MCard from '../primitives/MCard.vue'
import MSingleColumnLayout from './MSingleColumnLayout.vue'
import MTabLayout from './MTabLayout.vue'

const meta: Meta<typeof MTabLayout> = {
  component: MTabLayout,
  parameters: {
    docs: {
      description: {
        component: 'The `MTabLayout` component tests.',
      },
    },
    viewport: {
      defaultViewport: 'singleColumn',
    },
  },
  tags: ['!autodocs, tests'],
  // Note: The `vueRouter` settings below do not integrate well, e.g. changing path outputs to the default '/' no matter what.
  decorators: [vueRouter([{ path: '/', name: 'tabs', component: MTabLayout }])],
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      const listItems = [
        {
          title: 'Item 1',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
        {
          title: 'Item 2',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
        {
          title: 'Item 3',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
      ]
      return { args, listItems }
    },
    template: `
     <div>
        <MTabLayout v-bind="args">
          <template #tab-1>
            <MSingleColumnLayout>
              <MCard title="Tab 1 Card">
                <p>The initial tab is set to tab 1, instead of the default 0.</p>
                <p class="tw-mt-2">Test this by changing the props in Storybook.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
      </div>
    `,
  }),
  argTypes: {
    currentTab: {
      // Storybook does not handle the runtime error catching for initialTab correctly, manually limiting it to 0 and 1.
      control: { type: 'number', min: 0 },
    },
  },
}

export default meta

/**
 * Test that the MTabLayout component renders with the default props and
 * that the tabs are displayed correctly.
 */
export const DefaultTestStory = {
  args: {
    tabs: [{ label: 'Example Tab 1' }, { label: 'Example Tab 2' }],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const tabButtons = canvas.getAllByRole('tab')

    // Assert that the tab layout is rendered.
    await expect(tabButtons[0]).toBeInTheDocument()

    // Assert that the tab buttons are rendered.
    await expect(tabButtons).toHaveLength(2)
    await expect(tabButtons[0]).toHaveTextContent('Example Tab 1')
    await expect(tabButtons[1]).toHaveTextContent('Example Tab 2')

    // Assert that the first tab is active.
    await expect(tabButtons[0]).toHaveAttribute('aria-selected', 'true')
    await expect(tabButtons[1]).not.toHaveAttribute('aria-selected', 'true')

    // Click the another tab.
    await userEvent.click(tabButtons[1])
    await waitFor(async () => {
      await expect(tabButtons[0]).not.toHaveAttribute('aria-selected', 'true')
      await expect(tabButtons[1]).toHaveAttribute('aria-selected', 'true')
    })
  },
}

/**
 * Test that correct url is displayed when switching tabs.
 */
/* export const RouteUrlTestStory = {
  args: {
    tabs: [{ label: 'Tab 1' }, { label: 'Tab 2' }],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const tabButtons = canvas.getAllByRole('tab')
    const routeUrl = canvas.getByTestId('route-url')

    // Click the second tab.
    await userEvent.click(tabButtons[1])
    await waitFor(async () => {
      await expect(routeUrl).toHaveTextContent('/?tab=1')
    })

    // Click the first tab.
    await userEvent.click(tabButtons[0])
    await waitFor(async () => {
      await expect(routeUrl).toHaveTextContent('/?tab=0')
    })
  },
} */

/**
 * Test that a user can set the initial tab to be displayed.
 */
export const InitialTabTestStory = {
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }, { label: 'Tab 3' }],
    currentTab: 1,
  },
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const tabButtons = canvas.getAllByRole('tab')

    // Assert that the second tab is active.
    await expect(tabButtons[1]).toHaveAttribute('aria-selected', 'true')
    await expect(tabButtons[0]).not.toHaveAttribute('aria-selected', 'true')

    // Update the current tab to be the third tab.
    args.currentTab = 3
    await waitFor(async () => {
      await expect(tabButtons[1]).not.toHaveAttribute('aria-selected', 'true')
      await expect(tabButtons[3]).toHaveAttribute('aria-selected', 'true')
    })
  },
}

/**
 * Test that tab highlighting works correctly.
 */
export const TabHighlightTestStory = {
  args: {
    tabs: [{ label: 'Tab 0', highlighted: true }, { label: 'Tab 1' }, { label: 'Tab 2' }],
  },
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const highlightedTab = canvas.getByTestId('tab-0-highlighted')

    // Assert that the highlighted tab is rendered.
    await expect(highlightedTab).toBeVisible()

    // Update the args to change the highlighted state of the first tab.
    args.tabs = [
      { label: 'Tab 0', highlighted: false }, // Update the first tab's highlighted to false
      { label: 'Tab 1' },
      { label: 'Tab 2', highlighted: true }, // Update the third tab's highlighted to true
    ]

    // Assert that the highlighted tab is no longer rendered.
    await waitFor(async () => {
      const highlightedTab2 = canvas.getByTestId('tab-2-highlighted')
      await expect(highlightedTab).not.toBeVisible()
      await expect(highlightedTab2).toBeVisible()
    })
  },
}

/**
 * Test that the tab content is rendered correctly.
 */
export const TabContentTestStory = {
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const tabButtons = canvas.getAllByRole('tab')
    const tabContent = canvas.getByTestId('tab-content')

    // Click the second tab.
    await userEvent.click(tabButtons[1])
    await waitFor(async () => {
      await expect(tabContent).toHaveTextContent('Tab 1 Card')
    })

    // Click the third tab.
    await userEvent.click(tabButtons[2])
    await waitFor(async () => {
      // Assert that the first tab content is no longer visible.
      await expect(tabContent).not.toHaveTextContent('Tab 1 Card')
      // Assert that the third tab content is visible.
      await expect(tabContent).toHaveTextContent('Tab 2')
    })

    // Click the first tab.
    await userEvent.click(tabButtons[0])
    await waitFor(async () => {
      // Assert that the first tab content is visible.
      await expect(tabContent).toHaveTextContent('Tab 0')
      // Assert that the third tab content is no longer visible.
      await expect(tabContent).not.toHaveTextContent('Tab 2')
    })
  },
}

/**
 * Test that you can disable and enable tabs.
 */
export const TabDisabledTestStory = {
  args: {
    tabs: [
      { label: 'Tab 0', disabledTooltip: 'This tab is disabled' },
      { label: 'Tab 1' },
      { label: 'Tab 2', disabledTooltip: 'This is disabled' },
    ],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const tabButtons = canvas.getAllByRole('tab')

    // Assert that the first and third tabs are disabled.
    await expect(tabButtons[0]).toBeDisabled()
    // Check you can't click on the disabled tab
    await expect(tabButtons[0]).toHaveStyle({ pointerEvents: 'none' })
    await expect(tabButtons[2]).toBeDisabled()

    // Assert that the disabled tooltip is rendered.
    const tabTooltip = canvas.getByTestId('tab-0-tooltip')
    await fireEvent.mouseOver(tabButtons[0])
    await expect(tabTooltip).toBeVisible()

    // Verify that tab content for tab 0 is not visible.
    const tabContent = canvas.getByTestId('tab-content')
    await expect(tabContent).not.toHaveTextContent('Tab 0')
    await expect(tabContent).toHaveTextContent('Tab 1')
    await expect(tabContent).not.toHaveTextContent('Tab 2')
  },
}

/**
 * Test tab component in mobile view.
 * The component should render a dropdown when the viewport is set to mobile.
 */
export const MobileViewTestStory = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile', // Set the viewport to mobile
    },
  },
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const input = canvas.getByTestId('tab-input')
    const trigger = canvas.getByTestId('tab-trigger')
    const selectedOption = canvas.getByTestId('tab-selected-option')
    const tabContent = canvas.getByTestId('tab-content')

    // Assert that the dropdown button is visible.
    await expect(input).toBeVisible()
    await expect(trigger).toBeVisible()

    // Assert that the dropdown initially displays the first tab and its content .is visible
    await expect(selectedOption).toHaveTextContent('Tab 0')
    await expect(tabContent).toHaveTextContent('Tab 0')

    // Click the dropdown button.
    await userEvent.click(input)
    const selectOption1 = canvas.getByTestId('tab-Tab 1')
    const selectOption2 = canvas.getByTestId('tab-Tab 2')
    await userEvent.keyboard('[ArrowDown]')

    // Assert that the dropdown menu is visible. and displays the other tabs.
    await expect(selectOption1).toBeVisible()
    await expect(selectOption2).toBeVisible()

    // Click the second option.
    await fireEvent.click(selectOption1)
    await userEvent.keyboard('[Enter]')

    // Assert that the second tab is now active .
    await expect(selectOption1).toHaveAttribute('data-state', 'checked')

    // Assert that the dropdown button now shows the label and content for the second tab
    await expect(selectedOption).toHaveTextContent('Tab 1')
    await expect(tabContent).toHaveTextContent('Tab 1 Card')
  },
}

/**
 * Test mobile view with initial tab set.
 */
export const MobileViewInitialTabTestStory = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile', // Set the viewport to mobile
    },
  },
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
    currentTab: 1,
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)

    const input = canvas.getByTestId('tab-input')
    const selectedOption = canvas.getByTestId('tab-selected-option')
    const tabContent = canvas.getByTestId('tab-content')

    // Assert that the dropdown button is visible.
    await expect(input).toBeVisible()

    // Assert that the dropdown initially displays the second tab and its content is visible.
    await expect(selectedOption).toHaveTextContent('Tab 1')
    await expect(tabContent).toHaveTextContent('Tab 1 Card')

    // Click the dropdown button.
    await userEvent.click(input)
    const selectOption0 = canvas.getByTestId('tab-Tab 0')
    const selectOption2 = canvas.getByTestId('tab-Tab 2')

    // Assert that the dropdown menu is visible and displays the other tabs.
    await expect(selectOption0).toBeVisible()
    await expect(selectOption2).toBeVisible()

    // Click the first option (Tab 0).
    await userEvent.click(selectOption0)
    await userEvent.keyboard('[Enter]')
    // Assert that the dropdown button now shows the label and content for the first tab.
    await expect(selectedOption).toHaveTextContent('Tab 0')
    await expect(tabContent).toHaveTextContent('Tab 0')
    // Assert that the first tab is now active.
    await userEvent.keyboard('[Enter]')
    await expect(selectOption0).toHaveAttribute('data-state', 'checked')
  },
}

/**
 * Test mobile view with disabled tab in dropdown.
 */
export const MobileViewDisabledTabTestStory = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile', // Set the viewport to mobile
    },
  },
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1', disabledTooltip: 'This tab is disabled' }, { label: 'Tab 2' }],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const input = canvas.getByTestId('tab-input')
    const selectedOption = canvas.getByTestId('tab-selected-option')
    const tabContent = canvas.getByTestId('tab-content')

    // Assert that the dropdown button is visible.
    await expect(input).toBeVisible()
    await expect(selectedOption).toBeVisible()

    // Assert that the dropdown initially displays the first tab and its content is visible.
    await expect(selectedOption).toHaveTextContent('Tab 0')
    await expect(tabContent).toHaveTextContent('Tab 0')

    // Click the dropdown button.
    await userEvent.click(input)
    await waitFor(async () => {
      const selectOption1 = canvas.getByTestId('tab-Tab 1')
      const selectOption2 = canvas.getByTestId('tab-Tab 2')

      // Assert that the dropdown menu is visible and displays the other tabs.
      await expect(selectOption1).toBeVisible()
      await expect(selectOption2).toBeVisible()

      // Assert that the second option (Tab 1) is disabled.
      await expect(selectOption1).toHaveAttribute('disabled', 'true')

      // Asser that the third option (Tab 2) is not disabled.
      await expect(selectOption2).not.toHaveAttribute('disabled', 'true')

      // Attempt to click the disabled option (Tab 1).
      await userEvent.click(selectOption1)

      // Assert that the disabled tab is not selected.
      await expect(selectOption1).toHaveAttribute('data-state', 'unchecked')
      await expect(tabContent).not.toHaveTextContent('Tab 1 Card')
    })
  },
}
