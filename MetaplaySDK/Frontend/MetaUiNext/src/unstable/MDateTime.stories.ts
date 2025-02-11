import { DateTime } from 'luxon'

import type { Meta, StoryObj } from '@storybook/vue3'

import MDateTime from './MDateTime.vue'

const meta: Meta<typeof MDateTime> = {
  component: MDateTime,
  argTypes: {},
  parameters: {
    docs: {
      description: {
        component: 'A component that displays a date and time in a human-readable format.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MDateTime>

export const Default: Story = {
  args: {
    instant: DateTime.fromISO('2021-01-01T12:34:56.789Z'),
  },
}

export const NonUtcTime: Story = {
  args: {
    instant: DateTime.fromISO('2021-01-01T12:34:56.789+01:00'),
  },
}

export const DisableTooltip: Story = {
  args: {
    instant: DateTime.fromISO('2021-01-01T12:34:56.789Z'),
    disableTooltip: true,
  },
}
