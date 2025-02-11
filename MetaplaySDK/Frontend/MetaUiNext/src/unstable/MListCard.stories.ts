import type { Meta, StoryObj } from '@storybook/vue3'

import MListCard from './MListCard.vue'

const meta: Meta<typeof MListCard> = {
  // @ts-expect-error -- Generics
  component: MListCard,
  tags: ['autodocs'],
  argTypes: {
    // variant: {
    //   control: {
    //     type: 'select',
    //   },
    //   options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    // },
    // badgeVariant: {
    //   control: {
    //     type: 'select',
    //   },
    //   options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    // },
  },
  parameters: {
    viewport: {
      defaultViewport: 'singleColumn',
    },
    docs: {
      description: {
        component: 'TBD',
      },
    },
  },
  render: (args) => ({
    components: { MListCard },
    setup: () => ({ args }),
    template: `
    <MListCard v-bind="args">
    </MListCard>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MListCard>

/**
 * Demonstrates the default appearance of the card with a short title and a list of dummy data.
 */
export const Default: Story = {
  args: {
    title: 'Short Title',
    items: [{ title: 'Dummy Data', description: 'Lorem ipsum dolor sit amet.' }],
  },
}
