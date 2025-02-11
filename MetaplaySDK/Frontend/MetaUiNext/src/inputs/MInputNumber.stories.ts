import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputNumber from './MInputNumber.vue'

const meta: Meta<typeof MInputNumber> = {
  component: MInputNumber,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
  },
  render: (args) => ({
    components: { MInputNumber },
    setup: () => {
      const number = ref(args.modelValue)
      watch(
        () => args.modelValue,
        (value) => {
          number.value = value
        }
      )
      return { args, number }
    },
    template: `<div>
      <MInputNumber v-bind="args" v-model="number"/>
      <pre class="tw-mt-2">Output: {{ number }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'MInputNumber is a simple number input that can be used in forms.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputNumber>

/**
 * Demonstrates the default behavior of the `MInputNumber` component, showing a hint message.
 */
export const Default: Story = {
  args: {
    label: 'Number',
    modelValue: 1337,
    hintMessage: 'Decimal places are allowed.',
  },
}

/**
 * Demonstrates the component with a minimum value constraint of 3.
 */
export const Min3: Story = {
  args: {
    label: 'Number >= 3',
    modelValue: 5,
    min: 3,
  },
}

/**
 * Demonstrates the component with a maximum value constraint of 6.
 */
export const Max6: Story = {
  args: {
    label: 'Number <= 6',
    modelValue: 5,
    max: 6,
  },
}

/**
 * Demonstrates the component with both minimum and maximum value constraints (3 and 6 respectively).
 */
export const Min3Max6: Story = {
  args: {
    label: 'Number >= 3 && <= 6',
    modelValue: 5,
    min: 3,
    max: 6,
  },
}

/**
 * Demonstrates the component with a custom step size of 5 for incrementing and decrementing the value.
 */
export const CustomStepSize: Story = {
  args: {
    label: 'Number +/- 5',
    modelValue: 10,
    step: 5,
    hintMessage: 'The arrows increase and decrease the input by the step size.',
  },
}

/**
 * Demonstrates the component with a range from 0 to 100 and a step size of 25.
 */
export const Min0Max100Step25: Story = {
  args: {
    label: 'Number +/- 25',
    modelValue: 50,
    min: 0,
    max: 100,
    step: 25,
  },
}

/**
 * Demonstrates the component with a placeholder text when the input is empty.
 */
export const Placeholder: Story = {
  args: {
    label: 'Empty by default',
    placeholder: 'Enter a number',
  },
}

/**
 * Demonstrates the component allowing undefined input values.
 */
export const AllowUndefined: Story = {
  args: {
    label: 'Optional Number',
    placeholder: 'Undefined',
    allowUndefined: true,
  },
}

/**
 * Demonstrates the component clearing the input when the value is zero.
 */
export const ClearOnZero: Story = {
  args: {
    label: 'Player Limit',
    placeholder: 'Unlimited',
    clearOnZero: true,
  },
}

/**
 * Demonstrates the component in a disabled state.
 */
export const Disabled: Story = {
  args: {
    label: 'Number (disabled)',
    modelValue: 3,
    disabled: true,
  },
}

/**
 * Demonstrates the component with a danger variant, indicating an invalid input for `MInputNumber`.
 */
export const Danger: Story = {
  args: {
    label: 'Number (invalid)',
    modelValue: 666,
    variant: 'danger',
    hintMessage: 'Hints turn red when the variant is danger.',
  },
}

/**
 * Demonstrates the component with a success variant, indicating a valid input for `MInputNumber`.
 */
export const Success: Story = {
  args: {
    label: 'Duration',
    modelValue: 4,
    variant: 'success',
  },
}

/**
 * While not ideal, the clear button should look ok with a variant icon for `MInputNumber`.
 */
export const SuccessAndClearable: Story = {
  args: {
    label: 'Multiple icons',
    modelValue: 1,
    variant: 'success',
    allowUndefined: true,
  },
}

/**
 * Demonstrates the component without a label for `MInputNumber`.
 */
export const NoLabel: Story = {
  args: {
    modelValue: 5,
  },
}
