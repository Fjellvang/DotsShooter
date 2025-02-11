<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Export LiveOps Events"
  :action="onDownloadClicked"
  trigger-button-label="Export"
  ok-button-label="Download"
  :ok-button-disabled-tooltip="disableDownloadMessage"
  disable-safety-lock
  permission="api.liveops_events.export"
  trigger-button-full-width
  @show="onShow"
  @hide="onHide"
  )
  template(#default)
    div(class="tw-mb-3 tw-text-sm") Enter the IDs of the LiveOps Events you want to export in to the text area below.
    MInputTextArea(
      :model-value="liveOpsIdBatchImportString"
      :rows="5"
      placeholder="Comma separated list of IDs:\n03cf3f5a4e5cca0-0-c3be913379de7a6a, 03cf3f7539b7cd0-0-a33bcde46cabb06e, ..."
      @update:model-value="onLiveOpsIdBatchImportStringChange($event)"
      )

  template(#right-panel)
    meta-list-card(
      title="Export Preview"
      :item-list="exportResults"
      :page-size="4"
      empty-message="No events available to preview yet."
      :dangerous="!!disableDownloadMessage"
      )
      template(#item-card="{ item: exportedEvent }")
        MListItem
          span(v-if="exportedEvent.isValid") {{ exportedEvent.eventInfo?.displayName }}
          span(v-else) {{ exportedEvent.error }}
          template(#bottom-left) {{ exportedEvent.eventId }}
          template(#top-right)
            MBadge(:variant="exportedEvent.isValid ? 'success' : 'danger'")
              span(v-if="exportedEvent.isValid") Event Found
              span(v-else) Error

  template(#ok-button-icon)
    fa-icon(
      icon="file-download"
      class="tw-mb-[0.05rem] tw-h-3.5 tw-w-4"
      )
</template>

<script lang="ts" setup>
import { debounce } from 'lodash-es'
import { watch, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaListCard } from '@metaplay/meta-ui'
import { MActionModalButton, MInputTextArea, MListItem, MBadge } from '@metaplay/meta-ui-next'

import { downloadDataAsJson } from '../../coreUtils'
import type {
  ExportLiveOpsEventsRequest,
  ExportLiveOpsEventsResponse,
  LiveOpsEventExportResult,
} from '../../liveOpsEventServerTypes'

const gameServerApi = useGameServerApi()

const props = defineProps<{
  /**
   * Optional: Pre-fills the list of IDs to be exported.
   */
  prefillText?: string
}>()

/**
 * Input string entered by the user.
 */
const liveOpsIdBatchImportString = ref('')

/**
 * Package data to be downloaded.
 */
const downloadPackage = ref<string>()

/**
 * Results of an export attempt. `undefined` indicates that we are waiting for new results.
 */
const exportResults = ref<LiveOpsEventExportResult[] | undefined>([])

/**
 * Tooltip message to display in the download button.
 */
const disableDownloadMessage = ref<string | undefined>('Enter IDs of the LiveOps events to export.')

/**
 * Handles the change in the `liveOpsIdBatchImportString`.
 * @param newValue The new value of the `liveOpsIdBatchImportString`.
 */
function onLiveOpsIdBatchImportStringChange(newValue: string): void {
  // Debounce (ie: delay) the change.
  debounceLiveOpsIdBatchImportString(newValue)

  // But we want the UI to immediately enter loading state and block submission by disabling the download button.
  disableDownloadMessage.value = 'Validating IDs.'
  exportResults.value = undefined
}

const debounceLiveOpsIdBatchImportString = debounce((newValue: string) => {
  liveOpsIdBatchImportString.value = newValue
}, 300)

/**
 * Watch for changes in the input string liveOpsIdBatchImportString and then trigger an export from the server.
 */
watch(liveOpsIdBatchImportString, async (newVal) => {
  // Extract event IDs from the input string from comma separated list of IDs.
  const inputEventIdList: string[] = newVal
    .split(/,+/)
    .map((id) => id.trim())
    .filter((id) => id !== '')
  await exportLiveOpsEvents(inputEventIdList)
})

/**
 * Call the server with the eventIds to generate the exported JSON file.
 * @param eventIds List of event IDs to export.
 */
async function exportLiveOpsEvents(eventIds: string[]): Promise<void> {
  if (eventIds.length === 0) {
    // If there are no IDs then the result will be empty, so don't bother the server
    exportResults.value = []
    disableDownloadMessage.value = 'Enter IDs to export.'
  } else {
    // If there are IDs then we need to ask the server to validate and export them

    // Set the result list to undefined so that the export preview shows a loading skeleton.
    exportResults.value = undefined

    const payload: ExportLiveOpsEventsRequest = {
      eventIds,
    }

    const response = (await gameServerApi.post<ExportLiveOpsEventsResponse>('/exportLiveOpsEvents', payload)).data

    if (response.isValid) {
      exportResults.value = response.eventResults
      downloadPackage.value = response.package
      disableDownloadMessage.value = undefined
    } else {
      // Sort events with errors to the top of the list.
      exportResults.value = response.eventResults.sort((a, b) => {
        return a.isValid === b.isValid ? 0 : a.isValid ? 1 : -1
      })
      disableDownloadMessage.value = 'Review the ID errors to enable download.'
    }
  }
}

/**
 * Download the exported JSON file.
 */
async function onDownloadClicked(): Promise<void> {
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  downloadDataAsJson(downloadPackage.value!, 'live-ops-event.export')
}

async function onShow(): Promise<void> {
  liveOpsIdBatchImportString.value = props.prefillText ?? ''
}

async function onHide(): Promise<void> {
  // Clearing the state so that when `liveOpsIdBatchImportString` is updated in the `onShow`, the change is recognized.
  exportResults.value = []
  downloadPackage.value = undefined
  liveOpsIdBatchImportString.value = ''
}
</script>
