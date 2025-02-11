import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MActionModalButton from '../composites/MActionModalButton.vue'
import MButton from '../primitives/MButton.vue'
import MListItem from '../primitives/MListItem.vue'
import MInputSingleSelectDropdown from './MInputSingleSelectDropdown.vue'
import MInputSingleSelectSwitch from './MInputSingleSelectSwitch.vue'

const meta: Meta<typeof MInputSingleSelectDropdown> = {
  // @ts-expect-error Storybook doesn't like generics.
  component: MInputSingleSelectDropdown,
  tags: ['autodocs'],
  args: {
    label: 'Role',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
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
    components: { MInputSingleSelectDropdown },
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
      <MInputSingleSelectDropdown v-bind="args" v-model="role"/>
      <pre class="tw-mt-2">Output: {{ role }}</pre>
    </div>`,
  }),
  parameters: {
    viewport: {
      defaultViewport: 'twoColumn',
    },
    docs: {
      description: {
        component:
          'The `MInputSingleSelectDropdown` is an input element that allows users to select a single option from a list of predefined values. It can be used in a variety of contexts such as forms, modals or views to present users with a clear list of choices.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSingleSelectDropdown>

/**
 * At its simplest, the `MInputSingleSelectDropdown` requires you to provide an array of options, each consisting of a key-value pair with
 * `label` and `value`. Additionally, you need to set the `modelValue` and bind it using v-model. The component will then render a dropdown
 * populated with the options and display the selected value.
 */
export const Default: Story = {
  args: {
    placeholder: 'Choose a role',
    hintMessage: 'This element only supports string values.',
  },
}

interface Option<T> {
  label: string
  value: T
  disabled?: boolean
}

/**
 * The `MInputSingleSelectDropdown` is a generic component designed to accommodate any type of options.
 * Both the options property and the modelValue can handle various data types, including strings, numbers,
 * and objects. This versatility ensures that the component can be flexibly utilized across a wide range of
 * use cases.
 */
export const GenericsOption: Story = {
  render: () => ({
    components: { MInputSingleSelectDropdown },
    setup: () => {
      // Define options with generics for number, string, and object values
      const options1: Array<Option<number>> = [
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
      <div>Number options
        <MInputSingleSelectDropdown :options="options1" v-model="value1"/>
      </div>
      <div> Output: {{ value1 }} </div>

      <div>Fruit options
        <MInputSingleSelectDropdown :options="options2" v-model="value2"/>
      </div>
      <div> Output: {{ value2 }} </div>
    </div>`,
  }),
}

/**
 * The MInputSingleSelectDropdown features two main slots that enable you to customize the appearance of the
 * selection and option items. By default, the component displays the labels of the selected value and available
 * options in the dropdown and input field, respectively.
 *
 * You can use the #selection and #option slots to override this default rendering, allowing you to provide custom templates
 * for both the selection and option items.
 */
export const CustomRenderingUsingSlots: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdown, MListItem },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectDropdown v-bind="args" label="Custom #selection template">
        <template #selection="{ value }">
          <MListItem class="tw-w-full">
            <span>Label: {{ value?.label }}</span>
            <template #top-right>ID: {{ value?.value.id }}</template>
            <template #bottom-left>Color: {{ value?.value.color }} Available: {{ value?.value.available }}</template>
            <template #bottom-right>Quantity: {{ value?.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdown>

      <MInputSingleSelectDropdown v-bind="args" label="Custom #option template">
        <template #option="{ option }">
          <MListItem class="tw-w-full">
            <span>Label: {{ option.label }}</span>
            <template #top-right>ID: {{ option.value.id }}</template>
            <template #bottom-left>Color: {{ option.value.color }} Available: {{ option.value.available }}</template>
            <template #bottom-right>Quantity: {{ option.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdown>

      <MInputSingleSelectDropdown v-bind="args" label="Custom templates">
        <template #selection="{ value }">
          <MListItem class="tw-w-full">
            <span>Label: {{ value?.label }}</span>
            <template #top-right>ID: {{ value?.value.id }}</template>
            <template #bottom-left>Color: {{ value?.value.color }} Available: {{ value?.value.available }}</template>
            <template #bottom-right>Quantity: {{ value?.value.quantity }}</template>
          </MListItem>
        </template>

        <template #option="{ option }">
          <MListItem class="tw-w-full">
            <span>Label: {{ option.label }}</span>
            <template #top-right>ID: {{ option.value.id }}</template>
            <template #bottom-left>Color: {{ option.value.color }} Available: {{ option.value.available }}</template>
            <template #bottom-right>Quantity: {{ option.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdown>

      <MInputSingleSelectDropdown v-bind="args" label="Custom templates with initial value" :modelValue="{ id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 }">
        <template #selection="{ value }">
          <MListItem class="tw-w-full">
            <span>Label: {{ value?.label }}</span>
            <template #top-right>ID: {{ value?.value.id }}</template>
            <template #bottom-left>Color: {{ value?.value.color }} Available: {{ value?.value.available }}</template>
            <template #bottom-right>Quantity: {{ value?.value.quantity }}</template>
          </MListItem>
        </template>

        <template #option="{ option }">
          <MListItem class="tw-w-full">
            <span>Label: {{ option.label }}</span>
            <template #top-right>ID: {{ option.value.id }}</template>
            <template #bottom-left>Color: {{ option.value.color }} Available: {{ option.value.available }}</template>
            <template #bottom-right>Quantity: {{ option.value.quantity }}</template>
          </MListItem>
        </template>
      </MInputSingleSelectDropdown>

    </div>`,
  }),
  args: {
    placeholder: 'Choose a fruit',
    options: [
      { label: 'Apple', value: { id: 1, name: 'Apple', color: 'Red', available: true, quantity: 10 } },
      { label: 'Banana', value: { id: 2, name: 'Banana', color: 'Yellow', available: false, quantity: 1 } },
      { label: 'Cherry', value: { id: 3, name: 'Cherry', color: 'Red', available: false, quantity: 5 } },
    ],
  },
}

/**
 * To disable the `MInputSingleSelectDropdown`, set the `disabled` prop to `true`. The component will appear
 * visually muted and users will not be able to interact with it.
 *
 * This is useful when the dropdown is in a read-only state or when the options should not be changed.
 */
export const Disabled: Story = {
  args: {
    modelValue: 'admin',
    placeholder: 'Choose a role',
    disabled: true,
  },
}

/**
 * Set the `placeholder` prop on the `MInputSingleSelectDropdown` to provide a hint or prompt for users. This placeholder text will be
 * displayed in the input field when no option is selected, guiding users on the expected input or encouraging them to make a selection.
 *
 * Note: To display the placeholder text when the component is first rendered, set the `modelValue` to `undefined`.
 * Once an option is selected, the placeholder text will no longer be visible.
 *
 * Set the `hintMessage` prop to provide additional information or context about the input field. This message will be displayed below
 * the input field. It can be used to provide instructions, tips, or error messages to users, helping them understand the input requirements
 * or the result of their selection.
 */
export const InputCuesAndHints: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdown },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectDropdown v-bind="args" placeholder=""/>
      <MInputSingleSelectDropdown v-bind="args" hintMessage="Success hint message"/>
    </div>`,
  }),
}

/**
 * Contextual colors offer visual cues to users about the state of the dropdown. Set the `variant` prop to
 * `success` or `danger` to highlight the dropdown with a green or red color, respectively. This can be used to
 * indicate successful selections or errors, providing users with clear feedback on their input.
 */
export const ContextualVariants: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdown },
    setup: () => ({ args }),
    template: `<div>
      <MInputSingleSelectDropdown v-bind="args"/>
      <MInputSingleSelectDropdown v-bind="args" variant="success" hintMessage="Success hint message"/>
      <MInputSingleSelectDropdown v-bind="args" variant="danger" hintMessage="Danger hint message"/>
    </div>`,
  }),
}

/**
 * The `MInputSingleSelectDropdown` component supports an optional `label` prop to provide a descriptive title for the input field.
 * This label is displayed above the dropdown and helps users understand the purpose of the input and the type of information
 * they should provide.
 *
 * Use clear and concise labels that describe the expected input or the options available to users. If you prefer a cleaner and minimalistic
 * look and design, you can omit the `label` prop to hide the `label` and display the dropdown without additional text, reducing visual clutter.
 */
export const OptionalLabel: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdown },
    setup: () => ({ args }),
    template: `<div class="tw-space-y-10">
      <MInputSingleSelectDropdown v-bind="args" label="Role"/>
      <MInputSingleSelectDropdown v-bind="args" hintMessage="I have no label"/>
    </div>`,
  }),
  args: {
    label: undefined,
  },
}

/**
 * This component is responsive and gracefully handles long labels that may overflow the container.
 *
 * We recommend keeping labels concise to maintain a clean and visually appealing interface. For additional
 * information or longer descriptions, consider utilizing the `hintMessage` prop to provide supplementary context
 * without cluttering the main label.
 */
export const OptionLabelOverflow: Story = {
  args: {
    label: 'Select a member',
    options: [
      {
        label:
          'Super long member name that will overflow lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet',
        value: 'member1',
      },
      {
        label:
          'An even longer member name that will overflow for sure. No idea why you would expect this to look nice. lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet lorem ipsum dolor sit amet',
        value: 'member2',
      },
      {
        label:
          'ANameWithNoSpacesThatWillOverflowSuperBadlyLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmetLoremIpsumDolorSitAmet',
        value: 'member3',
      },
    ],
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
 * The is a versatile component and can be used in forms, modals and views. You can
 * also conditionally display it based on a specific state, user interaction or screen size.
 */
export const DropdownUsage: Story = {
  render: (args) => ({
    components: { MInputSingleSelectDropdown, MActionModalButton, MButton, MInputSingleSelectSwitch },
    setup() {
      const isVisible = ref(false) // Control visibility using ref
      const toggleDropdown = () => {
        isVisible.value = !isVisible.value // Function to toggle dropdown visibility
      }
      return { args, isVisible, toggleDropdown }
    },
    template: `<div class="tw-space-y-3">
      <div class="tw-font-semibold">🔍 Quest for the Hidden Dropdown</div>
      <p>Can you uncover my secret? Look closely and see if you can discover me hidden among the clues!</p>
      <div>
        <MActionModalButton modalTitle="Example Modal" triggerButtonLabel="Open" :action="() => {}">
        <MInputSingleSelectDropdown v-bind="args" hintMessage="Here I am."/>
        <template #result-panel><p> Congratulations! Your role has been updated to {{ args.modelValue }}.</p></template>
        </MActionModalButton>
        <div class="tw-text-xs tw-text-neutral-400 tw-my-1">Explore your surroundings and look for unusual patterns.</div>

      </div>

      <div>
        <MButton @click="toggleDropdown">Click</MButton>
        <MInputSingleSelectDropdown v-if="isVisible" v-bind="args" hintMessage="Here I am."/>
        <div v-else class="tw-text-xs tw-text-neutral-400 tw-my-1">I could be lurking anywhere.</div>
      </div>

      <div>
        <MInputSingleSelectSwitch v-bind="args"/>
        <div class="@lg:tw-block tw-hidden tw-text-xs tw-text-neutral-400 tw-my-1">Perhaps a smaller view might be the key to uncover what you are looking for. </div>
      </div>
    </div>`,
  }),
  parameters: {
    viewport: {
      defaultViewport: 'twoColumn',
    },
  },
  args: {
    label: 'Role',
    options: [
      { label: 'Admin', value: 'admin' },
      { label: 'User', value: 'user' },
      { label: 'Guest', value: 'guest' },
    ],
    modelValue: 'admin',
  },
}
