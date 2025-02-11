<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData || allowDebugExport")
  MActionModalButton(
    modal-title="Export Player"
    :action="downloadOk"
    trigger-button-label="Export Player"
    variant="danger"
    ok-button-label="Download"
    :ok-button-disabled-tooltip="!exportArchive ? 'No data to export.' : undefined"
    disable-safety-lock
    permission="api.entity_archive.export"
    @show="fetchExportData()"
    data-testid="export-player"
    )
    template(#default)
      p(class="tw-mb-1") This is the persisted player data for #[MBadge {{ playerData?.model.playerName || 'n/a' }}] as an #[MBadge entity archive]. You can use it for raw debugging as well as copying players between deployments.
      p(class="tw-mb-4 tw-text-sm tw-text-neutral-500") Please note that you are taking a copy of a player's personally identifiable information (PII) and you should only do this if you have a legitimate reason or the player's consent.

      h6(class="tw-mb-1") Serialized Player Data
        MClipboardCopy(
          :contents="exportArchive"
          :disabled="exportSize >= sizeLimit"
          class="tw-ml-1"
          data-testid="copy-player-to-clipboard"
          )

      div(v-if="exportSize < sizeLimit")
        pre(
          class="code-box tw-h-[10rem] tw-text-wrap tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600"
          )
          span(
            v-if="exportArchive"
            data-testid="export-payload"
            ) {{ exportArchive }}
          span(v-else)
            b-skeleton(width="85%")
            b-skeleton(width="55%")
            b-skeleton(width="70%")

            b-skeleton(
              width="80%"
              class="tw-mt-4"
              )
            b-skeleton(width="65%")

        div(
          v-if="exportArchive"
          class="tw-mb-4 tw-mt-2 tw-w-full tw-text-right tw-text-xs tw-text-neutral-500"
          )
          span Export archive size:&nbsp;
          meta-abbreviate-number(:value="exportSize")

      MCallout(
        v-else
        title="Export Preview Disabled"
        )
        p Export preview and copying disabled because of its large size of #[meta-abbreviate-number(:value="exportSize")]b! You can still download the data 👍
        p(class="tw-text-sm tw-text-orange-800") Some OS & browser combinations are known to have performance issues parsing large blobs of data. Let us know if you are one of them and we'll figure it out!

    template(#ok-button-icon)
      fa-icon(
        icon="file-download"
        class="tw-mb-[0.05rem] tw-h-3.5 tw-w-4"
        )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { roughSizeOfObject } from '@metaplay/meta-ui'
import { MActionModalButton, MBadge, MCallout, MClipboardCopy } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { downloadDataAsJson } from '../../../coreUtils'
import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the target player whose data is to be exported.
   */
  playerId: string
  /**
   * Allow player exporting even when the player can't be loaded by the server, due to
   * deserialization or migration issues. Should only be used for debugging scenarios.
   * Defaults to false.
   */
  allowDebugExport?: boolean
}>()

const gameServerApi = useGameServerApi()

/**
 * Subscribe to the target player's data.
 */
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * Archive or record containing the player data that is to be exported.
 */
const exportArchive = ref<string>()

/**
 * Estimated size of the JSON file that is to be exported.
 */
const exportSize = computed(() => {
  return roughSizeOfObject(exportArchive.value)
})

/**
 * If the file is larger than this then we don't show it in the UI for performance reasons.
 */
const sizeLimit = 30_0000

/**
 * Custom entity data type.
 */
interface EntityInfo {
  player: string[]
  guild?: string[]
}

/**
 * Retrieve the player data that is to be exported.
 */
async function fetchExportData(): Promise<void> {
  // Clear the export data before fetching new data.
  exportArchive.value = undefined

  // Construct the query.
  const entities: EntityInfo = {
    player: [props.playerId],
  }
  if (playerData.value?.guild) {
    entities.guild = [playerData.value.guild.id]
  }
  const payload = { entities, allowExportOnFailure: props.allowDebugExport }

  // Fetch new data from the server.
  exportArchive.value = JSON.stringify((await gameServerApi.post('/entityArchive/export', payload)).data)
}

/**
 * Download the exported data as JSON.
 */
async function downloadOk(): Promise<void> {
  const exportFileName = `${props.playerId.replace(':', '_')}.export`

  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  downloadDataAsJson(exportArchive.value!, exportFileName)
}
</script>
