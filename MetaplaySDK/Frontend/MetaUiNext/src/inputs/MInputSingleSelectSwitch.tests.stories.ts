import { fn, within, userEvent, expect, waitFor, fireEvent } from '@storybook/test'
import type { Meta, StoryContext } from '@storybook/vue3'

import MInputSingleSelectSwitch from './MInputSingleSelectSwitch.vue'

const meta: Meta<typeof MInputSingleSelectSwitch> = {
  // @ts-expect-error Storybook doesn't seem to like generics?
  component: MInputSingleSelectSwitch,
  tags: ['tests', '!autodocs'],
  decorators: [() => ({ template: '<div style="margin-top: 1em;"><story/></div>' })],
  argTypes: {
    size: {
      control: { type: 'select' },
      options: ['small', 'default'],
    },
    variant: {
      control: { type: 'select' },
      options: ['primary', 'success', 'danger', 'warning', 'neutral'],
    },
  },
  args: {
    modelValue: 'option1',
    'onUpdate:modelValue': fn(),
    options: [
      { label: 'Option 1', value: 'option1' },
      { label: 'Option 2', value: 'option2' },
      { label: 'Option 3', value: 'option3' },
      { label: 'Option 4', value: 'option4' },
    ],
  },
  parameters: {
    viewport: { defaultViewport: 'threeColumn' },
    docs: {
      description: {
        component: 'The `MInputSingleSelectSwitch` component interaction tests',
      },
    },
  },
  render: (args) => ({
    components: { MInputSingleSelectSwitch },
    setup: () => ({ args }),
    data: () => ({ modelValue: args.modelValue }),
    template: `<div>
      <MInputSingleSelectSwitch v-bind="args" v-model="modelValue"/>
    </div>`,
  }),
}

export default meta

//- Interaction tests-----------------------------------------------------------------------------------------------

/**
 * Test case to verify that the switch can be toggled between options.
 */
export const DefaultTestStory = {
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    // 1. Check that the first option is selected by default.
    const firstOption = canvas.getByTestId('option-1-input')
    await expect(firstOption).toBeChecked()

    // 2. Verify that the second option is not selected.
    const secondOption = canvas.getByTestId('option-2-input')
    await expect(secondOption).not.toBeChecked()

    // 3. click the third option and verify that it is now selected.
    const thirdOption = canvas.getByTestId('option-3-input')
    await userEvent.click(thirdOption)
    await expect(thirdOption).toBeChecked()
  },
}

/**
 * Test case to verify that the switch can be disabled.
 */
export const DisabledTestStory = {
  args: { disabled: true },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    // 1. Check that the first option is disabled.
    const firstOption = canvas.getByTestId('option-1-input')
    await expect(firstOption).toBeDisabled()

    // 2. Try to select the second option and assert that it cannot be checked.
    const secondOption = canvas.getByTestId('option-2-input')
    await userEvent.click(secondOption)
    await expect(secondOption).not.toBeChecked()

    // 3. Assert that the second option's label has a 'not-allowed' cursor style when disabled.
    const secondOptionLabel = canvas.getByTestId('option-2-label')
    await expect(secondOptionLabel).toHaveStyle({ cursor: 'not-allowed' })
  },
}

/**
 * Test case to verify that the switch variant can be customized.
 */
export const VariantTestStory = {
  play: async ({ canvasElement, args }: StoryContext) => {
    const switchIndicator = canvasElement.querySelector('[data-part="indicator"]')

    // 1. Set the initial variant to 'primary' and verify that the switch indicator has the 'primary' variant class.
    args.variant = 'primary'
    await expect(switchIndicator).toHaveClass('tw-bg-blue-500')

    // 2. Set the variant to 'success' and verify that the switch indicator has the 'success' variant class.
    args.variant = 'success'
    await waitFor(async () => {
      await expect(switchIndicator).toHaveClass('tw-bg-green-500')
    })
  },
}

/**
 * Test case to verify that the switch size can be customized.
 */
export const SizeTestStory = {
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('option-1-label')

    // 1. Set the initial size to 'default' and verify that the first option has the 'default' size class.
    args.size = 'default'
    await expect(firstOption).toHaveClass('tw-text-sm tw-px-3')

    // 2. Set the size to 'small' and verify that the first option has the 'small' size class.
    args.size = 'small'
    await waitFor(async () => {
      await expect(firstOption).toHaveClass('tw-text-xs tw-px-2')
    })
  },
}

/**
 * Test case to verify that the switch can handle dynamic options.
 */
export const AddingExtraOptionsTestStory = {
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const firstOption = canvas.getByTestId('option-1-input')
    const secondOption = canvas.getByTestId('option-2-input')
    const thirdOption = canvas.getByTestId('option-3-input')

    // 1. Check that string options.
    await expect(firstOption).toBeChecked()
    await expect(secondOption).not.toBeChecked()
    await userEvent.click(thirdOption)
    await expect(thirdOption).toBeChecked()

    args.options = [
      ...(args.options as Array<{ label: string; value: string }>),
      { label: 'Option 5', value: 'option5' },
      { label: 'Option 6', value: 'option6' },
    ]
    await userEvent.click(thirdOption)
    await expect(thirdOption).toBeChecked()

    // 2. Check that the new options are added.

    const fifthOption = canvas.getByTestId('option-5-input')
    const sixthOption = canvas.getByTestId('option-6-input')

    await waitFor(async () => {
      await expect(fifthOption).toBeVisible()
      await expect(sixthOption).toBeVisible()

      //TODO: Fix this test the dropdown is broken.
      // await userEvent.click(fifthOption)
      // await expect(fifthOption).toBeChecked()
      // await expect(sixthOption).not.toBeChecked()
    })
  },
}

/**
 * Test case to verify if the switch can handle generic options.
 */
export const CustomOptionsTestStory = {
  args: {
    options: [
      { label: 'Option 1', value: { id: 1, name: 'thing 1' } },
      { label: 'Option 2', value: { id: 2, name: 'thing 2' } },
      { label: 'Option 3', value: { id: 3, name: 'thing 3' } },
    ],
    modelValue: { id: 1, name: 'thing 1' },
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
 * Test case to verify that the switch converts to a dropdown select in mobile view.
 * Test is commmented out till dropdown select is able to better handle generic options.
 */
export const MobileViewTestStory = {
  parameters: { viewport: { defaultViewport: 'mobile' } },
  args: { dataTestid: 'mobile-view-test' },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const input = canvas.getByTestId('mobile-view-test-input')
    const trigger = canvas.getByTestId('mobile-view-test-trigger')
    await expect(input).toBeVisible()
    await expect(trigger).toBeVisible()
    await userEvent.click(input)

    const dropdown = canvas.getByTestId('mobile-view-test-dropdown')
    await expect(dropdown).toBeVisible()

    // 1. Check that the first option is selected by default.
    const firstMobileOption = canvas.getByTestId('mobile-view-test-Option 1')
    await expect(firstMobileOption).toHaveAttribute('data-state', 'checked')

    // 2. Verify that the second option is not selected.
    const secondMobileOption = canvas.getByTestId('mobile-view-test-Option 2')
    await expect(secondMobileOption).toHaveAttribute('data-state', 'unchecked')

    // 3. Click the third option and verify that it is now selected.
    await userEvent.click(dropdown)
    const thirdMobileOption = canvas.getByTestId('mobile-view-test-Option 3')
    await fireEvent.click(thirdMobileOption)
    await expect(thirdMobileOption).toHaveAttribute('data-state', 'checked')
    await userEvent.click(dropdown)
    await expect(firstMobileOption).toHaveAttribute('data-state', 'unchecked')
  },
}

/**
 * Test case to verify that the dropdown select is disabled in mobile view.
 */
export const MobileViewDisabledTestStory = {
  args: { disabled: true, dataTestid: 'mobile-view-test' },
  parameters: { viewport: { defaultViewport: 'mobile' } },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const input = canvas.getByTestId('mobile-view-test-input')
    const trigger = canvas.getByTestId('mobile-view-test-trigger')

    // 1. Verify that the dropdown select is disabled.
    await userEvent.click(input)
    await expect(input).toBeDisabled()
    await expect(input).toHaveStyle({ cursor: 'not-allowed' })

    // 2. Verify that the trigger is disabled.
    await userEvent.click(trigger)
    await expect(trigger).toBeDisabled()
    await expect(trigger).toHaveStyle({ cursor: 'not-allowed' })
  },
}
