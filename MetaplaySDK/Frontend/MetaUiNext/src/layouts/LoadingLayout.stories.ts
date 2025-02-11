import type { Meta, StoryObj } from '@storybook/vue3'

import LoadingLayout from './LoadingLayout.vue'

const meta: Meta<typeof LoadingLayout> = {
  component: LoadingLayout,
}

export default meta
type Story = StoryObj<typeof LoadingLayout>

export const Default: Story = {
  render: (args) => ({
    components: { LoadingLayout },
    setup: () => ({ args }),
    template: '<loading-layout v-bind="args"></loading-layout>',
  }),
  args: {},
  parameters: {
    docs: {
      description: {
        component: 'A layout that displays a loading spinner and a message.',
      },
    },
  },
}
