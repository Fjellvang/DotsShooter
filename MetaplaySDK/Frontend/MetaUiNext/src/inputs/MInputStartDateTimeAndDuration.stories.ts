import { DateTime, Duration } from 'luxon'
import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputStartDateTimeAndDuration from './MInputStartDateTimeAndDuration.vue'

const meta: Meta<typeof MInputStartDateTimeAndDuration> = {
  component: MInputStartDateTimeAndDuration,
  tags: ['autodocs'],
  args: {
    startDateTime: DateTime.now(),
    duration: Duration.fromObject({ hours: 1 }),
  },
  argTypes: {
    startDateTime: {
      control: false,
    },
    duration: {
      control: false,
    },
  },
  render: (args) => ({
    components: { MInputStartDateTimeAndDuration },
    setup: () => {
      const startDateTime = ref(args.startDateTime)
      const duration = ref(args.duration)
      return { args, startDateTime, duration }
    },
    template: `<div>
      <MInputStartDateTimeAndDuration v-bind="args" :startDateTime="startDateTime" @update:startDateTime="startDateTime = $event" :duration="duration" @update:duration="duration = $event"/>
      <pre class="tw-mt-2">Output startDateTime: {{ startDateTime }}</pre>
      <pre class="tw-mt-2">Output duration: {{ duration }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'A component that allows you to input a start date time and a duration.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputStartDateTimeAndDuration>

/**
 * Demonstrates the default behavior of the `MInputStartDateTimeAndDuration` component with a start date and time, and a duration.
 */
export const Default: Story = {}

/**
 * Shows the `MInputStartDateTimeAndDuration` component in a disabled state, preventing user interaction.
 */
export const Disabled: Story = {
  args: {
    disabled: true,
  },
}
