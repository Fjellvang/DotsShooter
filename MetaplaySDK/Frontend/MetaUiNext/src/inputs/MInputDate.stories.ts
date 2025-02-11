import { DateTime } from 'luxon'
import { ref, watch } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputDate from './MInputDate.vue'

const meta: Meta<typeof MInputDate> = {
  component: MInputDate,
  args: {
    modelValue: DateTime.now().toUTC().toISODate(),
  },
  tags: ['autodocs'],
  argTypes: {
    modelValue: {
      control: { type: 'text' },
      description: 'Current value of the input as a ISO 8601 date string. For example `2021-01-01`',
    },
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
    minIsoDate: {
      control: { type: 'text' },
      description:
        'Optional: Dates before the given ISO 8601 date will be disabled. Passing in the string value `utcNow` sets this value to the current date. For example `2021-01-01`',
    },
    maxIsoDate: {
      control: { type: 'text' },
      description:
        'Optional: Dates after the given ISO 8601 date will be disabled. Passing in the string value `utcNow` sets this value to the current date. For example `2021-01-01`',
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'MInputDate is a rich calendar view popover for a date. The selected date is a plain date, meaning that it has no information about time zones and does not describe any specific day in UTC time. The output is an ISO 8601 date string. Please note that there is no undefined state for this input. If you need to represent an undefined date, you should make a wrapper for this component to handle that UX in a way that makes sense in your context.',
      },
    },
  },
  render: (args) => ({
    components: { MInputDate },
    setup: () => {
      const date = ref(args.modelValue)
      watch(
        () => args.modelValue,
        (value) => {
          date.value = value
        }
      )
      return { args, date }
    },
    template: `<div>
      <MInputDate v-bind="args" v-model="date"/>
      <pre class="tw-mt-2">Output: {{ date }}</pre>
    </div>`,
  }),
}

export default meta
type Story = StoryObj<typeof MInputDate>

/**
 * You can click to open a pop-over calendar, use the scroll wheel, or use arrow keys to select a date.
 */
export const Default: Story = {
  args: {
    label: 'Date',
    hintMessage: 'This input returns an ISO 8601 date string.',
  },
}

/**
 * Passing in `utcNow` as the `minIsoDate` prop will set the minimum date to the current date in UTC. Notice how the calendar view does not allow you to select a date earlier than today.
 */
export const MinDateNow: Story = {
  args: {
    label: 'Later than the current date',
    minIsoDate: 'utcNow',
  },
}

/**
 * You can also set the minimum date to a specific date. In this case, the minimum date is set to 30 days ago.
 * Broken story.
 */
export const MinDate30DaysAgo: Story = {
  args: {
    label: 'Later than 30 days ago',
    minIsoDate: DateTime.now().minus({ days: 30 }).toUTC().toISODate(),
  },
}

/**
 * Passing in `utcNow` as the `maxIsoDate` prop will set the maximum date to the current date in UTC. Notice how the calendar view does not allow you to select a date later than today.
 */
export const MaxDateNow: Story = {
  args: {
    label: 'Earlier than today',
    maxIsoDate: 'utcNow',
  },
}

/**
 * You can also set the maximum date to a specific date. In this case, the maximum date is set to 30 days from now.
 */
export const MaxDateIn30: Story = {
  args: {
    label: 'Earlier than 30 days from now',
    maxIsoDate: DateTime.now().plus({ days: 30 }).toUTC().toISODate(),
  },
}

/**
 * You can set both the minimum and maximum date to create a range of selectable dates. In this case, the range is between 3 days ago and 3 days from now.
 */
export const MinMaxDate: Story = {
  args: {
    label: 'Between 3 days ago and 3 days from now',
    minIsoDate: DateTime.now().minus({ days: 3 }).toUTC().toISODate(),
    maxIsoDate: DateTime.now().plus({ days: 3 }).toUTC().toISODate(),
  },
}

/**
 * The `success` variant is used to indicate that the input is valid.
 */
export const Success: Story = {
  args: {
    label: 'Date (success)',
    variant: 'success',
    hintMessage: 'This is a success hint.',
  },
}

/**
 * The `danger` variant is used to indicate that the input is invalid.
 */
export const Danger: Story = {
  args: {
    label: 'Date (danger)',
    variant: 'danger',
    hintMessage: 'This is a danger hint.',
  },
}

/**
 * The `disabled` prop will disable the input.
 */
export const Disabled: Story = {
  args: {
    label: 'Date (disabled)',
    disabled: true,
  },
}

/**
 * The `noLabel` prop will remove the label from the input.
 */
export const NoLabel: Story = {
  args: {},
}

/**
 * The component will automatically adjust its formatting based on its width.
 */
export const ReactiveWidth: Story = {
  render: (args) => ({
    components: { MInputDate },
    setup: () => ({ args }),
    data: () => ({
      date: 'modelValue' in args ? args.modelValue : DateTime.utc().toISODate(),
    }),
    template: `<div>
      <MInputDate v-bind="args" v-model="date" style="width:220px"/>
      <MInputDate v-bind="args" v-model="date" style="width:170px"/>
      <MInputDate v-bind="args" v-model="date" style="width:125px"/>
    </div>
    `,
  }),
}
