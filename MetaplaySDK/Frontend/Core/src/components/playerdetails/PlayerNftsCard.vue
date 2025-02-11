<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  meta-list-card(
    title="NFTs"
    icon="cubes"
    :tooltip="`Shows NFTs owned by ${playerData.model.playerName || 'n/a'}.`"
    :itemList="playerNfts"
    :emptyMessage="`${playerData.model.playerName || 'n/a'} doesn't have any NFTs.`"
    permission="api.players.view"
    class="mt-2"
    data-testid="player-nfts-card"
    )
    template(#item-card="{ item: nft }")
      MListItem(:avatarUrl="nft.imageUrl")
        span(v-if="nft.name") {{ nft.name }}
        span(
          v-else
          class="tw-italic"
          ) Unnamed {{ nft.typeName }}

        template(#top-right) {{ nft.collectionId }}/{{ nft.tokenId }}

        template(#bottom-left)
          div(v-if="nft.description") {{ nft.description }}
          span(
            v-else
            class="tw-italic"
            ) No description.

        template(#bottom-right)
          MTextButton(
            permission="api.nft.view"
            :to="`/web3/nft/${nft.collectionId}/${nft.tokenId}`"
            data-testid="view-nft"
            ) View NFT

    template(#description) NFT ownerships update automatically but you can also #[MTextButton(permission="api.nft.refresh_from_ledger" @click="refreshOwnership" data-testid="nft-refresh-button") refresh now].
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MListItem, MTextButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const gameServerApi = useGameServerApi()

const props = defineProps<{
  playerId: string
}>()

const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const playerNfts = computed((): any[] | undefined => playerData.value?.nfts)

const { showSuccessNotification } = useNotifications()

async function refreshOwnership(): Promise<void> {
  showSuccessNotification('NFT ownership update triggered. It might take a few moments.')
  await gameServerApi.post(`players/${props.playerId}/refreshOwnedNfts`)
  showSuccessNotification('NFT ownerships updated.')
  playerRefresh()
}
</script>
