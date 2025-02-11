import { fn } from '@storybook/test'
import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MInputSwitch from './MInputSwitch.vue'

const meta: Meta<typeof MInputSwitch> = {
  component: MInputSwitch,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['primary', 'success', 'warning', 'danger'],
    },
    size: {
      control: { type: 'radio' },
      options: ['extraSmall', 'small', 'default'],
    },
  },
  args: {
    modelValue: true,
    'onUpdate:modelValue': fn(),
  },
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => ({ args }),
    template: `<div>
      <MInputSwitch v-bind="args"/>
    </div>`,
  }),
  parameters: {
    docs: {
      description: {
        component:
          'The `MInputSwitch` is a simple control element that lets users toggle between two states for example, enabling or disabling features, switching modes, or controlling content visibility.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputSwitch>

export const Default: Story = {
  args: {
    name: 'default switch',
  },
}

/**
 * The `MInputSwitch` component is available in three different sizes: `default`, `small`, and `extraSmall`. Use the `size` prop to adjust the size
 * of the switch to fit the layout or design of your application. The default size is `default`.
 */
export const SwitchSizes: Story = {
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => ({ args }),
    template: `<div class="tw-space-x-2">
      <MInputSwitch v-bind="args"/>
      <MInputSwitch v-bind="args" size="small"/>
      <MInputSwitch v-bind="args" size="extraSmall"/>
    </div>`,
  }),
}

/**
 * The `MInputSwitch` component is available in four different variants: `primary`, `success`, `warning`, and `danger`. Use the `variant` prop
 * to add contextual color to the switch based on the state or status it represents. The default variant is `primary`.
 */
export const SwitchVariants: Story = {
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => ({ args }),
    template: `<div class="tw-space-x-2">
      <MInputSwitch v-bind="args"/>
      <MInputSwitch v-bind="args" variant="success"/>
      <MInputSwitch v-bind="args" variant="warning"/>
      <MInputSwitch v-bind="args" variant="danger"/>
    </div>`,
  }),
}

/**
 * Use the `disabled` prop to disable the switch component. When the switch is disabled, the switch appears muted and the user cannot interact with it.
 * This is useful when you want to prevent the user from accessing a certain feature or functionality based on certain conditions.
 */
export const DisabledSwitchWithVariants: Story = {
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => ({ args }),
    template: `<div class="tw-space-x-2">
      <MInputSwitch v-bind="args" />
      <MInputSwitch v-bind="args" variant="success"/>
      <MInputSwitch v-bind="args" variant="danger"/>
      <MInputSwitch v-bind="args" variant="warning"/>
    </div>`,
  }),
  args: {
    disabled: true,
  },
}

/**
 * The `MInputSwitch` component can be used to control access to certain features or functionality based on the user's permissions. Use the `permission` prop
 * to specify the permission required to access the feature or functionality controlled by the switch.
 *
 * If the user does not have the required permission, the switch will be disabled and a tooltip will be displayed when the user hovers over it.
 *
 */
export const Permissions: Story = {
  render: (args) => ({
    components: { MInputSwitch },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])
      return {
        args,
      }
    },
    template: `<div class="tw-space-x-2">
      <MInputSwitch v-bind="args" permission="example-permission"/>
      <MInputSwitch v-bind="args" permission="example-permission2"/>
    </div>`,
  }),
}
