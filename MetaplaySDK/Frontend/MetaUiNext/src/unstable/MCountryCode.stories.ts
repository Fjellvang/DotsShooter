import type { Meta, StoryObj } from '@storybook/vue3'

import MCountryCode from './MCountryCode.vue'

const meta: Meta<typeof MCountryCode> = {
  component: MCountryCode,
  argTypes: {},
  parameters: {
    docs: {
      description: {
        component: 'A component that displays a country flag and name based on an ISO country code.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MCountryCode>

export const Default: Story = {
  args: {
    isoCode: 'DE',
  },
}
