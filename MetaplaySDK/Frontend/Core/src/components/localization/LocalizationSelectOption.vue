<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- An element for use in the localization select component to show details about a given localization. -->

<template lang="pug">
div(style="font-size: 0.95rem")
  div(class="font-weight-bold") {{ localizationFromId?.name ?? 'No name available' }}
  div(class="small font-mono") {{ localizationId }}
  div(
    v-if="localizationFromId"
    class="small"
    ) Built #[meta-time(:date="localizationFromId?.buildStartedAt")]
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useSubscription } from '@metaplay/subscriptions'

import type { MinimalLocalizationInfo } from '../../localizationServerTypes'
import { getAllLocalizationsSubscriptionOptions } from '../../subscription_options/localization'

const props = defineProps<{
  /**
   * Id of the localization to show in the card.
   */
  localizationId: string
}>()

const { data: allLocalizationsData } = useSubscription(getAllLocalizationsSubscriptionOptions())

const localizationFromId = computed((): MinimalLocalizationInfo | undefined => {
  return (allLocalizationsData.value ?? []).find((localization) => localization.id === props.localizationId)
})
</script>
