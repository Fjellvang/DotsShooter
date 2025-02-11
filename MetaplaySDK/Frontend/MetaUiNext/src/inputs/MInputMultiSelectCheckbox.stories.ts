import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputMultiSelectCheckbox from './MInputMultiSelectCheckbox.vue'
import MInputSwitch from './MInputSwitch.vue'

const meta: Meta<typeof MInputMultiSelectCheckbox> = {
  // @ts-expect-error Storybook doesn't like generics.
  component: MInputMultiSelectCheckbox,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
    size: {
      control: { type: 'radio' },
      options: ['default', 'small'],
    },
  },
  args: {
    label: 'Roles',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
    ],
    modelValue: ['admin'],
  },
  render: (args) => ({
    components: { MInputMultiSelectCheckbox },
    setup: () => {
      const checked = ref(args.modelValue)
      watch(
        () => args.modelValue,
        (value) => {
          checked.value = value
        }
      )
      return { args, checked }
    },
    template: `<div>
      <MInputMultiSelectCheckbox v-bind="args" v-model="checked"/>
      <pre class="tw-mt-2">Output: {{ checked }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component: 'A multi-select checkbox component that can be used in forms.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputMultiSelectCheckbox>

/**
 * Demonstrates the default behavior of the `MInputMultiSelectCheckbox` component, showing a hint message.
 */
export const Default: Story = {
  args: {
    hintMessage: 'This component only supports strings and numbers as return values.',
  },
}

/**
 * Demonstrates the component with numeric values as options.
 */
export const NumberValues: Story = {
  args: {
    label: 'Numbers',
    options: [
      { label: 'One', value: 1 },
      { label: 'Two', value: 2 },
      { label: 'Three', value: 3 },
    ],
    modelValue: [1, 2],
  },
}

/**
 * Demonstrates the component with checkboxes stacked vertically.
 */
export const Vertical: Story = {
  args: {
    vertical: true,
  },
}

/**
 * Demonstrates the component with small-sized checkboxes.
 */
export const Small: Story = {
  args: {
    size: 'small',
  },
}

/**
 * Demonstrates the component in a disabled state.
 */
export const Disabled: Story = {
  args: {
    disabled: true,
  },
}

/**
 * Demonstrates the component with a success variant and a hint message.
 */
export const Success: Story = {
  args: {
    variant: 'success',
    hintMessage: 'Success hint message.',
  },
}

/**
 * Demonstrates the component with a danger variant and a hint message.
 */
export const Danger: Story = {
  args: {
    variant: 'danger',
    hintMessage: 'Danger hint message.',
  },
}

/**
 * Demonstrates the component without a label.
 */
export const NoLabel: Story = {}

/**
 * Demonstrates the component with long labels that could potentially overflow the container.
 */
export const Overflow: Story = {
  args: {
    label: 'Do not do this',
    options: [
      {
        label: 'Some labels could be really long and the surrounding layout should not explode because of this',
        value: 'admin',
      },
      {
        label: 'Notmuchwecandoaboutverylonglabelswithoutspacesastheywillsurelyoverflowanycontainer',
        value: 'guest',
      },
      { label: 'What if we had a ton of options 1?', value: '1' },
      { label: 'What if we had a ton of options 2?', value: '2' },
      { label: 'What if we had a ton of options 3?', value: '3' },
      { label: 'What if we had a ton of options 4?', value: '4' },
      { label: 'What if we had a ton of options 5?', value: '5' },
      { label: 'What if we had a ton of options 6?', value: '6' },
      { label: 'What if we had a ton of options 7?', value: '7' },
      { label: 'What if we had a ton of options 8?', value: '8' },
      { label: 'What if we had a ton of options 9?', value: '9' },
      { label: 'What if we had a ton of options 10?', value: '10' },
      { label: 'What if we had a ton of options 11?', value: '11' },
      { label: 'What if we had a ton of options 12?', value: '12' },
      { label: 'What if we had a ton of options 13?', value: '13' },
      { label: 'What if we had a ton of options 14?', value: '14' },
      { label: 'What if we had a ton of options 15?', value: '15' },
    ],
  },
}

/**
 * Demonstrates the component with long labels and vertical stacking.
 */
export const VerticalOverflow: Story = {
  args: {
    vertical: true,
    label: 'Do not do this',
    options: [
      {
        label: 'Some labels could be really long and the surrounding layout should not explode because of this',
        value: 'admin',
      },
      {
        label: 'Notmuchwecandoaboutverylonglabelswithoutspacesastheywillsurelyoverflowanycontainer',
        value: 'guest',
      },
      { label: 'What if we had a ton of options 1?', value: '1' },
      { label: 'What if we had a ton of options 2?', value: '2' },
      { label: 'What if we had a ton of options 3?', value: '3' },
      { label: 'What if we had a ton of options 4?', value: '4' },
      { label: 'What if we had a ton of options 5?', value: '5' },
      { label: 'What if we had a ton of options 6?', value: '6' },
      { label: 'What if we had a ton of options 7?', value: '7' },
      { label: 'What if we had a ton of options 8?', value: '8' },
      { label: 'What if we had a ton of options 9?', value: '9' },
      { label: 'What if we had a ton of options 10?', value: '10' },
      { label: 'What if we had a ton of options 11?', value: '11' },
      { label: 'What if we had a ton of options 12?', value: '12' },
      { label: 'What if we had a ton of options 13?', value: '13' },
      { label: 'What if we had a ton of options 14?', value: '14' },
      { label: 'What if we had a ton of options 15?', value: '15' },
    ],
  },
}

/**
 * Demonstrates enabling and disabling multiple checkboxes using a `switch` control.
 */
export const EnablingDisabledMultiSelectCheckboxes: Story = {
  render: () => ({
    components: { MInputSwitch, MInputMultiSelectCheckbox },
    setup: () => {
      const isEnabled = ref(false)
      const modelValue = ref([])
      const options = [
        { label: 'Option 1', value: '1' },
        { label: 'Option 2', value: '2' },
        { label: 'Option 3', value: '3' },
      ]
      return { isEnabled, options, modelValue }
    },
    template: `
      <div>
        <p>Control switch to enable multiple checkboxes below</p>
        <MInputSwitch size="small" v-model="isEnabled"/>
        <MInputMultiSelectCheckbox :disabled="!isEnabled" :options="options" v-model="modelValue" label="Multiple Checkboxes"/>
      </div>
    `,
  }),
}
