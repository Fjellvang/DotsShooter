import type { Meta, StoryObj } from '@storybook/vue3'

import MProgressBar from './MProgressBar.vue'

const meta: Meta<typeof MProgressBar> = {
  component: MProgressBar,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['default', 'neutral', 'success', 'danger', 'warning'],
    },
    value: {
      type: 'number',
      control: {
        type: 'range',
        min: 0,
        max: 1,
        step: 0.01,
      },
    },
  },
  parameters: {
    docs: {
      description: {
        component: 'A progress bar that displays a percentage value.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MProgressBar>

export const Default: Story = {
  render: (args) => ({
    components: { MProgressBar },
    setup: () => ({ args }),
    template: `
    <div style="width: 300px">
      <MProgressBar v-bind="args">
      </MProgressBar>
    </div>
    `,
  }),
  args: {
    value: 0.7,
  },
}

export const HiddenPercentageText: Story = {
  render: (args) => ({
    components: { MProgressBar },
    setup: () => ({ args }),
    template: `
    <div style="width: 300px">
      <MProgressBar v-bind="args">
      </MProgressBar>
    </div>
    `,
  }),
  args: {
    hidePercentageValue: true,
    value: 0.5,
  },
}

export const Customized: Story = {
  render: (args) => ({
    components: { MProgressBar },
    setup: () => ({ args }),
    template: `
    <div style="width: 300px">
      <MProgressBar v-bind="args">
      </MProgressBar>
    </div>
    `,
  }),
  args: {
    min: 5,
    max: 45.7,
    value: 30.5,
    variant: 'warning',
  },
}
