<template lang="pug">
meta-list-card(
  title="Variants"
  icon="table"
  tooltip="Variants configured for this experiment."
  :itemList="variants"
  :sortOptions="sortOptions"
  emptyMessage="No variants for this experiment!"
  class="h-100"
  )
  template(#item-card="{ item: variant }")
    MListItem {{ variant.id }}
      template(#badge)
        MBadge(
          v-if="variant.isConfigMissing"
          tooltip="This variant has been removed from the current game configs and can no longer be used."
          variant="danger"
          ) Missing
        MBadge(
          v-if="variant.weight === 0"
          tooltip="This variant has a weight of 0% and no players can enter it."
          variant="warning"
          ) No weight

      template(#top-right)
        span(v-if="variant.analyticsId") Analytics ID: #[span(class="tw-font-semibold") {{ variant.analyticsId }}]
        span(v-else) Analytics ID: #[span(class="tw-font-semibold tw-text-red-500") None]

      template(#bottom-left)
        meta-abbreviate-number(
          :value="variant.playerCount"
          unit="player"
          )
        |
        | currently in this group.

      template(#bottom-right)
        MTooltip(:content="variantRolloutTooltip(variant.weight)") {{ variant.weight }}% of total rollout
        div(
          v-if="variant.isConfigMissing || !experiment"
          class="tw-text-red-500"
          ) Config diff missing
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MTooltip, MBadge, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { calculatedAudienceSize } from '../../experimentUtils'
import { getSingleExperimentSubscriptionOptions } from '../../subscription_options/experiments'
import {
  getDatabaseItemCountsSubscriptionOptions,
  getGameDataSubscriptionOptions,
  getPlayerSegmentsSubscriptionOptions,
} from '../../subscription_options/general'

const props = defineProps<{
  experimentId: string
}>()

const { data: experimentInfoData } = useSubscription(getSingleExperimentSubscriptionOptions(props.experimentId))

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())

const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

const { data: databaseItemCountsData } = useSubscription(getDatabaseItemCountsSubscriptionOptions())

const experiment = computed(() => gameData.value?.serverGameConfig.PlayerExperiments[props.experimentId])

interface Variant {
  id: string
  weight: number
  diffParams: any
  playerCount: number
  isConfigMissing: boolean
  analyticsId: string
}

const variants = computed((): Variant[] => {
  const variants: Variant[] = [
    {
      id: 'Control',
      weight: experimentInfoData.value.state.controlWeight,
      diffParams: null,
      playerCount: numberOfPlayersInVariant('null'),
      isConfigMissing: false,
      analyticsId: experiment.value?.controlVariantAnalyticsId,
    },
  ]

  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  Object.entries(experimentInfoData.value.state.variants).forEach(([id, variant]) => {
    variants.push({
      id,
      weight: (variant as any).weight,
      diffParams: `newPatch=${props.experimentId}:${id}`,
      playerCount: numberOfPlayersInVariant(id),
      isConfigMissing: (variant as any).isConfigMissing,
      analyticsId: experiment.value?.variants[id]?.analyticsId,
    })
  })

  // Normalize weights
  const totalWeight = Object.values(variants).reduce((sum, variant) => sum + variant.weight, 0)
  Object.values(variants).forEach((variant) => {
    variant.weight = Math.round((variant.weight / totalWeight) * 100)
  })

  return variants
})

/**
 * Total number of players in the game. Returns 0 if that data isn't available yet.
 */
const totalPlayerCount = computed((): number => {
  return databaseItemCountsData.value?.totalItemCounts.Players || 0
})

/**
 * Cached lookup of calculated audience size.
 */
const cachedCalculatedAudienceSize = computed(() => {
  return calculatedAudienceSize(totalPlayerCount.value, experimentInfoData.value, playerSegmentsData.value)
})

/**
 * Return a percentage of the calculated audience size. Rounded for display.
 * @param percentageWeight Percentage to use.
 * @returns Rounded player count.
 */
function percentageOfCalculatedAudienceSize(percentageWeight: number): number {
  const audiencePlayerCount = cachedCalculatedAudienceSize.value.size
  const playerCount = (percentageWeight / 100) * audiencePlayerCount
  return Math.floor(playerCount)
}

/**
 * Returns the number of players in the given variant.
 * @param variantId Variant to return data for.
 * @returns Number of players currently in this variant, or 0 if that cannot be calculated.
 */
function numberOfPlayersInVariant(variantId: string | null): number {
  if (variantId) {
    return experimentInfoData.value.stats.variants[variantId]?.numPlayersInVariant ?? 0
  } else {
    return 0
  }
}

/**
 * Tooltip message to show number of players in a variant, based on it's rollout percentage.
 * @param variantWeight Rollout percentage of the variant.
 * @returns Tooltip message.
 */
function variantRolloutTooltip(variantWeight: number): string {
  if (experimentInfoData.value.state.enrollTrigger === 'NewPlayers') {
    return `Approximately ${Math.round(variantWeight * (experimentInfoData.value.state.rolloutRatioPermille / 1000))}% of new players.`
  } else if (
    experimentInfoData.value.state.hasCapacityLimit &&
    experimentInfoData.value.state.maxCapacity < percentageOfCalculatedAudienceSize(100)
  ) {
    return `Approximately ${Math.round(experimentInfoData.value.state.maxCapacity * (variantWeight / 100))} players out of a max capacity of ${experimentInfoData.value.state.maxCapacity} players in this experiment.`
  } else {
    return `Approximately ${percentageOfCalculatedAudienceSize(variantWeight)} players out of a calculated audience of ${percentageOfCalculatedAudienceSize(100)} players.`
  }
}

const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('ID', 'id', MetaListSortDirection.Ascending),
  new MetaListSortOption('ID', 'id', MetaListSortDirection.Descending),
  new MetaListSortOption('Weight', 'weight', MetaListSortDirection.Ascending),
  new MetaListSortOption('Weight', 'weight', MetaListSortDirection.Descending),
]
</script>
