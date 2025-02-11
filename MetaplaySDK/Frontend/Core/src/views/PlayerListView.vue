<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!allActivePlayersData"
  :error="allActivePlayersError"
  )
  //- Search
  b-row(
    class="justify-content-center"
    style="margin-top: 8rem; margin-bottom: 7rem"
    )
    b-col(
      md="10"
      lg="7"
      xl="5"
      )
      h4(
        class="tw-mb-1 tw-ml-2"
        data-testid="player-search"
        ) #[fa-icon(icon="search")] Find a player
      meta-input-player-select(
        :value="selectedPlayer"
        @input="onPlayerSelected"
        )

  //- Active players list. Shown only when developer flag is on.
  div(v-if="uiStore.showDeveloperUi")
    div(
      v-if="!allActivePlayersData"
      class="pt-5 tw-w-full tw-text-center"
      )
      b-spinner(
        label="Loading..."
        class="mt-5"
        )/

    div(v-else)
      MSingleColumnLayout
        MCard(
          title="Recently Active Players"
          data-testid="active-players-list"
          )
          MCallout(
            v-if="allActivePlayersData.length === 0"
            title="No recently active players"
            variant="neutral"
            ) No players have logged in since last server reboot.

          b-table(
            v-else
            small
            striped
            hover
            responsive
            :items="allActivePlayersData"
            :fields="recentlyActiveTableFields"
            primary-key="entityId"
            sort-by="startAt"
            sort-desc
            :tbody-tr-class="rowClass"
            class="table-fixed-column"
            @row-clicked="rowClicked"
            )
            template(#cell(entityId)="data")
              MTooltip(
                v-if="data.item.deletionStatus.startsWith('Deleted')"
                content="Player has been deleted"
                class="tw-mr-1"
                ) ☠️
              span {{ data.item.entityId }}

            template(#cell(displayName)="data")
              span {{ data.item.displayName }}
              MTooltip(
                v-if="data.item.totalIapSpend > 0"
                :content="'Total IAP spend: $' + data.item.totalIapSpend.toFixed(2, 2)"
                noUnderline
                class="tw-ml-2"
                )
                fa-icon(
                  icon="money-check-alt"
                  class="text-muted"
                  )
              MTooltip(
                v-if="data.item.isDeveloper"
                content="This player is a developer."
                noUnderline
                class="tw-ml-2"
                )
                fa-icon(
                  icon="user-astronaut"
                  size="sm"
                  class="text-muted"
                  )

            template(#cell(createdAt)="data")
              MetaTime(:date="data.item.createdAt")

            template(#cell(lastLoginAt)="data")
              MetaTime(:date="data.item.lastLoginAt")

  MetaRawData(
    :kvPair="allActivePlayersData"
    name="activePlayers"
    )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { type ActivePlayerInfo, MetaRawData, MetaTime, type PlayerListItem, useUiStore } from '@metaplay/meta-ui'
import { MCallout, MCard, MSingleColumnLayout, MTooltip, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllActivePlayersSubscriptionOptions } from '../subscription_options/players'

const uiStore = useUiStore()

const { data: allActivePlayersData, error: allActivePlayersError } = useSubscription(
  getAllActivePlayersSubscriptionOptions()
)

type BTableField = string | { key: string; label: string }

const recentlyActiveTableFields = computed((): BTableField[] => {
  const allFields: BTableField[] = [
    {
      key: 'entityId',
      label: 'ID',
    },
    {
      key: 'displayName',
      label: 'Name',
    },
    'level',
    {
      key: 'createdAt',
      label: 'Joined',
    },
  ]
  if (Math.max(document.documentElement.clientWidth || 0, window.innerWidth || 0) < 576) {
    return filterTableFields(allFields, ['entityId', 'displayName'])
  } else {
    return allFields
  }
})

function filterTableFields(allFields: BTableField[], desiredFields: string[]): BTableField[] {
  return allFields.filter((element) => {
    let key
    if (typeof element === 'string') {
      key = element
    } else {
      key = element.key
    }
    return desiredFields.includes(key)
  })
}

const router = useRouter()

const selectedPlayer = ref<PlayerListItem>()

async function onPlayerSelected(player: PlayerListItem): Promise<void> {
  selectedPlayer.value = player
  await router.push(`/players/${player.id}`)
}

async function rowClicked(item: ActivePlayerInfo): Promise<void> {
  await router.push(`/players/${item.entityId}`)
}

function rowClass(item: ActivePlayerInfo, type: any): string {
  if (!item || type !== 'row') {
    return ''
  }
  if (item.deletionStatus.startsWith('Deleted')) {
    return 'text-danger table-row-link'
  } else {
    return 'table-row-link'
  }
}
</script>
