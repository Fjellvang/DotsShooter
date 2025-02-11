<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Segments"
  icon="user-tag"
  :itemList="items"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :emptyMessage="`${ownerTitle} doesn't belong to any segments!`"
  :pageSize="8"
  permission="api.segmentation.view"
  data-testid="segments-card"
  )
  template(#item-card="{ item: segment }")
    MListItem {{ segment.info.displayName }}
      template(#top-right): meta-audience-size-estimate(:sizeEstimate="segment.sizeEstimate")
      template(#bottom-left) {{ segment.info.description }}
      template(#bottom-right): MTextButton(
        permission="api.segmentation.view"
        :to="`/segments/${segment.info.segmentId}`"
        ) View segment
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton, usePermissions } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getPlayerSegmentsSubscriptionOptions } from '../../subscription_options/general'
import MetaAudienceSizeEstimate from '../MetaAudienceSizeEstimate.vue'

const props = defineProps<{
  /**
   * A list of available segments.
   */
  segments?: string[]
  /**
   * Name of the parent component, shown when an item list is null or empty.
   */
  ownerTitle?: string
}>()

const permissions = usePermissions()

/**
 * Subscribe to data needed to render this component.
 */
const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

/**
 * List of all items in the player segements config.
 */
const items = computed(() => {
  if (!permissions.doesHavePermission('api.segmentation.view')) {
    return []
  } else if (playerSegmentsData.value?.segments == null) {
    // Still loading segment data..
    return undefined
  } else if (!props.segments) {
    // Owner is still loading segment list..
    return []
  } else {
    // Map segments to segment infos
    return props.segments.map((segment: any) =>
      playerSegmentsData.value.segments.find((playerSegment: any) => playerSegment.info.segmentId === segment)
    )
  }
})

/**
 * Search fields to be passed to the meta-list-card.
 */
const searchFields = ['info.displayName', 'info.description']

/**
 * Sort options array to be passed to the meta-list-card component.
 */
const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Name', 'info.displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'info.displayName', MetaListSortDirection.Descending),
]
</script>
