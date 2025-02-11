import type { Meta, StoryObj } from '@storybook/vue3'

import MButton from '../primitives/MButton.vue'
import MCard from '../primitives/MCard.vue'
import { DisplayError } from '../utils/DisplayErrorHandler'

const meta: Meta<typeof MCard> = {
  component: MCard,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    },
    badgeVariant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    },
  },
  parameters: {
    viewport: {
      defaultViewport: 'singleColumn',
    },
    docs: {
      description: {
        component:
          'Cards are the primary way to display information inside pages in the Meta UI design system. They generally look best in two column layouts but can fit dynamic aspect ratios.',
      },
    },
  },
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MCard>

/**
 * Demonstrates the default appearance of the card with a short title.
 */
export const Default: Story = {
  args: {
    title: 'Short Title',
  },
}

/**
 * Shows the card with a subtitle that gives more explanations of what the card is and how it should be used.
 */
export const Subtitle: Story = {
  args: {
    title: 'Subtitle Card',
    subtitle: 'In some cases a subtitle is needed to provide more context to the card.',
  },
}

/**
 * Illustrates the card with an empty pill badge, typically used to indicate how many items are in a list.
 */
export const EmptyPill: Story = {
  args: {
    title: 'List of Things',
    badge: '0',
  },
}

/**
 * Demonstrates the card with a pill badge showing more complex content. Usually used together with the primary `badgeVariant`.
 */
export const Pill: Story = {
  args: {
    title: 'List of Things',
    badge: '10/35',
    badgeVariant: 'primary',
  },
}

/**
 * Demonstrates the card with a button on the right side of the header for interactive actions.
 */
export const HeaderButtonRightContent: Story = {
  render: (args) => ({
    components: { MCard, MButton },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      <template #header-right>
        <MButton variant="primary" size="small">Look at me!</MButton>
      </template>

      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
  args: {
    title: 'Card With Header Right Content',
  },
}

/**
 * Illustrates the card with a clickable header, emitting an event on click.
 */
export const ClickableHeader: Story = {
  args: {
    title: 'Card With a Clickable Header',
    subtitle: 'Click the header to invoke a "headerClick" event.',
    clickableHeader: true,
  },
}

/**
 * Shows the card in a loading state, useful for indicating data is being fetched.
 */
export const LoadingState: Story = {
  args: {
    title: 'Loading Card',
    isLoading: true,
  },
}

/**
 * Demonstrates text wrapping in all of the primary content slots with long content.
 */
export const TextWrap: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      <template #subtitle>
        Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.
      </template>

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
    </MCard>
    `,
  }),
  args: {
    title: 'Card Headers Should Deal With Very Long Text by Wrapping to The Next Line',
    badge: '1337',
  },
}

/**
 * Illustrates the card handling overflow with ellipsis in the title and scrollable body content.
 */
export const Overflow: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
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
    </MCard>
    `,
  }),
  args: {
    title: 'WhatHappensWhenTheTitleIsAllOneWordAndStillLong.YouWouldProbablyWantThisToBeTruncatedWithEllipsis.',
    badge: '1337',
  },
}

/**
 * Shows the card in an error state, displaying a custom error message and details.
 */
export const Error: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
  args: {
    title: 'Custom Error Card',
    error: new DisplayError(
      'Custom error title',
      'Oh no, something went wrong while loading data for this card!',
      500,
      undefined,
      [
        {
          title: 'Example stack trace',
          content: 'Some long stack trace here',
        },
      ]
    ),
  },
}

/**
 * Demonstrates the warning variant of the card, highlighting cautionary information.
 */
export const Warning: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
  args: {
    title: 'Warning Card (TODO)',
    variant: 'warning',
  },
}

/**
 * Shows the danger variant of the card, emphasizing critical or error-prone information.
 */
export const Danger: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
  args: {
    title: 'Dangerous Card (TODO)',
    variant: 'danger',
  },
}

/**
 * Demonstrates the neutral variant of the card, suitable for muted background information.
 */
export const Neutral: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
  args: {
    title: 'Neutral Card (TODO)',
    variant: 'neutral',
  },
}

/**
 * Shows the success variant of the card, indicating a positive or completed action.
 */
export const Success: Story = {
  render: (args) => ({
    components: { MCard },
    setup: () => ({ args }),
    template: `
    <MCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MCard>
    `,
  }),
  args: {
    title: 'Successful Card (TODO)',
    variant: 'success',
  },
}
