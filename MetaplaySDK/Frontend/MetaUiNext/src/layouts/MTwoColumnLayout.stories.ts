import type { Meta, StoryObj } from '@storybook/vue3'

import MCard from '../primitives/MCard.vue'
import MTwoColumnLayout from './MTwoColumnLayout.vue'

const meta: Meta<typeof MTwoColumnLayout> = {
  component: MTwoColumnLayout,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'The `MTwoColumnLayout` is a base layout component designed to create visually consistent and responsive two column layout throughout the whole dashboard. It is useful for scenarios where you want to display content side by side.',
      },
    },
  },
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MTwoColumnLayout/>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MTwoColumnLayout>

/**
 * The `MTwoColumnLayout` component offers default styling that ensures consistent spacing and alignment for your content. This creates a balanced, clean, and cohesive structure
 * enhancing both the readability and overall presentation of your material.
 */
export const Default: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MTwoColumnLayout>
        <MCard title="Example content 1">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 2">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 3">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MTwoColumnLayout>
    `,
  }),
}

/**
 * When a single child element is wrapped in the `MTwoColumnLayout component`, it is automatically centered within the layout.
 */
export const SingleChildElement: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MTwoColumnLayout>
        <MCard title="Example content 1">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MTwoColumnLayout>
    `,
  }),
}

/**
 * As the number of child elements increases, the layout will automatically distribute them evenly across two columns. If necessary, the elements neatly wrap to the next row
 * maintaining a consistent and visually appealing structure.
 */
export const MultipleChildElements: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MTwoColumnLayout>
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
        <MCard title="Example content 6">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 7">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
        <MCard title="Example content 8">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MTwoColumnLayout>
    `,
  }),
}

/**
 * If the final row of the layout contains only a single child element, this element will be automatically centered within the layout. This ensures that the element is positioned
 * symmetrically, contributing to a balanced and visually appealing design.
 */
export const SingleLastElements: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MTwoColumnLayout>
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
      </MTwoColumnLayout>
    `,
  }),
}
