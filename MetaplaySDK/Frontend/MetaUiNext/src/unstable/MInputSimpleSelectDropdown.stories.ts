import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputSimpleSelectDropdown from './MInputSimpleSelectDropdown.vue'

const meta: Meta<typeof MInputSimpleSelectDropdown> = {
  // @ts-expect-error Storybook doesn't like generics.
  component: MInputSimpleSelectDropdown,
  tags: ['autodocs'],
  args: {
    label: 'Role',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
      { label: 'Game Admin', value: 'gameAdmin' },
      { label: 'Game Viewer', value: 'gameViewer' },
    ],
    modelValue: undefined,
  },
  argTypes: {
    modelValue: {
      control: false,
    },
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
  },
  render: (args) => ({
    components: { MInputSimpleSelectDropdown },
    setup: () => {
      const role = ref(args.modelValue)
      watch(
        () => args.modelValue,
        (newValue) => {
          role.value = newValue
        }
      )
      return { args, role }
    },
    template: `<div>
      <MInputSimpleSelectDropdown v-bind="args" v-model="role"/>
      <pre class="tw-mt-2">Output: {{ role }}</pre>
    </div>`,
  }),
  parameters: {
    viewport: {
      defaultViewport: 'mobile',
    },
    docs: {
      description: {
        component:
          'The `MInputSimpleSelectDropdown` is a simple, out-of-the-box solution designed to display a list of actions or options for users to choose from. It includes a built-in search feature that filters options based on a single property, the `label`, making it ideal for most common use cases. If you need more advanced features or customization, you can explore the [MInputSingleSelectDropdownEx](./?path=/docs/inputs-minputsingleselectdropdownex--overview) for greater flexibility.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSimpleSelectDropdown>

/**
 * The `MInputSimpleSelectDropdown` is designed to display a basic list of options from which a user
 * can select a single value. Each option includes a label and a corresponding value of any type. It
 * features a simple search functionality that filters options based on their `label` property,
 * making it ideal for most common use cases.
 */

export const Default: Story = {
  args: {
    placeholder: 'Choose a role',
    hintMessage: 'This element only supports string labels.',
  },
}

interface Option<T> {
  label: string
  value: T
  disabled?: boolean
}

/**
 * The `label` is always a string representing the human-readable name displayed in the dropdown
 * list. The `value` can be of any type (e.g., a simple string or number, or a more complex object
 * with multiple key-value pairs) and represents the actual value emitted when the option is
 * selected.
 */

export const DropdownOptions: Story = {
  render: () => ({
    components: { MInputSimpleSelectDropdown },
    setup: () => {
      // Define options with generics for number, string, and object values
      const options1: Array<Option<number>> = [
        { label: '0', value: 0 },
        { label: '1', value: 1 },
        { label: '2', value: 2 },
        { label: '3', value: 3 },
        { label: '4', value: 4 },
      ]

      // Define options with object values
      const options2: Array<Option<{ id: number; name: string; color: string; available: boolean; quantity: number }>> =
        [
          { label: 'Apple', value: { id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 } },
          { label: 'Banana', value: { id: 2, name: 'Banana', color: 'Yellow', available: false, quantity: 1 } },
          { label: 'Cherry', value: { id: 3, name: 'Cherry', color: 'Red', available: false, quantity: 5 } },
        ]

      const value1 = ref()
      const value2 = ref()

      // Using reactive object to track model values for multiple types
      return { options1, value1, options2, value2 }
    },
    template: `<div class="tw-space-y-3">
        <MInputSimpleSelectDropdown :options="options1" v-model="value1" label="Number Options"/>
        <div> Output: {{ value1 }} </div>
        <MInputSimpleSelectDropdown :options="options2" v-model="value2" label="Fruit Options"/>
        <div> Output: {{ value2 }} </div>
    </div>`,
  }),
}

/**
 * The `MInputSimpleSelectDropdown` features a built-in search functionality that allows users to
 * filter options by their
 * `label`. Only the `label` is considered during the search, making it simple to quickly find
 * options based on the visible text.
 *
 * If you have a complex value that requires more advanced search capabilities, such as filtering
 * based on a value's properties other than the `label`, consider using the
 * `MInputSingleSelectDropdownEx` component, which offers greater flexibility.
 */
export const Searching: Story = {
  args: {
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
      { label: 'Game Admin', value: 'gameAdmin' },
      { label: 'Game Viewer', value: 'gameViewer' },
    ],
  },
}

/**
 * To allow users to clear the selected option, set the `showClearButton` prop to `true`. This will display a clear button
 * at the end of the input field, enabling users to quickly remove their selection.
 *
 * When the clear button is clicked, the selected value will be set to `undefined`.
 * If the field cannot be empty, pair this feature with custom validation to ensure it's properly validated before submission.
 */
export const ClearButton: Story = {
  render: (args) => ({
    components: { MInputSimpleSelectDropdown },
    setup: () => {
      const role = ref()
      const role1 = ref('admin')

      const variant = ref<string | undefined>() // Default to 'danger' variant
      watch(
        () => role1.value,
        (newValue) => {
          role1.value = newValue
          variant.value = role1.value ? '' : 'danger' // Change variant based on value
        },
        { immediate: true }
      )
      return { args, role, role1, variant }
    },
    template: `<div>
      <MInputSimpleSelectDropdown v-bind="args" v-model="role"/>
      <div class="tw-mt-1 tw-mb-4"> Output: {{ role }} </div>
      <MInputSimpleSelectDropdown v-bind="args" :variant="variant" v-model="role1" :modelValue="user" :hintMessage="role1 ? '': 'This field is required'"/>
      <div class="tw-mt-1 tw-mb-4"> Output: {{ role1 }} </div>
      </div>`,
  }),
  args: {
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
      { label: 'Game Admin', value: 'gameAdmin' },
      { label: 'Game Viewer', value: 'gameViewer' },
    ],
    placeholder: 'Select a role',
    showClearButton: true,
    hintMessage: 'Use the clear button to reset your selection.',
  },
}

/**
 * To disable the `MInputSimpleSelectDropdown`, set the `disabled` prop to `true`. The component will appear
 * visually muted and users will not be able to interact with it.
 *
 * This is useful when the dropdown is in a read-only state or when the options should not be changed.
 * Use the `hintMessage` prop to provide the user with a reason for the disabled state or to guide them on how to enable the dropdown.
 */
export const DisabledDropdown: Story = {
  args: {
    placeholder: 'Choose a role',
    disabled: true,
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
      { label: 'Game Admin', value: 'gameAdmin' },
      { label: 'Game Viewer', value: 'gameViewer' },
    ],
    hintMessage: 'This dropdown is disabled.',
  },
}

/**
 * You can disable specific options in the dropdown by setting the `disabled` property in the option object to `true`.
 * This will visually indicate that the option is not selectable and users will not be able to choose it.
 */
export const DisabledOption: Story = {
  args: {
    options: [
      { label: 'Game Admin', value: 'gameadmin', disabled: true },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest', disabled: true },
      { label: 'Developer', value: 'developer' },
      { label: 'Game Tester', value: 'gametester' },
      { label: 'Customer Support', value: 'customerSupport', disabled: true },
    ],
    placeholder: 'Choose a role',
  },
}

/**
 * This component is responsive and gracefully handles long labels that may overflow the container.
 * We recommend keeping labels concise to maintain a clean and visually appealing interface.
 *
 * For additional information or longer descriptions, consider utilizing the `hintMessage` prop to provide
 * supplementary context without cluttering the main label.
 */
export const OptionLabelOverflow: Story = {
  render: (args) => ({
    components: { MInputSimpleSelectDropdown },
    setup: () => ({ args }),
    template: `<div>
      <MInputSimpleSelectDropdown v-bind="args" />
      <MInputSimpleSelectDropdown v-bind="args" variant="success" hintMessage="Success hint message"/>
      <MInputSimpleSelectDropdown v-bind="args" variant="warning" hintMessage="Warning hint message"/>
      <MInputSimpleSelectDropdown v-bind="args" variant="danger" hintMessage="Danger hint message"/>
    </div>`,
  }),
  args: {
    label: 'Select a member',
    options: [
      {
        label:
          'Super long member name that will overflow lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet',
        value:
          'Super long member name that will overflow lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet',
      },
      {
        label:
          'An even longer member name that will overflow for sure. No idea why you would expect this to look nice. lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet',
        value:
          'An even longer member name that will overflow for sure. No idea why you would expect this to look nice. lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet',
      },
      {
        label:
          'ANameWithNoSpacesThatWillOverflowSuperBadlyLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmet',
        value:
          'ANameWithNoSpacesThatWillOverflowSuperBadlyLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmet',
      },
    ],
    showClearButton: true,
  },
}

/**
 * The `MInputSimpleSelectDropdown` supports a variety of variants to help you communicate the importance of the selection.
 * You can choose between the default, success, warning, and danger variants to match your design system.
 */
export const VisualVariants: Story = {
  render: (args) => ({
    components: { MInputSimpleSelectDropdown },
    setup: () => ({ args }),
    template: `<div>
      <MInputSimpleSelectDropdown v-bind="args" />
      <MInputSimpleSelectDropdown v-bind="args" variant="success" hintMessage="Success hint message"/>
      <MInputSimpleSelectDropdown v-bind="args" variant="warning" hintMessage="Warning hint message"/>
      <MInputSimpleSelectDropdown v-bind="args" variant="danger" hintMessage="Danger hint message"/>
      <MInputSimpleSelectDropdown v-bind="args" showClearButton />
    </div>`,
  }),
  args: {
    placeholder: 'Choose a role',
  },
}

/**
 * The `MInputSimpleSelectDropdown` is compact and space-efficient, making it ideal for handling long lists of options or
 * when space is limited. The options list remains concealed until users interact with the dropdown, ensuring a clean and
 * uncluttered interface. Additionally, once it is open, users can easily scroll through the list to find and select the
 * desired option.
 */
export const LongOptionsList: Story = {
  args: {
    label: 'Select a member',
    options: [
      { label: 'Member 1', value: 'member1' },
      { label: 'Member 2', value: 'member2' },
      { label: 'Member 3', value: 'member3' },
      { label: 'Member 4', value: 'member4' },
      { label: 'Member 5', value: 'member5' },
      { label: 'Member 6', value: 'member6' },
      { label: 'Member 7', value: 'member7' },
      { label: 'Member 8', value: 'member8' },
      { label: 'Member 9', value: 'member9' },
      { label: 'Member 10', value: 'member10' },
      { label: 'Member 11', value: 'member11' },
      { label: 'Member 12', value: 'member12' },
      { label: 'Member 13', value: 'member13' },
      { label: 'Member 14', value: 'member14' },
      { label: 'Member 15', value: 'member15' },
      { label: 'Member 16', value: 'member16' },
      { label: 'Member 17', value: 'member17' },
      { label: 'Member 18', value: 'member18' },
      { label: 'Member 19', value: 'member19' },
      { label: 'Member 20', value: 'member20' },
      { label: 'Member 21', value: 'member21' },
      { label: 'Member 22', value: 'member22' },
      { label: 'Member 23', value: 'member23' },
      { label: 'Member 24', value: 'member24' },
      { label: 'Member 25', value: 'member25' },
      { label: 'Member 26', value: 'member26' },
      { label: 'Member 27', value: 'member27' },
      { label: 'Member 28', value: 'member28' },
      { label: 'Member 29', value: 'member29' },
      { label: 'Member 30', value: 'member30' },
      { label: 'Member 31', value: 'member31' },
      { label: 'Member 32', value: 'member32' },
    ],
  },
}
