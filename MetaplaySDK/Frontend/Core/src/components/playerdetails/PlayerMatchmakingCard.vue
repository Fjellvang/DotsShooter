<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  meta-list-card(
    v-if="playerData"
    title="Matchmaking"
    icon="chess"
    :tooltip="`Shows matchmakers that ${playerName} belongs to.`"
    :itemList="matchmakers"
    :searchFields="searchFields"
    :filterSets="filterSets"
    :sortOptions="sortOptions"
    :emptyMessage="`${playerName} isn't registered to any matchmakers.`"
    permission="api.matchmakers.view"
    data-testid="player-matchmaking-card"
    )
    template(#item-card="{ item: matchmaker }")
      MCollapse(extraMListItemMargin)
        template(#header)
          MListItem(
            noLeftPadding
            data-testid="player-matchmaking-list-entry"
            ) {{ matchmaker.matchmakerName }}
            template(#badge)
              MBadge(
                v-if="matchmaker.isParticipant"
                variant="success"
                ) Participant
            template(#top-right)
              div(v-if="matchmaker.isParticipant") MMR: {{ matchmaker.defenseMmr }}
              div(v-else) Not a participant
            template(#bottom-left) {{ matchmaker.matchmakerDescription }}
            template(#bottom-right)
              div(v-if="matchmaker.isParticipant") Percentile: {{ Math.round(matchmaker.percentile * 10000) / 100 }}%
              div(v-if="matchmaker.isParticipant") Bucket: {{ matchmaker.bucketInfo.mmrLow }} - {{ matchmaker.bucketInfo.mmrHigh }}
              MTextButton(:to="`/matchmakers/${matchmaker.matchmakerId}`") View matchmaker

        div(class="border rounded-sm bg-light py-2 tw-px-4")
          div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="wrench")] Admin Controls
          div(class="small text-muted mb-2") You can add or remove this player from the matchmaker<!-- and preview what matches they would receive-->. These actions are safe to use in production.
          div(class="tw-mt-1 tw-inline-flex tw-w-full tw-justify-end")
            MActionModalButton(
              :modal-title="matchmaker.isParticipant ? 'Remove Player from Matchmaker' : 'Add Player to Matchmaker'"
              :action="matchmaker.isParticipant ? exitMatchmaker : enterMatchmaker"
              :trigger-button-label="matchmaker.isParticipant ? 'Remove Player' : 'Add Player'"
              variant="warning"
              :ok-button-label="matchmaker.isParticipant ? 'Remove from Matchmaker' : 'Add to Matchmaker'"
              permission="api.matchmakers.admin"
              @show="selectedMatchmaker = matchmaker"
              data-testid="add-remove-player-matchmaker"
              )
              span(v-if="matchmaker.isParticipant") Removing a player from a matchmaker means they will no longer be available for other players to match against.
              span(v-else)
                p Adding a player into a matchmaker makes them a valid target for other players to match against.
                p(class="tw-text-xs+ tw-text-neutral-500") Players typically get added into matchmakers as a part of normal gameplay. This action is mostly useful for faster testing during development.

            //- Commented out because of missing API
            //- meta-button(variant="primary" @click="selectedMatchmaker = item" modal="simulate-matchmaking" data-testid="simulate-matchmaker-button" permission="api.matchmakers.view").ml-2 Simulate

  //- Modals --------

  //- Simulate
  //- Commented out because of missing API
  //- b-modal#simulate-matchmaking(title="Simulate Matchmaking" size="md" @show="simulateMatchmaking" centered no-close-on-backdrop)
    h5 Matchmaking Results #[span.small.text-no-transform(v-if="simulationResult?.numTries") After #[meta-plural-label(:value="simulationResult.numTries" label="iteration")]]

    //- Error
    div(v-if="simulationResult?.response?.data?.error")
      MErrorCallout(:error="simulationResult.response.data.error")

    //- Results
    div(v-else-if="simulationResult?.response?.responseType === 'Success' && simulationBestMatchPlayer").rounded.border.p-3
      blistgroup(flush)
        //- TODO: Re-use the new player overview list once it's merged to develop
        metalistgroupitem
          span {{ simulationBestMatchPlayer.model.playerName }}
          template(#top-right) {{ simulationBestMatchPlayer.id }}
          template(#bottom-right): meta-button(link permission="api.players.view" :to="`/players/${simulationResult.response.bestCandidate}`") View player

    //- No results
    div(v-else-if="simulationResult?.response?.responseType").pt-4
      p No matches found!

    //- Loading
    div(v-else).tw-w-full.tw-text-center.pt-3
      b-spinner(label="Loading...")/

    template(#modal-footer="{ ok }")
      meta-button(variant="secondary" data-testid="simulate-matchmaking-close-button" @click="ok") Close
</template>

<script lang="ts" setup>
import { computed, ref, getCurrentInstance } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MActionModalButton, MBadge, MCollapse, MListItem, MTextButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllMatchmakersForPlayerSubscriptionOptions } from '../../subscription_options/matchmaking'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * The player to show matchmakers for.
   */
  playerId: string
}>()

// Data
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))
const playerName = computed(() => playerData.value.model.playerName || 'n/a')

const { data: matchmakers, refresh: matchmakersRefresh } = useSubscription<any[] | undefined>(
  getAllMatchmakersForPlayerSubscriptionOptions(props.playerId)
)
const selectedMatchmaker = ref<any>(null)

// Search, sort, filter
const searchFields = ['matchmakerId', 'matchmakerName', 'matchmakerDescription']
const sortOptions = [
  new MetaListSortOption('Name', 'matchmakerName', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'matchmakerName', MetaListSortDirection.Ascending),
  new MetaListSortOption('MMR', 'defenseMmr', MetaListSortDirection.Ascending),
  new MetaListSortOption('MMR', 'defenseMmr', MetaListSortDirection.Descending),
  new MetaListSortOption('Percentile', 'percentile', MetaListSortDirection.Ascending),
  new MetaListSortOption('Percentile', 'percentile', MetaListSortDirection.Descending),
]
const filterSets = [
  new MetaListFilterSet('participating', [
    new MetaListFilterOption('Participating', (x: any) => x.isParticipant),
    new MetaListFilterOption('Not participating', (x: any) => !x.isParticipant),
  ]),
]

// Simulation modal
// Commented out because of missing API
// const simulationResult = ref<any>(null)
// const simulationBestMatchPlayerSubscription = convert to useDynamicSubscription (computed(() => simulationResult.value?.response?.bestCandidate || null), true)
// const simulationBestMatchPlayer = simulationBestMatchPlayerSubscription.data
// async function simulateMatchmaking () {
//   try {
//     if (!selectedMatchmaker.value) throw new Error('No matchmaker selected')
//     const response = await useGameServerApi().post(`matchmakers/${selectedMatchmaker.value.matchmakerId}/test`, {
//       $type: 'Game.Server.Matchmaking.IdlerMatchmakerQuery, Server',
//       AttackerId: id.value,
//     })
//     simulationResult.value = response.data
//   } catch (e) {
//     simulationResult.value = e
//   }
// }

// Add to matchmaker modal
const vue = getCurrentInstance()

const { showSuccessNotification, showErrorNotification } = useNotifications()

async function enterMatchmaker(): Promise<void> {
  if (!selectedMatchmaker.value) throw new Error('No matchmaker selected')
  await useGameServerApi()
    .post(`matchmakers/${selectedMatchmaker.value.matchmakerId}/add/${props.playerId}`)
    .then((res) => {
      const message = `${playerName.value} added to ${selectedMatchmaker.value.matchmakerName} successfully.`
      showSuccessNotification(message)
      matchmakersRefresh()
    })
    .catch((error) => {
      if (error.response.status === 409) {
        const message = error.response.data.error.details
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        showErrorNotification(message)
      } else {
        console.error(error)
      }
    })
}

// Remove from matchmaker modal
async function exitMatchmaker(): Promise<void> {
  if (!selectedMatchmaker.value) throw new Error('No matchmaker selected')
  await useGameServerApi()
    .post(`matchmakers/${selectedMatchmaker.value.matchmakerId}/remove/${props.playerId}`)
    .then((res) => {
      const message = `${playerName.value} removed from ${selectedMatchmaker.value.matchmakerName} successfully.`
      showSuccessNotification(message)
      matchmakersRefresh()
    })
    .catch((error) => {
      if (error.response.status === 409) {
        const message = error.response.data.error.details
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        showErrorNotification(message)
      } else {
        console.error(error)
      }
    })
}
</script>
