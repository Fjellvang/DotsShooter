import type { Meta, StoryObj } from '@storybook/vue3'

import MCodeBlock from './MCodeBlock.vue'

const meta: Meta<typeof MCodeBlock> = {
  component: MCodeBlock,
  tags: ['autodocs'],
  argTypes: {},
  parameters: {
    docs: {
      description: {
        component: 'A code block that displays code with optional syntax highlighting and a file name.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MCodeBlock>

export const MinimalUse: Story = {
  render: (args) => ({
    components: { MCodeBlock },
    setup: () => ({ args }),
    template: `
    <MCodeBlock v-bind="args">
line 1
line 2
export const TwoColumns: Story = {
  render: (args) => ({
    components: { MActionModal },
    setup: () => ({ args }),
    template: 'Very long template string that will cause overflow in the content area and will make the body scrollable.',
  }),
  args: {},
}
last line
    </MCodeBlock>
    `,
  }),
  args: {
    language: '',
    fileName: '',
  },
}

export const JSON: Story = {
  render: (args) => ({
    components: { MCodeBlock },
    setup: () => ({ args }),
    template: `
    <MCodeBlock v-bind="args">
line 1
line 2
export const TwoColumns: Story = {
  render: (args) => ({
    components: { MActionModal },
    setup: () => ({ args }),
    template: 'Very long template string that will cause overflow in the content area and will make the body scrollable.',
  }),
  args: {},
}
last line
    </MCodeBlock>
    `,
  }),
  args: {
    language: 'json',
    fileName: 'some-config.json',
  },
}
