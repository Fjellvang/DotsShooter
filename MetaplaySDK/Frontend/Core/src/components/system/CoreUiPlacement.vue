<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- Display all components that are registered to a given UiPlacement. -->

<template lang="pug">
div(
  class="tw-grid tw-grid-cols-1 tw-gap-3 lg:tw-grid-cols-2"
  :class="{ 'tw-gap-y-0': smallBottomMargin }"
  )
  //- Loop through all components that are registered to the given placement.
  //- Expand the component to full width if it is the last component or if it is set to full width.
  div(
    v-for="(placementInfo, index) in filteredUiComponents"
    :key="placementInfo.uniqueId"
    class="tw-@container"
    :class="{ 'tw-mb-1': smallBottomMargin, 'lg:tw-col-span-2': alwaysFullWidth || placementInfo.width === 'full' || shouldCenterComponent(index) }"
    )
    //- Use $attrs and $listeners to forward all props and listeners to components. This may change in Vue3.
    //- Also pass in any props that were defined with the placement.
    //- Sets a max width of 36rem (half of page container width) and centers the component if it's the last one and not set to full width.
    component(
      v-bind="Object.assign({}, $attrs, placementInfo.props)"
      :is="placementInfo.vueComponent"
      :class="{ 'lg:tw-max-w-[36rem] lg:tw-mx-auto': shouldCenterComponent(index) }"
      )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { usePermissions } from '@metaplay/meta-ui-next'

import { useCoreStore } from '../../coreStore'
import type { UiPlacement } from '../../integration_api/uiPlacementApis'

const props = defineProps<{
  /**
   * Name of the UI placement to be displayed.
   */
  placementId: UiPlacement
  alwaysFullWidth?: boolean
  smallBottomMargin?: boolean
}>()

const coreStore = useCoreStore()
const permissions = usePermissions()

/**
 * List of components that are registered to the given placement.
 * By default all components are visible to all users however,
 * components that have the 'displayPermission' property,
 * will only be visible to users with the required permission.
 */
const filteredUiComponents = computed(() => {
  return coreStore.uiComponents[props.placementId]?.filter((placementInfo) => {
    return permissions.doesHavePermission(placementInfo.displayPermission)
  })
})

/**
 * Helper function to determine if the component should be centered.
 * Checks all previous components for full vs half width to see if the current component is the only one on the row.
 */
function shouldCenterComponent(index: number): boolean {
  if (!filteredUiComponents.value) return false

  // Only care about the last component on the placement.
  if (index !== filteredUiComponents.value.length - 1) {
    return false
  }

  // Ignore if the component is set to full width.
  if (filteredUiComponents.value[index].width === 'full' || props.alwaysFullWidth) {
    return false
  }

  // Count the number of previous components. Full width components count as 2.
  let previousComponents = 0
  for (let i = 0; i < index; i++) {
    if (filteredUiComponents.value[i].width === 'full') {
      previousComponents += 2
    } else {
      previousComponents++
    }
  }

  // If the number of previous components is even, then the current component is the only one on the row.
  return previousComponents % 2 === 0
}
</script>

<style scoped>
@media (min-width: 576px) {
  .small-margin-between div + div {
    margin-top: 4px;
  }
}
</style>
