import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputSingleSelectRadio from './MInputSingleSelectRadio.vue'

const meta: Meta<typeof MInputSingleSelectRadio> = {
  // @ts-expect-error Storybook doesn't like generics.
  component: MInputSingleSelectRadio,
  tags: ['autodocs'],
  args: {
    label: 'Role',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
    ],
    modelValue: 'admin',
  },
  argTypes: {
    modelValue: {
      control: false,
    },
    variant: {
      control: { type: 'select' },
      options: ['primary', 'success', 'danger'],
    },
    size: {
      control: { type: 'radio' },
      options: ['default', 'small'],
    },
  },
  render: (args) => ({
    components: { MInputSingleSelectRadio },
    setup: () => ({ args }),
    data: () => ({ selected: args.modelValue }),
    template: `<div>
      <MInputSingleSelectRadio v-bind="args" v-model="selected"/>
      <pre class="tw-mt-2">Output: {{ selected }}</pre>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component:
          'The `MInputSingleSelectRadio` component allows users to select a single option from a set of mutually exclusive choices. This component is ideal for scenarios where users need to choose one value from a list of distinct options.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSingleSelectRadio>

/**
 * The `MInputSingleSelectRadio` lets users see all options before making a selection. Each option pairs a radio button with a label,
 * creating a visually engaging experience.
 */
export const Default: Story = {
  args: {
    modelValue: 'admin',
    hintMessage: 'This component only supports strings and numbers as return values.',
  },
}

/**
 * Set the `modelValue` to `undefined` to use the `MInputSingleSelectRadio` component without a pre-selected option.
 * Once the user selects an option, they cannot deselect it.
 *
 * If you need to allow users to deselect an option, consider using the `MInputCheckbox` instead.
 */
export const NoDefaultValue: Story = {
  args: {
    modelValue: undefined,
  },
}

/**
 * You can use the `MInputSingleSelectRadio` component without a label. However, we recommend providing a label to help users understand the purpose of the component.
 * If you choose not to include a label, make sure the context is clear and users can easily understand the options.
 */
export const NoLabel: Story = {
  args: {
    label: undefined,
  },
}

// Generic option type for the options prop.
interface Option<T> {
  label: string
  value: T
}
/**
 * The `MInputSingleSelectRadio` component allows you to define the data type for the options and the selected value when using the component. This flexibility
 * lets you vary the type based on your specific use case. For example, you can use a number or object as the value type, depending on the data you need to work with.
 */
export const GenericsOption: Story = {
  render: () => ({
    components: { MInputSingleSelectRadio },
    setup: () => {
      // Define options with generics for number, string, and object values
      const options1: Array<Option<number>> = [
        { label: '1', value: 1 },
        { label: '2', value: 2 },
        { label: '3', value: 3 },
        { label: '4', value: 4 },
      ]

      // Define options with object values
      const options2: Array<Option<{ id: number; name: string }>> = [
        { label: 'Object 1', value: { id: 1, name: 'test 1' } },
        { label: 'Object 2', value: { id: 2, name: 'test 2' } },
        { label: 'Object 3', value: { id: 3, name: 'test 3' } },
        { label: 'Object 4', value: { id: 4, name: 'test 4' } },
      ]

      // Using reactive object to track model values for multiple types
      const modelValue1 = ref(1)
      const modelValue2 = ref({ id: 1, name: 'test 1' })

      return { options1, modelValue1, options2, modelValue2 }
    },
    template: `<div class="tw-space-y-10">
      <div class="tw-space-y-2">
        <div>Number</div>
        <MInputSingleSelectRadio :options="options1" v-model="modelValue1"/>
        <pre>Output: {{ modelValue1 }}</pre>
      </div>

      <div class="tw-space-y-2">
        <div>Object</div>
        <MInputSingleSelectRadio :options="options2" v-model="modelValue2" />
        <pre>Output: {{ modelValue2 }}</pre>
      </div>
    </div>`,
  }),
}

/**
 * You can disable the radio button group by using the optional `disabledTooltip` prop. This prevents users from
 * interacting with the component and displays a tooltip when users hover over the component explaining *why* the
 * component is disabled.
 *
 * When disabled, the entire radio button group is grayed out and users are unable to make selections.
 * Contextual colors also appear muted to indicate that the component is disabled.
 */
export const DisabledWithTooltip: Story = {
  render: (args) => ({
    components: { MInputSingleSelectRadio },
    setup: () => ({ args }),
    data: () => ({ selected: args.modelValue, selected2: args.modelValue, selected3: args.modelValue }),
    template: `<div class="tw-space-y-4">
      <MInputSingleSelectRadio v-bind="args" v-model="selected"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected2" variant="success"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected3" variant="danger"/>
    </div>`,
  }),
  args: {
    disabledTooltip: 'This component is disabled',
  },
}

/**
 * The `MInputSingleSelectRadio` component allows you to disable specific options. This is useful when you want to prevent users from selecting certain options.
 * Disabled options are grayed out and users cannot interact with them. Set the option's `disabled` property to `true` to disable it.
 *
 * @example `{ label: 'Two', value: 2, disabled: true }`.
 */
export const DisabledOption: Story = {
  render: (args) => ({
    components: { MInputSingleSelectRadio },
    setup: () => ({ args }),
    data: () => ({ selected: args.modelValue, selected2: args.modelValue, selected3: args.modelValue }),
    template: `<div class="tw-space-y-4">
      <MInputSingleSelectRadio v-bind="args" v-model="selected"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected2" variant="success"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected3" variant="danger"/>
    </div>`,
  }),
  args: {
    label: 'Count',
    options: [
      { label: 'One', value: 1 },
      { label: 'Two', value: 2, disabled: true },
      { label: 'Three', value: 3 },
      { label: 'Four', value: 4, disabled: true },
      { label: 'Five', value: 5 },
    ],
    modelValue: 3,
  },
}

/**
 * You can switch the layout of the `MInputSingleSelectRadio` component to a vertical orientation. Use the `vertical` prop to stack the radio buttons vertically,
 * for example when you have a long list of options or when you want to save space.
 */
export const verticalRadioGroup: Story = {
  args: {
    vertical: true,
  },
}

/**
 * Use the `size` prop to adjust the size of the `MInputSingleSelectRadio` component. The `small` size is ideal for compact layouts where space is limited.
 */
export const Sizes: Story = {
  render: (args) => ({
    components: { MInputSingleSelectRadio },
    setup: () => ({ args }),
    data: () => ({ selected: args.modelValue, selected2: args.modelValue }),
    template: `<div class="tw-space-y-4">
      <MInputSingleSelectRadio v-bind="args" v-model="selected" label="Default role"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected2" size="small" label="Smaller role"/>
    </div>`,
  }),
}

/**
 * The `MInputSingleSelectRadio` component supports different variants to help you convey the context of the selection. There are three variants available: `primary`, `success`, and `danger`.
 * Use the `variant` prop to set the color of the radio button.
 */
export const Variants: Story = {
  render: (args) => ({
    components: { MInputSingleSelectRadio },
    setup: () => ({ args }),
    data: () => ({ selected: args.modelValue, selected2: args.modelValue, selected3: args.modelValue }),
    template: `<div class="tw-space-y-4">
      <MInputSingleSelectRadio v-bind="args" v-model="selected"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected2" variant="success"/>
      <MInputSingleSelectRadio v-bind="args" v-model="selected3" variant="danger"/>
    </div>`,
  }),
}

/**
 * The `MInputSingleSelectRadio` component is designed to be responsive and adapts well to long lists of options and lengthy labels.
 * Additonally, it automatically stacks radio buttons vertically on smaller screens.
 *
 * For the best user experience, we recommended to keep labels short and limit the number of options to 2-10 items. For longer lists
 * consider using a different component, such as the `MInputSingleSelectDropdown`.
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
    modelValue: '2',
  },
}

/**
 * The `MInputSingleSelectRadio` is responsive when stacked vertically, gracefully adapting to different screen sizes.
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
    modelValue: '2',
  },
}
