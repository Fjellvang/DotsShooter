<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A card that shows all incidents for a single player. Defers rendering to the incident-history-card. -->

<template lang="pug">
incident-history-card(
  :incidents="incidents"
  :showDescription="true"
  :showMainPageLink="false"
  :playerName="playerData?.model.playerName"
  data-testid="player-incident-history-card"
  )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'
import IncidentHistoryCard from '../incidents/IncidentHistoryCard.vue'

const props = defineProps<{
  /**
   * Id of the player whose incidents we want to show.
   */
  playerId: string
}>()

// Subscribe to a single player's data.
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * Incidents data to be rendered in this component.
 */
const incidents = computed(() => {
  return playerData.value?.incidentHeaders || []
})
</script>
