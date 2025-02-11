import { DateTime, Duration } from 'luxon'

import type { Meta, StoryObj } from '@storybook/vue3'

import MInputDateTime from './MInputDateTime.vue'

const meta: Meta<typeof MInputDateTime> = {
  component: MInputDateTime,
  tags: ['autodocs'],
  args: {
    modelValue: DateTime.now(),
  },
  argTypes: {
    modelValue: {
      control: false,
    },
    minDateTime: {
      control: { type: 'text' },
      description: 'The minimum date time that can be selected. The time picker will respect this limit.',
    },
    maxDateTime: {
      control: { type: 'text' },
      description: 'The maximum date time that can be selected. The time picker will respect this limit.',
    },
    variant: {
      control: { type: 'select' },
      options: ['default', 'success', 'danger'],
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'MInputDateTime is a wrapper for MInputDate and MInputTime components. It is used to select a date and time. The returned Luxon `DateTime` object describes an instant in time. The selected time is always in UTC.',
      },
    },
  },
  render: (args) => ({
    components: { MInputDateTime },
    setup: () => ({ args }),
    data: () => ({ datetime: args.modelValue }),
    template: `<div>
      <MInputDateTime v-bind="args" v-model="datetime"/>
      <pre class="tw-mt-2">Output: {{ datetime }}</pre>
    </div>`,
  }),
}

export default meta
type Story = StoryObj<typeof MInputDateTime>

/**
 * At its simplest, you get two inputs: one for date and one for time. The date is a calendar view and the time is a number picker.
 */
export const Default: Story = {
  args: {
    label: 'Date and Time',
    hintMessage: 'This input returns a Luxon DateTime object.',
  },
}

/**
 * You can set the minimum time to the current time in UTC. The date and time pickers will respect this limit.
 */
export const MinTimeUtcNow: Story = {
  args: {
    label: 'Later than UTC now',
    minDateTime: 'utcNow',
  },
}

/**
 * You can also set the minimum time to a specific time. In this case, the minimum time is set to 8:35 UTC three days ago.
 */
export const MinTimeMorning3DaysAgo: Story = {
  args: {
    label: 'Later than 6:35 (UTC) three days ago',
    minDateTime: DateTime.utc()
      .minus(Duration.fromObject({ days: 3 }))
      .set({ hour: 8, minute: 35 }),
  },
}

/**
 * You can set the maximum time to the current time in UTC. The date and time pickers will respect this limit.
 */
export const MaxTimeUtcNow: Story = {
  args: {
    label: 'Earlier than now',
    maxDateTime: 'utcNow',
  },
}

/**
 * You can also set the maximum time to a specific time. In this case, the maximum time is set to 20:35 UTC in 3 days.
 */
export const MaxTimeEveningIn3Days: Story = {
  args: {
    label: 'Earlier than 18:35 (UTC)in 3 days',
    maxDateTime: DateTime.now()
      .plus(Duration.fromObject({ days: 3 }))
      .set({ hour: 20, minute: 35 }),
  },
}

/**
 * The `success` variant is used to indicate that the input is valid.
 */
export const Success: Story = {
  args: {
    label: 'Time (success)',
    variant: 'success',
    hintMessage: 'This is a success hint.',
  },
}

/**
 * The `danger` variant is used to indicate that the input is invalid.
 */
export const Danger: Story = {
  args: {
    label: 'Time (danger)',
    variant: 'danger',
    hintMessage: 'This is a danger hint.',
  },
}

/**
 * The `disabled` prop can be used to disable the input.
 */
export const Disabled: Story = {
  args: {
    label: 'Date (disabled)',
    disabled: true,
  },
}

/**
 * You can use the input without a label.
 */
export const NoLabel: Story = {}

/**
 * The `allowEmpty` prop can be used to allow the input to be empty. This is useful when the lack of input is a valid choice, but it should also be combined with clever use of the `hintMessage` prop to explain what the empty value means in context.
 */
export const AllowEmpty: Story = {
  render: (args) => ({
    components: { MInputDateTime },
    setup: () => ({ args }),
    data: () => ({ datetime: undefined }), // Initialize datetime as undefined
    template: `<div>
      <MInputDateTime v-bind="args" v-model="datetime"/>
      <pre class="tw-mt-2">Output: {{ datetime }}</pre>
    </div>`,
  }),
  args: {
    label: 'Possibly empty date time',
    allowEmpty: true,
    hintMessage:
      'The hint message can be used to explain what the empty value means in context. Maybe some feature is disabled?',
  },
}

/**
 * The input can be disabled and still allow an empty value. This use case does not make a huge amount of sense and is visually displeasing.
 */
export const DisabledAndEmptyAllowed: Story = {
  args: {
    label: 'Date (disabled)',
    disabled: true,
    allowEmpty: true,
  },
}

/**
 * You can manually pass in an "illegal" time to the `modelValue` prop. In this case, the time is set to whatever you passed in, but the time picker controls will respect the min and max datetimes. This is an unsupported UI state and might look a bit wonky.
 */
export const MinTimeNoonTomorrowButDefaultsToNow: Story = {
  args: {
    label: 'Later than UTC 12:00 tomorrow',
    minDateTime: DateTime.utc().plus({ days: 1 }).set({ hour: 12 }),
    hintMessage: 'Please avoid doing this this.',
    variant: 'danger',
  },
}
