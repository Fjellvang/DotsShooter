import { DateTime, Duration } from 'luxon'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputDurationOrEndDateTime from './MInputDurationOrEndDateTime.vue'

const meta: Meta<typeof MInputDurationOrEndDateTime> = {
  component: MInputDurationOrEndDateTime,
  tags: ['autodocs'],
  argTypes: {
    modelValue: {
      control: false,
    },
    referenceDateTime: {
      control: false,
    },
    inputMode: {
      control: { type: 'select' },
      options: ['duration', 'endDateTime'],
    },
  },
  args: {
    modelValue: Duration.fromObject({ hours: 1, minutes: 30 }),
    referenceDateTime: DateTime.now(),
  },
  parameters: {
    docs: {
      description: {
        component: 'This component allows you to input a duration or an end date time.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputDurationOrEndDateTime>

/**
 * Demonstrates the default behavior of the component, showing a duration input with initial values and a reference date time.
 */
export const Default: Story = {
  render: (args) => ({
    components: { MInputDurationOrEndDateTime },
    setup: () => ({ args }),
    data: () => ({
      selectedDuration: args.modelValue,
    }),
    template: `<div>
      <MInputDurationOrEndDateTime v-bind="args" v-model="selectedDuration"/>
      <pre class="tw-mt-2">Output: {{ selectedDuration }}</pre>
    </div>`,
  }),
  args: {
    label: 'Duration',
    hintMessage: 'Initial value and a reference date time must always be set.',
  },
}

/**
 * Demonstrates the component in 'endDateTime' input mode, allowing the user to select an end date and time.
 */
export const EndDateTime: Story = {
  args: {
    label: 'End date time',
    inputMode: 'endDateTime',
  },
}

/**
 * Demonstrates the component in a disabled state, where the user cannot interact with the input fields.
 */
export const Disabled: Story = {
  args: {
    label: 'Duration (disabled)',
    inputMode: 'duration',
    disabled: true,
  },
}

/**
 * Demonstrates the component without a label, showing how it behaves when no label is provided.
 */
export const NoLabel: Story = {
  args: {
    inputMode: 'duration',
  },
}

/**
 * Use can use the `duration-hint-message` and `end-time-hint-message` slots to provide custom hint messages for the duration and end date time inputs. We use this internally in complex scenarios like communicating how the game server time skip works.
 */
export const CustomHintMessages: Story = {
  args: {
    label: 'Time Picker With Custom Hints',
  },
  render: (args) => ({
    components: { MInputDurationOrEndDateTime },
    setup: () => ({ args }),
    data: () => ({
      selectedDuration: args.modelValue,
    }),
    template: `<div>
      <MInputDurationOrEndDateTime v-bind="args" v-model="selectedDuration">
        <template #duration-hint-message="durationHintProps">Custom message for this datetime: {{ durationHintProps.dateTime }}</template>
        <template #end-time-hint-message="endDateTimeHintProps">Custom message for this duration: {{ endDateTimeHintProps.duration }}</template>
      </MInputDurationOrEndDateTime>
    </div>`,
  }),
}
