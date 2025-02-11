import type { Meta, StoryObj } from '@storybook/vue3'

import MIpAddress from './MIpAddress.vue'

const meta: Meta<typeof MIpAddress> = {
  component: MIpAddress,
  argTypes: {},
  parameters: {
    docs: {
      description: {
        component: 'A component that displays an IP address in a human-readable format.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MIpAddress>

export const Default: Story = {
  args: {
    ipAddress: '8.8.8.8',
  },
}

export const Localhost: Story = {
  args: {
    ipAddress: '::1',
  },
}
