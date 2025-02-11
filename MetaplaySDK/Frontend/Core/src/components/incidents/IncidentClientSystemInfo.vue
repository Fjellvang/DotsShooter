<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  v-if="playerIncidentData"
  title="Client System Info"
  :variant="!playerIncidentData.clientSystemInfo ? 'neutral' : 'primary'"
  )
  b-table-simple(
    small
    responsive
    )
    b-tbody
      b-tr(
        v-for="field in getObjectPrintableFields(playerIncidentData.clientSystemInfo)"
        :key="field.key"
        )
        b-td {{ field.name }}
        b-td(
          v-if="field.value !== null"
          class="tw-text-right"
          ) {{ field.value }}
        b-td(
          v-else
          class="text-muted tw-text-right"
          ) undefined
</template>

<script lang="ts" setup>
import { getObjectPrintableFields } from '@metaplay/meta-ui'
import { MCard } from '@metaplay/meta-ui-next'
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
</script>
