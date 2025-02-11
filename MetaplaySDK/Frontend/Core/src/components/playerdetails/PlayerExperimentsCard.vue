<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  v-if="playerData"
  title="Experiments"
  icon="flask"
  :tooltip="`Shows experiments that this ${playerData.model.playerName || 'n/a'} belongs to.`"
  :itemList="items"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :emptyMessage="`${playerData.model.playerName || 'n/a'} isn't involved in any experiments.`"
  :moreInfoUri="'/experiments'"
  moreInfoLabel="experiments"
  moreInfoPermission="api.experiments.view"
  permission="api.experiments.view"
  data-testid="player-experiments-card"
  )
  template(#item-card="{ item: experiment }")
    MListItem {{ experiment.name }} / {{ experiment.experimentId }}
      template(#badge)
        MBadge(v-if="experiment.isTester") Tester

      template(#top-right)
        MBadge(
          :tooltip="experimentPhaseDetails(experiment.phase)"
          :variant="!experiment.notActiveReason ? 'success' : 'neutral'"
          ) {{ experiment.phase }}

      template(#bottom-left) {{ experiment.description }}
      template(#bottom-right)
        div(v-if="experiment.variantId") Variant: {{ experiment.variantId }}
        div(v-else) Variant: Control
        div(class="tw-flex tw-flex-col")
          player-action-set-tester(
            :playerId="playerId"
            :experimentId="experiment.experimentId"
            )
          MTextButton(:to="`/experiments/${experiment.experimentId}`") View experiment
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption, experimentPhaseDetails } from '@metaplay/meta-ui'
import { MBadge, MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllExperimentsSubscriptionOptions } from '../../subscription_options/experiments'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'
import PlayerActionSetTester from './adminactions/PlayerActionSetTester.vue'

const props = defineProps({
  /**
   * Id of the player whose experiments we want to to see.
   */
  playerId: {
    type: String,
    required: true,
  },
})

const { data: experimentInfos } = useSubscription(getAllExperimentsSubscriptionOptions())
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const searchFields = ['experimentId', 'variantId', 'description']
const sortOptions = [
  new MetaListSortOption('Name', 'experimentId', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'experimentId', MetaListSortDirection.Descending),
  new MetaListSortOption('Phase', 'experimentPhase', MetaListSortDirection.Ascending),
  new MetaListSortOption('Phase', 'experimentPhase', MetaListSortDirection.Descending),
]

const items = computed(() => {
  if (experimentInfos.value?.experiments) {
    const experiments = playerData.value.experiments
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const enrolledExperiments = Object.entries(experiments).filter(([key, value]: any) => value.isPlayerEnrolled)
    return enrolledExperiments.map(([key, value]: any) => {
      const experimentInfo = experimentInfos.value.experiments.find(
        (experimentInfo: any) => experimentInfo.experimentId === key
      )
      return {
        experimentId: key,
        name: experimentInfo?.displayName || '',
        description: experimentInfo?.description || '',
        variantId: value.enrolledVariant,
        isTester: value.isPlayerTester,
        phase: value.experimentPhase === 'Ongoing' ? 'Active' : value.experimentPhase,
        notActiveReason: value.whyNotActive,
      }
    })
  } else return []
})
</script>
