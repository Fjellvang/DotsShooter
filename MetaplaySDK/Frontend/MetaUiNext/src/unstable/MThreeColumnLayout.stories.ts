import type { Meta, StoryObj } from '@storybook/vue3'

import MCard from '../primitives/MCard.vue'
import MThreeColumnLayout from './MThreeColumnLayout.vue'

const meta: Meta<typeof MThreeColumnLayout> = {
  component: MThreeColumnLayout,
  tags: ['autodocs'],
  args: {
    stretchItemsVertically: true,
  },
  render: (args) => ({
    components: {
      MThreeColumnLayout,
      MCard,
    },
    parameters: {
      layout: 'fullscreen',
      docs: {
        description: {
          component:
            'The `MThreeColumnLayout` is a base layout component designed to create visually consistent and responsive three column layout. It is ideal for displaying large amounts of content side by side in a clean, organized, and user-friendly manner.',
        },
      },
    },
    setup: () => ({ args }),
    template: `
      <MThreeColumnLayout/>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MThreeColumnLayout>

/**
 * The `MThreeColumnLayout` is a flex component that evenly distributes its child elements across three columns. It dynamically adapts to the content, ensuring
 * each element occupies an equal amount of space within the columns.
 */

export const Default: Story = {
  render: (args) => ({
    components: {
      MThreeColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MThreeColumnLayout>
        <MCard title="Example content 1">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 2">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 3">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MThreeColumnLayout>
    `,
  }),
}

/**
 * Typically, when there is only a single child element, it is centered within the layout to draw the user's attention to it.
 */
export const SingleChildElement: Story = {
  render: (args) => ({
    components: {
      MThreeColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MThreeColumnLayout>
        <MCard title="Example content 1">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MThreeColumnLayout>
    `,
  }),
}

/**
 * As the number of child elements increases, the layout will automatically distribute them evenly across three columns, wrapping them to the next row as
 * necessary.
 */
export const MultipleChildElements: Story = {
  render: (args) => ({
    components: {
      MThreeColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MThreeColumnLayout>
        <MCard title="Example content 1">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 2">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 3">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 4">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MThreeColumnLayout>
    `,
  }),
}

/**
 * If the final row contains fewer than three child elements, they will be automatically centered within the layout, maintaining a uniform and polished
 * appearance across the entire page.
 */
export const OddNumberedChildElements: Story = {
  render: (args) => ({
    components: {
      MThreeColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MThreeColumnLayout>
        <MCard title="Example content 1">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 2">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 3">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 4">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 5">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MThreeColumnLayout>
    `,
  }),
}
