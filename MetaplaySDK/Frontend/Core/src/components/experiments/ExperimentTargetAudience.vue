!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Experiment Audience"
  :error="singleExperimentError"
  data-testid="experiment-statistics-card"
  )
  template(#default)
    table(class="tw-w-full")
      tbody(class="tw-divide-y tw-divide-neutral-200 *:*:tw-py-1.5")
        tr
          td Enroll Trigger
          td(
            v-if="singleExperimentData.state.enrollTrigger === 'Login'"
            class="tw-text-right"
            ) At login
          td(
            v-else-if="singleExperimentData.state.enrollTrigger === 'NewPlayers'"
            class="tw-text-right"
            ) At account creation only

        tr
          td Estimated Audience
          td(
            v-if="singleExperimentData.state.enrollTrigger === 'Login'"
            class="tw-text-right"
            )
            meta-audience-size-estimate(
              v-if="singleExperimentData.state.targetCondition"
              :sizeEstimate="cachedTotalEstimatedAudienceSize"
              )
            meta-abbreviate-number(
              v-else
              :value="totalPlayerCount"
              unit="player"
              )
          td(
            v-else-if="singleExperimentData.state.enrollTrigger === 'NewPlayers'"
            class="tw-text-right"
            ) New players only

        tr
          td Enrollment Target
          td(
            v-if="singleExperimentData.state.isRolloutDisabled"
            class="tw-text-right"
            ) #[MBadge(variant="warning") Disabled]
          td(
            v-else-if="singleExperimentData.state.enrollTrigger === 'NewPlayers'"
            class="tw-text-right"
            ) {{ singleExperimentData.state.rolloutRatioPermille / 10 }}% of new players
          td(
            v-else-if="singleExperimentData.state.targetCondition != null"
            class="tw-text-right"
            ) {{ singleExperimentData.state.rolloutRatioPermille / 10 }}% of the above
          td(
            v-else
            class="tw-text-right"
            ) {{ singleExperimentData.state.rolloutRatioPermille / 10 }}% of the above

        tr
          td Max Enrollment
          td(
            v-if="singleExperimentData.state.hasCapacityLimit"
            class="tw-text-right"
            ) #[meta-abbreviate-number(:value="singleExperimentData.state.maxCapacity" unit="player")]
          td(
            v-else
            class="text-right"
            ) ∞

        tr
          td Addressable Audience
          td(
            v-if="singleExperimentData.state.enrollTrigger === 'Login'"
            class="tw-text-right"
            ) ~#[meta-abbreviate-number(:value="cachedCalculatedAudienceSize.size" unit="player")] #[MBadge(:tooltip="cachedCalculatedAudienceSize.tooltip" shape="pill") ?]
          td(
            v-else-if="singleExperimentData.state.enrollTrigger === 'NewPlayers' && !singleExperimentData.state.hasCapacityLimit"
            class="tw-text-right"
            ) {{ singleExperimentData.state.rolloutRatioPermille / 10 }}% of new players
          td(
            v-else-if="singleExperimentData.state.enrollTrigger === 'NewPlayers' && singleExperimentData.state.hasCapacityLimit"
            class="tw-text-right"
            ) Up to {{ singleExperimentData.state.maxCapacity }} new players
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MBadge, MCard } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { routeParamToSingleValue } from '../../coreUtils'
import { calculatedAudienceSize, totalEstimatedAudienceSize } from '../../experimentUtils'
import { getSingleExperimentSubscriptionOptions } from '../../subscription_options/experiments'
import {
  getDatabaseItemCountsSubscriptionOptions,
  getPlayerSegmentsSubscriptionOptions,
} from '../../subscription_options/general'
import MetaAudienceSizeEstimate from '../MetaAudienceSizeEstimate.vue'

const route = useRoute()
// EXPERIMENTS -----------------------------------------
const experimentId = routeParamToSingleValue(route.params.id)
const { data: singleExperimentData, error: singleExperimentError } = useSubscription(
  getSingleExperimentSubscriptionOptions(experimentId || '')
)
// AUDIENCE DATA
const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())
const { data: databaseItemCountsData } = useSubscription(getDatabaseItemCountsSubscriptionOptions())
/**
 * Total number of players in the game. Returns 0 if that data isn't available yet.
 */
const totalPlayerCount = computed((): number => {
  return databaseItemCountsData.value?.totalItemCounts.Players || 0
})
/**
 * Cached lookup of total estimated audience size.
 */
const cachedTotalEstimatedAudienceSize = computed((): number | null => {
  return totalEstimatedAudienceSize(totalPlayerCount.value, singleExperimentData.value, playerSegmentsData.value)
})
/**
 * Cached lookup of calculated audience size.
 */
const cachedCalculatedAudienceSize = computed(() => {
  return calculatedAudienceSize(totalPlayerCount.value, singleExperimentData.value, playerSegmentsData.value)
})
</script>
