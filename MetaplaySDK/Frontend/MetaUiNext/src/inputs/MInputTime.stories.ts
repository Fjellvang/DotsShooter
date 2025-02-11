import { DateTime } from 'luxon'
import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputTime from './MInputTime.vue'

const meta: Meta<typeof MInputTime> = {
  component: MInputTime,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component:
          'MInputTime shows two number pickers for hours and minutes. The selected time is a plain time, meaning that it has no information about time zones and does not describe any specific instant in UTC time. The output is an ISO 8601 time string. Please note that there is no undefined state for this input. If you need to represent an undefined time, you should make a wrapper for this component to handle that UX in a way that makes sense in your context.',
      },
    },
  },
  args: {
    label: 'Time',
    modelValue: DateTime.now().toUTC().toISOTime({
      suppressSeconds: true,
      includeOffset: false,
    }),
    hintMessage: 'This input returns an ISO 8601 date string.',
  },
  argTypes: {
    minIsoTime: {
      control: false,
    },
    maxIsoTime: {
      control: false,
    },
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
  },
  render: (args) => ({
    components: { MInputTime },
    setup: () => {
      const time = ref(args.modelValue)
      watch(
        () => args.modelValue,
        (value) => {
          time.value = value
        }
      )
      return { args, time }
    },
    template: `<div>
      <MInputTime v-bind="args" v-model="time"/>
      <pre class="tw-mt-2">Output: {{ time }}</pre>
    </div>`,
  }),
}

export default meta
type Story = StoryObj<typeof MInputTime>

/**
 * You can type, click the arrow buttons, use the scroll wheel, or use the up and down arrows to select a time.
 */
export const Default: Story = {
  args: {
    label: 'Time',
    hintMessage: 'This input returns an ISO 8601 date string.',
  },
}

/**
 * Passing in `utcNow` as the `minIsoTime` prop will set the minimum time to the current time in UTC. Notice how the hour and minute selections do their best to never allow smaller values, but instead clamp the time to the current time when the user tries to select a smaller time.
 */
export const MinTimeUtcNow: Story = {
  args: {
    label: 'Later than UTC now',
    minIsoTime: 'utcNow',
  },
}

/**
 * You can also set the minimum time to a specific time. In this case, the minimum time is set to 8:35.
 */
export const MinTimeMorning: Story = {
  args: {
    label: 'Later than 8:35',
    minIsoTime: '08:35',
  },
}

/**
 * Setting the maximum time to `utcNow` will set the maximum time to the current time in UTC. Notice how the hour and minute selections do their best to never allow larger values, but instead clamp the time to the current time when the user tries to select a larger time.
 */
export const MaxTimeUtcNow: Story = {
  args: {
    label: 'Earlier than UTC now',
    maxIsoTime: 'utcNow',
  },
}

/**
 * You can also set the maximum time to a specific time. In this case, the maximum time is set to 20:35.
 */
export const MaxTimeEvening: Story = {
  args: {
    label: 'Earlier than 20:35',
    maxIsoTime: '20:35',
  },
}

/**
 * The `success` variant is used to indicate that the input is valid.
 */
export const Success: Story = {
  args: {
    label: 'Time (success)',
    variant: 'success',
    hintMessage: 'This is a success hint.',
  },
}

/**
 * The `danger` variant is used to indicate that the input is invalid.
 */
export const Danger: Story = {
  args: {
    label: 'Time (danger)',
    variant: 'danger',
    hintMessage: 'This is a danger hint.',
  },
}

/**
 * The `disabled` prop disables the input.
 */
export const Disabled: Story = {
  args: {
    label: 'Time (disabled)',
    disabled: true,
  },
}

/**
 * You can also use the input without a label.
 */
export const NoLabel: Story = {}
