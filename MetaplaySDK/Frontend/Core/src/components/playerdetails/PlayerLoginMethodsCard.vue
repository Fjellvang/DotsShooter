<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  meta-list-card(
    title="Login Methods"
    icon="key"
    :itemList="allAuths"
    tooltip="You can move these login methods to connect to another account via the admin actions."
    :searchFields="searchFields"
    :sortOptions="sortOptions"
    :defaultSortOption="defaultSortOption"
    :filterSets="filterSets"
    :emptyMessage="`${playerData.model.playerName || 'n/a'} has no credentials attached.`"
    data-testid="player-login-methods-card"
    )
    template(#item-card="{ item }")
      MCollapse(extraMListItemMargin)
        template(#header)
          MListItem(noLeftPadding)
            span(v-if="item.type === 'device'") #[fa-icon(icon="key")] Client token
            span(v-else) #[fa-icon(icon="user-tag")] {{ item.displayString }}

            template(#top-right)
              span(class="tw-space-x-1") Attached #[meta-time(:date="item.attachedAt")]
                MIconButton(
                  permission="api.players.auth"
                  variant="danger"
                  aria-label="Remove this authentication method."
                  @click="onRemoveAuthClick(item)"
                  )
                  fa-icon(icon="trash-alt")

            template(#bottom-left) {{ item.id }}

        //- Collapse content
        div(v-if="item.devices && item.devices.length > 0")
          div(class="tw-mb-2 tw-font-semibold") Known Devices
          MList(
            showBorder
            striped
            )
            MListItem(
              v-for="device in item.devices"
              :key="device.id"
              condensed
              ) {{ device.deviceModel }}
              template(#top-right)
                span(class="tw-text-neutral-500") {{ device.id }}
        div(
          v-else
          class="tw-text-center tw-italic tw-text-neutral-400"
          ) No devices recorded for this login method.

      MActionModal(
        ref="removeAuthModal"
        title="Remove Authentication Method"
        :action="removeAuth"
        @hidden="authToRemove = null"
        )
        p(class="tw-mb-2") You are about to remove #[MBadge(v-if="authToRemove") {{ authToRemove.displayString }}] from #[MBadge {{ playerData.model.playerName }}]. They will not be able to login to their account using this method.

        p(
          v-if="allAuths.length === 1"
          class="text-danger tw-mb-4 tw-italic"
          ) Note: Removing this last auth method means the account will be orphaned!

        meta-no-seatbelts(:name="playerData.model.playerName || 'n/a'")
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  parseAuthMethods,
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortDirection,
  MetaListSortOption,
} from '@metaplay/meta-ui'
import {
  MActionModal,
  MBadge,
  MCollapse,
  MIconButton,
  MList,
  MListItem,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player whose device list to show.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const authToRemove = ref<any>(null)
const removeAuthModal = ref<typeof MActionModal>()

const searchFields = ['displayString', 'id']
const sortOptions = [
  new MetaListSortOption('Attached time ', 'attachedAt', MetaListSortDirection.Ascending),
  new MetaListSortOption('Attached time ', 'attachedAt', MetaListSortDirection.Descending),
]
const defaultSortOption = 1
const filterSets = [
  new MetaListFilterSet('type', [
    new MetaListFilterOption('Client token', (x: any) => x.type === 'device'),
    new MetaListFilterOption('Social auth', (x: any) => x.type === 'social'),
  ]),
]

const allAuths = computed(() => {
  return parseAuthMethods(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    playerData.value.model.attachedAuthMethods,
    playerData.value.model.deviceHistory
  )
})

function onRemoveAuthClick(item: any): void {
  authToRemove.value = item
  removeAuthModal.value?.open(item)
}

const { showSuccessNotification, showErrorNotification } = useNotifications()

async function removeAuth(): Promise<void> {
  const identifier = authToRemove.value.name
  try {
    await gameServerApi.delete(
      `/players/${playerData.value.id}/auths/${authToRemove.value.name}/${authToRemove.value.id}`
    )
    const message = `'${identifier}' deleted from ${playerData.value.model.playerName || 'n/a'}.`
    showSuccessNotification(message)
    playerRefresh()
  } catch (error: any) {
    const message = `Failed to remove '${identifier}' from ${playerData.value.model.playerName || 'n/a'}. Reason: ${error.response.data.error.details}`
    const toastTitle = 'Backend Error'
    showErrorNotification(message, toastTitle)
  }
}
</script>
