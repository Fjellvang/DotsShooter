<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="staticInfos.featureFlags.guilds && playerData")
  MButton(
    permission="api.players.guild_tools"
    @click="guildToolsModal?.open"
    ) Guild Tools

  MModal(
    ref="guildToolsModal"
    :title="`Guild Tools for ${playerData.model.playerName || 'n/a'}`"
    @show="resetModal"
    )
    template(#action-button-icon)
      fa-icon(
        icon="chess-rook"
        class="tw-mr-1 tw-h-4 tw-w-4"
        )
    template(#default)
      div(class="!tw-overflow-x-hidden")
        h6(class="tw-mb-1") Guild Recommendations
        p(class="tw-mb-2 tw-text-sm") An example list of guilds that could be recommended for #[MBadge {{ playerData.model.playerName || 'n/a' }}].
        p(class="tw-text-sm+ tw-text-neutral-500") Note: This is not the exact list shown in the game at the moment, but rather an example of what recommendations the current game logic produces.

        div(class="tw-mb-2 tw-mt-3")
          div(
            v-if="recommendations == null"
            class="tw-my-2 tw-flex tw-flex-wrap"
            )
            b-spinner(
              class="tw-my-5"
              label="Loading..."
              )/
          //- Styling below to make this look like old b-list/group. Smaller font in everything compared to old.
          MList(
            v-else-if="recommendations && recommendations.length > 0"
            showBorder
            )
            MListItem(
              v-for="recommendation in recommendations"
              :key="recommendation.guildId"
              class="tw-px-5"
              striped
              )
              span {{ recommendation.displayName }}
              template(#top-right)
                MTextButton(:to="`/guilds/${recommendation.guildId}`") {{ recommendation.guildId }}
              template(#bottom-left)
                span {{ recommendation.numMembers }} / {{ recommendation.maxNumMembers }} members
          div(
            v-else
            class="small tw-italic tw-text-neutral-500"
            ) No guilds found. This could be because you have no guilds, or because the guild search failed. You can refresh to try again.

          div(class="tw-end-0 tw-mt-2")
            MButton(
              size="small"
              @click="refresh"
              ) Refresh
              template(#icon): fa-icon(
                icon="sync-alt"
                class="tw-mr-1 tw-h-4 tw-w-3"
                )

    template(#right-panel)
      div(class="!tw-overflow-x-hidden")
        h6(class="tw-mb-1") Guild Search
        p(class="tw-mb-2") Perform a guild search as #[MBadge {{ playerData.model.playerName || 'n/a' }}] would see it.
        span(class="tw-text-sm+ tw-text-neutral-500") You can use this tool to preview how particular players are shown guild search results in the game.

        div(class="tw-mb-2 tw-mt-3")
          //- TODO: Consider moving this to an automatic search without a button?
          meta-generated-form(
            v-model="searchContents"
            typeName="Metaplay.Core.Guild.GuildSearchParamsBase"
            :logic-version="playerData.model.logicVersion"
            @status="searchContentsValid = $event"
            )
          div(class="tw-end-0 tw-mt-2")
            MButton(
              :disabled-tooltip="!searchContentsValid ? 'Please complete the search form before searching.' : undefined"
              size="small"
              @click="search"
              ) Search

          // TODO: error handling!
          div(
            v-if="searchResults == null"
            class="tw-justify-center"
            )
            b-spinner(
              class="tw-my-5"
              label="Loading..."
              )/
          p(v-if="searchResults !== null && searchResults.isError === true") Search failed
          div(v-if="searchResults !== null && searchResults.isError === false")
            // TODO: review layout
            p #[meta-plural-label(:value="searchResults.guildInfos.length" label="result")].
            // b-table(striped responsive :items="searchResults.guildInfos")

            MList(
              v-if="searchResults.guildInfos && searchResults.guildInfos.length > 0"
              showBorder
              )
              MListItem(
                v-for="guildInfo in searchResults.guildInfos"
                :key="guildInfo.guildId"
                class="tw-px-5"
                striped
                )
                span {{ guildInfo.displayName }}
                template(#top-right)
                  MTextButton(:to="`/guilds/${guildInfo.guildId}`") {{ guildInfo.guildId }}
                template(#bottom-left)
                  span {{ guildInfo.numMembers }} / {{ guildInfo.maxNumMembers }} members
</template>

<script lang="ts" setup>
import { ref } from 'vue'

import { useGameServerApi, useStaticInfos } from '@metaplay/game-server-api'
import { MBadge, MButton, MList, MListItem, MModal, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'
import MetaGeneratedForm from '../../generatedui/components/MetaGeneratedForm.vue'

const props = defineProps<{
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))
const staticInfos = useStaticInfos()
const recommendations = ref()
const searchContents = ref<any>({})
const searchContentsValid = ref(false)
const searchResults = ref<any>([])
const guildToolsModal = ref<typeof MModal>()

async function resetModal(): Promise<void> {
  searchContents.value = {}
  searchContentsValid.value = false
  searchResults.value = []
  await refresh()
}

async function search(event: any): Promise<void> {
  searchResults.value = null
  const res = await gameServerApi.post(`/players/${playerData.value.id}/guildSearch`, searchContents.value)
  searchResults.value = res.data
}

async function refresh(): Promise<void> {
  recommendations.value = null
  const res = await gameServerApi.post(`/players/${playerData.value.id}/guildRecommendations`)
  recommendations.value = res.data
}
</script>
