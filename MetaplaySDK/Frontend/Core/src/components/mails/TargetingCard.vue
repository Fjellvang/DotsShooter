<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  v-if="playerSegmentsData"
  :title="`Segments: match ${anyOrAll}`"
  icon="user-tag"
  :itemList="items"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :emptyMessage="`${ownerTitle} has no targeting.`"
  :pageSize="8"
  permission="api.segmentation.view"
  data-testid="targeting-card"
  )
  template(#item-card="slotProps")
    MListItem(v-if="!slotProps.item.missing") {{ slotProps.item.info.displayName }}
      template(#top-right): meta-audience-size-estimate(
        :sizeEstimate="slotProps.item.sizeEstimate"
        small
        )
      template(#bottom-left) {{ slotProps.item.info.description }}
      template(#bottom-right): MTextButton(
        :to="`/segments/${slotProps.item.info.segmentId}`"
        permission="api.segmentation.view"
        ) View segment
    MListItem(v-else) {{ slotProps.item.missing.segment }}
      template(#bottom-left) This segment is missing in the currently active game config.
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton, usePermissions } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getPlayerSegmentsSubscriptionOptions } from '../../subscription_options/general'
import MetaAudienceSizeEstimate from '../MetaAudienceSizeEstimate.vue'
import type { TargetConditionContent } from './mailUtils'

// TODO: Evaluate component and consider refactoring it into a meta-component.
const props = withDefaults(
  defineProps<{
    targetCondition: TargetConditionContent | null
    ownerTitle?: string
  }>(),
  {
    ownerTitle: 'This entity',
  }
)

const permissions = usePermissions()
const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

const searchFields = ['info.displayName', 'info.description']
const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Name', 'info.displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'info.displayName', MetaListSortDirection.Descending),
]
const anyOrAll = computed(() => {
  return segmentAllList.value ? 'ALL' : 'ANY'
})
const segmentAllList = computed(() => {
  return props.targetCondition?.requireAllSegments
})
const segmentAnyList = computed(() => {
  return props.targetCondition?.requireAnySegment
})
const targetSegments = computed(() => {
  return segmentAllList.value ?? segmentAnyList.value
})

const items = computed(() => {
  if (!permissions.doesHavePermission('api.segmentation.view')) {
    return []
  } else if (!playerSegmentsData.value) {
    // Still loading segment data..
    return undefined
  } else if (!targetSegments.value) {
    // No targeting
    return []
  } else {
    // Map segments to segment infos
    return targetSegments.value.map((segment: string) => {
      let result = playerSegmentsData.value.segments.find(
        (playerSegment: any) => playerSegment.info.segmentId === segment
      )
      if (result === undefined) {
        result = { missing: { segment } }
      }
      return result
    })
  }
})
</script>
