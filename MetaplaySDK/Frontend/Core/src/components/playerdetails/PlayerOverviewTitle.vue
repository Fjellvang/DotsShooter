<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A wrapper component for displaying overview card title. -->

<template lang="pug">
span(v-if="playerData")
  fa-icon(
    v-if="playerData.model.isOnline && playerData.model.isClientConnected && playerData.model.clientAppPauseStatus == 'Running'"
    size="xs"
    icon="circle"
    class="text-success"
    )
  fa-icon(
    v-else-if="playerData.model.isOnline"
    size="xs"
    icon="circle"
    class="text-warning"
    )
  fa-icon(
    v-else-if="Math.abs(DateTime.fromISO(playerData.model.stats.lastLoginAt).diffNow('hours').hours) < 12"
    size="xs"
    :icon="['far', 'circle']"
    class="text-success"
    )
  fa-icon(
    v-else
    size="xs"
    :icon="['far', 'circle']"
    class="text-dark"
    )
  span(class="ml-2") {{ displayTitle }} #[MClipboardCopy(v-if="displayTitle === playerData.model.playerId" :contents="playerData.model.playerId")]
  MTooltip(
    v-if="playerData.model.totalIapSpend > 0"
    :content="'Total IAP spend: $' + playerData.model.totalIapSpend.toFixed(2, 2)"
    noUnderline
    class="text-muted ml-2"
    ) #[fa-icon(icon="money-check-alt")]
  MTooltip(
    v-if="playerData.model.isDeveloper"
    content="This player is a developer."
    noUnderline
    class="text-muted ml-2"
    ) #[fa-icon(icon="user-astronaut" data-testid="player-is-developer-icon")]
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MClipboardCopy, MTooltip } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player displayed on the overview card header title.
   */
  playerId: string
}>()

const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

// TODO: Move logic to c# side. (There should be no players that have no names.)
const displayTitle = computed(() => {
  if (playerData.value?.model.playerName) {
    return playerData.value?.model.playerName
  } else {
    return playerData.value?.model.playerId
  }
})
</script>
