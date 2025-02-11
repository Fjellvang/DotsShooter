import type { Meta, StoryObj } from '@storybook/vue3'

import MBadge from '../primitives/MBadge.vue'
import MButton from '../primitives/MButton.vue'
import MCallout from '../primitives/MCallout.vue'
import MTextButton from '../primitives/MTextButton.vue'
import MClipboardCopy from '../unstable/MClipboardCopy.vue'
import { DisplayError } from '../utils/DisplayErrorHandler'
import MPageOverviewCard, { type MPageOverviewCardAlert } from './MPageOverviewCard.vue'

const meta: Meta<typeof MPageOverviewCard> = {
  component: MPageOverviewCard,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'primary', 'warning', 'danger'],
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'The `MPageOverviewCard` provides a high-level overview of the page content and serves as a visual anchor, helping users quickly understand the purpose and content of the page upon arrival.',
      },
    },
  },
  render: (args) => ({
    components: { MPageOverviewCard },
    setup: () => ({ args }),
    template: `
    <MPageOverviewCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MPageOverviewCard>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MPageOverviewCard>

/**
 * The `MPageOverviewCard` is strategically positioned and should provide a clear understanding of the underlying page content.
 * Always include a title to introduce the page content and provide context to the user.
 */
export const Default: Story = {
  args: {
    title: 'Short Title',
  },
}

/**
 * Use the `#badge` slot to highlight important information or indicate the status of the page content.
 * Badges can be used to draw attention to specific details, for example to indicate that a broadcast is active.
 */
export const TitleWithBadge: Story = {
  render: (args) => ({
    components: { MPageOverviewCard, MBadge },
    setup: () => ({ args }),
    template: `
    <MPageOverviewCard v-bind="args">
      <template #badge>
        <MBadge>Active</MBadge>
      </template>
      Lorem ipsum dolor sit amet.
    </MPageOverviewCard>
    `,
  }),
  args: {
    title: 'Short Title',
  },
}

/**
 * Add a short description using the `subtitle` prop to provide additional context and guidance to the user.
 * This can include a brief summary of the page content, a call to action, or a description of the page purpose.
 *
 * For bespoke descriptions, you can use the `#subtitle` slot. This lets you fully customize the subtitle with
 * rich content, including icons, links, badges, and other custom elements, along with your text.
 */
export const Subtitle: Story = {
  render: (args) => ({
    components: { MPageOverviewCard, MTextButton },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-flex-col tw-gap-2">
      <MPageOverviewCard v-bind="args">
      </MPageOverviewCard>
      <MPageOverviewCard title="Subtitle Slot">
        <template #subtitle>
          For a more customized subtitle, use the #subtitle slot. This allows you to add rich content such as icons, links, badges, and other custom elements.
          <MTextButton to="/">Learn More</MTextButton>!
        </template>
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
    </div>
    `,
  }),
  args: {
    title: 'Subtitle Card',
    subtitle:
      'In some cases a subtitle is needed to provide more context to the card. Set the `subtitle` prop to add a short description to the card.',
  },
}

/**
 * Use the `#default` slot to add custom content to the card and really make it your own. Place any content
 * such as tables, lists or images in this slot to provide users with a more detailed overview of the page content.
 *
 * This slot is responsive and will gracefully handle large amounts of content by wrapping it to the next line.
 * However, we recommend keeping the content concise and to the point as the `MPageOverviewCard` is primarily intended
 * to provide a high-level overview of the page content.
 */
export const DefaultSlot: Story = {
  render: (args) => ({
    components: { MPageOverviewCard },
    setup: () => ({ args }),
    template: `
    <MPageOverviewCard v-bind="args">
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
    </MPageOverviewCard>
    `,
  }),
  args: {
    title: 'Custom Content Card',
    id: 'Player:ZArvpuPqNL',
  },
}

/**
 * Use the `alerts` prop or the `#alerts` slot to display important messages or alerts to the user. This can be
 * used to inform users of critical information, warnings, or errors that they need to be aware of when viewing
 * the page content.
 */
export const Alerts: Story = {
  render: (args) => ({
    components: { MPageOverviewCard, MCallout, MButton },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-flex-col tw-gap-2">
      <MPageOverviewCard v-bind="args">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
      <MPageOverviewCard>
        <template #alerts>
          <MCallout title="Custom Alert" variant="warning">
            Use this slot to create an alert with custom elements such as buttons, icons, etc.
            <MButton>Some Action</MButton>
          </MCallout>
        </template>
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
    </div>
    `,
  }),
  args: {
    title: 'Alerts Slot Card',
    alerts: [
      {
        title: 'Example Alert',
        message: 'This alert has been created by setting the `alerts` prop on the overview card.',
        variant: 'neutral',
      },
      {
        title: 'Example Alert',
        message: 'The alert object at a minimum requires a title and message to be displayed.',
        variant: 'success',
      },
      {
        title: 'Example Alert',
        message:
          'In addition you can also set the variant, or provide a link and linkText to help a user understand what to do next. Follow the link to',
        variant: 'danger',
        link: 'https://www.metaplay.io/',
        linkText: 'Learn More',
      },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ] satisfies MPageOverviewCardAlert[] as any,
  },
}

/**
 * Set the `id` prop to display a unique identifier for the page content. This can be used to reference
 * the page content when communicating with other users or when troubleshooting an issue.
 */
export const ID: Story = {
  args: {
    title: 'Card With an ID',
    id: 'Player:ZArvpuPqNL',
  },
}

/**
 * Set the `idLabel` prop when you need to be very precise in your communications about the type of the identifier.
 * This can be valuable if your users deal with a variety of identifiers like names, UUIDs, slugs or URIs.
 */
export const CustomIdentifierLabel: Story = {
  args: {
    title: 'Card With a Slug',
    id: 'foo-bar',
    idLabel: 'Slug',
  },
}

/**
 * You can display a loading state by setting the `isLoading` prop to `true`. This is useful when fetching
 * data for the card or when the content is still being loaded.
 */
export const LoadingState: Story = {
  args: {
    title: 'Always Loading',
    isLoading: true,
  },
}

/**
 * By setting the `error` prop, you can catch and display errors that occur when fetching data for the card.
 * The error can be an instance of our custom `DisplayError` class, or a standard `Error` object.
 */
export const ErrorState: Story = {
  render: (args) => ({
    components: { MPageOverviewCard },
    setup: () => ({ args }),
    template: `
    <MPageOverviewCard v-bind="args">
      Lorem ipsum dolor sit amet.
    </MPageOverviewCard>
    `,
  }),
  args: {
    title: 'French military victories',
    error: new DisplayError(
      'No victories found',
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
 * Add action buttons in the `#buttons` slot to provide users with quick access to common actions or tasks.
 * Buttons can be used to trigger specific actions, navigate to other pages, or open modals that provide additional
 * information.
 */
export const Button: Story = {
  render: (args) => ({
    components: { MPageOverviewCard, MButton },
    setup: () => ({ args }),
    template: `
    <MPageOverviewCard v-bind="args">
      Lorem ipsum dolor sit amet.
      <template #buttons>
        <MButton>Some Action</MButton>
      </template>
    </MPageOverviewCard>
    `,
  }),
  args: {
    title: 'Card With a button',
  },
}

/**
 * Use the `#buttons` slot sparingly to avoid overwhelming users with too many options. While the `MPageOverviewCard`
 * is responsive and will adjust the layout of the buttons based on the available space we recommend limiting the number
 * of buttons to a maximum of 3-4 to maintain a clean and user-friendly design.
 */
export const ManyButtons: Story = {
  render: (args) => ({
    components: { MPageOverviewCard, MButton },
    setup: () => ({ args }),
    template: `
    <MPageOverviewCard v-bind="args">
      Lorem ipsum dolor sit amet.
      <template #buttons>
        <MButton>Button 1</MButton>
        <MButton>Button 2</MButton>
        <MButton>Button 3</MButton>
        <MButton>Button 4</MButton>
        <MButton>Button 5</MButton>
        <MButton>Button 6</MButton>
        <MButton>Button 7</MButton>
        <MButton>Button 8</MButton>
        <MButton>Button 9</MButton>
      </template>
    </MPageOverviewCard>
    `,
  }),
  args: {
    title: 'Card With too many buttons',
    id: 'Player:ZArvpuPqNL',
  },
}

/**
 * Avoid using long text in the titles or id's as it can cause the card to overflow and break the layout.
 * If you need to display long text, consider using the `subtitle` prop or `#subtitle` slot instead.
 */
export const Overflow: Story = {
  render: (args) => ({
    components: { MPageOverviewCard, MButton, MClipboardCopy },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-flex-col tw-gap-2">
      <MPageOverviewCard v-bind="args" title="Card Headers Should Deal With Very Long Text by Wrapping to The Next Line" id="Player:ZArvpuPqNL">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>

      <MPageOverviewCard v-bind="args" title="WhatHappensWhenTheTitleIsAllOneWordAndStillLongTruncatedOverflow" id="Player:ZArvpuPqNL">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>

      <MPageOverviewCard v-bind="args" title="A Long Overivew Title And Long ID" id="Broadcast:19as238938478172y82374huI127874398291829832129898981231298KIturBs1209345123">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>

      <MPageOverviewCard v-bind="args" title="A Long Overivew Title And Long ID" variant="success">
        <template #subtitle>
          ID:Broadcast:19as238938478172y82374huI127874398291829832129898981231298KIturBs1209345123
          <MClipboardCopy :contents="'Broadcast:19as238938478172y82374huI127874398291829832129898981231298KIturBs1209345123'"/>
        </template>
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
    </div>
    `,
  }),
}

/**
 * Use contextual variants to draw attention or convey status of the page overview or content to the user.
 * By default, the `MPageOverviewCard` uses the `neutral` variant, but you can change it to `success`, `danger`
 * or `warning` to change the appearance of the card as shown in the examples below.
 */
export const Variants: Story = {
  render: (args) => ({
    components: { MPageOverviewCard },
    setup: () => ({ args }),
    template: `
    <div class="tw-flex tw-flex-col tw-gap-2">
      <MPageOverviewCard v-bind="args" title="Warning Card" variant="warning">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
      <MPageOverviewCard v-bind="args" title="Dangerous Card" variant="danger">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
      <MPageOverviewCard v-bind="args" title="Neutral Card" variant="neutral">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
      <MPageOverviewCard v-bind="args" title="Successful Card" variant="success">
        Lorem ipsum dolor sit amet.
      </MPageOverviewCard>
    </div>
    `,
  }),
}
