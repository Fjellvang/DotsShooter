import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MActionModalButton from '../composites/MActionModalButton.vue'
import MInputSingleSelectSwitch from '../inputs/MInputSingleSelectSwitch.vue'
import MButton from '../primitives/MButton.vue'
import MListItem from '../primitives/MListItem.vue'
import MInputSingleSelectDropdownEx, {
  type MInputSingleSelectDropdownExOption,
} from './MInputSingleSelectDropdownEx.vue'

interface Fruit {
  id: number
  name: string
  color: string
  available: boolean
  quantity: number
}

const FruitOptions: Array<MInputSingleSelectDropdownExOption<Fruit>> = [
  { label: 'Apple', value: { id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 } },
  { label: 'Banana', value: { id: 2, name: 'Banana', color: 'Yellow', available: false, quantity: 1 } },
  { label: 'Cherry', value: { id: 3, name: 'Cherry', color: 'Red', available: false, quantity: 5 } },
  { label: 'Grape', value: { id: 4, name: 'Grape', color: 'Purple', available: true, quantity: 20 } },
  { label: 'Kiwi', value: { id: 5, name: 'Kiwi', color: 'Green', available: true, quantity: 15 } },
  { label: 'Orange', value: { id: 6, name: 'Orange', color: 'Orange', available: true, quantity: 8 } },
  { label: 'Peach', value: { id: 7, name: 'Peach', color: 'Orange', available: true, quantity: 12 } },
  { label: 'Pear', value: { id: 8, name: 'Pear', color: 'Green', available: false, quantity: 3 } },
]

// Number options
const NumberOptions: Array<MInputSingleSelectDropdownExOption<number>> = [
  { label: '0', value: 0 },
  { label: '111', value: 111 },
  { label: '222', value: 222 },
  { label: '333', value: 333 },
  { label: '444', value: 444 },
]

// String options
const StringOptions: MInputSingleSelectDropdownExOption[] = [
  { label: 'Apple', value: 'Apple' },
  { label: 'Banana', value: 'Banana' },
  { label: 'Cherry', value: 'Cherry' },
]

const meta: Meta<typeof MInputSingleSelectDropdownEx> = {
  // @ts-expect-error Storybook doesn't like generics.
  component: MInputSingleSelectDropdownEx,
  tags: ['autodocs'],
  args: {
    label: 'Role',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
      { label: 'Developer', value: 'developer' },
      { label: 'Game Admin', value: 'gameadmin' },
      { label: 'Game Tester', value: 'gametester' },
      { label: 'Customer Support', value: 'customerSupport' },
    ],
    modelValue: undefined,
  },
  argTypes: {
    modelValue: {
      control: false,
    },
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger', 'warning'],
    },
  },
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx },
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
      <MInputSingleSelectDropdownEx v-bind="args" v-model="role"/>
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
          'The `MInputSingleSelectDropdown` is a flexible dropdown component for selecting a single options from a list. It includes a built-in search for filtering the options, making it easy to use for common cases. For advanced needs, it supports custom data types, search logic and/or rendering for greater flexibility and control.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSingleSelectDropdownEx>

/**
 * The `MInputSingleSelectDropdown` is a component that displays a list of options, each consisting of a label
 * and a corresponding value of any type. It provides a simple, built-in search functionality, allowing users to
 * quickly find and select an option from the list.
 */
export const Default: Story = {
  args: {
    placeholder: 'Choose a role',
    hintMessage: 'This element supports only searching string labels and values.',
  },
}

/**
 * The `MInputSingleSelectDropdown` supports various data types for its `options` property.
 * Both the labels and values can be strings, numbers, objects, or other types,
 * making the component flexible for different use cases.
 */
export const GenericsOption: Story = {
  render: () => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => {
      const value1 = ref()
      const value2 = ref()
      const value3 = ref()

      // Using reactive object to track model values for multiple types
      return { NumberOptions, value1, StringOptions, value2, FruitOptions, value3 }
    },
    template: `<div class="tw-space-y-3">
      <div>
        <MInputSingleSelectDropdownEx :options="NumberOptions" v-model="value1" label="Number options"/>
        <div> Output: {{ value1 }} </div>
      </div>

      <div>
        <MInputSingleSelectDropdownEx :options="StringOptions" v-model="value2" label="String options"/>
        <div> Output: {{ value2 }} </div>
      </div>
      <div>
        <MInputSingleSelectDropdownEx :options="FruitOptions" v-model="value3" label="Object options"/>
        <div> Output: {{ value3 }} </div>
      </div>
    </div>`,
  }),
  args: {
    hintMessage: 'This element supports only searching string or number labels and values.',
  },
}

/**
 * The `MInputSingleSelectDropdown` provides built-in search functionality, allowing users to filter options
 * based on the input query. It filters the options list with the `label` field of each option. For more complex data structures, the
 * `searchFunction` prop can be used to define custom search logic.
 *
 * In the examples below, both non-string types can be searched because they have text labels.
 */
export const BasicSearch: Story = {
  render: () => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => {
      const selectedNumber = ref()
      const selectedFruit = ref()

      return { NumberOptions, selectedNumber, FruitOptions, selectedFruit }
    },
    template: `<div>
      <MInputSingleSelectDropdownEx label="Number" :options="NumberOptions" v-model="selectedNumber" placeholder="Search by number label"/>
      <pre class="tw-mt-2">Output: {{ selectedNumber }}</pre>

      <MInputSingleSelectDropdownEx label="Fruit" :options="FruitOptions" v-model="selectedFruit" placeholder="Search by fruit label" />
      <pre class="tw-mt-2">Output: {{ selectedFruit }}</pre>
    </div>`,
  }),
}

/**
 * For complex data with many searchable fields you can define custom search logic using the `searchFunction` prop. You can decide to filter based on the label,
 * value and/or any other fields in the data.
 *
 * We recommend using the `hintMessage` and `placeholder` props to guide users on which values they can search for.
 */
export const CustomSearch: Story = {
  render: () => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => {
      function searchByName(
        options: Array<{ label: string; value: Fruit }>,
        query: string
      ): Array<{ label: string; value: Fruit }> {
        return options.filter((option) => option.label.toLowerCase().includes(query))
      }

      function searchByColor(
        options: Array<{ label: string; value: Fruit }>,
        query: string
      ): Array<{ label: string; value: Fruit }> {
        return options.filter((option) => option.value.color.toLowerCase().includes(query))
      }

      function searchByNameOrColor(
        options: Array<{ label: string; value: Fruit }>,
        query: string
      ): Array<{ label: string; value: Fruit }> {
        return options.filter(
          (option) => option.label.toLowerCase().includes(query) || option.value.color.toLowerCase().includes(query)
        )
      }

      const value = ref()
      const value1 = ref()
      const value2 = ref()

      return { FruitOptions, searchByName, searchByColor, searchByNameOrColor, value, value1, value2 }
    },
    template: `<div class="tw-space-y-3">

      <MInputSingleSelectDropdownEx label="Search by label" :options="FruitOptions" :searchFunction="searchByName" v-model="value" placeholder="Search by fruit name" hintMessage="Search results only display matches to the fruit name."/>
      <pre class="tw-mt-2">Output: {{ value }}</pre>

      <MInputSingleSelectDropdownEx label="Search by color" :options="FruitOptions" :searchFunction="searchByColor" v-model="value1" placeholder="Search fruit color" hintMessage="There can be multiple matches in this search."/>
      <pre class="tw-mt-2">Output: {{ value1 }}</pre>

      <MInputSingleSelectDropdownEx label="Search by name & color" :options="FruitOptions" :searchFunction="searchByNameOrColor" v-model="value2" placeholder="Search fruit name or color" hintMessage="Search results display matches to both the fruit name or color"/>
      <pre class="tw-mt-2">Output: {{ value2 }}</pre>

    </div>`,
  }),
}

/**
 * The `MInputSingleSelectDropdown` supports custom UI rendering via slots.
 * By default, it displays the option's `label` in both the input field and the dropdown.
 *
 * Use the `#selection` slot to customize the content shown in the input field when an option is selected.
 * You can add extra information, icons, or formatting to the selected value, providing users with more context.
 */
export const UsingSelectionSlots: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx, MListItem },
    setup: () => ({ args }),
    template: `<div class="tw-space-y-10">
      <MInputSingleSelectDropdownEx v-bind="args" label="Default slot">
      </MInputSingleSelectDropdownEx>

      <MInputSingleSelectDropdownEx v-bind="args" label="Using custom #selection slot">
        <template #selection="{ option }">
          <MListItem class="tw-w-full" noLeftPadding>
            <span>Label: {{ option?.value.name }}</span>
            <template #top-right>ID: {{ option?.value.id }}</template>
            <template #bottom-left>Color: {{ option?.value.color }} Available: {{ option?.value.available }}</template>
            <template #bottom-right>Quantity: {{ option?.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdownEx>

      <MInputSingleSelectDropdownEx v-bind="args" label="Custom templates with initial value" :modelValue="{ id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 }">
        <template #selection="{ option }">
          <MListItem class="tw-w-full" noLeftPadding>
            <span>Label: {{ option?.value.name }}</span>
            <template #top-right>ID: {{ option?.value.id }}</template>
            <template #bottom-left>Color: {{ option?.value.color }} Available: {{ option?.value.available }}</template>
            <template #bottom-right>Quantity: {{ option?.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdownEx>
    </div>`,
  }),
  args: {
    placeholder: 'Choose a fruit',
    options: [
      { value: { id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 }, label: 'Apple' },
      { value: { id: 2, name: 'Banana', color: 'Yellow', available: false, quantity: 1 }, label: 'Banana' },
      { value: { id: 3, name: 'Cherry', color: 'Red', available: false, quantity: 5 }, label: 'Cherry' },
    ],
  },
}

/**
 * Use the `#option` slot to customize the appearance of the options in the dropdown list. This slot lets you modify
 * how each option is displayed, enabling you to add custom styling, icons, or extra information.
 *
 * You can also use both the `#option` and `#selection` slots together for full customization.
 * Remember to provide separate templates for each slot to ensure proper rendering.
 */
export const UsingOptionsSlot: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx, MListItem },
    setup: () => ({ args }),
    template: `<div class="tw-space-y-10">
      <MInputSingleSelectDropdownEx v-bind="args" label="Custom #option template">
        <template #option="{ optionInfo }">
          <MListItem class="tw-w-full" noLeftPadding>
            <span>Label: {{ optionInfo.value.name }}</span>
            <template #top-right>ID: {{ optionInfo.value.id }}</template>
            <template #bottom-left>Color: {{ optionInfo.value.color }} Available: {{ optionInfo.value.available }}</template>
            <template #bottom-right>Quantity: {{ optionInfo.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdownEx>

      <MInputSingleSelectDropdownEx v-bind="args" label="Custom #option and #selection templates">
        <template #selection="{ option }">
          <MListItem class="tw-w-full" noLeftPadding>
            <span>Label: {{ option?.value.name }}</span>
            <template #top-right>ID: {{ option?.value.id }}</template>
            <template #bottom-left>Color: {{ option?.value.color }} Available: {{ option?.value.available }}</template>
            <template #bottom-right>Quantity: {{ option?.value.quantity }}</template>
          </MListItem>
        </template>

        <template #option="{ optionInfo }">
          <MListItem class="tw-w-full" noLeftPadding>
            <span>Label: {{ optionInfo.value.name }}</span>
            <template #top-right>ID: {{ optionInfo.value.id }}</template>
            <template #bottom-left>Color: {{ optionInfo.value.color }} Available: {{ optionInfo.value.available }}</template>
            <template #bottom-right>Quantity: {{ optionInfo.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdownEx>
    </div>`,
  }),
  args: {
    placeholder: 'Choose a fruit',
    options: [
      { value: { id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 }, label: 'Apple' },
      { value: { id: 2, name: 'Banana', color: 'Yellow', available: false, quantity: 1 }, label: 'Banana' },
      { value: { id: 3, name: 'Cherry', color: 'Red', available: false, quantity: 5 }, label: 'Cherry' },
    ],
  },
}

/**
 * Add a `label` to the `MInputSingleSelectDropdown` component to provide a title for the input field.
 * The label appears above the dropdown, helping users understand its purpose and what information to enter.
 *
 * In minimalist designs, the label can be omitted, but it's recommended to use a placeholder or hint message instead.
 *
 * Best Practices:
 * - Use clear, concise labels to describe the expected input or available options.
 * - Avoid lengthy labels to keep the interface clean and avoid clutter, especially on smaller screens.
 */
export const DropdownLabel: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => ({ args }),
    template: `<div class="tw-space-y-3">
      <MInputSingleSelectDropdownEx v-bind="args" hintMessage="This dropdown has no label."/>
      <MInputSingleSelectDropdownEx v-bind="args" label="New role"/>
      <MInputSingleSelectDropdownEx v-bind="args" label="This is a very very very very very long label, in most cases just keep it short and sweet." hintMessage="This label is a little too long"/>
    </div>`,
  }),
  args: {
    label: undefined,
  },
}

/**
 * Set the `placeholder` prop on the `MInputSingleSelectDropdown` to provide a hint or prompt for users. This placeholder text will be
 * displayed in the input field when no option is selected, guiding users on the expected input or encouraging them to make a selection.
 */
export const PlaceholderText: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectDropdownEx v-bind="args"/>
    </div>`,
  }),
  args: {
    placeholder: 'Choose a role',
  },
}

/**
 * Set the `hintMessage` prop to add a hint message to the `MInputSingleSelectDropdown` and provide additional information or context about
 * the input field. This message is displayed below the input field and can be used to provide instructions, tips, or error messages, helping
 * users understand the input requirements or the results of their selection.
 */
export const HintMessage: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectDropdownEx v-bind="args" placeholder="" hintMessage="This is a hint. Select a role to continue."/>
      <MInputSingleSelectDropdownEx v-bind="args" placeholder="Choose a role" variant="success" hintMessage="Success hint."/>
      <MInputSingleSelectDropdownEx v-bind="args" placeholder="Choose a role" variant="warning" hintMessage="This is a warning."/>
      <MInputSingleSelectDropdownEx v-bind="args" placeholder="Choose a role" variant="danger" hintMessage="This field is required."/>
    </div>`,
  }),
  args: {},
}

/**
 * You can set the initial value of the dropdown by providing a value for the `modelValue` prop. This will pre-select the
 * corresponding option in the dropdown, displaying the label of the selected value in the input field.
 *
 * This is a useful feature when you need to pre-populate the dropdown with a default value or when you want to display the
 * current selection in an edit form or view.
 */
export const initialValue: Story = {
  args: {
    label: 'Role',
    modelValue: 'admin',
  },
}

/**
 * The `danger` variant can be used in combination with custom validation logic to indicate when a field is required.
 * When the `variant` is set to `danger`, the dropdown will be highlighted in red and an an icon will appear to
 * alert users that the input is invalid or incomplete. This can help users quickly identify and correct any issues
 * with their selection.
 */
export const CustomValidation: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx },
    setup: () => {
      const value = ref(args.modelValue)
      const variant = ref<string | undefined>() // Default to 'danger' variant
      watch(
        () => value.value,
        (newValue) => {
          value.value = newValue
          variant.value = value.value ? '' : 'danger' // Change variant based on value
        },
        { immediate: true }
      )
      return { args, value, variant }
    },
    template: `<div>
      <MInputSingleSelectDropdownEx v-bind="args" :variant="variant" v-model="value" :hintMessage="value ? '': 'This field is required'" />
    </div>`,
  }),
  args: {
    label: 'Role',
    modelValue: undefined,
    showClearButton: true,
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
    components: { MInputSingleSelectDropdownEx },
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
      <MInputSingleSelectDropdownEx v-bind="args" v-model="role"/>
      <div class="tw-mt-1 tw-mb-4"> Output: {{ role }} </div>
      <MInputSingleSelectDropdownEx v-bind="args" :variant="variant" v-model="role1" :modelValue="user" :hintMessage="role1 ? '': 'This field is required'"/>
      <div class="tw-mt-1 tw-mb-4"> Output: {{ role1 }} </div>
      </div>`,
  }),
  args: {
    placeholder: 'Select a role',
    showClearButton: true,
    hintMessage: 'Use the clear button to reset your selection.',
  },
}

/**
 * To disable the `MInputSingleSelectDropdown`, set the `disabled` prop to `true`. The component will appear
 * visually muted and users will not be able to interact with it.
 *
 * This is useful when the dropdown is in a read-only state or when the options should not be changed.
 * Use the `hintMessage` prop to provide the user with a reason for the disabled state or to guide them on how to enable the dropdown.
 */
export const DisabledDropdown: Story = {
  args: {
    placeholder: 'Choose a role',
    disabled: true,
    hintMessage: 'This dropdown is disabled.',
  },
}

/**
 * You can disable specific options in the dropdown by setting the `disabled` property in the option object to `true`.
 * This will visually indicate that the option is not selectable and users will not be able to choose it.
 */
export const DisabledOption: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx, MListItem },
    setup: () => {
      const options: MInputSingleSelectDropdownExOption[] = [
        { label: 'Admin', value: 'admin', disabled: true },
        { label: 'User', value: 'user' },
        { label: 'Guest', value: 'guest' },
        { label: 'Developer', value: 'developer', disabled: true },
        { label: 'Game Admin', value: 'gameadmin' },
        { label: 'Game Tester', value: 'gametester' },
        { label: 'Customer Support', value: 'customerSupport' },
      ]

      const options2: Array<
        MInputSingleSelectDropdownExOption<{
          name: string
          description: string
          category: string
          permissions: string[]
        }>
      > = [
        {
          label: 'Admin',
          value: {
            name: 'admin',
            description: 'Administrator with full system access',
            category: 'Management',
            permissions: ['read', 'write', 'delete'], // Full access
          },
          disabled: true,
        },
        {
          label: 'User',
          value: {
            name: 'user',
            description: 'Regular user with limited access',
            category: 'General',
            permissions: ['read', 'write'], // Limited write permissions
          },
        },
        {
          label: 'Guest',
          value: {
            name: 'guest',
            description: 'Guest user with read-only permissions',
            category: 'General',
            permissions: ['read'], // Read-only access
          },
        },
        {
          label: 'Developer',
          value: {
            name: 'developer',
            description: 'Developer with access to dev tools',
            category: 'Technical',
            permissions: ['read', 'write', 'debug'], // Debugging and writing access
          },
          disabled: true,
        },
        {
          label: 'Game Admin',
          value: {
            name: 'gameadmin',
            description: 'Administrator for game-specific settings',
            category: 'Gaming',
            permissions: ['read', 'write', 'moderate'], // Moderate game-related content
          },
        },
        {
          label: 'Game Tester',
          value: {
            name: 'gametester',
            description: 'Tester for game features and bugs',
            category: 'Gaming',
            permissions: ['read', 'debug'], // Debugging game features
          },
        },
        {
          label: 'Customer Support',
          value: {
            name: 'customerSupport',
            description: 'Support representative for customers',
            category: 'Support',
            permissions: ['read', 'write', 'resolve'], // Resolve customer tickets
          },
        },
      ]

      return { args, options, options2 }
    },
    template: `<div>
      <MInputSingleSelectDropdownEx v-bind="args" :options="options"/>
      <MInputSingleSelectDropdownEx v-bind="args" :options="options2">
        <template #selection="{ option }">
           <MListItem class="tw-w-full" noLeftPadding>
            <span>{{ option?.label }}</span>
            <template #top-right> {{ option?.value.permissions }} </template>
            <template #bottom-left> {{ option?.value.description }}</template>
            <template #bottom-right>{{ option?.value.category }}</template>
          </MListItem>
        </template>
        <template #option="{ optionInfo }">
           <MListItem class="tw-w-full" noLeftPadding>
            <span>{{ optionInfo.label }}</span>
            <template #top-right>{{ optionInfo.value.permissions }}</template>
            <template #bottom-left>{{ optionInfo.value.description }} Available: {{ optionInfo.label.available }}</template>
            <template #bottom-right>{{ optionInfo.value.category }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdownEx>
    </div>`,
  }),
  args: {
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
    components: { MInputSingleSelectDropdownEx },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectDropdownEx v-bind="args" />
      <MInputSingleSelectDropdownEx v-bind="args" variant="success" hintMessage="Success hint message"/>
      <MInputSingleSelectDropdownEx v-bind="args" variant="warning" hintMessage="Warning hint message"/>
      <MInputSingleSelectDropdownEx v-bind="args" variant="danger" hintMessage="Danger hint message"/>
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
 * The `MInputSingleSelectDropdown` is compact and space-efficient, making it ideal for handling long lists of options or when
 * space is limited. The options list remains concealed until users interact with the dropdown, ensuring a clean and uncluttered
 * interface. Additionally, once it is open, users can easily scroll through the list to find and select the desired option.
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

/**
 * * The `MInputSingleSelectDropdown` is a versatile component that works in forms, modals, and views.
 * It can be conditionally displayed based on state, user interaction, or screen size.
 *
 * The component handles different UI scenarios and fits seamlessly into various layouts.
 * It automatically manages the stacking of the input field and dropdown, ensuring they display
 * correctly even with other overlapping elements. This is especially useful in forms, settings,
 * and modals where dropdowns are needed.
 */
export const ZIndexStackingOrder: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdownEx, MActionModalButton, MButton, MInputSingleSelectSwitch },
    setup() {
      const isVisible = ref(false) // Control visibility using ref
      const toggleDropdown = () => {
        isVisible.value = !isVisible.value // Function to toggle dropdown visibility
      }
      return { args, isVisible, toggleDropdown }
    },
    template: `
    <div class="tw-space-y-3">
      <div class="tw-font-semibold">🔍 Quest for the Hidden Dropdown</div>
      <p>Can you uncover my secret? Look closely and see if you can discover me hidden among the clues!</p>

      <div>
        <MActionModalButton modalTitle="Example Modal" triggerButtonLabel="Open" :action="() => {}">
        <MInputSingleSelectDropdownEx v-bind="args" hintMessage="Here I am."/>
        <div>Are you sure you want to do this?</div>
        <template #result-panel><p> Congratulations! Your role has been updated to {{ args.modelValue }}.</p></template>
        </MActionModalButton>
        <div class="tw-text-xs tw-text-neutral-400 tw-my-1">Explore your surroundings and look for unusual patterns.</div>
      </div>

      <div>
        <MButton @click="toggleDropdown">Click</MButton>
        <MInputSingleSelectDropdownEx v-bind="args" v-if="isVisible" hintMessage="Here I am."/>
        <div v-else class="tw-text-xs tw-text-neutral-400 tw-my-1">I could be lurking anywhere.</div>
      </div>

    </div>`,
  }),
  parameters: {
    viewport: {
      defaultViewport: 'twoColumn',
    },
  },
  args: {},
}
