import type { Meta, StoryObj } from '@storybook/vue3'

import MInputSingleFile, { type FileError } from './MInputSingleFile.vue'

const meta: Meta<typeof MInputSingleFile> = {
  component: MInputSingleFile,
  tags: ['autodocs'],
  render: (args) => ({
    components: { MInputSingleFile },
    setup: () => ({ args }),
    data: () => ({ content: args.modelValue }),
    template: `<div>
      <MInputSingleFile v-bind="args" v-model="content"/>
      <pre class="tw-mt-2">Output: {{ content?.toString() }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'A file input component that allows the user to select a single file.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSingleFile>

/**
 * Demonstrates the default behavior of the `MInputSingleFile` component with a basic label and hint message.
 */
export const Default: Story = {
  args: {
    label: 'File',
    hintMessage: 'You can select one file at a time.',
  },
}

/**
 * Shows the `MInputSingleFile` component restricting file selection to JSON files only.
 */
export const LimitedFileType: Story = {
  args: {
    label: 'Only JSON Files',
    acceptedFileTypes: '.json',
  },
}

/**
 * Demonstrates the `MInputSingleFile` component's file validation functionality, ensuring the file is a JSON and smaller than 2KB.
 */
export const FileValidation: Story = {
  args: {
    label: 'JSON File Smaller Than 2KB',
    validationFunction: (file: File) => {
      const errors: FileError[] = []
      if (file.size > 2048) errors.push('FILE_TOO_LARGE')
      if (file.type !== 'application/json') errors.push('FILE_INVALID_TYPE')
      return errors
    },
  },
}

/**
 * Illustrates the `MInputSingleFile` component's ability to limit file selection to files smaller than 2KB.
 */
export const SmallFilesOnly: Story = {
  args: {
    label: 'File Smaller Than 2KB',
    maxFileSize: 2048,
  },
}

/**
 * Shows the `MInputSingleFile` component restricting file selection to files larger than 2KB.
 */
export const LargeFilesOnly: Story = {
  args: {
    label: 'File Larger Than 2KB',
    minFileSize: 2048,
  },
}

/**
 * Demonstrates the use of a placeholder text within the `MInputSingleFile` component.
 */
export const Placeholder: Story = {
  args: {
    label: 'File',
    placeholder: 'Select a file',
  },
}

/**
 * Illustrates the `MInputSingleFile` component in a disabled state, preventing file selection.
 */
export const Disabled: Story = {
  args: {
    label: 'File',
    disabled: true,
  },
}

/**
 * Shows the `MInputSingleFile` component in a loading state, typically used while a file is being processed.
 */
export const Loading: Story = {
  args: {
    label: 'File',
    variant: 'loading',
  },
}

/**
 * Demonstrates the `MInputSingleFile` component with a 'danger' variant, highlighting potential errors or warnings.
 */
export const Danger: Story = {
  args: {
    label: 'File',
    variant: 'danger',
    hintMessage: 'Hints turn red when the variant is danger.',
  },
}

/**
 * Shows the `MInputSingleFile` component with a 'success' variant, indicating a successful file selection or upload.
 */
export const Success: Story = {
  args: {
    label: 'File',
    variant: 'success',
    hintMessage: 'Hints are still neutral when the variant is success.',
  },
}

/**
 * Demonstrates the `MInputSingleFile` component without a label, showing how it appears in a minimal configuration.
 */
export const NoLabel: Story = {
  args: {},
}
