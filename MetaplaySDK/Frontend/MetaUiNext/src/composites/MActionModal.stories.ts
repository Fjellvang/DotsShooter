import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MButton from '../primitives/MButton.vue'
import MIconButton from '../primitives/MIconButton.vue'
import MActionModal from './MActionModal.vue'

const meta: Meta<typeof MActionModal> = {
  component: MActionModal,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    },
    action: {
      control: false,
    },
    okButtonDisabled: {
      control: {
        type: 'boolean',
      },
    },
    modalSize: {
      control: {
        type: 'inline-radio',
      },
      options: ['default', 'large'],
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          '`MActionModal` is a modal tied to an actuation button. It interrupts user interaction with the dashboard to prompt for confirmation of actions, such as deleting or editing an item.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MActionModal>

/**
 * The `MActionModal` is an effective way to capture user's attention for actions that require
 * additional confirmation or input from the user before they run. For example deleting an
 * item or submitting a form. To create a `MActionModal`, you need to provide `title`,
 * `action`, and the content to be shown on the modal.
 */
export const SimpleModal: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
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
    <MButton v-if="modalRef" @click="modalRef.open()">Show Modal</MButton>
    <p>{{ debugStatus }}</p>
    <MActionModal v-bind="args" :action="action" ref="modalRef">
      <p>Are you sure you want to do this?</p>
    </MActionModal>
    `,
  }),
  args: {
    title: 'Simple Action Modal',
  },
}

/**
 * The `action` property takes in a function that returns a promise. Once executed the
 * modal will automatically display the appropriate state based on the promise's state ie:
 * loading, success, or error.
 */
export const ActionStateLoading: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()
      const modalRef3 = ref<typeof MActionModal>()

      const debugStatus = ref('')
      async function action() {
        await new Promise(() => {
          // No resolution or rejection here
        })
      }
      async function actionSuccess() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
      }
      async function actionError() {
        throw new Error('Something bad happened!')
      }
      return {
        modalRef,
        modalRef2,
        modalRef3,
        debugStatus,
        action,
        actionSuccess,
        actionError,
        args,
      }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef" @click="modalRef.open()">Loading State Modal</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef" title="This action will never resolve">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()">Success State Modal</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="actionSuccess" ref="modalRef2" title="This action will succeed">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>

      <MButton v-if="modalRef3" @click="modalRef3.open()">Error State Modal</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="actionError" ref="modalRef3" title="This action will fail">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>
    </div>
    `,
  }),
  args: {
    title: 'Title TBD',
  },
}

/**
 * Add content to the `result-panel` slot to display a custom message or display the
 * results of an action after a promise has been resolved. This will override the default
 * behaviour of the MActionModal component preventing it from automatically close.
 */
export const ActionSuccessCustomContent: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
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
    <MButton v-if="modalRef" @click="modalRef.open()">Modal with custom results</MButton>
    <p>{{ debugStatus }}</p>
    <MActionModal v-bind="args" :action="action" ref="modalRef">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      <template #result-panel>
        <p>Custom success state content.</p>
      </template>
    </MActionModal>
    `,
  }),
  args: {
    title: 'This action has a custom success view',
  },
}

/**
 * The `MButton` and/or `MIconButton` components can be used as triggers for the `MActionModal`.
 *
 */
export const CustomTriggerButton: Story = {
  render: (args) => ({
    components: { MActionModal, MButton, MIconButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()
      const modalRef3 = ref<typeof MActionModal>()

      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef, modalRef2, modalRef3, debugStatus, action, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef" @click="modalRef.open()">Default trigger Button</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef">
        <p>Are you sure you want to do this?</p>
      </MActionModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()">
        <template #icon>
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
            <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75
            0 10-1.061 1.06l1.06 1.06z" />
          </svg>
        </template>
        Trigger button with an icon
      </MButton>
        <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef2">
        <p>Are you sure you want to do this?</p>
      </MActionModal>

      <MIconButton v-if="modalRef3" @click="modalRef3.open()">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
          <path d="M10 2a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 2zM10 15a.75.75 0 01.75.75v1.5a.75.75 0 01-1.5 0v-1.5A.75.75 0 0110 15zM10 7a3 3 0 100 6 3 3 0 000-6zM15.657 5.404a.75.75 0 10-1.06-1.06l-1.061 1.06a.75.75 0 001.06 1.06l1.06-1.06zM6.464 14.596a.75.75 0 10-1.06-1.06l-1.06 1.06a.75.75 0 001.06 1.06l1.06-1.06zM18 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 0118 10zM5 10a.75.75 0 01-.75.75h-1.5a.75.75 0 010-1.5h1.5A.75.75 0 015 10zM14.596 15.657a.75.75 0 001.06-1.06l-1.06-1.061a.75.75 0 10-1.06 1.06l1.06 1.06zM5.404 6.464a.75.75 0 001.06-1.06l-1.06-1.06a.75.75
          0 10-1.061 1.06l1.06 1.06z" />
        </svg>
      </MIconButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef3">
        <p>Are you sure you want to do this?</p>
      </MActionModal>
    </div>
    `,
  }),
  args: {
    title: 'This action will succeed',
  },
}

/**
 * There are two sizes of modals: default and large.
 * The default size is good for a single column of text or form inputs, while the large doubles the width to fit multiple columns or other extra-wide content.
 */
export const ModalSizes: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
    setup: () => {
      const modalRef1 = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()
      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef1, modalRef2, debugStatus, action, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef1" @click="modalRef1.open()">Default Modal</MButton>
      <MActionModal v-bind="args" ref="modalRef1">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()"> Large Modal</MButton>
      <MActionModal v-bind="args" ref="modalRef2" modalSize="large">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>
    </div>
    `,
  }),
  args: {
    title: 'Fairly Normal Length Title',
    action: async () => {
      // Do nothing.
    },
  },
}

/**
 * The MActionModal comes with three main slots in the modal body: `default`, `right-panel`, and `bottom-panel`.
 *
 * Use the `right-panel` slot to create a two-column layout where `default` slot in on the left and `right-panel` on the right. This will also set the `modal-size` prop to `large` to accommodate the two columns. The right panel is best used for complex forms or to preview the output of any inputs in the left column.
 * Similarly, use the `bottom-panel` slot to add content below the `default` and `right-panel` slots. It is best used for additional information or instructions that are not part of the main action.
 *
 * Please note that both additional slots assume you are also using the `default` slot, as that is the main content of the modal.
 */
export const ModalContentLayout: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()
      const modalRef3 = ref<typeof MActionModal>()
      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef, modalRef2, modalRef3, debugStatus, action, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef" @click="modalRef.open()">Single Column Modal</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef" title="Single column modal layout">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()">Two Column Modal</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef2" title="Two column modal layout">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <template #right-panel>
          <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        </template>
      </MActionModal>

      <MButton v-if="modalRef3" @click="modalRef3.open()">All Three Slots</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef3" title="Three column modal layout">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <template #right-panel>
          <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        </template>
        <template #bottom-panel>
          <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        </template>
      </MActionModal>
    </div>
    `,
  }),
}

/**
 * The `MActionModal` component is designed to gracefully manage lengthy and detailed content.
 * Both the title and body section content will seamlessly adjust their heights accordingly to
 * accommodate their respective content.
 *
 * For wide content, the body section will overflow automatically enabling horizontal scrolling
 * along the x-axis..
 */
export const ModalContentOverflows: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()

      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef, modalRef2, debugStatus, action, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef" @click="modalRef.open()">Long Content Modal</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef">
        <h3>H3 Header 1</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 2</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 3</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 4</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 5</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 6</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 7</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <h3>H3 Header 8</h3>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()">Wide Content Modal</MButton>
        <p>{{ debugStatus }}</p>
        <MActionModal v-bind="args" :action="action" ref="modalRef2">
          <h3>H3 Header</h3>
          <pre>
            export const TwoColumns: Story = {
              render: (args) => ({
                components: { MActionModal },
                setup: () => ({ args }),
                template: 'Very long template string that will cause overflow in the modal content area and will make the body scrollable.',
              }),
              args: {},
            }
        </pre>
      </MActionModal>
    </div>
    `,
  }),
  args: {
    title: 'Long Title That Will Cause Overflow In The Header Area And Will Not Be Truncated With Ellipsis',
  },
}

/**
 * Disable the `OK` button by setting the `okButtonDisabledTooltip` prop to `string` that explains the reason why the button is disabled. This will
 * prevent users from confirming the action while offering a clear explanation of why they can't perform the action.
 */
export const OkButtonDisabled: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()

      const debugStatus = ref<string>('')
      async function action(): Promise<void> {
        await new Promise<void>((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef, modalRef2, debugStatus, action, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef2" @click="modalRef2.open()">Disable the OK button</MButton>
      <p>{{ debugStatus }}</p>
      <MActionModal v-bind="args" :action="action" ref="modalRef2">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>
    </div>
    `,
  }),
  args: {
    title: 'The OK button is disabled',
    okButtonDisabledTooltip: 'I cant do it Dave',
  },
}

/**
 * The MActionModal's Ok button has a safety lock that prevents accidental triggering of the action.
 * This feature adds an extra layer of security, requiring users to 'unlock' an action before they can trigger it.
 * To disable this feature, set the `disableSafetyLock` prop to `true`.
 */
export const OkButtonSafetyLock: Story = {
  render: (args) => ({
    components: { MActionModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MActionModal>()
      const modalRef2 = ref<typeof MActionModal>()

      const debugStatus = ref('')
      async function action() {
        await new Promise((resolve) => setTimeout(resolve, 2000))
        debugStatus.value = 'done'
      }
      return { modalRef, modalRef2, debugStatus, action, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef" @click="modalRef.open()">Ok Button with Safety Lock</MButton>
      <MActionModal v-bind="args" :action="action" ref="modalRef">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()">Ok Button with Safety Lock Disabled</MButton>
      <MActionModal v-bind="args" :action="action" ref="modalRef2" disableSafetyLock>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MActionModal>
    </div>
    `,
  }),
  args: {
    title: 'The OK button has a lock',
  },
}
