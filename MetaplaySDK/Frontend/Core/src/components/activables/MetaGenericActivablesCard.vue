<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-activables-base-card(
  :hideDisabled="hideDisabled"
  :hideConversion="hideConversion"
  :activables="activablesData"
  :playerId="playerId"
  :category="category"
  :longList="longList"
  :emptyMessage="emptyMessage"
  :title="title"
  :hideCollapse="hideCollapse"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  permission="api.activables.view"
  @refreshActivables="activablesRefresh"
  )
  template(#collapseContents="{ item: activableInfo }")
    MList(striped)
      MListItem(condensed) Next Schedule Phase
        template(#top-right)
          span(
            v-if="!activableInfo.activable.config.activableParams.isEnabled"
            class="text-muted tw-italic"
            ) Disabled
          span(
            v-else-if="activableInfo.activable.debugState"
            class="text-warning"
            ) Debug override!
          span(v-else-if="activableInfo.nextPhase") #[meta-activable-phase-badge(:activable="activableInfo.activable" :phase="activableInfo.nextPhase" :playerId="playerId" :typeName="`${categoryDisplayName?.toLocaleLowerCase()}`")] #[meta-time(:date="String(activableInfo.nextPhaseStartTime)")]
          span(
            v-else-if="activableInfo.scheduleStatus"
            class="text-muted tw-italic"
            ) None
          span(
            v-else
            class="text-muted tw-italic"
            ) No schedule
      MListItem(condensed) Lifetime Left
        template(#top-right)
          meta-duration(
            v-if="activableInfo.activable.state.hasOngoingActivation && activableInfo.activable.state.activationRemaining !== null"
            :duration="activableInfo.activable.state.activationRemaining"
            showAs="humanizedSentenceCase"
            )
          span(
            v-else-if="activableInfo.activable.state.hasOngoingActivation"
            class="text-muted tw-italic"
            ) Forever
          span(
            v-else
            class="text-muted tw-italic"
            ) n/a
      MListItem(condensed) Cooldown Left
        template(#top-right)
          meta-duration(
            v-if="activableInfo.activable.state.isInCooldown && activableInfo.activable.state.cooldownRemaining !== null"
            :duration="activableInfo.activable.state.cooldownRemaining"
            showAs="humanizedSentenceCase"
            )
          span(
            v-else-if="activableInfo.activable.state.isInCooldown"
            class="text-muted tw-italic"
            ) Forever
          span(
            v-else
            class="text-muted tw-italic"
            ) n/a
      MListItem(condensed) Activations
        template(#top-right) {{ activableInfo.activable.state.numActivated }}/{{ activableInfo.activable.config.activableParams.maxActivations || '&#8734;' }}
      MListItem(condensed) Consumes During Current Activation
        template(#top-right)
          span(v-if="activableInfo.activable.state.hasOngoingActivation") {{ activableInfo.activable.state.currentActivationNumConsumed }}/{{ activableInfo.activable.config.activableParams.maxConsumesPerActivation || '&#8734;' }}
          span(
            v-else
            class="text-muted tw-italic"
            ) n/a
      MListItem(condensed) Total Consumes
        template(#top-right) {{ activableInfo.activable.state.totalNumConsumed }}/{{ activableInfo.activable.config.activableParams.maxTotalConsumes || '&#8734;' }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListSortOption, MetaListSortDirection } from '@metaplay/meta-ui'
import { MList, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import {
  getAllActivablesForPlayerSubscriptionOptions,
  getAllActivablesSubscriptionOptions,
} from '../../subscription_options/activables'
import { getStaticConfigSubscriptionOptions } from '../../subscription_options/general'
import MetaActivablePhaseBadge from './MetaActivablePhaseBadge.vue'
import MetaActivablesBaseCard from './MetaActivablesBaseCard.vue'

const props = defineProps<{
  /**
   * Title to be shown on the card.
   */
  title: string
  /**
   * Group name assigned to similar activables.
   * @example 'In-game events'
   */
  category: string
  /**
   * Custom message to be shown when there are no activables available to display.
   */
  emptyMessage: string
  /**
   * Optional: Custom time other than the current time used to fetch activiable data for that specific time.
   */
  customEvaluationIsoDateTime?: string
  /**
   * Optional: Id of the player whose data we are interested in.
   */
  playerId?: string
  /**
   * Optional: If true hide disabled activable data.
   */
  hideDisabled?: boolean
  /**
   * Optional: If true hide activable conversion statistics.
   */
  hideConversion?: boolean
  /**
   * Optional: If true show the 50 list items on one page.
   * Defaults 8.
   */
  longList?: boolean
  /**
   * Optional: If true renders a non-collapsible list card.
   */
  hideCollapse?: boolean
}>()

/**
 * Subscribe to data needed to render this component.
 */
const { data: staticConfigData, refresh: activablesRefresh } = useSubscription(getStaticConfigSubscriptionOptions())

const { data: activablesData } = useSubscription(() => {
  if (props.playerId) {
    return getAllActivablesForPlayerSubscriptionOptions(props.playerId, props.customEvaluationIsoDateTime)
  } else {
    return getAllActivablesSubscriptionOptions(props.customEvaluationIsoDateTime)
  }
})

/**
 *  Additional data about the activable.
 */
const activablesMetadata = computed(() => {
  return staticConfigData.value?.activablesMetadata
})

/**
 * The activable category name to be displayed.
 */
const categoryDisplayName = computed(() => {
  return activablesMetadata.value?.categories[props.category].shortSingularDisplayName
})

/**
 * Search fields array to be passed to the meta-list-card component.
 */
const searchFields = [
  'activable.config.displayName',
  'activable.config.displayShortInfo',
  'activable.config.description',
  'phaseDisplayString',
]

/**
 * Sort options array to be passed to the meta-list-card component.
 */
const sortOptions = computed(() => {
  const sortOptions = [
    MetaListSortOption.asUnsorted(),
    new MetaListSortOption('Phase', 'phaseSortOrder', MetaListSortDirection.Ascending),
    new MetaListSortOption('Phase', 'phaseSortOrder', MetaListSortDirection.Descending),
    new MetaListSortOption('Name', 'activable.config.displayName', MetaListSortDirection.Ascending),
    new MetaListSortOption('Name', 'activable.config.displayName', MetaListSortDirection.Descending),
  ]
  if (!props.hideConversion) {
    sortOptions.push(new MetaListSortOption('Conversion', 'conversion', MetaListSortDirection.Ascending))
    sortOptions.push(new MetaListSortOption('Conversion', 'conversion', MetaListSortDirection.Descending))
  }
  return sortOptions
})
</script>
