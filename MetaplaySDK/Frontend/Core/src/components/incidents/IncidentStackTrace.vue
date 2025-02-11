<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  v-if="playerIncidentData"
  title="Stack Trace"
  :variant="!stackTraceRows ? 'neutral' : 'primary'"
  )
  //b-card-title
    b-row(
      no-gutters
      align-v="center"
      )
      fa-icon(
        icon="code"
        class="mr-2"
        )
      span(class="tw-mr-1") Stack Trace
      MClipboardCopy(:contents="rawStackTrace")

  p(
    v-if="!stackTraceRows"
    class="tw-m-0 tw-text-center tw-italic tw-text-neutral-500"
    ) Stack trace not included in this incident report.

  div(
    v-else
    style="max-height: 20rem"
    class="log border rounded bg-light tw-w-full"
    )
    pre
      div(
        v-for="(row, index) in stackTraceRows"
        :key="index"
        class="m-0 tw-space-x-1"
        )
        span(
          v-for="(word, index) in row.split(' ')"
          :key="index"
          :class="index === 0 ? '' : 'text-muted'"
          ) {{ word }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MCard, MClipboardCopy } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getPlayerIncidentSubscriptionOptions } from '../../subscription_options/incidents'

const props = defineProps<{
  /**
   * ID of the incident to show.
   */
  incidentId: string
  /**
   * ID of the player to show.
   */
  playerId: string
}>()

const { data: playerIncidentData } = useSubscription(
  getPlayerIncidentSubscriptionOptions(props.playerId, props.incidentId)
)

/**
 * Either the raw stack trace data to be copied or an empty string when there is no data.
 */
const rawStackTrace = computed((): string => playerIncidentData.value?.stackTrace || '')

/**
 * List of stack trace data to be displayed in rows.
 */
const stackTraceRows = computed(() => {
  return playerIncidentData.value?.stackTrace?.split('\n')
})
</script>
