import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputText from './MInputText.vue'

const meta: Meta<typeof MInputText> = {
  component: MInputText,
  tags: ['autodocs'],
  args: {
    modelValue: 'Matti Meikäläinen 🥸',
    label: 'Name',
    placeholder: 'Enter your name',
  },
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'loading', 'danger', 'success'],
    },
    type: {
      control: { type: 'radio' },
      options: ['text', 'email', 'password'],
    },
  },
  render: (args) => ({
    components: { MInputText },
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
      <MInputText v-bind="args" v-model="content" />
      <pre class="tw-mt-2">Output: {{ content }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'A text input component that can be used in forms.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputText>

/**
 * Demonstrates the default behavior of the `MInputText` component with a hint message.
 */
export const Default: Story = {
  args: {
    hintMessage: 'Undefined is also a valid value for text fields.',
  },
}

/**
 * Shows the `MInputText` component with a debounce feature, delaying the update of the model value by 1 second.
 */
export const Debounced: Story = {
  args: {
    debounce: 1000,
    hintMessage: 'This field has a 1 second debounce.',
  },
}

/**
 * Demonstrates the `MInputText` component with a placeholder text to guide user input.
 */
export const Placeholder: Story = {
  args: {
    label: 'City',
    placeholder: 'For example: Helsinki',
    modelValue: '',
  },
}

/**
 * Shows the `MInputText` component configured to accept email input with a specific placeholder.
 */
export const Email: Story = {
  args: {
    label: 'Email',
    placeholder: 'doesthiswork@metaplay.io',
    type: 'email',
    modelValue: '',
  },
}

/**
 * Demonstrates the `MInputText` component with a clear button to reset the input value.
 */
export const ShowClearButton: Story = {
  args: {
    label: 'Clearable',
    modelValue: 'Clear me',
    showClearButton: true,
  },
}

/**
 * Shows the `MInputText` component in a disabled state, preventing user interaction.
 */
export const Disabled: Story = {
  args: {
    label: 'Would you like a raise?',
    modelValue: "Nah, I'm good, thanks.",
    disabled: true,
  },
}

/**
 * Demonstrates the `MInputText` component with a loading state, typically used to indicate an ongoing process.
 */
export const Loading: Story = {
  args: {
    label: 'Player Name',
    modelValue: '⭐️L3G0L4S_1337⭐️',
    variant: 'loading',
  },
}

/**
 * Shows the `MInputText` component in a danger state, often used to indicate an error or invalid input.
 */
export const Danger: Story = {
  args: {
    label: 'This is fine',
    modelValue: 'Commit and run',
    variant: 'danger',
    hintMessage: 'Hints turn red when the variant is danger.',
  },
}

/**
 * Demonstrates the `MInputText` component in a success state, indicating valid input or a successful operation.
 */
export const Success: Story = {
  args: {
    label: 'This is fine',
    modelValue: 'Lint, test and build',
    variant: 'success',
    hintMessage: 'Hints are still neutral when the variant is success.',
  },
}

/**
 * While not ideal, the clear button should look ok with a variant icon.
 */
/**
 * Shows the `MInputText` component with both a clear button and a success state, indicating valid and resettable input.
 */
export const ClearableAndSuccess: Story = {
  args: {
    label: 'Multiple icons',
    modelValue: 'To clear or not to clear?',
    variant: 'success',
    showClearButton: true,
  },
}

/**
 * Demonstrates the `MInputText` component without a label, showing how it behaves in a minimal configuration.
 */
export const NoLabel: Story = {
  args: {
    modelValue: '',
  },
}
