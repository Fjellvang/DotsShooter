<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(:is-loading="!detailEntries")
  template(#overview)
    MPageOverviewCard(
      title="Raw Player Details"
      :subtitle="`There were a total of ${errorCount} errors encountered when trying to retrieve this player's details.`"
      :id="playerId"
      data-testid="raw-player-overview-card"
      )
      p(class="tw-text-sm tw-text-neutral-500") This page allows you to inspect a player's details even when the server is unable to correctly deserialize the player's data.

  MCard(
    v-if="detailEntries"
    title="Technical Details"
    noBodyPadding
    data-testid="raw-player-technical-details-card"
    )
    MList
      MCollapse(
        v-for="(entry, index) in detailEntries"
        :key="entry.title"
        :is-open-by-default="firstErrorIndex === index"
        extra-m-list-item-margin
        )
        template(#header)
          MListItem(noLeftPadding) {{ entry.title }}
            template(#badge)
              MBadge(
                v-if="entry.data"
                variant="success"
                ) Valid
              MBadge(
                v-else
                variant="danger"
                ) Not valid

        //- Show data or error in the collapse.
        pre(v-if="entry.data") {{ entry.data }}
        MErrorCallout(
          v-else
          :error="errorToDisplayError(entry)"
          )

  MetaRawData(
    :kv-pair="detailEntries"
    name="detailEntries"
    )
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaRawData, type PlayerRawInfoResult } from '@metaplay/meta-ui'
import {
  DisplayError,
  MBadge,
  MCard,
  MCollapse,
  MErrorCallout,
  MList,
  MListItem,
  MPageOverviewCard,
  MViewContainer,
} from '@metaplay/meta-ui-next'

import { routeParamToSingleValue } from '../coreUtils'

const gameServerApi = useGameServerApi()
const route = useRoute()

/**
 * ID of the player to display. Grabbed from the route.
 */
const playerId = computed(() => routeParamToSingleValue(route.params.id))

/**
 * Results of raw player fetch.
 */
const detailEntries = ref<PlayerRawInfoResult[]>()

/**
 * Fetch raw player data.
 */
onMounted(async () => {
  const response = await gameServerApi.get(`/players/${playerId.value}/raw`)
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  detailEntries.value = Object.values(response.data)
})

/**
 * Total number of errors found.
 */
const errorCount = computed((): number => {
  return (
    detailEntries.value?.reduce((count: number, entry: PlayerRawInfoResult) => count + (entry.error ? 1 : 0), 0) ?? 0
  )
})

/**
 * Index of the first error encountered, or -1 if no errors were found. This is used to automatically expand the first
 * error in the UI.
 */
const firstErrorIndex = computed(() => {
  if (detailEntries.value) {
    for (let i = 0; i < detailEntries.value.length; i++) {
      if (detailEntries.value[i].error) {
        return i
      }
    }
  }
  return -1
})

/**
 * Create a `DisplayError` from a `PlayerRawInfoResult` error.
 * @param result Info result. Must contain an error.
 */
function errorToDisplayError(result: PlayerRawInfoResult): DisplayError {
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const displayError = new DisplayError(result.error!.title, '')
  if (result.error?.details) {
    displayError.addDetail('Details', result.error.details)
  }
  return displayError
}
</script>
