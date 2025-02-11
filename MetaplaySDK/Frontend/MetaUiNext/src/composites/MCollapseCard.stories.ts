import type { Meta, StoryObj } from '@storybook/vue3'

import MCollapseCard from './MCollapseCard.vue'

const meta: Meta<typeof MCollapseCard> = {
  component: MCollapseCard,
  tags: ['autodocs'],
  argTypes: {
    badge: {
      control: {
        type: 'text',
      },
    },
  },
  parameters: {
    docs: {
      description: {
        component: 'A card that can be expanded or collapsed to show or hide its content.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MCollapseCard>

/**
 * Demonstrates the default appearance of the `MCollapseCard` with a simple title.
 */
export const Default: Story = {
  args: {
    title: 'Short Title',
  },
}

/**
 * Shows the `MCollapseCard` with a badge, demonstrating its use in indicating counts or statuses.
 */
export const Pill: Story = {
  args: {
    title: 'List of Things',
    badge: '10/35',
  },
}

/**
 * Illustrates how to use the header-right slot to add custom content to the header of the `MCollapseCard`.
 */
export const HeaderRightContent: Story = {
  render: (args) => ({
    components: { MCollapseCard },
    setup: () => ({ args }),
    template: `
    <MCollapseCard v-bind="args">
      <template #header-right>
        Right side content
      </template>

      Lorem ipsum dolor sit amet.
    </MCollapseCard>
    `,
  }),
  args: {
    title: 'Passing content in the header-right slot',
  },
}
