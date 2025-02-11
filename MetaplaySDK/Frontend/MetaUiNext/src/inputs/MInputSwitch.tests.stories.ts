import { fn, within, userEvent, expect, waitFor } from '@storybook/test'
import type { Meta, StoryContext, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MInputSwitch from './MInputSwitch.vue'

const meta: Meta<typeof MInputSwitch> = {
  component: MInputSwitch,
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger', 'warning'],
    },
    size: {
      control: { type: 'radio' },
      options: ['extraSmall', 'small', 'default'],
    },
  },
  args: {
    modelValue: true,
    'onUpdate:modelValue': fn(),
  },
  parameters: {
    docs: {
      description: {
        component: 'The `MInputSwitch` component tests.',
      },
    },
  },
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => ({ args }),
    template: `<div>
      <MInputSwitch v-bind="args" data-testid="test"/>
    </div>`,
  }),
}

export default meta

/**
 * Test 1: Check default switch state and toggling it.
 */
export const Default = {
  args: {
    name: 'default switch',
  },
  play: async ({ canvasElement }: StoryContext) => {
    const canvas = within(canvasElement)
    const switchInput = canvas.getByTestId('test-switch-input')

    // Assert that the switch is initially checked (true).
    await expect(switchInput).toBeChecked()

    // Simulate clicking the switch to toggle it on.
    await userEvent.click(switchInput)

    // Assert that the switch is now unchecked (false).
    await expect(switchInput).not.toBeChecked()
  },
}

/**
 * Test 2: Verify that the switch is disabled and it cannot be toggled when disabled prop is `true`.
 */
export const DisabledByDefault = {
  args: {
    modelValue: false,
    disabled: true,
  },
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const switchInput = canvas.getByTestId('test-switch-input')

    // Initial State: Assert that the switch is disabled and not checked.
    await expect(switchInput).toBeDisabled()
    await expect(switchInput).not.toBeChecked()

    // Simulate clicking the switch to toggle it off.
    await userEvent.click(switchInput)
    // Assert that the switch is still not checked.
    await expect(switchInput).not.toBeChecked()

    // Simulate clicking the switch to toggle it off.
    args.disabled = false
    await waitFor(async () => {
      // Assert that the switch is no longer disabled.
      await expect(switchInput).not.toBeDisabled()
    })
    // Simulate clicking the switch to toggle it on.
    await userEvent.click(switchInput)

    // Assert that the switch is checked.
    await expect(switchInput).toBeChecked()
  },
}

/**
 * Test 3: Verify that switch sizes can be adjusted using the `size` prop.
 */
export const SwitchSizes = {
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const switchInput = canvas.getByTestId('test-switch-control')

    // Assert that the switch is initially the default size.
    await expect(switchInput).toHaveClass('tw-w-11')

    args.size = 'small'
    // Assert that the switch is now the small size.
    await waitFor(async () => {
      await expect(switchInput).toHaveClass('tw-w-8')
    })

    args.size = 'extraSmall'
    // Assert that the switch is now the extra small size.
    await waitFor(async () => {
      await expect(switchInput).toHaveClass('tw-w-6')
    })
  },
}

/**
 * Test 4: Verify that switch variants can be adjusted using the `variant` prop.
 */
export const SwitchVariants = {
  play: async ({ canvasElement, args }: StoryContext) => {
    const canvas = within(canvasElement)
    const switchControl = canvas.getByTestId('test-switch-control')

    // Assert that the switch is initially the primary variant
    await expect(switchControl).toHaveClass('tw-bg-blue-500')

    args.variant = 'success'
    // Assert that the switch is now the success variant
    await waitFor(async () => {
      await expect(switchControl).toHaveClass('tw-bg-green-500')
    })

    args.variant = 'danger'
    // Assert that the switch is now the danger variant
    await waitFor(async () => {
      await expect(switchControl).toHaveClass('tw-bg-red-500')
    })
  },
}

/**
 * Test 5: Verify that permissions can be used to disable the switch.
 */
export const Permission: StoryObj<typeof MInputSwitch> = {
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])
      return {
        args,
      }
    },
    template: `<MInputSwitch v-bind="args" data-testid="test"/>`,
  }),
  play: async ({ canvasElement, args }) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const canvas = within(canvasElement)
    const switchInput = canvas.getByTestId('test-switch-input')
    // Assert that the switch is initially enabled (false).
    await expect(switchInput).not.toBeDisabled()

    // Set the permission to disable the switch.
    // @ts-expect-error -- Does this actually work? Typings say no.
    args.permission = 'new permission'
    await waitFor(async () => {
      // Find the switch input again.
      const switchInput = canvas.getByTestId('test-switch-input')
      // Assert that the switch is now disabled (true).
      await expect(switchInput).toBeDisabled()
    })
  },
}
