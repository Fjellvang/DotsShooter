import { fn, within, userEvent, expect, waitFor } from '@storybook/test'
import type { Meta, StoryContext } from '@storybook/vue3'

import MInputSingleSelectRadio from './MInputSingleSelectRadio.vue'

const meta: Meta<typeof MInputSingleSelectRadio> = {
  // @ts-expect-error Storybook doesn't like generics.
  component: MInputSingleSelectRadio,
  tags: ['!autodocs', 'tests'],
  args: {
    label: 'Role',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
    ],
    modelValue: 'admin',
    'onUpdate:modelValue': fn(),
  },
  argTypes: {
    modelValue: {
      control: false,
    },
    variant: {
      control: { type: 'select' },
      options: ['primary', 'success', 'danger'],
    },
    size: {
      control: { type: 'radio' },
      options: ['default', 'small'],
    },
  },
  render: (args) => ({
    components: { MInputSingleSelectRadio },
    setup: () => ({ args }),
    data: () => ({ selected: args.modelValue }),
    template: `<div>
      <MInputSingleSelectRadio v-bind="args" v-model="selected"/>
      <pre class="tw-mt-2">Output: {{ selected }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component:
          'The `MInputSingleSelectRadio` component allows users to select a single option from a set of mutually exclusive choices. This component is ideal for scenarios where users need to choose one value from a list of distinct options.',
      },
    },
  },
}

export default meta

/**
 * Test case for ensuring that the radio group can be interacted with.
 */
export const DefaultTestStory = {
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('admin-input')
    const secondRadio = canvas.getByTestId('user-radio-button')
    const secondOption = canvas.getByTestId('user-input')
    const thirdOption = canvas.getByTestId('guest-input')

    // 1. Check that the first option is selected by default.
    await userEvent.click(firstOption)
    await expect(firstOption).toBeChecked()

    // 2. Select the second option.
    await userEvent.click(secondRadio)
    await waitFor(async () => {
      await expect(firstOption).not.toBeChecked()
      await expect(secondOption).toBeChecked()
    })

    // 3. Select the third option.
    await userEvent.click(thirdOption)
    await waitFor(async () => {
      await expect(firstOption).not.toBeChecked()
      await expect(secondOption).not.toBeChecked()
      await expect(thirdOption).toBeChecked()
    })
  },
}

/**
 * Test case to verify if the `MInputSingleSelectRadio` can handle generic options.
 */
export const CustomOptionsTestStory = {
  args: {
    options: [
      { label: 'Option 1', value: { id: 1, name: 'test 1' } },
      { label: 'Option 2', value: { id: 2, name: 'test 2' } },
      { label: 'Option 3', value: { id: 3, name: 'test 3' } },
    ],
    modelValue: { id: 1, name: 'test 1' },
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('option-1-input')
    const secondOption = canvas.getByTestId('option-2-input')
    const thirdOption = canvas.getByTestId('option-3-input')

    // 1. Check that the first option is selected by default.
    await expect(firstOption).toBeChecked()
    await expect(secondOption).not.toBeChecked()
    await userEvent.click(thirdOption)
    await expect(thirdOption).toBeChecked()
  },
}

/**
 * Test case for ensuring that the radio group can be disabled.
 */
export const DisabledTestStory = {
  args: {
    disabledTooltip: 'This radio group is disabled',
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('admin-input')
    const firstRadio = canvas.getByTestId('admin-radio-button')
    const secondOption = canvas.getByTestId('user-input')
    const secondRadio = canvas.getByTestId('user-radio-button')
    const tooltip = canvas.getByTestId('tooltip-Role')

    // 1. Check that the first option is selected by default.
    await userEvent.click(firstOption)
    await expect(firstOption).toBeChecked()
    await expect(firstRadio).toHaveStyle({ cursor: 'not-allowed' })

    // 2. Select the second option.
    await userEvent.click(secondOption)
    await expect(secondRadio).toHaveStyle({ cursor: 'not-allowed' })
    await waitFor(async () => {
      await expect(secondOption).not.toBeChecked()
      await expect(tooltip).toBeVisible()
    })
  },
}

/**
 * Test case for ensuring that a single option in the radio group can be disabled.
 */
export const DisabledOptionTestStory = {
  args: {
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user', disabled: true },
      { label: 'Guest', value: 'guest' },
    ],
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('admin-input')
    const secondOption = canvas.getByTestId('user-input')
    const secondRadio = canvas.getByTestId('user-radio-button')
    const thirdOption = canvas.getByTestId('guest-input')
    const thirdRadio = canvas.getByTestId('guest-radio-button')

    // 1. Check that the first option is selected by default.
    await userEvent.click(firstOption)

    // 2. Verify that the second option is disabled and cannot be selected.
    await expect(secondRadio).toHaveStyle({ cursor: 'not-allowed' })
    await userEvent.click(secondOption)
    await waitFor(async () => {
      await expect(secondOption).not.toBeChecked()
    })

    // 3. Verify that the third option is enabled and can be selected.
    await expect(thirdRadio).toHaveStyle({ cursor: 'pointer' })
    await userEvent.click(thirdOption)
    await waitFor(async () => {
      await expect(thirdOption).toBeChecked()
    })
  },
}

/**
 * Test case for ensuring that the radio group can be rendered without a default value.
 */
export const NoDefaultTestStory = {
  args: {
    modelValue: undefined,
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('admin-input')
    const secondOption = canvas.getByTestId('user-input')
    const thirdOption = canvas.getByTestId('guest-input')

    // 1. Check that the none of the options are selected by default.
    await expect(firstOption).not.toBeChecked()
    await expect(secondOption).not.toBeChecked()
    await expect(thirdOption).not.toBeChecked()
  },
}

/**
 * Test case for ensuring that the radio group can be navigated using the keyboard.
 */
export const AccessibilityTestStory = {
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('admin-input')
    const secondOption = canvas.getByTestId('user-input')

    // 1. Verify that the first option is selected by default.
    await expect(firstOption).toBeChecked()

    // Test keyboard navigation
    firstOption.focus()
    await userEvent.keyboard('{arrowdown}')

    // 2. Verify that the second option is selected and the first option is deselected.
    await expect(firstOption).not.toBeChecked()
    await expect(secondOption).toBeChecked()

    // Test keyboard navigation
    await userEvent.keyboard('{arrowup}')

    // 3. Verify that the first option is selected and the second option is deselected.
    await expect(firstOption).toBeChecked()
  },
}

/**
 * Test case to ensure the contextual colors of the radio group can be customized.
 */
export const variantTestStory = {
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('admin-input')
    const firstRadio = canvas.getByTestId('admin-control')

    // 1. Check that the first option is selected by default.
    await expect(firstOption).toBeChecked()

    // 2. Verify the default variant.
    await expect(firstRadio).toHaveClass('tw-bg-blue-500')

    // 3. Set the variant to 'success'.
    args.variant = 'success'
    await waitFor(async () => {
      await expect(firstRadio).toHaveClass('tw-bg-green-500')
    })

    // 4. Set the variant to 'danger'.
    args.variant = 'danger'
    await waitFor(async () => {
      await expect(firstRadio).toHaveClass('tw-bg-red-500')
    })
  },
}
