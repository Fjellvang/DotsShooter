<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Kick a Player From the Guild"
  :action="kickMember"
  trigger-button-label="Kick a Member"
  :trigger-button-disabled-tooltip="triggerButtonDisabledReason"
  variant="warning"
  :ok-button-label="playerOptions?.length > 1 ? 'Kick Player' : 'Kick and Close Guild'"
  :ok-button-disabled-tooltip="!chosenPlayer ? `Select a member to ${playerOptions?.length > 1 ? 'kick out of' : 'kick out and close'} this guild.` : undefined"
  permission="api.guilds.kick_member"
  @show="resetModal"
  data-testid="action-kick-member"
  )
  div(v-if="playerOptions")
    p(v-if="playerOptions.length >= 2")
      i Note: Kicking all {{ playerOptions.length }} members from #[MBadge {{ guildData.model.displayName }}] will also automatically close it.
    p(
      v-else
      class="tw-text-red-500"
      )
      i Note: #[MBadge(variant="danger") {{ lastPlayerName }}] is the last player in #[MBadge {{ guildData.model.displayName }}] and kicking them will also close the guild.

    div(class="tw-my-2 tw-font-semibold") Select a Guild Member
    meta-input-select(
      :value="chosenPlayer"
      :options="playerOptions"
      placeholder="Select a player..."
      :searchFields="['playerId', 'displayName']"
      @input="chosenPlayer = $event"
      )
      template(#option="{ option }")
        MListItem(class="!tw-px-0 !tw-py-0")
          span(class="tw-text-sm") {{ option?.displayName }}
          template(#top-right) {{ option?.playerId }}

    meta-no-seatbelts(
      v-if="chosenPlayer"
      :name="chosenPlayer.displayName"
      class="tw-mt-4"
      )/
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MActionModalButton, MBadge, MListItem, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSingleGuildSubscriptionOptions } from '../../../subscription_options/guilds'

const props = defineProps<{
  guildId: string
}>()

const gameServerApi = useGameServerApi()

/**
 * Subscribe to guild data.
 */
const { data: guildData, refresh: guildTriggerRefresh } = useSubscription(
  getSingleGuildSubscriptionOptions(props.guildId)
)

const hasMembers = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  return Object.keys(guildData.value.model.members).length !== 0
})

interface PlayerInfo {
  playerId: string
  displayName: string
}

/**
 * List of players to be displayed as options on the multiselect dropdown.
 */
const playerOptions = ref<Array<MetaInputSelectOption<PlayerInfo>>>([])

/**
 * The selected player.
 */
const chosenPlayer = ref<PlayerInfo>()

/**
 * Reset the modal.
 */
function resetModal(): void {
  chosenPlayer.value = undefined
  // Re-populate player options list on modal show as it might change based on subscriptions refresh, but live-refreshing messes up the select component.
  const newPlayerOptions: Array<MetaInputSelectOption<PlayerInfo>> = []
  for (const playerId in guildData.value.model.members) {
    const displayName = guildData.value.model.members[playerId].displayName
    newPlayerOptions.push({
      id: playerId,
      value: {
        playerId,
        displayName,
      },
    })
  }
  playerOptions.value = newPlayerOptions
}

/**
 * Last player name to be shown when only one memeber is left in the guild.
 */
const lastPlayerName = computed(() => playerOptions.value[0]?.value.displayName)

const { showSuccessNotification } = useNotifications()

/**
 * Kick the chosen member out and update the game server.
 */
async function kickMember(): Promise<void> {
  await gameServerApi.post(`/guilds/${props.guildId}/kickMember`, {
    playerId: chosenPlayer.value?.playerId,
  })
  showSuccessNotification(`${chosenPlayer.value?.displayName} kicked from ${guildData.value.model.displayName}.`)
  guildTriggerRefresh()
}

/**
 * Disable the trigger button under certain conditions.
 */
const triggerButtonDisabledReason = computed(() => {
  if (guildData.value.model.lifecyclePhase === 'Closed') {
    return 'Guild is closed.'
  } else if (!hasMembers.value) {
    return 'Guild has no members to kick.'
  } else {
    return undefined
  }
})
</script>
