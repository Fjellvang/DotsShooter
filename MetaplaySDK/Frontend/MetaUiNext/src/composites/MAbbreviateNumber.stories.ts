import type { Meta, StoryObj } from '@storybook/vue3'

import MAbbreviateNumber from './MAbbreviateNumber.vue'

const meta: Meta<typeof MAbbreviateNumber> = {
  component: MAbbreviateNumber,
  tags: ['autodocs'],
  argTypes: {},
  parameters: {
    docs: {
      description: {
        component:
          'A component that displays a number in a human-readable format, abbreviating large numbers with a unit.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MAbbreviateNumber>

/**
 * Demonstrates the default behavior of the `MAbbreviateNumber` component, showing a large number in a human-readable format.
 */
export const Default: Story = {
  args: {
    number: 123456789,
  },
}

/**
 * Shows the `MAbbreviateNumber` component without a tooltip, demonstrating the display of a large number without additional information.
 */
export const NoTooltip: Story = {
  args: {
    number: 123456789,
    disableTooltip: true,
  },
}

/**
 * Demonstrates the `MAbbreviateNumber` component with a small number, showing how it handles numbers that do not require abbreviation.
 */
export const SmallNumber: Story = {
  args: {
    number: 100,
  },
}

/**
 * Shows the `MAbbreviateNumber` component with rounding down enabled, demonstrating how the number is truncated to the nearest integer.
 */
export const Rounded: Story = {
  args: {
    number: 300000.14159,
    roundDown: true,
  },
}

/**
 * Demonstrates the `MAbbreviateNumber` component with a single unit, showing how it handles singular units.
 */
export const OneUnit: Story = {
  args: {
    number: 1,
    unit: 'lemming',
  },
}

/**
 * Shows the `MAbbreviateNumber` component with multiple units, demonstrating how it handles plural units.
 */
export const ManyUnits: Story = {
  args: {
    number: 987654321,
    unit: 'lemming',
  },
}
