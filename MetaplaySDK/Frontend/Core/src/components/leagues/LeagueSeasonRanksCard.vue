<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  v-if="singleLeagueSeasonData"
  title="All Ranks"
  :itemList="rankDisplayData"
  :searchFields="['name', 'description']"
  :sortOptions="sortOptions"
  :pageSize="10"
  data-testid="league-season-ranks-card"
  )
  template(#item-card="{ item: rank }")
    MListItem {{ rank.name }}
      template(#top-right)
        span #[meta-abbreviate-number(:value="rank.totalParticipants")] #[meta-plural-label(:value="rank.totalParticipants" label="participant" hideCount)]

      template(#bottom-left)
        div {{ rank.description }}
        div(v-if="rank.historicalData") #[meta-abbreviate-number(:value="rank.historicalData.numPromotions")] #[meta-plural-label(:value="rank.historicalData.numPromotions" label="promotion" hideCount)] | #[meta-abbreviate-number(:value="rank.historicalData.numDemotions")] #[meta-plural-label(:value="rank.historicalData.numDemotions" label="demotion" hideCount)] | #[meta-abbreviate-number(:value="rank.historicalData.numDropped")] dropped
        div(v-else) Statistics not available yet.

      template(#bottom-right)
        MTextButton(
          v-if="rank.totalDivisions > 0"
          :disabled-tooltip="divisionsInCurrentlySelectedRank === 0 ? 'There are no divisions available to view for this rank.' : undefined"
          @click="onViewDivisionClick(rank.id)"
          :data-testid="'view-division-button-' + rank.id"
          ) {{ maybePluralString(rank.totalDivisions, 'division') }}

        MTooltip(
          v-else
          content="There are no divisions available to view for this rank."
          ) View division

//- View division modal.
MActionModal(
  ref="viewDivisionModal"
  title="View Division"
  :action="onViewDivisionModalOk"
  ok-button-label="View Division"
  :ok-button-disabled-tooltip="!selectedDivisionValid ? 'Select a division to view.' : undefined"
  disable-safety-lock
  @show="onViewDivisionModalShow"
  :data-testid="'view-division-modal' + selectedRankId"
  )
  div Select a division to view from #[strong {{ selectedRankName }}] of #[strong {{ singleLeagueSeasonData.displayName }}]
    MInputNumber(
      label="Division Index"
      :model-value="selectedDivision"
      placeholder="Enter division index..."
      :min="0"
      :max="divisionsInCurrentlySelectedRank - 1"
      :hint-message="divisionsInCurrentlySelectedRank === 1 ? 'There is only one division available in this rank, with the index 0.' : `There are a total of ${divisionsInCurrentlySelectedRank} divisions available, indexed from 0 to ${divisionsInCurrentlySelectedRank - 1}.`"
      @update:model-value="selectedDivision = $event"
      :data-testid="'division-index-input' + selectedRankId"
      )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MActionModal, MTooltip, MListItem, MInputNumber, MTextButton } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { getSingleLeagueSeasonSubscriptionOptions } from '../../subscription_options/leagues'

const props = defineProps<{
  leagueId: string
  seasonId: number
}>()

const router = useRouter()

const gameServerApi = useGameServerApi()

// Data sources -------------------------------------------------------------------------------------------------------

/**
 * Fetch league data.
 */
const { data: singleLeagueSeasonData } = useSubscription(
  getSingleLeagueSeasonSubscriptionOptions(props.leagueId, props.seasonId)
)

interface RankDisplayData {
  name: string
  description: string
  id: number
  totalParticipants: number
  totalDivisions: number
  // For seasons that have ended, we have extra data available.
  historicalData?: {
    numPromotions: number
    numDemotions: number
    numDropped: number
  }
}

/**
 * Converts raw league data into `RankDisplayData` for the selected league and season.
 */
const rankDisplayData = computed((): RankDisplayData[] => {
  const { ranks } = singleLeagueSeasonData.value
  return ranks.map((rank: any, index: number): RankDisplayData => {
    const displayData: RankDisplayData = {
      name: rank.rankName,
      description: rank.description,
      id: index,
      totalParticipants: rank.totalParticipants,
      totalDivisions: rank.numDivisions,
    }
    if (!singleLeagueSeasonData.value.isCurrent) {
      displayData.historicalData = {
        numPromotions: rank.numPromotions,
        numDemotions: rank.numDemotions,
        numDropped: rank.numDropped,
      }
    }
    return displayData
  })
})

/**
 * Sorting for the MetaListCard.
 */
const sortOptions = [
  new MetaListSortOption('Rank', 'id', MetaListSortDirection.Descending),
  new MetaListSortOption('Rank', 'id', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Descending),
  new MetaListSortOption('Participants', 'totalParticipants', MetaListSortDirection.Ascending),
  new MetaListSortOption('Participants', 'totalParticipants', MetaListSortDirection.Descending),
]

// Modal control ------------------------------------------------------------------------------------------------------

/**
 * Currently selected rank form the list of ranks.
 */
const selectedRankId = ref<number>()

const selectedRankName = computed(() => {
  if (selectedRankId.value !== undefined) {
    return rankDisplayData.value[selectedRankId.value].name
  }
  return undefined
})

/**
 * Reference to the modal for viewing divisions.
 */
const viewDivisionModal = ref<typeof MActionModal>()

function onViewDivisionClick(rankId: number): void {
  selectedRankId.value = rankId
  viewDivisionModal.value?.open(rankId)
}

function onViewDivisionModalShow(): void {
  selectedDivision.value = undefined
}

async function onViewDivisionModalOk(): Promise<void> {
  if (selectedRankId.value !== undefined && selectedDivision.value !== undefined) {
    await viewDivision(selectedRankId.value, selectedDivision.value)
  }
}

/**
 * Helper function to return the number of available divisions in a rank.
 */
const divisionsInCurrentlySelectedRank = computed(() => {
  return rankDisplayData.value[selectedRankId.value ?? 0].totalDivisions
})

/**
 * Currently selected division in the modal.
 */
const selectedDivision = ref<number>()

/**
 * Check if input is valid for view division button to activate
 */
const selectedDivisionValid = computed<boolean>(() => {
  return selectedDivision.value !== undefined
})

/**
 * User has selected a division on the modal and clicked on OK.
 */
const viewDivision = async (rank: number, division: number): Promise<void> => {
  const result = await gameServerApi.get(`/divisions/id/${props.leagueId}/${props.seasonId}/${rank}/${division}/`)
  const divisionEntityId = result.data
  const url = `/leagues/${props.leagueId}/${props.seasonId}/${divisionEntityId}`
  await router.push(url)
}
</script>
