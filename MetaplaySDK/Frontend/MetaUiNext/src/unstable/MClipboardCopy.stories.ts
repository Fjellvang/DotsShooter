import type { Meta, StoryObj } from '@storybook/vue3'

import MClipboardCopy from './MClipboardCopy.vue'

const meta: Meta<typeof MClipboardCopy> = {
  component: MClipboardCopy,
  tags: ['autodocs'],
  argTypes: {},
}

export default meta
type Story = StoryObj<typeof MClipboardCopy>

export const Default: Story = {
  render: (args) => ({
    components: { MClipboardCopy },
    setup: () => ({ args }),
    template: `
    Small inline button: <MClipboardCopy v-bind="args"></MClipboardCopy>
    `,
  }),
  args: {
    contents: 'Contents of the clipboard',
  },
  parameters: {
    docs: {
      description: {
        component: 'A button that copies the contents of the `contents` prop to the clipboard when clicked.',
      },
    },
  },
}

export const FullSize: Story = {
  render: (args) => ({
    components: { MClipboardCopy },
    setup: () => ({ args }),
    template: `
    <MClipboardCopy v-bind="args">Copy to Clipboard</MClipboardCopy>
    `,
  }),
  args: {
    contents: 'Contents of the clipboard',
    fullSize: true,
  },
}
