import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MCallout from '../primitives/MCallout.vue'
import MCard from '../primitives/MCard.vue'
import { DisplayError } from '../utils/DisplayErrorHandler'
import MTwoColumnLayout from './MTwoColumnLayout.vue'
import MViewContainer, { type MViewContainerAlert } from './MViewContainer.vue'

const meta: Meta<typeof MViewContainer> = {
  component: MViewContainer,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['neutral', 'warning', 'danger'],
    },
    alerts: {
      control: { type: 'object' },
      description:
        'Optional: An array of alerts to display at the top of the page. Each alert should have a title, message, and optional variant (neutral, warning, danger).',
    },
  },
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'The `MViewContainer` is a base component designed to create visually consistent and responsive main content area throughout the whole dashboard.  It contains, pads, and centers content as well as handling the display of page alerts, errors, and loading states',
      },
    },
  },
  render: (args) => ({
    components: {
      MViewContainer,
      MCard,
    },
    setup: () => ({ args }),
    template: `
      <MViewContainer v-bind="args">
       <template #overview>
          <MCard title="Overview card placeholder">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
        </template>
        <MCard title="Example content">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MViewContainer>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MViewContainer>

/**
 * There are two main content areas
 */
export const Default: Story = {
  render: (args) => ({
    components: {
      MViewContainer,
      MCard,
      MTwoColumnLayout,
    },
    setup: () => ({ args }),
    template: `
      <MViewContainer v-bind="args">
        <template #overview>
          <MCard title="Overview card placeholder">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
        </template>
        <MTwoColumnLayout>
          <MCard title="Example content 1">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
          <MCard title="Example content 2">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
          <MCard title="Example content 3">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
          <template #full>
            <MCard title="Example content 4">
              <p>Lorem ipsum dolor sit amet.</p>
            </MCard>
          </template>
          <MCard title="Example content 5">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
          <MCard title="Example content 6">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
          <MCard title="Example content 7">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
          <MCard title="Example content 8">
            <p>Lorem ipsum dolor sit amet.</p>
          </MCard>
        </MTwoColumnLayout>
      </MViewContainer>
    `,
  }),
  args: {},
}

/**
 * When an API call is in progress, the `MViewContainer` will display a loading skeleton.
 */
export const LoadingState: Story = {
  args: {
    isLoading: true,
  },
}

/**
 * Use the `error` prop to pass in any errors that occur during API calls or rendering. All
 * errors will be displayed at the top of the page providing immediate feedback to the user
 * regarding the issue encountered.
 */
export const ErrorState: Story = {
  args: {
    error: new DisplayError('Example Error', 'This is an example error message.'),
  },
}

/**
 * Additionally you can use `#errors` slot to include extra debugging information, links, tools,
 * or tips to help users debug and resolve the errors more easily.
 */
export const ErrorsSlot: Story = {
  render: (args) => ({
    components: {
      MViewContainer,
      MCallout,
    },
    setup: () => ({ args }),
    template: `
      <MViewContainer v-bind="args">
        <template #errors>
          <MCallout title="Debugging details" variant="danger">
            <p>Use the following tools to find more information about this error:</p>
            <p>Lorem ipsum dolor sit amet.</p>
            <p>Lorem ipsum dolor sit amet.</p>
            <p>Lorem ipsum dolor sit amet.</p>
          </MCallout>
        </template>
      </MViewContainer>
    `,
  }),
  args: {
    error: new DisplayError('Example Error', 'This is an example error message.'),
  },
}

/**
 * Use the `permission` prop to restrict content visibility to users with the required permission.
 * If the user lacks the required permission, an alert message will be displayed at the top of the
 * page and all other content will be hidden.
 */
export const NoPermission: Story = {
  render: (args) => ({
    components: {
      MViewContainer,
      MCard,
    },
    setup: () => {
      usePermissions().setPermissions(['example-permission'])
      return {
        args,
      }
    },
    template: `
      <MViewContainer v-bind="args">
        <MCard title="Example content">
          <p>Lorem ipsum dolor sit amet.</p>
        </MCard>
      </MViewContainer>
    `,
  }),
  args: {
    permission: 'example-permission2',
  },
}

/**
 * Alerts are displayed at the top of the page with a striped background to ensure visibility.
 */
export const Alerts: Story = {
  args: {
    alerts: [
      {
        title: 'Example Warning',
        message: 'This is an example warning message.',
        variant: 'warning',
      },
      {
        title: 'Example Warning (danger)',
        message: 'This is another example warning message.',
        variant: 'danger',
      },
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    ] satisfies MViewContainerAlert[] as any,
  },
}
