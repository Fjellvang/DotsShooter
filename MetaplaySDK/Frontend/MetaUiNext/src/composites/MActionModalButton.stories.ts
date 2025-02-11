import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MActionModal from './MActionModal.vue'
import MActionModalButton from './MActionModalButton.vue'

const meta: Meta<typeof MActionModalButton> = {
  component: MActionModalButton,
  tags: ['autodocs'],
  argTypes: {
    action: {
      control: { disable: true },
    },
    triggerButtonSize: {
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
    okButtonDisabled: {
      control: { type: 'boolean' },
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'The `MActionModalButton` is a wrapper component that integrates the functionality of `MButton` and `MActionModal` into a single component. This seamless combination allows you to control the appearance and behavior of both the action button and the modal dialog from a single interface.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MActionModalButton>

/**
 * To create an `MActionModalButton`, you need to provide three key properties: `triggerButtonLabel`, which sets the button’s label, `modalTitle`, which specifies the modal’s title, and an `action` function that is executed when the modal is confirmed. All other props are optional and can be used to customize the component’s appearance and behavior to suit your specific needs.
 *
 * Note: For automated testing, you should use use the `dataTestid` prop. This prop automatically generates unique `data-testid` attributes for both the button and modal components. By targeting these attributes in your testing framework, you can write precise and reliable tests to verify the component's functionality and behavior under various conditions. To see this in action, inspect the button and modal elements in the browser's developer tools to view the generated `data-testid` attributes.
 */
export const Default: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      const debugStatus = ref('')
      const buttonTestid = args.dataTestid ? `${args.dataTestid}-button` : ''
      const modalTestid = args.dataTestid ? `${args.dataTestid}-modal` : ''

      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { debugStatus, action, args, buttonTestid, modalTestid }
    },
    template: `
    <MActionModalButton v-bind="args" :action="action">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      <template #result-panel>
        <p>Inspect the button and modal elements in the browser's developer tools to view the generated 'data-testids'.</p>
        <br/>
        <p><strong>Button data-testid:</strong> <span>{{ buttonTestid }}</span></p>
        <p><strong>Modal data-testid:</strong> <span>{{ modalTestid }}</span></p>
      </template>
    </MActionModalButton>
    <p>{{ debugStatus }}</p>
    `,
  }),
  args: {
    triggerButtonLabel: 'Action button text',
    modalTitle: 'Fairly Normal Length Title',
    dataTestid: 'default-story',
  },
}

/**
 * Add custom labels to both the trigger and OK buttons by setting the `triggerButtonLabel` and `okButtonLabel` props. This feature allows you to tailor the button labels to better reflect the action being performed and the confirmation process.
 */
export const ActionAndOKButtonLabels: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      return { args }
    },
    template: `
    <MActionModalButton v-bind="args" ref="modalRef">
      <p>{{ debugStatus }}</p>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    `,
  }),
  args: {
    triggerButtonLabel: 'New action button label',
    okButtonLabel: 'New ok button label',
    modalTitle: 'Modified button labels example modal',
    action: async () => {
      // Do nothing.
    },
  },
}

/**
 * The button labels are meant to be short, concise, and straight to the point. Avoid using long text, phrases, or sentences as button labels, as this can cause the button to overflow and look visually unappealing, especially on smaller screens.
 */
export const ButtonLabelOverflows: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      return { args }
    },
    template: `
    <MActionModalButton v-bind="args" ref="modalRef">
      <p>{{ debugStatus }}</p>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    `,
  }),
  args: {
    triggerButtonLabel: 'This is a really long label for the Action button text',
    okButtonLabel: 'This is a really long label for the Ok button text',
    modalTitle: 'Modal Button label overflows',
    action: async () => {
      // Do nothing.
    },
  },
}

/**
 * By default, all content is organized into a single-column layout, stacking elements vertically for a clean and straightforward presentation. This default setup simplifies content management and ensures that the modal is easy to read and understand.
 */
export const SingleColumnModalLayout: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { debugStatus, action, args }
    },
    template: `
    <MActionModalButton v-bind="args" :action="action">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    <p>{{ debugStatus }}</p>
    `,
  }),
  args: {
    triggerButtonLabel: 'Action button text',
    modalTitle: 'Fairly Normal Length Title',
  },
}

/**
 * You can override the default layout by using the named slots provided by the component. Use the `right-panel` slot to organize your content in a two-column layout, with the primary content on the left and additional information on the right. This layout is ideal for displaying long and detailed content, as it prevents the modal from becoming too long and/or cluttered.
 */
export const TwoColumnModalLayout: Story = {
  render: (args) => ({
    components: { MActionModalButton, MActionModal },
    setup: () => {
      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { debugStatus, action, args }
    },
    template: `
    <MActionModalButton v-bind="args" :action="action">
      <template #right-panel>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </template>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    <p>{{ debugStatus }}</p>
    `,
  }),
  args: {
    triggerButtonLabel: 'Two column modal',
    modalTitle: 'Two column example modal',
  },
}

/**
 * Use the `bottom-panel` slot to add a disclaimer or additional information that supports the primary and secondary content in the two-column layout. This slot places content below the primary and secondary columns, making it ideal for related but non-essential information.
 */
export const ThreeColumnModalLayout: Story = {
  render: (args) => ({
    components: { MActionModalButton, MActionModal },
    setup: () => {
      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { debugStatus, action, args }
    },
    template: `
    <MActionModalButton v-bind="args" :action="action">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      <template #right-panel>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </template>
      <template #bottom-panel>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </template>
    </MActionModalButton>
    <p>{{ debugStatus }}</p>
    `,
  }),
  args: {
    triggerButtonLabel: 'Three column modal',
    modalTitle: 'Three column example modal',
  },
}

/**
 * Control user access to the action button by setting the `permission` prop to the required permission. If the user lacks the necessary permission, the button will be disabled, and a tooltip will appear on hover. This feature prevents unauthorized users from performing restricted actions.
 */
export const Permissions: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      const debugStatus = ref('')
      usePermissions().setPermissions(['example-permission'])
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { debugStatus, action, args }
    },
    template: `
    <MActionModalButton v-bind="args" :action="action" ref="modalRef">
      <p>{{ debugStatus }}</p>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    `,
  }),
  args: {
    triggerButtonLabel: 'Permissions enabled modal',
    triggerButtonDisabledTooltip: 'Sorry you do not have the required permissions to perform this action.',
    modalTitle: 'Permissions enabled example modal',
    permission: 'example-permission',
  },
}

/**
 * To disable the trigger button, use the `triggerButtonDisabledTooltip` prop to provide a custom message explaining
 * why the button is disabled. This will prevent users from accessing the modal while offering a clear explanation of
 * why they can't perform the action.
 *
 * When disabled, the trigger button will appear grayed out, and the custom message will be shown when the user hovers over it.
 *
 * This also improves accessibility and enhances the user experience for everyone, including those using screen readers.
 */
export const DisabledTriggerButton: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      return { args }
    },
    template: `
    <MActionModalButton v-bind="args" ref="modalRef">
      <p>{{ debugStatus }}</p>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    `,
  }),
  args: {
    triggerButtonLabel: 'Disabled action button modal',
    triggerButtonDisabledTooltip: 'You cannot perform this action at this time.',
    modalTitle: 'Action button disabled example modal',
    action: async () => {
      // Do nothing.
    },
  },
}

/**
 * Disable the `OK` button by setting the `okButtonDisabledTooltip` prop to `string` that explains the reason why the button is disabled. This will
 * prevent users from confirming the action while offering a clear explanation of why they can't perform the action.
 */
export const DisabledOkButtonWithTooltip: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      return { args }
    },
    template: `
    <MActionModalButton v-bind="args" ref="modalRef">
      <p>{{ debugStatus }}</p>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    `,
  }),
  args: {
    triggerButtonLabel: 'Disabled ok button modal',
    okButtonDisabledTooltip: 'You cannot perform this action.',
    modalTitle: 'Ok button disabled example modal',
    action: async () => {
      // Do nothing.
    },
  },
}

/**
 * In the event an action fails, the modal will display an error message to the user. Ideally, the message should inform users of any issues that may have occurred during the action process and provide guidance on how to proceed.
 */
export const ActionError: Story = {
  render: (args) => ({
    components: { MActionModalButton },
    setup: () => {
      const modalRef = ref<typeof MActionModalButton>()
      const debugStatus = ref('')
      async function action() {
        throw new Error('Something bad happened!')
      }
      return { modalRef, debugStatus, action, args }
    },
    template: `
    <MActionModalButton v-bind="args" :action="action" ref="modalRef">
      <p>{{ debugStatus }}</p>
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MActionModalButton>
    `,
  }),
  args: {
    triggerButtonLabel: 'Action error modal',
    modalTitle: 'Action error example modal',
  },
}
