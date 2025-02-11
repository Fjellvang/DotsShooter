import type { Meta, StoryObj } from '@storybook/vue3'

import MBadge from './MBadge.vue'
import MButton from './MButton.vue'
import MCallout from './MCallout.vue'

const meta: Meta<typeof MCallout> = {
  component: MCallout,
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
        component: 'A callout component that displays a message with an optional title and buttons.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MCallout>

/**
 * Demonstrates the warning variant of the callout. This is the default variant as most callouts should be actionable and important instead of informational.
 */
export const Warning: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Lorem ipsum dolor sit amet.
    </MCallout>
    `,
  }),
  args: {
    title: 'This is a warning!',
  },
}

/**
 * Shows the danger variant of the callout with a cautionary message.
 */
export const Danger: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Lorem ipsum dolor sit amet.
    </MCallout>
    `,
  }),
  args: {
    title: 'Maybe you should not do this...',
    variant: 'danger',
  },
}

/**
 * Illustrates the success variant of the callout with a positive message.
 */
export const Success: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Lorem ipsum dolor sit amet.
    </MCallout>
    `,
  }),
  args: {
    title: 'Looking good, boss!',
    variant: 'success',
  },
}

/**
 * Demonstrates the primary variant of the callout. This is a bit misleading as we show a neutral tone instead of a blue highlight to make the callouts less visually distracting.
 */
export const Primary: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Lorem ipsum dolor sit amet.
    </MCallout>
    `,
  }),
  args: {
    title: 'Looking good, boss!',
    variant: 'primary',
  },
}

/**
 * Shows the callout with a badge in the title.
 */
export const TitleWithABadge: Story = {
  render: (args) => ({
    components: { MCallout, MBadge },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Buttons should be on the right.
      <template #badge>
        <MBadge>Badge</MBadge>
      </template>
    </MCallout>
    `,
  }),
  args: {
    title: 'Title with a badge',
    variant: 'primary',
  },
}

/**
 * Demonstrates the callout with a single button, for when there are actions to be taken.
 */
export const OneButton: Story = {
  render: (args) => ({
    components: { MCallout, MButton },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Buttons should be on the right.
      <template #buttons>
        <MButton>Open logs</MButton>
      </template>
    </MCallout>
    `,
  }),
  args: {
    title: 'Interactive Callout',
    variant: 'primary',
  },
}

/**
 * Shows the callout with multiple buttons, testing the layout with a high number of interactive elements.
 */
export const ManyButtons: Story = {
  render: (args) => ({
    components: { MCallout, MButton },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      Callout should deal with lots of buttons in a reasonable way.
      <template #buttons>
        <MButton>OK</MButton>
        <MButton>Cancel</MButton>
        <MButton>Help</MButton>
        <MButton>More</MButton>
        <MButton>Less</MButton>
        <MButton>Close</MButton>
        <MButton>Super long text in a button</MButton>
        <MButton>Superunlikelytextinabutton</MButton>
      </template>
    </MCallout>
    `,
  }),
  args: {
    title: 'Interactive Callout',
    variant: 'primary',
  },
}

/**
 * Illustrates the callout with a very long title, demonstrating overflow handling.
 */
export const TitleOverflow: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" title="Long Title That Will Cause Overflow In The Header Area And Will Not Be Truncated With Ellipsis" style="width: 600px">
      Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.
    </MCallout>
    `,
  }),
  args: {},
}

// TODO: Reevaluate need for props when reviewing and implementing MCallout Component.
/* export const TitleAndBodyTruncateUsingProps: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
    </MCallout>
    `,
  }),
  args: {
    title: 'LongTitleThatWillCauseOverflowInTheHeaderAreaAndWillBeTruncatedWithEllipsis',
    body: 'Thisisanexampleofalongerrormessagewithnowhitespaceandwillcauseoverflowintheheaderareaandwillbetruncatedwithellipsis.',
    variant: 'danger',
  },
} */

/**
 * Demonstrates the callout with truncated title and body content using slots, showing ellipsis for overflow.
 */
export const TitleAndBodyTruncateUsingSlots: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" title="LongTitleThatWillCauseOverflowInTheHeaderAreaAndWillBeTruncatedWithEllipsis" style="width: 600px">
      <p>Thisisanexampleofalongerrormessagewithnowhitespaceandwillcauseoverflowintheheaderareaandwillbetruncatedwithellipsis..</p>
      <p>Slots checking currently does not work. Passing it through helper function doesn't do anything since slots take in a span/div.</p>
    </MCallout>
    `,
  }),
  args: {
    variant: 'danger',
  },
}

/**
 * Shows the callout with overflowing body content, testing the scrollability of the body.
 */
export const BodyOverflow: Story = {
  render: (args) => ({
    components: { MCallout },
    setup: () => ({ args }),
    template: `
    <MCallout v-bind="args" style="width: 600px">
      <p class="mb-2">Lorem ipsum dolor sit amet.</p>
      <pre class="text-sm">
export const TwoColumns: Story = {
  render: (args) => ({
    components: { MActionModal },
    setup: () => ({ args }),
    template: 'Very long template string that will cause overflow in the content area and will make the body scrollable.',
  }),
  args: {},
}
      </pre>
    </MCallout>
    `,
  }),
  args: {
    title: 'Maybe you should not do this...',
    variant: 'danger',
  },
}
