import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MActionModal from '../composites/MActionModal.vue'
import MTextButton from './MTextButton.vue'

const meta = {
  component: MTextButton,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'MTextButton behaves similar to the `MButton` and `MIconButton` and it can be used as a HTML `<button>` or `<a>` element for example to open a dialog, start, trigger an action, submit a form or to navigate to a new page or view.',
      },
    },
  },
  render: (args) => ({
    components: { MTextButton },
    setup: () => ({ args }),
    template: `
    <MTextButton v-bind="args">
      I am a button
    </MTextButton>
    `,
  }),
} satisfies Meta<typeof MTextButton>

export default meta
type Story = StoryObj<typeof MTextButton>

/**
 * The `MTextButton` component is an interactive button element that can be subtly integrated as part of a sentence or a text.
 *
 * Pro Tip: The wording should always clearly indicate what will happen when the button is clicked. Use clear and concise text
 * to make it easy for users to identify and understand the action/feature the button triggers.
 */
export const Default: Story = {
  args: {},
}

/**
 * Set the `to` prop to use the `MTextButton` as a link to navigate to both internal and external pages.
 */
export const LinkButton: Story = {
  render: (args) => ({
    components: { MTextButton },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-flex-col tw-gap-10">
      <div class="tw-flex tw-gap-2">
        <MTextButton v-bind="args" variant="primary" to="https://docs.metaplay.io/"> External link </MTextButton>
      </div>
    </div>
    `,
  }),
}

/**
 * Add an icon to the #icon slot to create a text/link button with an icon.
 */
export const TextButtonWithIcon: Story = {
  render: (args) => ({
    components: { MTextButton },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-gap-2">
      <MTextButton v-bind="args" variant="primary">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75
            0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Text button with icon
      </MTextButton>
    </div>
    `,
  }),
}

/**
 * The `MTextButton` component can be used to trigger a `MActionModal`. This allows you to subtly incorporate a modal
 * trigger without distracting the user from the main content.
 */
export const TextButtonWithModal: Story = {
  render: (args) => ({
    components: { MActionModal, MTextButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef, debugStatus, action, args }
    },
    template: `
    <MTextButton v-if="modalRef" @click="modalRef.open()">Show Modal</MTextButton>
    <p>{{ debugStatus }}</p>
    <MActionModal v-bind="args" :action="action" ref="modalRef" title="Text button modal">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModal>
    `,
  }),
}

/**
 * Contextual variants are available to visually distinguish text/link button from surrounding text making it
 * easier for users to identify and interact with them. Consistent use of variants can also convey the nature
 * and contextual importance of the action/feature it triggers.
 */
export const TextButtonVariants: Story = {
  render: (args) => ({
    components: { MTextButton },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-gap-2">
      <MTextButton v-bind="args" variant="primary"> I am a text button </MTextButton>
      <MTextButton v-bind="args" variant="success"> I am a text button </MTextButton>
      <MTextButton v-bind="args" variant="danger"> I am a text button </MTextButton>
      <MTextButton v-bind="args" variant="warning"> I am a text button </MTextButton>
      <MTextButton v-bind="args" variant="neutral"> I am a text button </MTextButton>
    </div>
    `,
  }),
}

/**
 * To disable the `MTextButton`, set the `disabledTooltip` prop to a custom message that explains why the button is disabled.
 * This will gray out the button and display the custom message when the user hovers their mouse cursor over it. In addition
 * contextual variants also appear grayed out when disabled.
 *
 * Pro Tip: Always provide a clear and concise explanation when disabling a button to help users understand why they can't interact
 * with it.
 */
export const DisabledWithATooltip: Story = {
  render: (args) => ({
    components: { MTextButton },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-gap-2">
      <MTextButton v-bind="args" variant="primary"> Disabled button </MTextButton>
      <MTextButton v-bind="args" variant="success"> Disabled button </MTextButton>
      <MTextButton v-bind="args" variant="danger"> Disabled button </MTextButton>
      <MTextButton v-bind="args" variant="warning"> Disabled button </MTextButton>
      <MTextButton v-bind="args" variant="neutral"> Disabled button </MTextButton>
    </div>
    `,
  }),
  args: {
    disabledTooltip: 'This is why it is disabled.',
  },
}

/**
 * When creating features and/or actions it is important to consider the necessary permissions for access.
 * The `MTextButton` component includes a built-in `hasPermission` prop that when set, ensures the features and/or
 * actions are only available to users with the required permission.
 */
export const HasPermission: Story = {
  render: (args) => ({
    components: { MTextButton },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])
      return {
        args,
      }
    },
    template: `
    <div class="tw-flex tw-gap-2">
      <MTextButton v-bind="args">
        This Should Work
      </MTextButton>
      <MTextButton v-bind="args" variant="success">
        This Should Work
      </MTextButton>
      <MTextButton v-bind="args" variant="danger">
        This Should Work
      </MTextButton>
      <MTextButton v-bind="args" variant="warning">
        This Should Work
      </MTextButton>
      <MTextButton v-bind="args" variant="neutral">
        This Should Work
      </MTextButton>
    </div>
    `,
  }),
  args: {
    permission: 'example-permission',
  },
}

/**
 * If a user lacks the necessary permission, the `MTextButton` component is automatically disabled and a tooltip,
 * explaining which permission is missing, is displayed when a user hovers their mouse cursor on the `MTextButton`.
 */
export const NoPermission: Story = {
  render: (args) => ({
    components: { MTextButton },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])

      return {
        args,
      }
    },
    template: `
    <div class="tw-flex tw-gap-2">
      <MTextButton v-bind="args">
        This Should Not Work
      </MTextButton>
      <MTextButton v-bind="args" variant="success">
        This Should Not Work
      </MTextButton>
      <MTextButton v-bind="args" variant="danger">
        This Should Not Work
      </MTextButton>
      <MTextButton v-bind="args" variant="warning">
        This Should Not Work
      </MTextButton>
      <MTextButton v-bind="args" variant="neutral">
        This Should Not Work
      </MTextButton>
    </div>
    `,
  }),
  args: {
    permission: 'example-permission2',
  },
}
