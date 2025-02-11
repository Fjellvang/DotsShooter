import type { Meta, StoryObj } from '@storybook/vue3'

import MButton from '../primitives/MButton.vue'
import MButtonGroupLayout from './MButtonGroupLayout.vue'

const meta: Meta<typeof MButtonGroupLayout> = {
  component: MButtonGroupLayout,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component:
          'A responsive layout component for arranging a group of buttons. Used for example in cards and modals for consistent button layout. Primary actions are right-aligned on wide containers and on the top on narrow containers.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MButtonGroupLayout>

export const Default: Story = {
  render: (args) => ({
    components: { MButtonGroupLayout, MButton },
    setup: () => ({ args }),
    template: `
<MButtonGroupLayout v-bind="args">
  <MButton>Open</MButton>
  <MButton variant="neutral">Close</MButton>
  <MButton>Longer Name</MButton>
  <MButton variant="warning">Even Longer Name</MButton>
  <MButton variant="danger">UnlikelyLongAndAnnoyingName</MButton>
  <MButton variant="danger">Delete</MButton>
  <MButton>Download</MButton>
</MButtonGroupLayout>
`,
  }),
  args: {},
}
