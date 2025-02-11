import type { Meta, StoryObj } from '@storybook/vue3'

import MInputSingleFileContents from './MInputSingleFileContents.vue'

const meta: Meta<typeof MInputSingleFileContents> = {
  component: MInputSingleFileContents,
  tags: ['autodocs'],
  render: (args) => ({
    components: { MInputSingleFileContents },
    setup: () => ({ args }),
    data: () => ({ content: args.modelValue }),
    template: `<div>
      <MInputSingleFileContents v-bind="args" v-model="content"/>
      <pre class="tw-mt-2">Output: {{ content?.toString() }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'A file input component that allows the user to select a single file and read its contents.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSingleFileContents>

/**
 * Demonstrates the default behavior of the `MInputSingleFileContents` component with a basic label and hint message.
 */
export const Default: Story = {
  args: {
    label: 'File',
    hintMessage: 'You can select one file at a time.',
  },
}

/**
 * Shows the `MInputSingleFileContents` component with a placeholder text to guide users on what to select.
 */
export const Placeholder: Story = {
  args: {
    label: 'File',
    placeholder: 'Select a file',
  },
}

/**
 * Illustrates the `MInputSingleFileContents` component in a disabled state, preventing file selection.
 */
export const Disabled: Story = {
  args: {
    label: 'File',
    disabled: true,
  },
}

/**
 * Shows the `MInputSingleFileContents` component in a loading state, typically used while a file is being processed.
 */
export const Loading: Story = {
  args: {
    label: 'File',
    variant: 'loading',
  },
}

/**
 * Demonstrates the `MInputSingleFileContents` component with a 'danger' variant, highlighting potential errors or warnings.
 */
export const Danger: Story = {
  args: {
    label: 'File',
    variant: 'danger',
    hintMessage: 'Hints turn red when the variant is danger.',
  },
}

/**
 * Shows the `MInputSingleFileContents` component with a 'success' variant, indicating a successful file selection or upload.
 */
export const Success: Story = {
  args: {
    label: 'File',
    variant: 'success',
    hintMessage: 'Hints are still neutral when the variant is success.',
  },
}

/**
 * Demonstrates the `MInputSingleFileContents` component without a label, showing how it appears in a minimal configuration.
 */
export const NoLabel: Story = {
  args: {},
}
