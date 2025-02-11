import { DateTime, Duration } from 'luxon'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputDuration from './MInputDuration.vue'

const meta: Meta<typeof MInputDuration> = {
  component: MInputDuration,
  tags: ['autodocs'],
  args: {
    modelValue: Duration.fromObject({ days: 1, hours: 2, minutes: 30 }),
  },
  argTypes: {
    modelValue: {
      control: false,
    },
    referenceDateTime: {
      control: false,
    },
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
  },
  parameters: {
    docs: {
      description: {
        component: 'MInputDuration is a simple numbers-based input for durations between one minute and one year.',
      },
    },
  },
  render: (args) => ({
    components: { MInputDuration },
    setup: () => ({ args }),
    data: () => ({ duration: args.modelValue }),
    template: `<div>
      <MInputDuration v-bind="args" v-model="duration"/>
      <pre class="tw-mt-2">Output: {{ duration }}  </pre>
    </div>`,
  }),
}

export default meta
type Story = StoryObj<typeof MInputDuration>

/**
 * The duration component allows you to input a duration in days, hours, and minutes using `MInputNumber` inputs.
 */
export const Default: Story = {
  args: {
    label: 'Duration',
    hintMessage: 'Negative values not allowed. Fractions are.',
  },
}

/**
 * The `referenceDateTime` prop allows you to set a reference time for the duration. This automatically previews when the duration would end in `UTC`.
 */
export const StartTime: Story = {
  args: {
    label: 'Duration with a start time (2021-1-1 12:00 UTC)',
    referenceDateTime: DateTime.fromISO('2021-01-01T12:00:00.000Z'),
  },
}

/**
 * The `disabled` prop disables the input.
 */
export const Disabled: Story = {
  args: {
    label: 'Duration (disabled)',
    modelValue: Duration.fromObject({ hours: 12 }),
    disabled: true,
  },
}

/**
 * The `success` variant is used to indicate that the input is valid.
 */
export const Success: Story = {
  args: {
    label: 'Success',
    variant: 'success',
    hintMessage: 'This is a success hint.',
  },
}
/**
 * The `danger` variant is used to indicate that the input is invalid.
 */
export const Danger: Story = {
  args: {
    label: 'Danger',
    variant: 'danger',
    hintMessage: 'This is a danger hint.',
  },
}

/**
 * You can use the input without a label.
 */
export const NoLabel: Story = {}

/**
 * Durations with negative or zero values are invalid. If an "instant" duration is needed, use the `allowEmpty` prop instead.
 */
export const Invalid: Story = {
  args: {
    label: 'Duration (invalid)',
    modelValue: Duration.fromObject({ hours: 0 }),
  },
}

/**
 * The `allowEmpty` prop allows the input to be empty. This is useful when the lack of input is a valid choice, but it should also be combined with clever use of the `hintMessage` prop to explain what the empty value means in context.
 */
export const AllowEmpty: Story = {
  args: {
    label: 'Possibly empty duration',
    modelValue: undefined,
    allowEmpty: true,
    hintMessage:
      'The hint message can be used to explain what the empty value means in context. Maybe some feature is disabled?',
  },
}
