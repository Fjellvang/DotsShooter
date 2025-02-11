<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-activables-base-card(
  :hideDisabled="hideDisabled"
  :hideConversion="hideConversion"
  :activables="activables"
  :playerId="playerId"
  category="OfferGroup"
  :longList="longList"
  :emptyMessage="emptyMessage"
  :title="title"
  :hideCollapse="hideCollapse"
  linkUrlPrefix="/offerGroups"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  permission="api.activables.view"
  @refreshActivables="offersRefresh"
  )
  template(#displayShortInfo="{ item: activableInfo }")
    | (
    span(:class="activableInfo.activable.config.displayShortInfo.startsWith('0') ? 'text-warning' : 'text-muted'") {{ activableInfo.activable.config.displayShortInfo }}
    | )

  template(#additionalTexts="{ item: activableInfo }")
    div(v-if="!hidePlacement") Placement: {{ activableInfo.activable.config.placement }}
    div(v-if="!hidePriority") Priority:
      meta-ordinal-number(
        v-if="activableInfo.activable.config.priority > 0"
        :number="activableInfo.activable.config.priority"
        class="ml-1"
        )
      span(
        v-else
        class="ml-1"
        ) {{ activableInfo.activable.config.priority }}
    div(v-if="!hideRevenue") Revenue: ${{ activableInfo.activable.revenue.toFixed(2) }}

  template(#collapseContents="{ item: activableInfo }")
    MListItem(condensed) Next Schedule Phase
      template(#top-right)
        span(
          v-if="!activableInfo.activable.config.activableParams.isEnabled"
          class="text-muted tw-italic"
          ) Disabled
        span(v-else-if="activableInfo.nextPhase") #[meta-activable-phase-badge(:activable="activableInfo.activable" :phase="activableInfo.nextPhase" :playerId="playerId" :typeName="`${categoryDisplayName?.toLocaleLowerCase()}`")] #[meta-time(v-if="activableInfo.nextPhaseStartTime" :date="activableInfo.nextPhaseStartTime")]
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
    MListItem(condensed) Individual Offers
      template(#top-right)
        div(
          v-for="(offer, key) in activableInfo.activable.offers"
          :key="key"
          )
          | {{ offer.config.displayName }} #[span(v-if="offer.referencePrice !== null" class="text-muted") ({{ offer.referencePrice }})] - #[meta-plural-label(:value="offer.state.numPurchasedInGroup" label="purchase")]
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getStaticConfigSubscriptionOptions } from '../../subscription_options/general'
import {
  getAllOffersSubscriptionOptions,
  getAllOffersForPlayerSubscriptionOptions,
} from '../../subscription_options/offers'
import MetaActivablePhaseBadge from '../activables/MetaActivablePhaseBadge.vue'
import MetaActivablesBaseCard from '../activables/MetaActivablesBaseCard.vue'

const props = defineProps<{
  /**
   * Title to be shown on the card.
   */
  title: string
  /**
   * Custom message to be shown when there are no offers available to display.
   */
  emptyMessage: string
  /**
   * Optional: Don't display offers that are disabled.
   */
  hideDisabled?: boolean
  /**
   * Optional: Don't show conversion rates for offers.
   */
  hideConversion?: boolean
  /**
   * Optional: Don't show placement information for offers.
   */
  hidePlacement?: boolean
  /**
   * Optional: Don't show priority information for offers.
   */
  hidePriority?: boolean
  /**
   * Optional: Don't show revenue information for offers.
   */
  hideRevenue?: boolean
  /**
   * Optional: The ID of the player whose player-specific offer data we are interested in.
   */
  playerId?: string
  /**
   * Optional: Custom time other than the current time used to fetch offer data for that specific time.
   */
  customEvaluationIsoDateTime?: string
  /**
   * Optional: Only show offers that match this placement.
   */
  placement?: string
  /**
   * Optional: Show ore items on the list card.
   */
  longList?: boolean
  /**
   * Optional: Hide extra information that's available by expanding the card item.
   */
  hideCollapse?: boolean
  /**
   * Optional: Default sort option.
   */
  defaultSortOption?: number
}>()

/**
 * Fetch static config data.
 */
const { data: staticConfig } = useSubscription(getStaticConfigSubscriptionOptions())

/**
 * Get global activables data.
 */
const activablesMetadata = computed(() => {
  return staticConfig.value?.activablesMetadata
})

/**
 * Get human readable category name.
 */
const categoryDisplayName = computed(() => {
  return activablesMetadata.value?.categories.OfferGroup.shortSingularDisplayName
})

/**
 * Subscribe to offers. Resubscribe whenever source data changes.
 */
const { data: offersData, refresh: offersRefresh } = useSubscription(() => {
  if (props.playerId) {
    return getAllOffersForPlayerSubscriptionOptions(props.playerId, props.customEvaluationIsoDateTime)
  } else {
    return getAllOffersSubscriptionOptions(props.customEvaluationIsoDateTime)
  }
})

/**
 * Extract relevant activables.
 */
const activables = computed((): any => {
  if (offersData.value) {
    const activables = Object.fromEntries(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      Object.entries(offersData.value.offerGroups).filter(
        ([key, value]: any) => !props.placement || value.config.placement === props.placement
      )
    )
    return {
      OfferGroup: {
        activables,
      },
    }
  } else {
    return null
  }
})

/**
 * Search fields for the card.
 */
const searchFields = ['activable.config.displayName', 'activable.config.description', 'phaseDisplayString']

/**
 * Sort options for the card.
 */
const sortOptions = computed(() => {
  const sortOptions = [
    MetaListSortOption.asUnsorted(),
    new MetaListSortOption('Priority', 'activable.config.priority', MetaListSortDirection.Ascending),
    new MetaListSortOption('Priority', 'activable.config.priority', MetaListSortDirection.Descending),
    new MetaListSortOption('Phase', 'phaseSortOrder', MetaListSortDirection.Ascending),
    new MetaListSortOption('Phase', 'phaseSortOrder', MetaListSortDirection.Descending),
    new MetaListSortOption('Name', 'activable.config.displayName', MetaListSortDirection.Ascending),
    new MetaListSortOption('Name', 'activable.config.displayName', MetaListSortDirection.Descending),
  ]
  if (!props.hideConversion) {
    sortOptions.push(new MetaListSortOption('Conversion', 'conversion', MetaListSortDirection.Ascending))
    sortOptions.push(new MetaListSortOption('Conversion', 'conversion', MetaListSortDirection.Descending))
  }
  if (!props.hideRevenue) {
    sortOptions.push(new MetaListSortOption('Revenue', 'activable.revenue', MetaListSortDirection.Ascending))
    sortOptions.push(new MetaListSortOption('Revenue', 'activable.revenue', MetaListSortDirection.Descending))
  }

  return sortOptions
})
</script>
