<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Tab navigation container.
div(
  class="tw-sticky tw-z-10 -tw-mx-1 tw-mb-10 tw-hidden tw-flex-row tw-flex-wrap tw-items-center tw-justify-center tw-space-x-1 tw-border-b tw-border-t tw-border-neutral-200 tw-bg-white tw-py-1 @sm:-tw-mx-4 @md:tw-flex"
  style="top: -1px"
  :data-testid="id"
  )
  //- No permission tooltip.
  MTooltip(
    v-for="(tab, index) in tabs"
    :key="index"
    :content="getTabDisabledReason(tab)"
    no-underline
    :class="{ 'tw-cursor-not-allowed': !!getTabDisabledReason(tab) }"
    :data-testid="`${id}-${index}-tooltip`"
    )
    //- Tab button.
    button(
      role="tab"
      class="tw-relative tw-rounded tw-px-4 tw-py-2 tw-font-semibold"
      :class="{ ['tw-bg-blue-500 tw-text-white']: internalCurrentTab === index && !getTabDisabledReason(tab), ['tw-text-blue-500 hover:tw-text-blue-600 active:tw-text-blue-700 hover:tw-bg-neutral-300 active:tw-bg-neutral-400']: internalCurrentTab !== index, ['tw-bg-neutral-200 tw-text-neutral-400 tw-pointer-events-none']: !!getTabDisabledReason(tab) }"
      :disabled="!!getTabDisabledReason(tab)"
      :aria-selected="internalCurrentTab === index"
      @click="selectTab(index)"
      :data-testid="`${id}-${index}`"
      ) {{ tab.label }}
      //- Highlighted dot with an exclamation mark inside.
      div(
        v-if="tab.highlighted"
        class="tw-absolute tw-right-1 tw-top-1 tw-size-3 tw-rounded-full tw-bg-white"
        :data-testid="`${id}-${index}-highlighted`"
        )
      svg(
        v-if="tab.highlighted"
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 20 20"
        fill="currentColor"
        class="tw-absolute tw-right-0.5 tw-top-0.5 tw-size-4"
        :class="{ 'tw-fill-red-500': internalCurrentTab !== index, 'tw-fill-red-600': internalCurrentTab === index }"
        )
        path(
          fill-rule="evenodd"
          d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-8-5a.75.75 0 0 1 .75.75v4.5a.75.75 0 0 1-1.5 0v-4.5A.75.75 0 0 1 10 5Zm0 10a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z"
          clip-rule="evenodd"
          )

//- Dropdown for narrow screens.
MInputSimpleSelectDropdown(
  label="Current Tab"
  :model-value="internalCurrentTab"
  :options="tabOptions"
  class="tw-mb-10 tw-block @md:tw-hidden"
  @update:model-value="(event) => selectTab(Number(event))"
  :data-testid="id"
  )

//- Tab content.
div(
  class="tw-mx-auto tw-min-h-96 tw-max-w-6xl tw-@container"
  :data-testid="`${id}-content`"
  )
  //- Missing permission callout.
  MSingleColumnLayout(v-if="!doesHavePermission(tabs[internalCurrentTab].permission)")
    MCallout(
      title="Not Authorized"
      class="tw-mx-auto tw-my-3 tw-max-w-xl"
      )
      //- Note: Ugly expression to work around Pug parser limitations when trying to interpolate this string.
      p Unfortunately, you do not have the
        |
        |
        MBadge(variant="warning") {{ tabs[internalCurrentTab].permission }}
        |
        | permission to view this tab. Please contact your game admin for access.

  //- Slot for tab content.
  //- TODO: Consider for to cache the slot content to avoid re-rendering when switching tabs.
  slot(
    v-else
    :key="internalCurrentTab"
    :name="`tab-${internalCurrentTab}`"
    )
    //- Missing content callout.
    MSingleColumnLayout
      MCallout(
        :title="`${tabs[internalCurrentTab].label} is empty`"
        variant="neutral"
        class="tw-mx-auto tw-max-w-xl"
        data-testid="default-content"
        )
        p Get going by adding content to the
          |
          |
          MBadge {{ tabs[internalCurrentTab].label }}
          |
          | template slot.
        pre(class="tw-mt-2 tw-rounded tw-border tw-border-neutral-300 tw-bg-neutral-50 tw-p-2 tw-text-xs")
          code {{ getExampleCodeForTab(internalCurrentTab) }}
</template>

<script lang="ts" setup>
import { ref, onMounted, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { usePermissions } from '../composables/usePermissions'
import MBadge from '../primitives/MBadge.vue'
import MCallout from '../primitives/MCallout.vue'
import MTooltip from '../primitives/MTooltip.vue'
import MInputSimpleSelectDropdown from '../unstable/MInputSimpleSelectDropdown.vue'
import MSingleColumnLayout from './MSingleColumnLayout.vue'

const route = useRoute()
const router = useRouter()

const { doesHavePermission } = usePermissions()

export interface TabOption {
  /**
   * The label of the tab. Keep it short and descriptive.
   */
  label: string
  /**
   * Optional: The permission required to view this tab. If the user does not have this permission, the tab button will be disabled.
   * @example 'api.database.status'
   */
  permission?: string
  /**
   * Optional: Whether the tab is disabled. Pass in a string to show a tooltip on hover.
   */
  disabledTooltip?: string
  /**
   * Optional: Whether the tab is highlighted with a little red exclamation mark. Defaults to false.
   */
  highlighted?: boolean
}

const props = withDefaults(
  defineProps<{
    /**
     * The list of tabs to display.
     */
    tabs: TabOption[]
    /**
     * Optional: The currently selected tab index. Defaults to 0.
     */
    currentTab?: number
    /**
     * Optional: A unique ID for this tab layout. You can have multiple tab layouts on the same page if you provide a unique ID. Defaults to "tab".
     */
    id?: string
  }>(),
  {
    currentTab: 0,
    id: 'tab',
  }
)

const emit = defineEmits<{
  onTabChanged: [value: number]
}>()

// Internal state and route query handling ----------------------------------------------------------------------------

/**
 * Get the reason why a tab is disabled.
 * @param tab The tab to check.
 * @returns The reason why the tab is disabled, if it is disabled. Otherwise, undefined.
 */
function getTabDisabledReason(tab: TabOption): string | undefined {
  return doesHavePermission(tab.permission) ? tab.disabledTooltip : `You need ${tab.permission} to view this tab.`
}

/**
 * Update both the route query and the current tab to ensure consistency between the URL and this component's internal state.
 * @param newTabIndex The new tab index that is selected.
 */
async function selectTab(newTabIndex: number): Promise<void> {
  await updateRouteQuery(newTabIndex)
  // Note: This is needed in storybook as the page doesn't re-mount when switching tabs.
  internalCurrentTab.value = newTabIndex

  // Let the parent know about the new tab.
  emit('onTabChanged', newTabIndex)
}

/**
 * Reactive reference holding the index of the currently selected tab from the route query. Undefined if the tab is invalid.
 */
const routeQuerySelectedTab = computed((): number | undefined => {
  if (!route.query[props.id]) {
    return undefined
  }

  if (typeof route.query[props.id] === 'string') {
    const tabIndex = Number(route.query[props.id])
    if (!isTabIndexOutOfBounds(tabIndex)) {
      return tabIndex
    }
  }

  console.warn(`Invalid route query tab parameter: ${JSON.stringify(route.query[props.id])}.`)
  return undefined
})

/**
 * Get the initial tab index from the route query or the component's initial tab prop.
 * @returns The initial tab index.
 */
function getInitialTab(): number {
  if (routeQuerySelectedTab.value) {
    return routeQuerySelectedTab.value
  }

  // Check if the initial tab index is out of bounds.
  if (isTabIndexOutOfBounds(props.currentTab)) {
    console.warn(`Invalid initial tab index: ${props.currentTab}.`)
    return 0
  }

  let initialTab
  // Check if the current tab is disabled. If it is, select the first tab that is not disabled.
  if (!props.tabs[props.currentTab].disabledTooltip) {
    initialTab = props.currentTab
  } else {
    initialTab = props.tabs.findIndex((tab) => !getTabDisabledReason(tab))

    if (initialTab === -1) {
      console.warn('All tabs are disabled. Falling back to showing the first tab.')
      initialTab = 0
    }
  }

  return initialTab
}

/**
 * Helper function to validate a tab index.
 * @param tabIndex The index to check.
 * @returns True if the index is out of bounds, false otherwise.
 */
function isTabIndexOutOfBounds(tabIndex: number): boolean {
  return tabIndex < 0 || tabIndex >= props.tabs.length
}

/**
 * Reactive reference holding the index of the currently active tab.
 */
const internalCurrentTab = ref<number>(getInitialTab())

watch(
  () => props.currentTab,
  (newTabIndex) => {
    if (isTabIndexOutOfBounds(newTabIndex)) {
      return
    }

    void selectTab(newTabIndex)
  }
)

// If the current tab is not in the route query, update the route query with the initial tab.
onMounted(async () => {
  if (!routeQuerySelectedTab.value) {
    await updateRouteQuery(internalCurrentTab.value)
  }

  // Let the parent know about the initial tab.
  emit('onTabChanged', internalCurrentTab.value)
})

/**
 * Update the tab parameter of the route query with the new tab index.
 * @param newTabIndex The index of the new tab.
 */
async function updateRouteQuery(newTabIndex: number): Promise<void> {
  // NOTE: This can race with other route changes on the same tick. Not an issue atm but revisit the approach if it becomes one.
  await router.replace({
    path: route.path,
    query: { ...route.query, [props.id]: newTabIndex.toString() },
  })
}

// Misc ---------------------------------------------------------------------------------------------------------------

/**
 * Tab options for the `MInputSingleSelectDropdown` component.
 */
const tabOptions = computed(() =>
  props.tabs.map((tab, index) => ({
    label: tab.label,
    value: index,
    disabled: !!getTabDisabledReason(tab),
  }))
)

function getExampleCodeForTab(tabIndex: number): string {
  return `MTabLayout(:tabs="tabs")
  template(#tab-${tabIndex})
    //- Your content here`
}
</script>
