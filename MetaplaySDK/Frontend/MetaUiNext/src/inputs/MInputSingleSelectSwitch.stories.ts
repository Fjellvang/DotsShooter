import { ref } from 'vue'

import { fn } from '@storybook/test'
import type { Meta, StoryObj } from '@storybook/vue3'

import MInputSingleSelectSwitch from './MInputSingleSelectSwitch.vue'

const meta: Meta<typeof MInputSingleSelectSwitch> = {
  // @ts-expect-error Storybook doesn't seem to like generics?
  component: MInputSingleSelectSwitch,
  tags: ['autodocs'],
  argTypes: {
    size: {
      control: { type: 'select' },
      options: ['small', 'default'],
    },
    variant: {
      control: { type: 'select' },
      options: ['primary', 'success', 'danger', 'warning', 'neutral'],
    },
  },
  args: {
    modelValue: 'option1',
    'onUpdate:modelValue': fn(),
    options: [
      { label: 'Option 1', value: 'option1' },
      { label: 'Option 2', value: 'option2' },
      { label: 'Option 3', value: 'option3' },
      { label: 'Option 4', value: 'option4' },
    ],
  },
  parameters: {
    docs: {
      description: {
        component:
          'The `MInputSingleSelectSwitch` component allows users to toggle between mutually exclusive options. This component is ideal for scenarios where users need to filter results, toggle between views, or choose one option from a set of distinct choices.',
      },
    },
  },
  render: (args) => ({
    components: { MInputSingleSelectSwitch },
    setup: () => ({ args }),
    data: () => ({ modelValue: args.modelValue }),
    template: `<div>
      <MInputSingleSelectSwitch v-bind="args" v-model="modelValue"/>
    </div>`,
  }),
}

export default meta
type Story = StoryObj<typeof MInputSingleSelectSwitch>

/**
 * The `MInputSingleSelectSwitch` enables users to toggle between multiple options while clearly indicating the selected value. It offers a great alternative to a dropdown
 * select element when there are a limited number of options and you want to provide a more visually engaging and interactive experience.
 */
export const Default: Story = {
  args: {
    label: 'Segmented switch',
    hintMessage: 'Zag only supports selecting strings as values.',
  },
}

// Define a generic option type for the options prop.
interface Option<T> {
  label: string
  value: T
}

/**
 * The `MInputSingleSelectSwitch` component allows you to define the data type for the options and the selected value when using the component. This flexibility
 * lets you vary the type based on your specific use case. For example, you can use a number or object as the value type, depending on the data you need to work with.
 */
export const GenericsOption: Story = {
  render: () => ({
    components: { MInputSingleSelectSwitch },
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
        { label: 'Object 1', value: { id: 1, name: 'thing 1' } },
        { label: 'Object 2', value: { id: 2, name: 'thing 2' } },
        { label: 'Object 3', value: { id: 3, name: 'thing 3' } },
        { label: 'Object 4', value: { id: 4, name: 'thing 4' } },
      ]

      // Using reactive object to track model values for multiple types
      const modelValue1 = ref(1)
      const modelValue2 = ref({ id: 1, name: 'thing 1' })

      return { options1, modelValue1, options2, modelValue2 }
    },
    template: `<div class="tw-space-y-10">
      <div>Number options
        <MInputSingleSelectSwitch :options="options1" v-model="modelValue1"/>
      </div>

      <div>Object options
        <MInputSingleSelectSwitch :options="options2" v-model="modelValue2" />
      </div>
    </div>`,
  }),
}

/**
 * This component is available in two sizes: `small` and `default`. Use the `size` prop and set it to `small` when space is limited or when you
 * need to fit multiple components in a single row.
 */
export const Size: Story = {
  render: (args) => ({
    components: { MInputSingleSelectSwitch },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectSwitch v-bind="args"/>
      <MInputSingleSelectSwitch v-bind="args" size="small"/>
    </div>`,
  }),
  args: {
    options: [
      { label: 'All', value: 'option1' },
      { label: 'Any', value: 'option2' },
    ],
  },
}

/**
 * Set the `disabled` prop to `true` to prevent users from interacting with the segmented switch. When disabled, the entire segmented control becomes non-interactive
 * and appears visually muted. This feature is useful for temporarily restricting user access to the control, such as when a certain condition needs to be met before
 * interaction is allowed.
 */
export const Disabled: Story = {
  args: {
    disabled: true,
    options: [
      { label: 'Chill', value: 'option1' },
      { label: 'Try hard', value: 'option2' },
    ],
  },
}

/**
 * Use contextual variants to give the segmented switch a distinct appearance based on the action or state it represents. By default, the segmented switch
 * has a `primary` variant which you can easily override by setting the `variant` prop to one of the following values: `success`, `danger`, `warning`.
 */
export const VariantClasses: Story = {
  render: (args) => ({
    components: { MInputSingleSelectSwitch },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectSwitch v-bind="args"/>
      <MInputSingleSelectSwitch v-bind="args" variant="success"/>
      <MInputSingleSelectSwitch v-bind="args" variant="warning"/>
      <MInputSingleSelectSwitch v-bind="args" variant="danger"/>
    </div>`,
  }),
}

/**
 * Contextual variants also appear muted when the segmented switch is disabled. This ensures that the switch maintains a consistent appearance
 * and clearly communicates its disabled state to users.
 */
export const DisabledVariantClasses: Story = {
  render: (args) => ({
    components: { MInputSingleSelectSwitch },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectSwitch v-bind="args"/>
      <MInputSingleSelectSwitch v-bind="args" variant="success"/>
      <MInputSingleSelectSwitch v-bind="args" variant="warning"/>
      <MInputSingleSelectSwitch v-bind="args" variant="danger"/>
    </div>`,
  }),
  args: {
    disabled: true,
  },
}

/**
 * The `MInputSingleSelectSwitch` labels are customizable and give you the flexibility to provide meaningful and descriptive labels for each option.
 * Use nouns or short phrases for the lable, with title-style capitalization, and maintain content uniformity. While labels can vary in length,
 * keeping them concise ensures the switch remains visually clean and easy to use.
 *
 * For longer descriptions or more detailed information, consider using a dropdown instead.
 */
export const ResponsiveLabel: Story = {
  args: {
    options: [
      { label: 'Option 1', value: 'option1' },
      { label: 'Option 2', value: 'option2' },
      { label: 'ThisLabelHasNoWhiteSpace', value: 'option3' },
      { label: 'This is a very long label name', value: 'option4' },
    ],
    hintMessage: 'We do not support wide use-cases. You should use a dropdown instead.',
  },
}

/**
 * The `MInputSingleSelectSwitch` component is built to be responsive, smoothly adapting to various screen sizes while managing the available options
 * effectively. For optimal usability, we recommend limiting the number of options to 4–5. For more than 5 options, we suggest using the
 * `MInputSingleSelectDropdown` componenet instead.
 */
export const ResponsiveSwitch: Story = {
  args: {
    options: [
      { label: 'Option 1', value: 'option1' },
      { label: 'Option 2', value: 'option2' },
      { label: 'Option 3', value: 'option3' },
      { label: 'Option 4', value: 'option4' },
      { label: 'Option 5', value: 'option5' },
      { label: 'Option 6', value: 'option6' },
      { label: 'Option 7', value: 'option7' },
      { label: 'Option 8', value: 'option8' },
    ],
    hintMessage: 'We do not support wide use-cases. You should use a dropdown instead.',
  },
}
