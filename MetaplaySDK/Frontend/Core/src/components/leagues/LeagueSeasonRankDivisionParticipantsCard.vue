<template lang="pug">
meta-list-card(
  v-bind="$attrs"
  title="All Participants"
  icon="users"
  :itemList="participants"
  :getItemKey="getItemKey"
  :searchFields="['playerAvatar.displayName']"
  :sortOptions="sortOptions"
  :filterSets="filterSets"
  )
  template(#item-card="{ item: participant }")
    MCollapse(extraMListItemMargin)
      //- Header
      template(#header)
        MListItem(noLeftPadding) {{ participant.playerAvatar.displayName }}
          template(#badge)
            MTooltip(
              v-if="!isRealParticipant(participant.participantId)"
              content="Placeholder player that will get automatically deleted once a real player joins."
              noUnderline
              )
              fa-icon(icon="robot")

          template(#top-right)
            div(class="tw-space-x-1") Position: #[meta-ordinal-number(:number="participant.sortOrderIndex + 1")]
              MIconButton(
                permission="api.leagues.participant_edit"
                :disabled-tooltip="deleteButtonTooltipContent(participant.participantId)"
                variant="danger"
                aria-label="Remove this participant."
                @click="onRemoveParticipantClick(participant)"
                )
                fa-icon(icon="trash-alt")

          template(#bottom-left)
            div {{ participant.participantInfo }}
            div Last action:
              meta-time(
                v-if="!isEpochTime(participant.divisionScore.lastActionAt)"
                :date="participant.divisionScore.lastActionAt"
                class="tw-ml-1"
                )
              span(
                v-else
                class="tw-ml-1"
                ) No actions
            div(v-if="!isCurrentSeason")
              div(v-if="!participant.resolvedDivisionRewards")
                span No reward received.
              div(v-else-if="participant.resolvedDivisionRewards")
                div(v-for="reward in participant.resolvedDivisionRewards.rewards")
                  meta-reward-badge(:reward="reward")
                span(v-if="participant.resolvedDivisionRewards.isClaimed") Reward claimed
                span(v-else) Reward unclaimed

          template(#bottom-right)
            MTextButton(
              v-if="isRealParticipant(participant.participantId)"
              :to="`/players/${participant.participantId}`"
              permission="api.players.view"
              data-testid="view-participant"
              ) View participant

      //- Content
      pre(class="code-box border rounded bg-light") {{ participant }}

    //- Modal
    MActionModal(
      ref="removeParticipantModal"
      title="Remove Participant"
      :action="removeParticipant"
      @hidden="participantToRemove = undefined"
      )
      p You are about to remove #[MBadge {{ participantToRemove.playerAvatar.displayName }}] from the division.
      meta-no-seatbelts(
        message=" The participant will lose all their progress in the league, and will have to start again from scratch. This action can't be undone."
        :name="participantToRemove.playerAvatar.displayName"
        )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import {
  MActionModal,
  MBadge,
  MCollapse,
  MIconButton,
  MListItem,
  MTooltip,
  MTextButton,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { isEpochTime, routeParamToSingleValue } from '../../coreUtils'
import { getSingleDivisionSubscriptionOptions } from '../../subscription_options/leagues'

const route = useRoute()
const gameServerApi = useGameServerApi()

const props = defineProps<{
  divisionId: string
}>()

const { data: singleDivisionData, refresh: singleDivisionRefresh } = useSubscription(
  getSingleDivisionSubscriptionOptions(props.divisionId)
)

const leagueId = routeParamToSingleValue(route.params.leagueId)
const participantToRemove = ref()
const removeParticipantModal = ref<typeof MActionModal>()

const participants = computed((): any[] | undefined => {
  if (!singleDivisionData.value) {
    return []
  }
  const participants = Object.values(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    singleDivisionData.value.model.participants
  )
  return participants.sort((a: any, b: any) => a.sortOrderIndex - b.sortOrderIndex)
})

const isCurrentSeason = computed(() => {
  if (!singleDivisionData.value) {
    return false
  }
  return !singleDivisionData.value.model.isConcluded
})

function getItemKey(item: any): string {
  return item.playerAvatar.displayName
}

const { showSuccessNotification, showErrorNotification } = useNotifications()

function onRemoveParticipantClick(participant: any): void {
  participantToRemove.value = participant
  removeParticipantModal.value?.open(participant)
}

async function removeParticipant(): Promise<void> {
  const response = await gameServerApi.post(
    `/leagues/${leagueId}/participant/${participantToRemove.value.participantId}/remove`
  )
  if (response.data.success) {
    showSuccessNotification('Participant successfully removed from division.')
  } else {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    showErrorNotification(response.data.errorMessage)
  }
  singleDivisionRefresh()
}

/**
 * Checks if the participant is a real player or a placeholder.
 */
function isRealParticipant(participantId: string): boolean {
  if (participantId === 'None') {
    return false
  } else {
    return true
  }
}

/**
 * Determines the disabled delete button content depending on past season or placeholder player.
 */
function deleteButtonTooltipContent(participantId: string): string | undefined {
  if (!isCurrentSeason.value) {
    return 'Cannot remove participant since season has concluded.'
  } else if (!isRealParticipant(participantId)) {
    return 'This is a placeholder player that cannot be removed.'
  } else {
    return undefined
  }
}

/**
 * Sorting for the MetaListCard.
 */
const sortOptions = [
  new MetaListSortOption('Position', 'sortOrderIndex', MetaListSortDirection.Ascending),
  new MetaListSortOption('Position', 'sortOrderIndex', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'playerAvatar.displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'playerAvatar.displayName', MetaListSortDirection.Descending),
]

const filterSets = [
  new MetaListFilterSet('resolvedDivisionRewards', [
    new MetaListFilterOption(
      'Reward claimed',
      (participant: any) => participant.resolvedDivisionRewards?.isClaimed === true,
      false
    ),
    new MetaListFilterOption(
      'Reward unclaimed',
      (participant: any) => participant.resolvedDivisionRewards?.isClaimed === false,
      false
    ),
    new MetaListFilterOption(
      'Reward not received',
      (participant: any) => participant.resolvedDivisionRewards === null,
      false
    ),
  ]),
]
</script>
