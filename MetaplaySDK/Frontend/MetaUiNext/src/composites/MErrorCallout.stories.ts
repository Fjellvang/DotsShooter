import type { Meta, StoryObj } from '@storybook/vue3'

import MButton from '../primitives/MButton.vue'
import { DisplayError } from '../utils/DisplayErrorHandler'
import MErrorCallout from './MErrorCallout.vue'

const meta: Meta<typeof MErrorCallout> = {
  component: MErrorCallout,
  tags: ['autodocs'],
  argTypes: {},
  parameters: {
    docs: {
      description: {
        component: 'A callout component that displays an error message with optional details.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MErrorCallout>

/**
 * Demonstrates the default usage of `MErrorCallout` with a detailed error object.
 */
export const Default: Story = {
  args: {
    error: new DisplayError(
      'Example Metaplay API Error',
      'This is an example of an error with added details.',
      500,
      undefined,
      [
        {
          title: 'API Request',
          content: {
            method: 'GET',
            path: '/api/v1/endpoint',
            body: { foo: 'bar' },
          },
        },
        {
          title: 'Stack Trace',
          content:
            'Exception in thread "main" sample.InputMismatchException at sample.base/sample.MPlay.throwFor(MPlay.sample:939) at sample.base/sample.MPlay.next(MPlay.sample:1594) at sample.base/sample.MPlay.nextFloat(MPlay.sample:2496) at com.example.mySampleProject.hello.main(hello.sample:12)',
        },
      ]
    ),
  },
}

/**
 * Shows `MErrorCallout` with a simple error title and message.
 */
export const TitleAndMessage: Story = {
  args: {
    error: new DisplayError('Example Error', 'This is an example of an error with and added message.'),
  },
}

/**
 * Illustrates `MErrorCallout` displaying a JavaScript error with a syntax error detail.
 */
export const JavascriptError: Story = {
  args: {
    error: new DisplayError('Example Javascript Error', 'Unexpected token', 'SyntaxError', undefined, [
      {
        title: 'Syntax Error',
        content: 'Unexpected token < in JSON at position 0',
      },
    ]),
  },
}

/**
 * Demonstrates `MErrorCallout` with an internal Metaplay error and additional details.
 */
export const MetaplayInternalError: Story = {
  args: {
    error: new DisplayError(
      'Example Metaplay Internal Error',
      'This is an example of an error with added details.',
      500,
      undefined,
      [
        {
          title: 'Internal Error',
          content: 'Example internal error message',
        },
      ]
    ),
  },
}

/**
 * Shows how `MErrorCallout` handles a very long error message, causing overflow and truncation.
 */
export const LongErrorExample: Story = {
  render: (args) => ({
    components: { MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      Lorem ipsum dolor sit amet.
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError(
      'Too long error message that gets truncated',
      'Culpa quo suscipit voluptas dolores aliquid porro unde et. Commodi quis similique labore voluptatem quos atque. Voluptas dolor voluptates inventore dolorum et. Sed et id rem. Beatae assumenda explicabo quia quo fugit est eaque. Sed eos repudiandae quasi error aut dolorem. Fugiat nobis explicabo et odit ut veniam. Magnam nobis quae qui. Dignissimos dolorem voluptatem voluptas quae quia iusto tempore reiciendis. Ea laboriosam eos aut et ratione mollitia. Nostrum aut deleniti dolore corporis voluptas. Exercitationem laborum non accusantium vitae velit.'
    ),
  },
}

/**
 * Illustrates `MErrorCallout` with a long error title and a badge, demonstrating overflow handling.
 */
export const LongErrorTitleWithBadge: Story = {
  render: (args) => ({
    components: { MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      Lorem ipsum dolor sit amet.
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError(
      'Example of a very long error message that will cause overflow in the title and wrap onto the next row',
      'The badge should look ok even in this case.',
      'Overflow'
    ),
  },
}

/**
 * Demonstrates `MErrorCallout` with a long content error, making the body scrollable.
 */
export const LongContentErrorExample: Story = {
  render: (args) => ({
    components: { MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      Lorem ipsum dolor sit amet.
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError('Example Javascript Error', 'Unexpected token', 'Overflow', undefined, [
      {
        title: 'Overflow in content',
        content:
          'Very long template string that will cause overflow in the content area and will make the body scrollable. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed non risus. Suspendisse lectus tortor, dignissim sit amet, adipiscing nec, ultricies sed, dolor.',
      },
    ]),
  },
}

/**
 * Shows `MErrorCallout` with an error title and message having no whitespace, causing overflow.
 */
export const ErrorOverflowNoWhiteSpace: Story = {
  render: (args) => ({
    components: { MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      Lorem ipsum dolor sit amet.
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError(
      'LongTitleThatWillCauseOverflowInTheHeaderAreaAndWillBeTruncatedWithEllipsis',
      'Thisisanexampleofalongerrormessagewithnowhitespaceandwillcauseoverflowintheheaderareaandwillbetruncatedwithellipsis.'
    ),
  },
}

/**
 * Demonstrates `MErrorCallout` with an error title and message having no whitespace and a badge.
 */
export const ErrorOverflowNoWhiteSpaceWithBadge: Story = {
  render: (args) => ({
    components: { MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      Lorem ipsum dolor sit amet.
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError(
      'LongTitleThatWillCauseOverflowInTheHeaderAreaAndWillBeTruncatedWithEllipsis',
      'Thisisanexampleofalongerrormessagewithnowhitespaceandwillcauseoverflowintheheaderareaandwillbetruncatedwithellipsis.',
      'Overflow'
    ),
  },
}

/**
 * Illustrates `MErrorCallout` with an error title and message having no whitespace and an overflow badge.
 */
export const ErrorOverflowNoWhiteSpaceWithOverflowBadge: Story = {
  render: (args) => ({
    components: { MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      Lorem ipsum dolor sit amet.
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError(
      'LongTitleThatWillCauseOverflowInTheHeaderAreaAndWillBeTruncatedWithEllipsis',
      'Thisisanexampleofalongerrormessagewithnowhitespaceandwillcauseoverflowintheheaderareaandwillbetruncatedwithellipsis.',
      'Thisisanexampleofalongerrormessagewithnowhitespaceandwillcauseoverflowintheheaderareaandwillbetruncatedwithellipsis.'
    ),
  },
}

/**
 * Shows `MErrorCallout` with an additional custom button, demonstrating slot usage.
 */
export const ExtraButton: Story = {
  render: (args) => ({
    components: { MButton, MErrorCallout },
    setup: () => ({ args }),
    template: `
    <MErrorCallout v-bind="args" style="width: 576px">
      <template #buttons>
        <MButton>Button</MButton>
      </template>
    </MErrorCallout>
    `,
  }),
  args: {
    error: new DisplayError('Example Error', 'This is an example of an error with and added button.'),
  },
}
