import type { Meta, StoryObj } from '@storybook/vue3'

import MList from '../primitives/MList.vue'
import MListItem from '../primitives/MListItem.vue'
import MPopover from './MPopover.vue'

const meta: Meta<typeof MPopover> = {
  component: MPopover,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'primary', 'warning', 'danger'],
    },
    size: {
      control: {
        type: 'inline-radio',
      },
      options: ['default', 'small'],
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'The `MPopover` is a wrapper component for displaying content on top of other elements on the dashboard. This component is great for displaying dropdowns and any other content that you want to show on demand. Note: The `MPopover` component is not a good option for creating a "modal" or a "tooltip" for those particular use-cases checkout the `MActionModalButton` or `MTooltip` components.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MPopover>

/**
 * The `MPopover` component has two main slots. The `trigger` slot that controls the visibility of the content and the `default` slot that contains the popover content.
 */
export const Default: Story = {
  render: (args) => ({
    components: {
      MPopover,
    },
    setup: () => ({ args }),
    template: `
    <Teleport to="body">
      <div id="root-popovers"></div>
    </Teleport>
    <MPopover v-bind="args"></MPopover>`,
  }),
  args: {
    triggerLabel: 'Default popover',
    title: 'Link selection popover',
    subtitle:
      'Add stuff to the default slot to see it here. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.',
  },
}

/**
 * When working with small spaces, reduce the size of the trigger button by setting the `triggerButtonSize` prop to 'small'.
 */
export const PopoverTriggerSize: Story = {
  render: (args) => ({
    components: {
      MPopover,
    },
    setup: () => ({ args }),
    template: `
    <div style="display: flex; gap: 1em;">
      <div>
        <Teleport to="body">
          <div id="root-popovers"></div>
        </Teleport>
        <MPopover v-bind="args" triggerLabel="Default"></MPopover>
      </div>
      <div>
        <Teleport to="body">
          <div id="root-popovers"></div>
        </Teleport>
        <MPopover v-bind="args" triggerLabel="Small" size="small"></MPopover>
      </div>
    </div>`,
  }),
  args: {
    title: 'Simple Popover',
    subtitle: 'Alter the size of the trigger button by setting the `triggerButtonSize` prop to "small".',
  },
}

/**
 * Ideally the trigger slot should contain short and concise text. Aim for a single word or a
 * short phrase as longer text is not visually appealing.
 */
export const PopoverTriggerButtonOverflow: Story = {
  render: (args) => ({
    components: {
      MPopover,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <Teleport to="body">
      <div id="root-popovers"></div>
    </Teleport>
    <div style="display: flex; gap: 1em;">
      <MPopover v-bind="args" title="Good example" triggerLabel="Menu">
        This is a good example of a short and concise trigger button content.
      </MPopover>

      <MPopover v-bind="args" title="Bad Example" triggerLabel="This is a bad example of a trigger label and it does not look good">
        This is a bad example of trigger button content. Aim for a word or short phrase with a maximum of 15 characters.
      </MPopover>

      <MPopover v-bind="args" title="Bad Example" triggerLabel="ThisIsABadExampleOfATriggerLabelWithNoWhiteSpaces">
        This is a bad example of trigger button content. Aim for a word or short phrase with a maximum of 15 characters.
      </MPopover>
    </div>`,
  }),
  args: {
    title: 'Popover Trigger Button Overflow',
  },
}

/**
 * The `MPopover` component body content can be any valid HTML element such as a `div` or a `list`,
 * and is automatically placed in the best-fitting position based on available space.
 * Alternatively, you can pass in string content to the `subtitle` prop and it will be shown in the of the popover body.
 */
export const PopoverBodyContent: Story = {
  render: (args) => ({
    components: {
      MPopover,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <Teleport to="body">
      <div id="root-popovers"></div>
    </Teleport>
    <div style="display: flex; gap: 1em;">
      <MPopover v-bind="args" title="Simple popover" triggerLabel="Simple Popover">
        Simple popover content.
      </MPopover>

      <MPopover v-bind="args" noBodyPadding title="Menu List" triggerLabel="Menu List">
        <MList>
          <MListItem clickable> Menu Option 1</MListItem>
          <MListItem clickable> Menu Option 2</MListItem>
          <MListItem clickable> Menu Option 3</MListItem>
          <MListItem clickable> Menu Option 4</MListItem>
          <MListItem clickable> Menu Option 5</MListItem>
        </MList>
      </MPopover>
    </div>`,
  }),
  args: {
    title: 'Simple Popover',
  },
}

/**
 * The popover is designed to neatly wrap content to the next line; however displaying long texts with no
 * whitespaces can be challenging. In such cases, instead of wrapping, the popover will be scrollable along the x-axis.
 */
export const PopoverBodyContentOverflow: Story = {
  render: (args) => ({
    components: {
      MPopover,
    },
    setup: () => ({ args }),
    template: `
    <Teleport to="body">
      <div id="root-popovers"></div>
    </Teleport>
    <div style="display: flex; gap: 1em;">
      <MPopover v-bind="args" title="Long content" triggerLabel="Long Content Popover">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.
      </MPopover>

      <MPopover v-bind="args" title="Scrollable along the x-axis" triggerLabel="Long content (No Whitespace)">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Longcontentwithnowhitespaceisalwaysachallenge.Trynottousesuchcontentintoomanyplaces.
      </MPopover>
    </div>`,
  }),
  args: {
    title: 'Long content',
  },
}

/**
 * When using a popover to display a list content, it is recommended to set the `noBodyPadding` prop to true.
 * This will remove the default padding from the popover body and will allow the list items to fill the entire width of the popover.
 * Additionally any content that overflows the height of the popover will be scrollable along the y-axis.
 */
export const PopoverListContentOverflow: Story = {
  render: (args) => ({
    components: {
      MPopover,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <Teleport to="body">
      <div id="root-popovers"></div>
    </Teleport>
    <MPopover v-bind="args" noBodyPadding>
      <MList>
        <MListItem clickable> Menu Option 1</MListItem>
        <MListItem clickable> Menu Option 2</MListItem>
        <MListItem clickable> Menu Option 3</MListItem>
        <MListItem clickable> Menu Option 4</MListItem>
        <MListItem clickable> Menu Option 5</MListItem>
        <MListItem clickable> Menu Option 6</MListItem>
        <MListItem clickable> Menu Option 7</MListItem>
        <MListItem clickable> Menu Option 8</MListItem>
        <MListItem clickable> Menu Option 9</MListItem>
        <MListItem clickable> Menu Option 10</MListItem>
        <MListItem clickable> Menu Option 11</MListItem>
        <MListItem clickable> Menu Option 12</MListItem>
        <MListItem clickable> Menu Option 13</MListItem>
        <MListItem clickable> Menu Option 14</MListItem>
        <MListItem clickable> Menu Option 15</MListItem>
        <MListItem clickable> Menu Option 16</MListItem>
        <MListItem clickable> Menu Option 17</MListItem>
        <MListItem clickable> Menu Option 18</MListItem>
        <MListItem clickable> Menu Option 19</MListItem>
        <MListItem clickable> Menu Option 20</MListItem>
        <MListItem clickable> Menu Option 21</MListItem>
        <MListItem clickable> Menu Option 22</MListItem>
        <MListItem clickable> Menu Option 23</MListItem>
        <MListItem clickable> Menu Option 24</MListItem>
        <MListItem clickable> Menu Option 25</MListItem>
      </MList>
    </MPopover>`,
  }),
  args: {
    triggerLabel: 'Scrollable list',
    title: 'Scrollable along the y-axis',
  },
}

/**
 * Use the `variant` prop to apply a contextual color to the popover. For example,
 * you can use the 'warning' variant to indicate that the content is a warning.
 */
export const PopoverVariants: Story = {
  render: (args) => ({
    components: {
      MPopover,
    },
    setup: () => ({ args }),
    template: `
    <Teleport to="body">
      <div id="root-popovers"></div>
    </Teleport>
    <div style="display: flex; gap: 1em;">
      <MPopover v-bind="args" triggerLabel="Default">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.
      </MPopover>

      <MPopover v-bind="args" variant="success" triggerLabel="Success">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.
      </MPopover>

      <MPopover v-bind="args" variant="warning" triggerLabel="Warning">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.
      </MPopover>

      <MPopover v-bind="args" variant="danger" triggerLabel="Danger">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.
      </MPopover>

      <MPopover v-bind="args" variant="neutral" triggerLabel="Neutral">
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam euismod, nisl eget aliquam ultricies, nunc nisl aliquet nunc, quis aliquam nisl nunc quis nisl.
      </MPopover>
    </div>`,
  }),
  args: {
    title: 'Popover Variants',
  },
}
