import type { Meta, StoryObj } from '@storybook/vue3'

import MInputHintMessage from './MInputHintMessage.vue'

const meta: Meta<typeof MInputHintMessage> = {
  component: MInputHintMessage,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['neutral', 'primary', 'success', 'danger', 'warning'],
    },
  },
  render: (args) => ({
    components: { MInputHintMessage },
    setup: () => ({ args }),
    template: '<MInputHintMessage v-bind="args">This is a hint.</MInputHintMessage>',
  }),
  parameters: {
    docs: {
      description: {
        component: 'A hint message that can be used to provide additional information to the user in iput components.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputHintMessage>

/**
 * Demonstrates the default behavior of the `MInputHintMessage` component, showing a neutral hint message.
 */
export const Default: Story = {}

/**
 * Demonstrates the `MInputHintMessage` component with a primary variant, highlighting important information.
 */
export const Primary: Story = {
  render: (args) => ({
    components: { MInputHintMessage },
    setup: () => ({ args }),
    template: '<MInputHintMessage v-bind="args">This is a primary hint.</MInputHintMessage>',
  }),
  args: {
    variant: 'primary',
  },
}

/**
 * Demonstrates the `MInputHintMessage` component with a neutral variant, providing general information.
 */
export const Neutral: Story = {
  render: (args) => ({
    components: { MInputHintMessage },
    setup: () => ({ args }),
    template: '<MInputHintMessage v-bind="args">This is a neutral hint.</MInputHintMessage>',
  }),
  args: {
    variant: 'neutral',
  },
}

/**
 * Demonstrates the `MInputHintMessage` component with a success variant, indicating a successful operation.
 */
export const Success: Story = {
  render: (args) => ({
    components: { MInputHintMessage },
    setup: () => ({ args }),
    template: '<MInputHintMessage v-bind="args">This is a success hint.</MInputHintMessage>',
  }),
  args: {
    variant: 'success',
  },
}

/**
 * Demonstrates the `MInputHintMessage` component with a danger variant, warning about potential issues.
 */
export const Danger: Story = {
  render: (args) => ({
    components: { MInputHintMessage },
    setup: () => ({ args }),
    template: '<MInputHintMessage v-bind="args">This is a danger hint.</MInputHintMessage>',
  }),
  args: {
    variant: 'danger',
  },
}

/**
 * Demonstrates the `MInputHintMessage` component with a warning variant, alerting the user to potential problems.
 */
export const Warning: Story = {
  render: (args) => ({
    components: { MInputHintMessage },
    setup: () => ({ args }),
    template: '<MInputHintMessage v-bind="args">This is a warning hint.</MInputHintMessage>',
  }),
  args: {
    variant: 'warning',
  },
}
