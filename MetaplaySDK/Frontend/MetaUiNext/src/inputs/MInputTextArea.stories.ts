import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputTextArea from './MInputTextArea.vue'

const meta: Meta<typeof MInputTextArea> = {
  component: MInputTextArea,
  tags: ['autodocs'],
  args: {
    modelValue: '',
  },
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'loading', 'danger', 'success'],
    },
  },
  render: (args) => ({
    components: { MInputTextArea },
    setup: () => {
      const content = ref(args.modelValue)
      watch(
        () => args.modelValue,
        (value) => {
          content.value = value
        }
      )
      return { args, content }
    },
    template: `<div>
      <MInputTextArea v-bind="args" v-model="content" />
      <pre class="tw-mt-2">Output: {{ content }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'A text area input component that can be used in forms.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputTextArea>

/**
 * Demonstrates the default behavior of the `MInputTextArea` component with a hint message.
 */
export const Default: Story = {
  args: {
    label: 'Text',
    modelValue:
      "It was a dark and stormy night. Our ship was tossed on the waves like a toy boat. The crew was terrified. The captain was terrified. Everyone but the ship's cat was terrified. I was too hungry to be scared. I was a cat, after all.",
    placeholder: 'Enter your name',
    hintMessage: 'Undefined is also a valid value for text fields.',
  },
}

/**
 * Shows the `MInputTextArea` component with a debounce feature, delaying the update of the model value by 1 second.
 */
export const Debounced: Story = {
  args: {
    label: 'Expensive Validation',
    modelValue: 'It was a dark and stormy night.',
    debounce: 1000,
    hintMessage: 'This field has a 1 second debounce.',
  },
}

/**
 * Demonstrates the `MInputTextArea` component with a placeholder text to guide user input.
 */
export const Placeholder: Story = {
  args: {
    label: 'Open feedback',
    placeholder: 'Enter your feedback here',
  },
}

/**
 * Shows the `MInputTextArea` component with a larger number of rows, suitable for longer text inputs.
 */
export const Large: Story = {
  args: {
    label: 'Input a player list',
    placeholder: 'Expecting a long list...',
    rows: 10,
  },
}

/**
 * Shows the `MInputTextArea` component in a disabled state, preventing user interaction.
 */
export const Disabled: Story = {
  args: {
    label: 'Disabled needs to look good',
    modelValue: 'Even when it has text inside.',
    disabled: true,
  },
}

/**
 * Shows the `MInputTextArea` component in a danger state, often used to indicate an error or invalid input.
 */
export const Danger: Story = {
  args: {
    label: 'Input can be false',
    modelValue: 'Commit and run',
    variant: 'danger',
    hintMessage: 'Hints turn red when the variant is danger.',
  },
}

/**
 * Demonstrates the `MInputTextArea` component in a success state, indicating valid input or a successful operation.
 */
export const Success: Story = {
  args: {
    label: 'Input can be valid',
    modelValue: 'Lint, test and build',
    variant: 'success',
    hintMessage: 'Hints are still neutral when the variant is success.',
  },
}

/**
 * Demonstrates the `MInputTextArea` component with a loading state, typically used to indicate an ongoing process.
 */
export const Loading: Story = {
  args: {
    label: 'Input can be pending for validation',
    modelValue: 'Someting that needs server-side validation...',
    variant: 'loading',
    hintMessage: 'Hints are still neutral when the variant is loading.',
  },
}

/**
 * Demonstrates the `MInputTextArea` component without a label, showing how it behaves in a minimal configuration.
 */
export const NoLabel: Story = {}
