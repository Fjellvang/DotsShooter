import type { Meta, StoryObj } from '@storybook/vue3'

import MCollapse from './MCollapse.vue'
import MListItem from './MListItem.vue'

const meta: Meta<typeof MCollapse> = {
  component: MCollapse,
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: {
        type: 'select',
      },
      options: ['neutral', 'success', 'danger', 'warning', 'primary'],
    },
  },
  parameters: {
    docs: {
      description: {
        component:
          'The `MCollapse` component is a simple wrapper for collapsible content. This is commonly used to hide lengthy content or sections that are not immediately relevant, improving user experience and decluttering the UI.',
      },
    },
    viewport: {
      defaultViewport: 'singleColumn',
    },
  },
}

export default meta
type Story = StoryObj<typeof MCollapse>

/**
 * The `MCollapse` has two main slots: `header` and `default`. The `header` slot is used to define the clickable section that toggles the visibility of the content in the `default` slot.
 */
export const Default: Story = {
  render: (args) => ({
    components: { MCollapse },
    setup: () => ({ args }),
    template: `
    <MCollapse v-bind="args">
      <template #header>
        <span>Header</span>
      </template>
      <p>This content goes into the body.</p>
    </MCollapse>
    `,
  }),
  args: {},
}

/**
 * In some cases you may want to hide the content by default and only show it when a certain condition is met.
 * You can achieve this by setting the `hideCollapse` prop to `true`.
 * Note, the `hideCollapse` prop does not affect the visibility of the header slot content.
 */
export const CollapsibleContentHidden: Story = {
  render: (args) => ({
    components: { MCollapse },
    setup: () => ({ args }),
    template: `
    <MCollapse v-bind="args">
      <template #header>
        <span>Header</span>
      </template>
      <p>This content goes into the body.</p>
    </MCollapse>
    `,
  }),
  args: {
    hideCollapse: true,
  },
}

/**
 * By default the `MCollapse` default content is hidden and clicking on the header will toggle the visibility of the content.
 * However you can be set it to be open by default by setting the `isOpenByDefault` prop to `true`.
 * This is useful when you want to show the content by default and allow the user to hide it if they want.
 */
export const OpenByDefault: Story = {
  render: (args) => ({
    components: { MCollapse },
    setup: () => ({ args }),
    template: `
    <MCollapse v-bind="args">
      <template #header>
        <span>Header</span>
      </template>
      <p>This content goes into the body.</p>
    </MCollapse>
    `,
  }),
  args: {
    isOpenByDefault: true,
  },
}

/**
 * The `MCollapse` component can be used to create a list of collapsible items.
 * This creates a clean and organized way to display a list of items with lengthy details.
 */
export const ListWithCollapsibleItems: Story = {
  render: (args) => ({
    components: { MCollapse },
    setup: () => ({ args }),
    template: `
    <div class="tw-space-y-2">
      <MCollapse v-bind="args">
        <template #header>
          <span>Item 1</span>
        </template>
        <p>Details for item 1</p>
      </MCollapse >
      <MCollapse v-bind="args">
        <template #header>
          <span>Item 2</span>
        </template>
        <p>Details for item 2</p>
      </MCollapse>
      <MCollapse v-bind="args">
        <template #header>
          <span>Item 3</span>
        </template>
        <p>Details for item 3</p>
      </MCollapse >
    </div>
    `,
  }),
}

/**
 * Set the `isOpenByDefault` prop to `true` to have one the items expanded by default.
 * This is useful when you want to draw the user's attention to a specific item however,
 * we do not recommend having more than one item expanded by default as it can be distracting
 * and overwhelming for the user.
 */
export const ListWithACollapsibleItemOpenByDefault: Story = {
  render: (args) => ({
    components: { MCollapse, MListItem },
    setup: () => ({ args }),
    template: `
    <div class="tw-space-y-2">
      <MCollapse v-bind="args">
        <template #header>Item 1</template>
        <p>Details for item 1</p>
      </MCollapse >
      <MCollapse v-bind="args" isOpenByDefault>
        <template #header>Item 2</template>
        <p>Details for item 2</p>
      </MCollapse >
      <MCollapse>
        <template #header>Item 3</template>
        <p>Details for item 3</p>
      </MCollapse>
      <MCollapse>
        <template #header>Item 4</template>
        <p>Details for item 4</p>
      </MCollapse>
      <MCollapse>
        <template #header>Item 5</template>
        <p>Details for item 5</p>
      </MCollapse>
    </div>
    `,
  }),
}

/**
 * Use the `MListItem` component within the header slot to create custom lists with
 * content beautifully positioned on all four corners of the header. See the
 * **[MListItem docs](http://localhost:6006/?path=/docs/primitives-mlistitem--docs)** for more information.
 *
 * **Styling Tips:** To create a clean and organized list, we recommend setting both the `extraMListItemMargin`
 * prop on the `MCollapse` component and the `noLeftPadding` prop on the `MListItem` component to true.
 * This will ensure a clean and consistent look between the icon and header section of the collapsible item.
 *
 * In the example below, Collapsible List A demonstrates alignment without the additional styling, while
 * Collapsible List B demonstrates correct alignment when the styling is applied.
 */
export const ListWithACollapsibleMListItem: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'twoColumn',
    },
  },
  render: (args) => ({
    components: { MCollapse, MListItem },
    setup: () => ({ args }),
    template: `
    <div class="tw-inline-flex tw-space-x-20">
      <div class="tw-space-1">
        Collapsible List A
        <MCollapse>
          <template #header>
          <MListItem>
            <p>Item 1</p>
            <template #top-right>Player:123456789</template>
            <template #bottom-left>Secondary content: Add a short descriptions here.</template>
            <template #bottom-right>Link here?</template>
          </MListItem>
          </template>
          <p>Details for item 1</p>
        </MCollapse >
        <MCollapse>
          <template #header>
          <MListItem>
            <p>Item 2</p>
            <template #top-right>Player:123456789</template>
            <template #bottom-left>Secondary content: Add a short descriptions here.</template>
            <template #bottom-right>Link here?</template>
          </MListItem>
          </template>
          <p>Details for item 2</p>
        </MCollapse>
      </div>
      <div class="tw-space-1">
        Collapsible List B
        <MCollapse v-bind="args">
          <template #header>
          <MListItem noLeftPadding>
            <p>Item 1</p>
            <template #top-right>Player:123456789</template>
            <template #bottom-left>Secondary content: Add a short descriptions here.</template>
            <template #bottom-right>Link here?</template>
          </MListItem>
          </template>
          <p>Details for item 1</p>
        </MCollapse >
        <MCollapse v-bind="args">
          <template #header>
          <MListItem noLeftPadding>
            <p>Item 2</p>
            <template #top-right>Player:123456789</template>
            <template #bottom-left>Secondary content: Add a short descriptions here.</template>
            <template #bottom-right>Link here?</template>
          </MListItem>
          </template>
          <p>Details for item 2</p>
        </MCollapse>
      </div>
    </div>
    `,
  }),
  args: {
    extraMListItemMargin: true,
  },
}

/**
 * Use the `variant` prop to change the hover and active state color of the header.
 * This defaults to `neutral`.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const Variants = (args: any) => ({
  components: { MCollapse },
  setup() {
    return { args }
  },
  template: `
    <div class="tw-space-y-2">
    <MCollapse v-bind="args">
        <template #header>
          <span>Default/Neutral</span>
        </template>
        <p>This content goes into the body.</p>
      </MCollapse>
      <MCollapse v-bind="args" variant="primary">
        <template #header>
          <span>Primary</span>
        </template>
        <p>This content goes into the body.</p>
      </MCollapse>
      <MCollapse v-bind="args" variant="success">
        <template #header>
          <span>Success</span>
        </template>
        <p>This content goes into the body.</p>
      </MCollapse>
      <MCollapse v-bind="args" variant="warning">
        <template #header>
          <span>Warning</span>
        </template>
        <p>This content goes into the body.</p>
      </MCollapse>
      <MCollapse v-bind="args" variant="danger">
        <template #header>
          <span>Danger</span>
        </template>
        <p>This content goes into the body.</p>
      </MCollapse>
    </div>
  `,
})
