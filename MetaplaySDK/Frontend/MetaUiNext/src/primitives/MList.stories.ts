import type { Meta, StoryObj } from '@storybook/vue3'

import MTwoColumnLayout from '../layouts/MTwoColumnLayout.vue'
import MCard from '../primitives/MCard.vue'
import MList from '../primitives/MList.vue'
import MListItem from '../primitives/MListItem.vue'

const meta: Meta<typeof MList> = {
  component: MList,
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
          'The `MList` is a wrapper component designed to neatly display a series of related content. Use this component to create a simple text list or a custom list with varied content, such as titles, descriptions, images, links, etc',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MList>

/**
 * By default all items are automatically displayed vertically i.e from top to bottom.
 */
export const Default: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <div class="tw-@container">
      <MTwoColumnLayout>
        <MCard title="Example List" noBodyPadding>
          <MList v-bind="args">
            <MListItem>
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 3
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 4
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>
      </MTwoColumnLayout>
    </div>
    `,
  }),
}

/**
 * To override the default vertical direction, set the `horizontal` prop to true.
 * This will display the list items horizontally i.e left to right and neatly
 * wraps to the next line when the items overflow the list container.
 */
export const HorizontalList: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
      MList,
    },
    setup: () => ({ args }),
    template: `
      <div class="tw-@container">
        <MTwoColumnLayout>
          <MCard title="Example Horizontal List" noBodyPadding>
            <MList v-bind="args">
              <div style="padding: 15px; border: solid blue 2px;">Card A</div>
              <div style="padding: 15px; border: solid blue 2px;">Card B</div>
              <div style="padding: 15px; border: solid blue 2px;">Card C</div>
              <div style="padding: 15px; border: solid blue 2px;">Card D</div>
              <div style="padding: 15px; border: solid blue 2px;">Card E</div>
              <div style="padding: 15px; border: solid blue 2px;">Card F</div>
              <div style="padding: 15px; border: solid blue 2px;">Card G</div>
              <div style="padding: 15px; border: solid blue 2px;">Card H</div>
            </MList>
          </MCard>
        </MTwoColumnLayout>
      </div>
    `,
  }),
  args: {
    horizontal: true,
  },
}

/**
 * All lists include a top border above each child element (except for the first element) as a visual indicator to separate list items as shown in the `default` list.
 * By default, the parent list container has no border however, you can add a border around your list by setting the `showBorder` prop to true.
 * For example use the bordered list to visually seperate list content from other related content.
 */
export const WithBorder: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <div class="tw-@container">
      <MTwoColumnLayout>
        <MCard title="Example Bordered List">
          <MList v-bind="args">
            <MListItem>
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 3
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 4
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>
      </MTwoColumnLayout>
    </div>
    `,
  }),
  args: {
    showBorder: true,
  },
}

/**
 * The list also includes a light grey border around it. You can set an alternative variant color
 * to the list border by setting the variant prop to one of the following values:'primary', 'success', 'danger', 'warning'.
 *
 * Note: This works best when the underlying background color also has a variant color. The `variant` prop in the `MList`
 * component will only change the border color of the list.
 */
export const VariantList: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <div class="tw-@container">
      <MTwoColumnLayout>
        <MCard title="Example Primary List">
          <MList v-bind="args" variant="primary">
            <MListItem variant="primary">
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem variant="primary">
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>

        <MCard title="Example Success List" variant="success">
          <MList v-bind="args" variant="success">
            <MListItem>
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>

        <MCard title="Example Warning List" variant="warning">
          <MList v-bind="args" variant="warning">
            <MListItem>
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>

        <MCard title="Example Danger List" variant="danger">
          <MList v-bind="args" variant="danger">
            <MListItem>
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>

      </MTwoColumnLayout>
    </div>
    `,
  }),
  args: {
    showBorder: true,
  },
}

/**
 * Striped list includes a light grey background on alternate rows.
 * This gives a nice visual separation between each item in the list and makes it easier to read.
 */
export const StripedList: Story = {
  render: (args) => ({
    components: {
      MTwoColumnLayout,
      MCard,
      MList,
      MListItem,
    },
    setup: () => ({ args }),
    template: `
    <div class="tw-@container">
      <MTwoColumnLayout>
        <MCard title="Example Striped List" noBodyPadding>
          <MList v-bind="args">
            <MListItem>
              Item 1
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 2
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 3
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
            <MListItem>
              Item 4
              <template #top-right>Something small</template>
              <template #bottom-left>Lorem ipsum dolor sit amet.</template>
              <template #bottom-right>Link here?</template>
            </MListItem>
          </MList>
        </MCard>
      </MTwoColumnLayout>
    </div>
    `,
  }),
  args: {
    striped: true,
  },
}
