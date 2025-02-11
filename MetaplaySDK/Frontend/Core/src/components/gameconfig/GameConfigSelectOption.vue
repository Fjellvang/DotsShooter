<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- An element for use in the game config select component to show details about a given game config. -->

<template lang="pug">
div(style="font-size: 0.95rem")
  div(class="font-weight-bold") {{ gameConfigFromId?.name ?? 'No name available' }}
  div(class="small font-mono") {{ gameConfigId }}
  div(
    v-if="gameConfigFromId"
    class="small"
    ) Built #[meta-time(:date="gameConfigFromId?.buildStartedAt")]
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useSubscription } from '@metaplay/subscriptions'

import type { MinimalGameConfigInfo } from '../../gameConfigServerTypes'
import { getAllGameConfigsSubscriptionOptions } from '../../subscription_options/gameConfigs'

const props = defineProps<{
  /**
   * Id of the game config to show in the card.
   */
  gameConfigId: string
}>()

const { data: allGameConfigsData } = useSubscription(getAllGameConfigsSubscriptionOptions())

const gameConfigFromId = computed((): MinimalGameConfigInfo | undefined => {
  return (allGameConfigsData.value ?? []).find((config) => config.id === props.gameConfigId)
})
</script>
