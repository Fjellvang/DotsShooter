import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MButton from '../primitives/MButton.vue'
import MModal from './MModal.vue'

const meta: Meta<typeof MModal> = {
  component: MModal,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
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
          'The MModal component provides a simple read-only modal overlay that can be used to display important information that a user can acknowledge but not modify.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MModal>

/**
 * The `MModal` is ideal for displaying view-only content like statistics, analytics details, or important information.
 * Users can read and review the content, but there are no additional controls or actions are needed.
 */
export const ReadOnlyModal: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MModal>()
      return { modalRef, args }
    },
    template: `
    <MButton v-if="modalRef" @click="modalRef.open()"> Show Modal</MButton>
    <MModal v-bind="args" ref="modalRef">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MModal>
    `,
  }),
  args: {
    title: 'Fairly Normal Length Title',
  },
}

/**
 * Use the `badge` prop to provide additional context or information about the content in the `MModal`.
 * This is useful when you want to highlight or draw attention to a specific detail or aspect of the content.
 */
export const ModalBadge: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MModal>()
      return { modalRef, args }
    },
    template: `
    <MButton v-if="modalRef" @click="modalRef.open()"> Show Modal</MButton>
    <MModal v-bind="args" ref="modalRef">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MModal>
    `,
  }),
  args: {
    title: 'Fairly Normal Length Title',
    badge: 'I am a badge',
  },
}

/**
 * By default, content in the `MModal` is shown in a single column. You can utilize the
 * `right-panel` slot to display content in a two-column layout. This is handy for
 * presenting different content types side by side, like an image and a description.
 * However, for the best user experience, use the single-column layout.
 */
export const ModalContentLayout: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef1 = ref<typeof MModal>()
      const modalRef2 = ref<typeof MModal>()

      return { modalRef1, modalRef2, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef1" @click="modalRef1.open()"> One Column Layout</MButton>
      <MModal v-bind="args" ref="modalRef1">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()"> Two Column Layout</MButton>
      <MModal v-bind="args" ref="modalRef2">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <template #right-panel>
          <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        </template>
      </MModal>
    </div>
    `,
  }),
  args: {
    title: 'Fairly Normal Length Title',
  },
}

/**
 * The default `MModal` size is suitable for most scenarios but you can set the `modalSize`
 * prop to `large` to increase the width of the modal. It is important to note that in small
 * screens the width of the modal is limited to the width of the viewport.
 */
export const ModalSizes: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef1 = ref<typeof MModal>()
      const modalRef2 = ref<typeof MModal>()
      return { modalRef1, modalRef2, args }
    },
    template: `
    <div class="tw-flex tw-gap-x-2">
      <MButton v-if="modalRef1" @click="modalRef1.open()">Default Modal</MButton>
      <MModal v-bind="args" ref="modalRef1">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MModal>

      <MButton v-if="modalRef2" @click="modalRef2.open()"> Large Modal</MButton>
      <MModal v-bind="args" ref="modalRef2" modalSize="large">
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
      </MModal>
    </div>
    `,
  }),
  args: {
    title: 'Fairly Normal Length Title',
  },
}

/**
 * When the `dismissable` prop is set to `true`, the `MModal` can be dismissed by clicking
 * outside the modal or by pressing the `ESC` key. The default value of `dismissable` is `false`
 * to ensure that a user always intentionally closes the modal.
 */
export const DismissableModal: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MModal>()
      return { modalRef, args }
    },
    template: `
    <MButton v-if="modalRef" @click="modalRef.open()"> Show Modal</MButton>
    <MModal v-bind="args" ref="modalRef">
      <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
    </MModal>
    `,
  }),
  args: {
    title: 'Fairly Normal Length Title',
    dismissable: true,
  },
}

/**
 * The `MModal` component is designed to gracefully manage lengthy and detailed content.
 * Both the title and body section content will seamlessly adjust their heights accordingly to
 * accommodate their respective content.
 */
export const VerticalContentOverflow: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MModal>()
      return { modalRef, args }
    },
    template: `
    <MButton v-if="modalRef" @click="modalRef.open()"> Show Modal</MButton>
    <MModal v-bind="args" ref="modalRef">
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
    </MModal>
    `,
  }),
  args: {
    title: 'Long Title That Will Cause Overflow In The Header Area And Will Not Be Truncated With Ellipsis',
  },
}

/**
 * In cases where the content exceeds the width of the modal, the modal body will become scrollable
 * along the horizontal axis. This ensures that users can still access and view the content seamlessly.
 */
export const HorizontalContentOverflow: Story = {
  render: (args) => ({
    components: { MModal, MButton },
    setup: () => {
      const modalRef = ref<typeof MModal>()
      return { modalRef, args }
    },
    template: `
    <MButton v-if="modalRef" @click="modalRef.open()"> Show Modal</MButton>
    <MModal v-bind="args" ref="modalRef">
      <h3>H3 Header</h3>
      <pre>
        export const TwoColumns: Story = {
          render: (args) => ({
            components: { MModal },
            setup: () => ({ args }),
            template: 'Very long template string that will cause overflow in the modal content area and will make the body scrollable.',
          }),
          args: {},
        }
      </pre>
    </MModal>
    `,
  }),
  args: {
    title: 'LongTitleThatWillCauseOverflowInTheHeaderAreaAndWillBeTruncatedWithEllipsis',
  },
}
