<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A component to visualize a humanized version of the estimated size for the number of players. -->

<template lang="pug">
MTooltip
  template(#content)
    div {{ tooltipContent }}
    MGameTimeOffsetHint(class="tw-mt-2")
  span(v-if="sizeEstimate === null") Estimate pending...
  span(v-else-if="sizeEstimate === undefined") Everyone
  span(v-else) ~#[meta-abbreviate-number(:value="sizeEstimate" roundDown disableTooltip unit="player")]
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { ref, onMounted, onUnmounted, computed } from 'vue'

import { MTooltip, MGameTimeOffsetHint } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getPlayerSegmentsSubscriptionOptions } from '../subscription_options/general'

const props = defineProps<{
  /**
   * Optional: Use one of either:
   * number: The count of players in the audience.
   * null: Size estimate not available.
   * undefined: Everyone.
   */
  sizeEstimate?: number | null // TODO: this interface is... creative. Refactor.
}>()

/**
 * Subscribe to player segment data.
 */
const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

let intervalId: ReturnType<typeof setTimeout> | undefined
/**
 * formattedUpdateTime will be undefined until setInterval fires in onMounted
 */
const formattedUpdateTime = ref<string | null>()

onMounted(() => {
  intervalId = setInterval(() => {
    if (playerSegmentsData.value?.lastUpdateTime) {
      formattedUpdateTime.value = DateTime.fromISO(
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        playerSegmentsData.value.lastUpdateTime
      ).toRelative()
    } else {
      formattedUpdateTime.value = 'some time ago'
    }
  }, 1000) // Update every second
})

const tooltipContent = computed(() => {
  if (props.sizeEstimate === undefined) {
    return undefined
  } else if (props.sizeEstimate === null) {
    return 'Audience size estimates have not yet been generated.'
  } else {
    return `Size estimate based on sampling taken ${formattedUpdateTime.value}. Actual sizes may differ and change over time.`
  }
})

onUnmounted(() => {
  clearInterval(intervalId)
})
</script>
