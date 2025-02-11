// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { vueRouter } from 'storybook-vue3-router'
import { ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../composables/usePermissions'
import MPageOverviewCard from '../composites/MPageOverviewCard.vue'
import MCard from '../primitives/MCard.vue'
import MList from '../primitives/MList.vue'
import MListItem from '../primitives/MListItem.vue'
import MRootLayout from './MRootLayout.vue'
import MSingleColumnLayout from './MSingleColumnLayout.vue'
import MTabLayout from './MTabLayout.vue'
import MTwoColumnLayout from './MTwoColumnLayout.vue'
import MViewContainer from './MViewContainer.vue'

const meta: Meta<typeof MTabLayout> = {
  component: MTabLayout,
  tags: ['autodocs'],
  parameters: {
    viewport: {
      defaultViewport: 'twoColumn',
    },
    docs: {
      description: {
        component:
          'The `MTabLayout` is a layout component that helps break a long page into separate sub-pages with a tab navigation. This component is designed to be used within the `MViewContainer` layout component.',
      },
    },
  },
  // Note: The `vueRouter` settings below do not integrate well, e.g. changing path outputs to the default '/' no matter what.
  decorators: [vueRouter([{ path: '/', name: 'tabs', component: MTabLayout }])],
  render: (args) => ({
    components: { MTabLayout },
    setup: () => {
      return { args }
    },
    template: `
      <div>
        <MTabLayout v-bind="args">
        </MTabLayout>
      </div>
    `,
  }),
  argTypes: {
    currentTab: {
      // Storybook does not handle the runtime error catching for initialTab correctly, manually limiting it to 0 and 1.
      control: { type: 'number', min: 0 },
    },
  },
}

export default meta
type Story = StoryObj<typeof MTabLayout>

/**
 * A contextual showcase of the `MTabLayout` component in a real-world scenario.
 *
 * At it's simplest, you pass a list of tab options to the `tabs` prop of `MTabLayout` to create tabs and then use the `tab-${index}` slots to place content inside the tabs.
 */
export const Default: Story = {
  args: {
    tabs: [{ label: 'Example Tab 1' }, { label: 'Example Tab 2' }],
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MRootLayout,
      MViewContainer,
      MPageOverviewCard,
      MTwoColumnLayout,
      MCard,
      MList,
      MListItem,
    },
    setup: () => {
      const listItems = [
        {
          title: 'Item 1',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
        {
          title: 'Item 2',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
        {
          title: 'Item 3',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
        {
          title: 'Item 4',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
        {
          title: 'Item 5',
          bottomLeft: 'Lorem ipsum dolor sit amet.',
          bottomRight: 'Link here?',
        },
      ]
      return { args, listItems }
    },
    template: `
    <MViewContainer>
      <template #overview>
        <MPageOverviewCard title="MTabLayout Example">
          Here you can see how the tabs might be used inside an MViewContainer to not have too many cards in a single page. A good rule of thumb is to have a maximum of 6 cards in a tab.
          </MPageOverviewCard>
      </template>
      <MTabLayout v-bind="args">
        <template #tab-0>
          <MTwoColumnLayout>
            <MCard v-for="i in [1, 2, 3, 4]" noBodyPadding title="Example Content">
              <MList>
                <MListItem v-for="(item, index) in listItems" >
                  {{ item.title }}
                  <template #top-right>{{ item.topRight }}</template>
                  <template #bottom-left>{{ item.bottomLeft }}</template>
                  <template #bottom-right>{{ item.bottomRight }}</template>
                </MListItem>
              </MList>
            </MCard>
            <MCard title="Content Below the Fold">
              <p>This card is now hard to see since the list cards occupy so much vertical space. If this was also a list card, there would be more scrolling.</p>
              <p class="tw-mt-2">You should consider moving this content to its own tab.</p>
            </MCard>
          </MTwoColumnLayout>
        </template>
        <template #tab-1>
        </template>
      </MTabLayout>
    </MViewContainer>
    `,
  }),
}

/**
 * The rendering slots for each tab are dynamically generated. Each slot is named by the index of the tab, such as `tab-0`, `tab-1`, etc.
 *
 * In this example, we are using the `tab-0` slot to render content for the first tab.
 *
 * Note: Vue does not have a runtime warning for using slots that do not exist. If you use a slot that does not exist (for example `tab-3` in this story), it will not render anything.
 */
export const UsingIndexBasedSlots: Story = {
  args: {
    tabs: [{ label: 'tab-0' }, { label: 'tab-1' }, { label: 'tab-2' }],
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      return { args }
    },
    template: `
      <div>
        <MTabLayout v-bind="args">
          <template #tab-0>
            <MSingleColumnLayout>
              <MCard title="template(#tab-0) Card">
                <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
          <template #tab-3>
            <MSingleColumnLayout>
              <MCard title="template(#tab-3) Card">
                <p>This content will not be visible</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
      </div>
    `,
  }),
}

/**
 * The `MTabLayout` component allows you to control active tab using the `initialTab` prop. `tab-0` is the default tab.
 *
 * This is useful when you want to focus attention to a specific tab after some async page data loads.
 */
export const InitialTab: Story = {
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
    currentTab: 1,
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      return { args }
    },
    template: `
      <div>
        <MTabLayout v-bind="args">
          <template #tab-1>
            <MSingleColumnLayout>
              <MCard title="Tab 1 Card">
                <p>The initial tab is set to tab 1, instead of the default 0.</p>
                <p class="tw-mt-2">Test this by changing the props in Storybook.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
      </div>
    `,
  }),
}

/**
 * You can highlight tabs by setting the `highlighted` property in the `tabOption`s. This is intended for calling attention to errors or similar high-priority tabs.
 */
export const TabHighlighting: Story = {
  args: {
    tabs: [{ label: 'Tab 0', highlighted: true }, { label: 'Tab 1' }, { label: 'Tab 2', highlighted: true }],
  },
}

/**
 * The `MTabLayout` component supports programmatic switching of tabs using the `currentTab` prop. You can use this to intelligently switch to a tab after some async data loads.
 */
export const ProgrammaticSwitching: Story = {
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      const currentTab = ref(0)

      // Update the tab every two seconds.
      setInterval(() => {
        currentTab.value = currentTab.value === 2 ? 0 : currentTab.value + 1
      }, 2000)

      return { args, currentTab }
    },
    template: `
      <div>
        <MTabLayout v-bind="args" :currentTab="currentTab">
        </MTabLayout>
      </div>
    `,
  }),
}

/**
 * The tabs defined by the `tabs` prop will be rendered regardless of whether any content is inside the slots.
 *
 * Empty tabs have a callout to show that the tab system is working as intended. If you want to programmatically hide tabs, you should filter the `tabs` prop before passing it to the `MTabLayout` component.
 */
export const EmptyTabCallouts: Story = {
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
  },
}

/**
 * You can disable individual tabs by using the optional `disabledTooltip` prop in the `tabOption`s.
 *
 * Disabled tabs will show a tooltip when hovered over. You must provide a reason for the tab to be disabled so the user knows why they can't access it.
 */
export const DisabledTabs: Story = {
  args: {
    tabs: [
      { label: 'Disabled Tab 1', disabledTooltip: 'This tab is disabled for reasons.' },
      { label: 'Enabled Tab' },
      { label: 'Disabled Tab 2', disabledTooltip: 'This tab is disabled for other reasons.' },
      { label: 'Disabled Tab 3', disabledTooltip: 'Lorem ipsum dolor sit amet.' },
    ],
  },
}

/**
 * Having all tabs disabled is an unsupported edge case. You should always have at least one tab enabled.
 */
export const AllTabsDisabled: Story = {
  args: {
    tabs: [
      { label: 'Disabled Tab 1', disabledTooltip: 'This tab is disabled for reasons.' },
      { label: 'Disabled Tab 2', disabledTooltip: 'This tab is disabled for other reasons.' },
      { label: 'Disabled Tab 3', disabledTooltip: 'Lorem ipsum dolor sit amet.' },
    ],
  },
}

/**
 * The `MTabLayout` component supports permission checking using the optional `permission` prop in the `tabOption`s.
 *
 * Permissions are strings like `api.example.permission` that are checked against the user's permissions.
 *
 * For best results, consider grouping often disabled tabs towards the end.
 */
export const TabsWithPermissions: Story = {
  args: {
    tabs: [
      { label: 'Enabled Tab' },
      { label: 'No Permission 1', permission: 'api.example.permission1' },
      { label: 'No Permission 2', permission: 'api.example.permission2' },
      { label: 'Disabled Tab', disabledTooltip: 'This tab is disabled for reasons.' },
    ],
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      const permissions = usePermissions()
      permissions.setPermissions(['example-permission'])
      return { args }
    },
    template: `
      <div>
        <MTabLayout v-bind="args">
          <template #tab-0>
            <MSingleColumnLayout>
              <MCard title="Missing Permission Tab 1 Card">
                <p>Tabs get disabled with tooltips if you don't have the permissions.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
      </div>
    `,
  }),
}

/**
 * You can deep link to a specific tab by setting the `tab` query parameter in the URL.
 *
 * This will work even if the tab is disabled for any reason, such as missing permissions, so remember to always have something to show in the tab even if it is disabled.
 */
export const NoPermissionDeepLink: Story = {
  args: {
    tabs: [
      { label: 'Enabled Tab' },
      { label: 'No Permission', permission: 'api.example.permission2' },
      { label: 'Disabled Tab 1', disabledTooltip: 'This tab is disabled for reasons.' },
    ],
    currentTab: 1,
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      const permissions = usePermissions()
      permissions.setPermissions(['example-permission'])
      return { args }
    },
    template: `
      <div>
        <MTabLayout v-bind="args">
          <template #tab-1>
            <MSingleColumnLayout>
              <MCard title="Missing Permission Tab 1 Card">
                <p>This content will not be rendered.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
      </div>
    `,
  }),
}

/**
 * While the `MTabLayout` component can handle a large number of tabs, human can't. You should limit the number of tabs to 5 and use short labels.
 *
 * Overflow on the left and right sides can vary depending on the width of the viewport and the zoom levels of the browser.
 */
export const OverflowOfTabButtons: Story = {
  args: {
    tabs: [
      { label: 'Tab 0' },
      { label: 'Tab 1' },
      { label: 'Tab 2' },
      { label: 'Tab 3' },
      { label: 'Tab 4' },
      { label: 'Tab 5' },
      { label: 'Tab 6' },
      { label: 'Tab 7' },
      { label: 'Tab 8' },
      { label: 'Tab 9' },
      { label: 'Tab 10' },
      { label: 'Tab 11 pushing it?' },
      { label: 'Tab 12 too many?' },
      { label: 'Tab 13 breaking it' },
    ],
  },
}

/**
 * While the `MTabLayout` component can handle long tab labels, they will always be a bad idea and we don't recommend it.
 */
export const LongTabButtonText: Story = {
  args: {
    tabs: [
      { label: 'This is a very long label for tab 0' },
      { label: 'An even longer label that looks bad on most screens because it will wrap' },
      { label: 'LabelsWithoutAnyWhitespaceAreAlsoBad' },
    ],
  },
}

/**
 * The tabs in the `MTabLayout` component automatically swap to a dropdown when the viewport is narrow.
 */
export const DropdownOnNarrowScreens: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile',
    },
  },
  args: {
    tabs: [{ label: 'Tab 0' }, { label: 'Tab 1' }, { label: 'Tab 2' }],
  },
}

/**
 * Narrow screens will automatically switch to a dropdown for tab navigation and the dropdown options will be disabled if the tab is disabled.
 */
export const DisabledTabsNarrow: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile',
    },
  },
  args: {
    tabs: [
      { label: 'Enabled Tab' },
      { label: 'Disabled Tab 1', disabledTooltip: 'This tab is disabled for reasons.' },
      { label: 'Disabled Tab 2', disabledTooltip: 'Lorem ipsum dolor sit amet.' },
    ],
  },
}

/**
 * You can have multiple `MTabLayout` components on the same page if you give them a unique `id` prop.
 */
export const MultipleTabLayouts: Story = {
  args: {
    tabs: [{ label: 'tab-0' }, { label: 'tab-1' }, { label: 'tab-2' }],
  },
  render: (args) => ({
    components: {
      MTabLayout,
      MSingleColumnLayout,
      MCard,
    },
    setup: () => {
      return { args }
    },
    template: `
      <div>
        <MTabLayout v-bind="args" id="tab1">
          <template #tab-0>
            <MSingleColumnLayout>
              <MCard title="template(#tab-0) Card">
                <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
         <MTabLayout v-bind="args" id="tab2">
          <template #tab-0>
            <MSingleColumnLayout>
              <MCard title="template(#tab-0) Card">
                <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed euismod, nisl nec ultricies aliquam, nisl nisl aliquet nisl, eget aliquet nisl.</p>
              </MCard>
            </MSingleColumnLayout>
          </template>
        </MTabLayout>
      </div>
    `,
  }),
}
