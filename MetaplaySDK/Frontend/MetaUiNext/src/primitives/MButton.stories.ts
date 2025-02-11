import { fn } from '@storybook/test'
import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MButtonGroupLayout from '../layouts/MButtonGroupLayout.vue'
import MButton from './MButton.vue'

const meta: Meta<typeof MButton> = {
  component: MButton,
  args: { onClick: fn() },
  tags: ['autodocs'],
  argTypes: {
    size: {
      control: {
        type: 'inline-radio',
      },
      options: ['small', 'default'],
    },
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    },
    safetyLock: {
      control: {
        type: 'boolean',
      },
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'MButton is a button component that users can click. We have pre-built different visual variants, sizes and states into this base component to make it as easy as possible to handle different edge cases in your UI. This component can be used as-is or together with the MButtonGroupLayout component if you need to arrange multiple buttons in a row.',
      },
    },
  },
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButton v-bind="args">
      I am a button
    </MButton>`,
  }),
}

export default meta
type Story = StoryObj<typeof MButton>

export const Default: Story = {
  args: {},
}

/**
 * You can use the `MButton` as a link by setting the `to` prop to navigate to both internal and external links.
 * Despite its visual apprerance as a button, it internally functions as a link tag.
 */
export const LinkStyledAsAButton: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args" variant="primary">External Link button</MButton>

      <MButton v-bind="args">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75 0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Link button with an icon
      </MButton>

      <MButton v-bind="args" variant="success">
        Success link button
      </MButton>

      <MButton v-bind="args" variant="warning">
        Warning link button
      </MButton>

      <MButton v-bind="args" variant="danger">
        Danger link button
      </MButton>
    </MButtonGroupLayout>

    `,
  }),
  args: {
    to: 'https://docs.metaplay.io/',
  },
}

/**
 * The `MButton` includes a default slot where you can add both text and image content such as an icon to the button label.
 * Icons provide good visual cues and can enhance overall usability of your button.
 */
export const ButtonsWithAnIcon: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton variant="primary">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75 0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Icon Button
      </MButton>

      <MButton v-bind="args" variant="success">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75 0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Icon Button
      </MButton>

      <MButton v-bind="args" variant="danger">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75
            0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Icon Button
      </MButton>

      <MButton v-bind="args" variant="warning">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75
            0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Icon Button
      </MButton>

      <MButton v-bind="args" variant="neutral">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75
            0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Icon Button
      </MButton>
    </MButtonGroupLayout>`,
  }),
}

/**
 * The `MButton` provides five main contextual variants that can be used to visually convey the severity of an action.
 * By default the `MButton` renders in the `primary` variant but you can easily customize this by assigning a different
 * variant to the `variant` prop. For example use the `danger` variant for buttons that enable destructive actions.
 */
export const ButtonVariants: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args" variant="primary"> Primary button </MButton>
      <MButton v-bind="args" variant="success"> Success button </MButton>
      <MButton v-bind="args" variant="danger"> Danger button </MButton>
      <MButton v-bind="args" variant="warning"> Warning button </MButton>
      <MButton v-bind="args" variant="neutral"> Neutral button </MButton>
    </MButtonGroupLayout>
    `,
  }),
}

/**
 * Set the `size` prop to change the padding inside of the button. The smallest button is the `smallIconOnly`.
 */
export const ButtonSizes: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton size="small"> Small Button </MButton>
      <MButton> Default Button</MButton>
    </MButtonGroupLayout>
    `,
  }),
}

/**
 * To disable the `MButton` component, provide an explanation via the `disabledTooltip` prop that details why the button is disabled.
 * When the button is disabled, it will visually appear grayed out to indicate its inactive state and will not be clickable.
 * Additionally, a tooltip containing the provided message will be displayed when the user hovers over the button.
 * This also improves accessibility and enhances the user experience for everyone, including those using screen readers
 */
export const DisabledButtonWithATooltip: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args" variant="primary"> Disabled button </MButton>
      <MButton v-bind="args" variant="success"> Disabled button </MButton>
      <MButton v-bind="args" variant="danger"> Disabled button </MButton>
      <MButton v-bind="args" variant="warning"> Disabled button </MButton>
      <MButton v-bind="args" variant="neutral"> Disabled button </MButton>
    </MButtonGroupLayout>
    `,
  }),
  args: {
    disabledTooltip: 'This is why it is disabled.',
  },
}

/**
 * When creating features and/or actions it is important to consider the necessary permissions for access.
 * The `MButton` component includes a built-in `HasPermission` prop that when set, ensures the features and/or
 * actions are only available to users with the required permission.
 */
export const HasPermission: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])
      return {
        args,
      }
    },
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args">
        This Should Work
      </MButton>
      <MButton v-bind="args" variant="success">
        This Should Work
      </MButton>
      <MButton v-bind="args" variant="danger">
        This Should Work
      </MButton>
      <MButton v-bind="args" variant="warning">
        This Should Work
      </MButton>
      <MButton v-bind="args" variant="neutral">
        This Should Work
      </MButton>
    </MButtonGroupLayout>
    `,
  }),
  args: {
    permission: 'example-permission',
  },
}

/**
 * If a user lacks the necessary permission, the `MButton` component is automatically disabled and a tooltip,
 * explaining which permission is missing, is displayed when a user hovers their mouse cursor on the `MButton`.
 */
export const NoPermission: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])

      return {
        args,
      }
    },
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args">
        This Should Not Work
      </MButton>
      <MButton v-bind="args" variant="success">
        This Should Not Work
      </MButton>
      <MButton v-bind="args" variant="danger">
        This Should Not Work
      </MButton>
      <MButton v-bind="args" variant="warning">
        This Should Not Work
      </MButton>
      <MButton v-bind="args" variant="neutral">
        This Should Not Work
      </MButton>
    </MButtonGroupLayout>
    `,
  }),
  args: {
    permission: 'example-permission2',
  },
}

/**
 * The `MButton` component includes a `safetyLock` feature. When set, it prevents accidental button triggers.
 * This feature adds an extra layer of security, requiring users to 'unlock' an action before it can be triggered.
 * By default, the `safetyLock` feature is disabled in local development environments, but we recommend enabling it in production.
 */
export const SafetyLock: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args"> Button With Safety Lock </MButton>
      <MButton v-bind="args" size="small"> Small Button With Safety Lock </MButton>
    </MButtonGroupLayout>
    `,
  }),
  args: {
    safetyLock: true,
  },
}

/**
 * The `safetyLock` feature can also be paired with the contextual variants. This can be used to visually convey
 * the severity of an action, while also providing an extra layer of security.
 */
export const SafetyLockVariants: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args" variant="primary">Primary</MButton>
      <MButton v-bind="args" variant="success">Success</MButton>
      <MButton v-bind="args" variant="warning">Warning</MButton>
      <MButton v-bind="args" variant="danger">Danger</MButton>
      <MButton v-bind="args" variant="neutral">Neutral</MButton>
    </MButtonGroupLayout>
    `,
  }),
  args: {
    safetyLock: true,
  },
}

/**
 * Additionally, the `safetyLock` feature does not interfere with other button states. For example, when the
 * button is disabled, the `safetyLock` remains active and clickable, but the button will be visually grayed out,
 * and toggling the safety lock will have no effect on the button's state.
 */
export const DisabledSafetyLock: Story = {
  render: (args) => ({
    components: { MButton, MButtonGroupLayout },
    setup: () => ({ args }),
    template: `
    <MButtonGroupLayout>
      <MButton v-bind="args"> Disabled button with safety lock </MButton>
    </MButtonGroupLayout>
    `,
  }),
  args: {
    safetyLock: true,
    disabledTooltip: 'This is why it is disabled.',
  },
}
