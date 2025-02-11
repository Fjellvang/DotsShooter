<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  MActionModalButton(
    modal-title="GDPR Data Export"
    :action="downloadData"
    trigger-button-label="GDPR Export"
    ok-button-label="Download"
    :ok-button-disabled-tooltip="!exportData ? 'No data to download.' : undefined"
    disable-safety-lock
    permission="api.players.gdpr_export"
    @show="fetchData()"
    data-testid="gdpr-export"
    )
    template(#default)
      h6(class="tw-mb-1") Personal Data of {{ playerData.model.playerName || 'n/a' }}
      p You can use this function to download personal data associated to player ID #[MBadge {{ playerData.id }}] stored in this deployment's database.

    template(#right-panel)
      MCallout(title="Other Data Source")
        p Your company might have other personal data associated with this ID in third party tools like analytics. You'll need to export those separately.
        p The player ID might also show up in short lived system logs like automatic error reports. Those logs do not contain any personal information in addition to this ID and will be automatically deleted according to your retention policies.

    template(#bottom-panel)
      h6(class="tw-mb-2") Export Preview
      div(v-if="exportSize < sizeLimit")
        pre(class="code-box tw-h-[10rem] tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600")
          span(
            v-if="exportData"
            data-testid="export-payload"
            ) {{ exportData }}
          span(v-else)
            b-skeleton(width="85%")
            b-skeleton(width="55%")
            b-skeleton(width="70%")

            b-skeleton(
              width="80%"
              class="tw-mt-4"
              )
            b-skeleton(width="65%")

      MCallout(
        v-else
        title="Export Preview Disabled"
        )
        p Export preview disabled because of its large size of #[meta-abbreviate-number(:value="exportSize")]b! You can still download the data 👍
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
import { MActionModalButton, MBadge, MCallout } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { downloadDataAsJson } from '../../../coreUtils'
import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player to export.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()

/**
 * Subscribe to the target player's data.
 */
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * GDPR export data received from the server. Note that this is deliberately an opaque `object` and not an actual type
 * as we don't want the dash to be able to use the data in any way.
 */
const exportData = ref<object>()

/**
 * Estimated size of the JSON file that is to be exported.
 */
const exportSize = computed(() => {
  return roughSizeOfObject(exportData.value)
})

/**
 * If the file is larger than this then we don't show it in the UI for performance reasons.
 */
const sizeLimit = 30_0000

/**
 * Fetch player export data from the server.
 */
async function fetchData(): Promise<void> {
  // Clear the export data before fetching new data.
  exportData.value = undefined

  // Fetch new data from the server.
  exportData.value = (await gameServerApi.get(`/players/${playerData.value.id}/gdprExport`)).data
}

/**
 * Download the exported data as JSON.
 */
async function downloadData(): Promise<void> {
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion, @typescript-eslint/no-unsafe-argument
  downloadDataAsJson(exportData.value!, playerData.value.id)
}
</script>
