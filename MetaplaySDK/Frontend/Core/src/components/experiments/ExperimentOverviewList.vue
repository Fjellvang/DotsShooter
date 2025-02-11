<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Experiment overview
table(class="tw-w-full")
  tbody(class="tw-divide-y tw-divide-neutral-200 *:*:tw-py-1.5")
    tr(class="tw-font-semibold")
      td #[fa-icon(icon="chart-bar")] Overview
    tr
      td Status
      td(class="tw-text-right")
        MBadge(:variant="phaseInfo.titleVariant") {{ phaseInfo.title }}
    tr
      td Created At
      td(class="tw-text-right") #[meta-time(:date="singleExperimentData.stats.createdAt" showAs="timeagoSentenceCase")]
    tr
      td Experiment Analytics ID
      td(
        v-if="!isExperimentMissing"
        :class="{ 'text-danger': !experiment.experimentAnalyticsId }"
        class="tw-text-right"
        ) {{ experiment.experimentAnalyticsId || 'None' }}
      td(
        v-else
        class="text-right text-danger"
        ) None

//- Rollout Details
table(class="tw-mt-3 tw-w-full")
  tbody(class="tw-divide-y tw-divide-neutral-200 *:*:tw-py-1.5")
    tr
      td(class="tw-font-semibold") #[fa-icon(icon="chart-bar")] Rollout Details
    tr
      td Total Participants
      td(
        v-if="singleExperimentData.state.numPlayersInExperiment === 0 && singleExperimentData.stats.ongoingFirstTimeAt === null"
        class="tw-text-right tw-italic tw-text-neutral-500"
        ) Not started
      td(
        v-else
        class="tw-text-right"
        ) #[meta-abbreviate-number(:value="singleExperimentData.state.numPlayersInExperiment" unit="player")]
    tr
      td Rollout Started At
      td(
        v-if="phase === 'Testing' && singleExperimentData.stats.ongoingFirstTimeAt === null"
        class="tw-text-right tw-italic tw-text-neutral-500"
        )
        span Not started
      td(
        v-else-if="phase === 'Testing'"
        class="tw-text-right"
        )
        span(class="tw-mr-1 tw-italic tw-text-neutral-500") Not started
        MBadge(
          tooltip="Experiment has previously been rolled out."
          shape="pill"
          ) ?
      td(
        v-else-if="singleExperimentData.stats.ongoingFirstTimeAt === singleExperimentData.stats.ongoingMostRecentlyAt"
        class="tw-text-right"
        )
        meta-time(
          :date="singleExperimentData.stats.ongoingFirstTimeAt"
          showAs="timeagoSentenceCase"
          )
      td(
        v-else
        class="tw-text-right"
        )
        meta-time(
          :date="singleExperimentData.stats.ongoingMostRecentlyAt"
          showAs="timeagoSentenceCase"
          )
        span(class="tw-mr-1")
        MBadge(
          tooltip="Experiment has previously been rolled out."
          shape="pill"
          ) ?
    tr
      td Running Time
      td(
        v-if="['Ongoing', 'Paused', 'Concluded'].includes(phase)"
        class="tw-text-right"
        )
        meta-duration(
          :duration="totalOngoingDuration.toString()"
          showAs="humanizedSentenceCase"
          )
        span(class="tw-mr-1")
        MBadge(
          v-if="!singleExperimentData.stats.ongoingFirstTimeAt !== singleExperimentData.stats.ongoingMostRecentlyAt"
          tooltip="Experiment has been active more than once."
          shape="pill"
          ) ?
      td(
        v-else
        class="tw-text-right"
        )
        span(class="tw-mr-1 tw-italic tw-text-neutral-500") Not started
        MBadge(
          v-if="singleExperimentData.stats.ongoingFirstTimeAt !== null"
          tooltip="Experiment has previously been rolled out."
          shape="pill"
          ) ?
    tr
      td Reached Capacity At
      td(
        v-if="!singleExperimentData.state.hasCapacityLimit"
        class="tw-text-right tw-italic tw-text-neutral-500"
        )
        span No max capacity
      td(
        v-else-if="singleExperimentData.stats.reachedCapacityFirstTimeAt === null"
        class="tw-text-right tw-italic tw-text-neutral-500"
        )
        span Not reached
      td(
        v-else-if="singleExperimentData.stats.reachedCapacityFirstTimeAt === singleExperimentData.stats.reachedCapacityMostRecentlyAt"
        class="tw-text-right"
        )
        meta-time(
          :date="singleExperimentData.stats.reachedCapacityFirstTimeAt"
          showAs="timeagoSentenceCase"
          )
      td(
        v-else
        class="tw-text-right"
        )
        meta-time(
          :date="singleExperimentData.stats.reachedCapacityMostRecentlyAt"
          showAs="timeagoSentenceCase"
          )
        span(class="tw-mr-1")
        MBadge(
          tooltip="Experiment has reached capacity more than once."
          shape="pill"
          ) ?
    tr
      td Concluded At
      td(
        v-if="['Testing', 'Ongoing', 'Paused'].includes(phase) && singleExperimentData.stats.concludedFirstTimeAt === null"
        class="tw-text-right tw-italic tw-text-neutral-500"
        )
        span Not concluded
      td(
        v-else-if="['Testing', 'Ongoing', 'Paused'].includes(phase)"
        class="tw-text-right"
        )
        span(class="tw-mr-1 tw-italic tw-text-neutral-500") Not concluded
        MBadge(
          tooltip="Experiment has previously been concluded."
          shape="pill"
          ) ?
      td(
        v-else-if="singleExperimentData.stats.concludedFirstTimeAt === singleExperimentData.stats.concludedMostRecentlyAt"
        class="tw-text-right"
        )
        meta-time(
          :date="singleExperimentData.stats.concludedFirstTimeAt"
          showAs="timeagoSentenceCase"
          )
      td(
        v-else
        class="tw-text-right"
        )
        meta-time(
          :date="singleExperimentData.stats.concludedMostRecentlyAt"
          showAs="timeagoSentenceCase"
          )
        span(class="tw-mr-1")
        MBadge(
          tooltip="Experiment has been concluded more than once."
          shape="pill"
          ) ?
</template>
<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { type Variant, MBadge } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { routeParamToSingleValue, parseDotnetTimeSpanToLuxon } from '../../coreUtils'
import { calculatedAudienceSize, totalEstimatedAudienceSize } from '../../experimentUtils'
import { getSingleExperimentSubscriptionOptions } from '../../subscription_options/experiments'
import {
  getGameDataSubscriptionOptions,
  getDatabaseItemCountsSubscriptionOptions,
  getPlayerSegmentsSubscriptionOptions,
} from '../../subscription_options/general'

const route = useRoute()
const experimentId = routeParamToSingleValue(route.params.id)

const { data: databaseItemCountsData } = useSubscription(getDatabaseItemCountsSubscriptionOptions())

const { data: singleExperimentData } = useSubscription(getSingleExperimentSubscriptionOptions(experimentId || ''))

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())

const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

const experiment = computed(() => gameData.value?.serverGameConfig.PlayerExperiments[experimentId])

const isExperimentMissing = computed(() => !experiment.value)

// PHASE STUFF -----------------------------------------

interface PhaseInfo {
  title: string
  titleVariant: Variant
}

type Phase = 'Testing' | 'Ongoing' | 'Paused' | 'Concluded'

/**
 * The current phase of the experiment.
 */
const phase = computed((): Phase => singleExperimentData.value?.state.lifecyclePhase)

/**
 * The title and variant of the current experiment phase.
 */
const phaseInfo = computed((): PhaseInfo => phaseInfos[phase.value])

/**
 * List of titles and variants for all experiment phases.
 */
const phaseInfos: Record<Phase, PhaseInfo> = {
  Testing: {
    title: 'Testing',
    titleVariant: 'primary',
  },
  Ongoing: {
    title: 'Active',
    titleVariant: 'success',
  },
  Paused: {
    title: 'Paused',
    titleVariant: 'warning',
  },
  Concluded: {
    title: 'Concluded',
    titleVariant: 'neutral',
  },
}

/**
 * Total running time of the experiment.
 */
const totalOngoingDuration = computed(() => {
  if (['Ongoing'].includes(phase.value)) {
    let duration = DateTime.now().diff(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      DateTime.fromISO(singleExperimentData.value.stats.ongoingMostRecentlyAt)
    )
    const ongoingDurationBeforeCurrentSpan = parseDotnetTimeSpanToLuxon(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      singleExperimentData.value.stats.ongoingDurationBeforeCurrentSpan
    )

    if (ongoingDurationBeforeCurrentSpan) {
      duration = duration.plus(ongoingDurationBeforeCurrentSpan)
    }
    return duration
  } else {
    return singleExperimentData.value.stats.ongoingDurationBeforeCurrentSpan
  }
})

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
