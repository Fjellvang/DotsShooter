<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Import LiveOps Events"
  :action="() => importLiveOpsEvent(false)"
  trigger-button-label="Import"
  ok-button-label="Import Events"
  :ok-button-disabled-tooltip="!isImportValid ? 'Upload a valid file to proceed.' : undefined"
  permission="api.liveops_events.import"
  trigger-button-full-width
  @show="onShow"
  )
  template(#default)
    div(class="tw-mb-3 tw-text-sm") You can import LiveOps Events into this environment by uploading a LiveOps Event JSON file. This allows you to move events between environments.
    MInputSingleFileContents(
      :model-value="liveOpsEventFileContents"
      label="Upload a File"
      accepted-file-types=".json"
      @update:model-value="(event) => onLiveOpsEventFileContentsChange(event)"
      )
    MInputSingleSelectSwitch(
      :model-value="conflictPolicy"
      label="Conflict Policy"
      :options="conflictPolicyOptions"
      class="tw-mt-3"
      @update:model-value="(event) => onConflictPolicyChange(event)"
      )

    div(class="tw-mt-2 tw-text-xs tw-text-neutral-500")
      p Conflict policy determines how to deal with conflicting events during import:
      p(class="tw-m-0.5 tw-mb-1") #[span(class="tw-font-semibold") Disallow] - Duplicate events will not be allowed.
      p(class="tw-m-0.5 tw-mb-1") #[span(class="tw-font-semibold") Overwrite] - Duplicate events will overwrite existing ones.
      p(class="tw-m-0.5") #[span(class="tw-font-semibold") Keep Old] - Duplicate events will be ignored.

  template(#right-panel)
    MErrorCallout(
      v-if="generalDiagnosticsDisplayError"
      :error="generalDiagnosticsDisplayError"
      )
    LiveOpsEventImportListCard(
      v-else
      list-mode="preview"
      :is-import-valid="isImportValid"
      :import-results="importResults"
      )

  template(#result-panel)
    MErrorCallout(
      v-if="!isImportValid"
      :error="importFailureDisplayError"
      class="tw-mb-3"
      )
    LiveOpsEventImportListCard(
      list-mode="result"
      :is-import-valid="isImportValid"
      :import-results="importResults"
      )
</template>

<script lang="ts" setup>
import { debounce } from 'lodash-es'
import { ref, watch, computed } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MInputSingleSelectSwitch,
  MInputSingleFileContents,
  MErrorCallout,
  DisplayError,
} from '@metaplay/meta-ui-next'
import { maybePluralString, maybePluralPrefixString } from '@metaplay/meta-utilities'

import {
  LiveOpsEventImportConflictPolicy,
  type LiveOpsEventImportResult,
  type ImportLiveOpsEventsRequest,
  type ImportLiveOpsEventsResponse,
  type LiveOpsEventDiagnostic,
} from '../../liveOpsEventServerTypes'
import LiveOpsEventImportListCard from './LiveOpsEventImportListCard.vue'

const gameServerApi = useGameServerApi()

// Modal --------------------------------------------------------------------------------------------------------------

async function onShow(): Promise<void> {
  // Reset state
  liveOpsEventFileContents.value = undefined
  conflictPolicy.value = LiveOpsEventImportConflictPolicy.Disallow
  importResults.value = []
}

// Form inputs --------------------------------------------------------------------------------------------------------

/**
 * String value that holds the serialized contents of the LiveOps Event JSON file.
 */
const liveOpsEventFileContents = ref<string>()

/**
 * Handles the change in the `liveOpsEventFileContents`.
 * @param newValue The new value of the `liveOpsEventFileContents`.
 */
function onLiveOpsEventFileContentsChange(newValue: string | undefined): void {
  // Debounce (ie: delay) the change.
  debounceLiveOpsEventFileContentsChange(newValue)

  // But we want the UI to immediately enter loading state and block submission.
  importResults.value = undefined
  importDiagnostics.value = []
  isImportValid.value = false
}

const debounceLiveOpsEventFileContentsChange = debounce((newValue: string | undefined) => {
  // If we re-upload the same file then `liveOpsEventFileContents` does not change, but we still want to trigger the
  // import validation. To make this happen, we toggle `liveOpsEventFileContents` to `undefined` first so that Vue
  // reactivity recognizes the change to the new value.
  if (liveOpsEventFileContents.value === newValue) {
    liveOpsEventFileContents.value = undefined
  }
  liveOpsEventFileContents.value = newValue
}, 300)

/**
 * Specifies the rules of how conflicts are resolved when importing events with the same ID.
 */
const conflictPolicy = ref<LiveOpsEventImportConflictPolicy>(LiveOpsEventImportConflictPolicy.Disallow)

const conflictPolicyOptions: Array<{
  value: LiveOpsEventImportConflictPolicy
  label: string
}> = [
  { value: LiveOpsEventImportConflictPolicy.Disallow, label: 'Disallow' },
  { value: LiveOpsEventImportConflictPolicy.Overwrite, label: 'Overwrite' },
  { value: LiveOpsEventImportConflictPolicy.KeepOld, label: 'Keep Old' },
]

/**
 * Handles the change in the `conflictPolicy`.
 * @param newValue The new value of the `conflictPolicy`.
 */
function onConflictPolicyChange(newValue: LiveOpsEventImportConflictPolicy): void {
  // Debounce (ie: delay) the change.
  debounceConflictPolicyChange(newValue)

  // But we want the UI to immediately enter loading state and block submission.
  importResults.value = undefined
  importDiagnostics.value = []
  isImportValid.value = false
}

const debounceConflictPolicyChange = debounce((newValue: LiveOpsEventImportConflictPolicy) => {
  conflictPolicy.value = newValue
}, 300)

/**
 * Watch for changes in `liveOpsEventFileContents` and `conflictPolicy` and then trigger an import validation from the server.
 */
watch(
  [liveOpsEventFileContents, conflictPolicy],
  async () => {
    await importLiveOpsEvent(true)
  },
  { deep: true }
)

// Validation ---------------------------------------------------------------------------------------------------------

/**
 * Results of an import. `undefined` indicates that we are waiting for new results.
 */
const importResults = ref<LiveOpsEventImportResult[] | undefined>([])

/**
 * Diagnostics from an import.
 */
const importDiagnostics = ref<LiveOpsEventDiagnostic[]>([])

/**
 * Indicates whether the import validation was successful.
 */
const isImportValid = ref<boolean>(false)

/**
 * Call the server with `liveOpsEventFileContents` based on `conflictPolicy` to import the events.
 * @param validateOnly - If true, the server will only validate the event but not import it.
 */
async function importLiveOpsEvent(validateOnly: boolean): Promise<void> {
  if (liveOpsEventFileContents.value) {
    // File has contents, ask the server to validate it.
    const payload: ImportLiveOpsEventsRequest = {
      validateOnly,
      conflictPolicy: conflictPolicy.value,
      package: liveOpsEventFileContents.value,
    }

    // Call the server.
    const response = (await gameServerApi.post<ImportLiveOpsEventsResponse>('/importLiveOpsEvents', payload)).data

    // Extract the results.
    importResults.value = response.eventResults
    importDiagnostics.value = response.generalDiagnostics
    isImportValid.value = response.isValid
  } else {
    // File contents is empty, don't bother the server.
    importResults.value = []
    importDiagnostics.value = []
  }
}

/**
 * Errors to be displayed when the import validation fails.
 */
const generalDiagnosticsDisplayError = computed((): DisplayError | undefined => {
  if (importDiagnostics.value.length > 0) {
    // If any diagnostics in the list, create a `DisplayError` object from them.
    const errorCount = importDiagnostics.value.length
    const displayError = new DisplayError(
      'Validation Failed',
      // NOTE unsure about english grammar below, needs to be checked
      `There ${maybePluralPrefixString(errorCount, 'was', 'were')} ${maybePluralString(errorCount, 'error')} while validating the import file.`,
      'Error'
    )

    // For each diagnostic in the `importDiagnostics` list, add a detail.
    importDiagnostics.value.forEach((diagnostic: LiveOpsEventDiagnostic) => {
      displayError.addDetail(diagnostic.level, diagnostic.message ?? 'No reason provided.')
    })

    return displayError
  } else {
    // If there are no diagnostics, return `undefined` to indicate that there are no errors on a general level
    // and show the validated events in the import preview.
    return undefined
  }
})

/**
 * Error to be displayed when the import operation fails.
 */
const importFailureDisplayError = new DisplayError(
  'Import Failed',
  `This is most likely because the server state changed between validation and import.
  The events listed below have not been imported and no server state was changed by this operation.`,
  'Error'
)
</script>
