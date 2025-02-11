import type { Meta, StoryObj } from '@storybook/vue3'

import MInputCheckbox from './MInputCheckbox.vue'

const meta: Meta<typeof MInputCheckbox> = {
  component: MInputCheckbox,
  tags: ['autodocs'],
  args: {
    label: 'Accept everything',
  },
  argTypes: {
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
    size: {
      control: { type: 'inline-radio' },
      options: ['small', 'default'],
    },
  },
  parameters: {
    docs: {
      description: {
        component: 'A checkbox component that can be used in forms.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MInputCheckbox>

/**
 * Demonstrates the default behavior of the `MInputCheckbox` component with a default slot.
 */
export const Default: Story = {
  args: {
    description: 'I will use this checkbox responsibly',
    hintMessage: 'Checkboxes return booleans.',
  },
}

/**
 * Demonstrates a `MInputCheckbox` that is checked by default.
 */
export const Checked: Story = {
  args: {
    description: 'This checkbox is checked by default.',
    modelValue: true,
  },
}

// TODO: Nicer disabled variant styling.
/**
 * Demonstrates a disabled `MInputCheckbox` with a label and description.
 */
export const Disabled: Story = {
  args: {
    label: 'Format C:/ ?',
    description: 'This action can not be undone.',
    disabled: true,
  },
}

/**
 * Demonstrates the success variant of the `MInputCheckbox` with a label, description, and hint message.
 */
export const Success: Story = {
  args: {
    label: 'Ok?',
    variant: 'success',
    description: 'This mostly makes sense when checked.',
    hintMessage: 'Success hint message.',
  },
}

// TODO: Nicer disabled variant styling.
/**
 * Demonstrates a disabled success variant of the `MInputCheckbox` with a label, description, and hint message.
 */
export const DisabledSuccess: Story = {
  args: {
    label: 'Ok?',
    variant: 'success',
    description: 'This mostly makes sense when checked.',
    hintMessage: 'Success hint message.',
    disabled: true,
  },
}

/**
 * Demonstrates the danger variant of the `MInputCheckbox` with a label, description, and hint message.
 */
export const Danger: Story = {
  args: {
    label: 'Ok?',
    variant: 'danger',
    description: 'This mostly makes sense when not checked.',
    hintMessage: 'Danger hint message.',
  },
}

// TODO: Nicer disabled variant styling.
/**
 * Demonstrates a disabled danger variant of the `MInputCheckbox` with a label, description, and hint message.
 */
export const DisabledDanger: Story = {
  args: {
    label: 'Ok?',
    variant: 'danger',
    description: 'This mostly makes sense when not checked.',
    hintMessage: 'Danger hint message.',
    disabled: true,
  },
}

/**
 * Demonstrates a `MInputCheckbox` with a very long description that wraps to multiple lines.
 */
export const LongDescription: Story = {
  args: {
    description:
      'This is a very long description that should wrap to multiple lines. It really should be shorter. I mean, who needs this much description for a checkbox?',
  },
}

/**
 * Demonstrates a `MInputCheckbox` with a description that overflows the container.
 */
export const DescriptionOverflow: Story = {
  args: {
    description:
      'Thisisastringofwordsthatislongerthanthecheckboxitselfandwilloverflowthecontainer.Whatonearthcouldpossiblybesolongthatitneedsthismanycharacters?Iguesswewillfindout.',
  },
}

/**
 * Demonstrates a `MInputCheckbox` without a label or description.
 */
export const NoLabelOrDescription: Story = {
  args: {},
}
