<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MPageOverviewCard(
  title="Player Overview"
  :id="displayPlayerId"
  data-testid="player-overview-card"
  )
  template(#title)
    core-ui-placement(
      alwaysFullWidth
      smallBottomMargin
      placementId="Players/Details/Overview:Title"
      :playerId="playerId"
      )

  template(#subtitle)
    core-ui-placement(
      alwaysFullWidth
      placementId="Players/Details/Overview:Subtitle"
      :playerId="playerId"
      )

  template(#caption) Save file size:&nbsp;
    MTextButton(
      permission="api.database.inspect_entity"
      :to="`/entities/${playerData.model.playerId}/dbinfo`"
      data-testid="model-size-link"
      )
      meta-abbreviate-number(
        :value="playerData.persistedSize"
        unit="byte"
        )

  div(class="tw-grid tw-grid-cols-1 tw-gap-4 @lg:tw-grid-cols-2")
    core-ui-placement(
      v-if="showLeftPanel"
      alwaysFullWidth
      placementId="Players/Details/Overview:LeftPanel"
      :playerId="playerId"
      )

    overview-list(
      listTitle="Overview"
      icon="chart-bar"
      :sourceObject="playerData"
      :items="coreStore.overviewLists.player"
      )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MPageOverviewCard, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../coreStore'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'
import OverviewList from '../global/OverviewList.vue'
import CoreUiPlacement from '../system/CoreUiPlacement.vue'

const props = defineProps<{
  /**
   * Id of the player whose overview card to show.
   */
  playerId: string
}>()

const coreStore = useCoreStore()
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const showLeftPanel = computed(() => {
  if (coreStore.uiComponents['Players/Details/Overview:LeftPanel']) {
    return coreStore.uiComponents['Players/Details/Overview:LeftPanel'].length > 0
  }
  return false
})

const displayPlayerId = computed(() => {
  if (playerData.value?.model.playerName) {
    return playerData.value?.model.playerId
  }
  return ''
})
</script>
