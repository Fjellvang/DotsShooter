<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.activables.view"
  :is-loading="!allOffersData"
  :error="allOffersError"
  )
  template(#overview)
    MPageOverviewCard(
      title="View Offer Groups"
      data-testid="offers-group-overview-card"
      )
      p(class="tw-mb-1") Offer groups are sets of in-game offers that can be scheduled, targeted and placed in different parts of your game.
      p(class="tw-text-xs+ tw-text-neutral-500") Individual offers can be re-used in multiple groups to show them in many places at the same time. You can set fine-tuned limits and conditions to both individual offers and their groups to create advanced shop management scenarios.

  div(v-if="uniquePlacements && uniquePlacements.length > 0")
    b-row(
      no-gutters
      align-v="center"
      class="mb-2 tw-mt-4"
      )
      h3 Offer Groups By Placement

    b-row(align-h="center")
      b-col(
        v-for="placement in uniquePlacements"
        :key="placement"
        lg="6"
        class="tw-mb-4"
        )
        offer-groups-card(
          :title="`${placement}`"
          :placement="placement"
          emptyMessage="This placements has no offer groups in it."
          :customEvaluationIsoDateTime="customEvaluationTime ? String(customEvaluationTime.toISO()) : undefined"
          hideCollapse
          hidePlacement
          class="h-100"
          )

  b-row(
    align-h="center"
    class="mb-2 tw-mt-4"
    )
    b-col(
      md="8"
      xl="7"
      class="tw-mb-4"
      )
      div(
        class="pl-3 bg-white rounded border shadow-sm tw-pb-4 tw-pr-4"
        data-testid="custom-time"
        )
        b-row(
          align-h="between"
          no-gutters
          class="mb-2 tw-mt-4"
          )
          span(class="font-weight-bold") Enable Custom Evaluation Time
            MBadge(
              tooltip="The phases on the page are evaluated according to the local time of your browser. Enabling custom evaluation allows you to set an exact time to evaluate against."
              shape="pill"
              class="tw-ml-1"
              ) ?
          MInputSwitch(
            :model-value="userEvaluationEnabled"
            class="tw-relative tw-top-1 tw-mr-1"
            name="customEvaluationTimeEnabled"
            size="small"
            @update:model-value="userEvaluationEnabled = $event"
            )
        div(
          v-if="userEvaluationEnabled"
          class="border-top pt-2 tw-mt-4"
          )
          MInputDateTime(
            label="Evaluation Time (UTC)"
            :model-value="userEvaluationTime"
            @update:model-value="onDateTimeChange"
            )

        div(class="mt-2 tw-w-full tw-text-center")
          span(class="small text-muted tw-italic") Schedules evaluated at {{ evaluationTimeUsed }}
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import { MBadge, MInputDateTime, MInputSwitch, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import OfferGroupsCard from '../components/offers/OfferGroupsCard.vue'
import { getAllOffersSubscriptionOptions } from '../subscription_options/offers'

// Custom user evaluation time ----------------------------------------------------------------------------------------

/**
 * Model for whether custom user evaluation time is enabled or not.
 */
const userEvaluationEnabled = ref(false)

/**
 * Model for custom user evaluation time input.
 */
const userEvaluationTime = ref<DateTime>(DateTime.now())

/**
 * What time to use for evaluating the activables card/
 */
const customEvaluationTime = computed((): DateTime | undefined => {
  if (userEvaluationEnabled.value) {
    return userEvaluationTime.value
  } else {
    return undefined
  }
})

/**
 * Returns ISO string of time that is being used to evaluate availability of activables.
 */
const evaluationTimeUsed = computed((): string => {
  if (userEvaluationEnabled.value) {
    return String(userEvaluationTime.value.toISO())
  } else {
    return String(DateTime.now().toISO())
  }
})

/**
 * Utility function to prevent undefined inputs.
 */
function onDateTimeChange(value?: DateTime): void {
  if (!value) return
  userEvaluationTime.value = value
}

// Activables data ----------------------------------------------------------------------------------------------------

const { data: allOffersData, error: allOffersError } = useSubscription(getAllOffersSubscriptionOptions())

const uniquePlacements = computed(() => {
  if (allOffersData.value) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const offerGroups = Object.values(allOffersData.value.offerGroups)
    const allPlacements = offerGroups.map((x: any) => x.config.placement)
    const uniquePlacements = [...new Set(allPlacements)]
    return uniquePlacements
  } else {
    return null
  }
})
</script>
