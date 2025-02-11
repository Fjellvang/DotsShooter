<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Card
MCard(title="Import Game Entities")
  template(#subtitle)
    p You can use this feature to import an archive of one or more game entities into the current deployment.

  //- Action Modal.
  div(class="tw-flex tw-justify-end")
    MActionModalButton(
      modal-title="Import Game Entities"
      :action="() => importArchive(entityArchiveFile ?? entityArchiveText)"
      trigger-button-label="Open Import Menu"
      ok-button-label="Import Archive"
      :ok-button-disabled-tooltip="!isFormValid ? 'Paste in or upload a valid entity archive to proceed.' : undefined"
      permission="api.entity_archive.import"
      @show="resetModal"
      data-testid="batch-import-entities"
      )
      template(#default)
        h6(class="tw-mb-1") Paste Archive Data
        p(class="tw-mb-4") You can upload the serialized data of an #[MBadge entity archive] here to import them into the current deployment.

        MInputTextArea(
          label="Paste in an archive..."
          :model-value="entityArchiveText"
          :placeholder="entityArchiveFile != null ? 'File upload selected' : `{'entities':{'player':...`"
          :variant="isFormValid !== undefined ? (isFormValid ? 'success' : 'danger') : 'default'"
          :rows="5"
          :disabled="!!entityArchiveFile"
          class="tw-mb-2"
          @update:model-value="onEntityArchiveTextChange"
          data-testid="entity-archive-text"
          )

        MInputSingleFileContents(
          label="...or upload a file..."
          :model-value="entityArchiveFile"
          :placeholder="entityArchiveText !== '' ? 'Manual paste selected' : 'Choose or drop an entity archive file'"
          :variant="isFormValid !== undefined ? (isFormValid ? 'success' : 'danger') : 'default'"
          :disabled="entityArchiveText !== ''"
          accepted-file-types=".json"
          class="tw-mb-3"
          @update:model-value="onEntityArchiveFileChange"
          data-testid="entity-archive-file"
          )

        MInputSingleSelectSwitch(
          :model-value="overwritePolicy"
          :options="overwritePolicyOptions"
          label="Overwrite policy"
          @update:model-value="updateOverwritePolicy"
          )

        div(class="tw-mt-2 tw-text-xs tw-text-neutral-500")
          p Overwrite policy determines how to deal with conflicting entities during import:
          p(class="tw-m-0.5") #[span(class="tw-font-semibold") Ignore] - Duplicate entities will not be imported.
          p(class="tw-m-0.5") #[span(class="tw-font-semibold") Overwrite] - Duplicate entities will overwrite existing ones.
          p(class="tw-m-0.5") #[span(class="tw-font-semibold") Create New] - Duplicate entities will be imported as new entities with new unique ID's.

      template(#right-panel)
        h6(class="tw-mb-4") Preview Incoming Data
        MCallout(
          v-if="!validationResultsObject && !displayError"
          title="Preview"
          variant="neutral"
          ) Paste in a valid #[MBadge entity archive] from a compatible game version to preview what data will be copied over.

        MList(
          v-else-if="validationResultsObject && validationResultsObject.length > 0"
          showBorder
          )
          MListItem(
            v-for="entity in validationResultsObject"
            :key="entity.sourceId"
            class="tw-px-3"
            )
            span(class="font-weight-bold") Source: {{ entity.sourceId }}
            template(#top-right)
              MBadge(
                v-if="!entity.error"
                variant="success"
                ) Validation ok
              MBadge(
                v-else
                variant="danger"
                ) Validation error

            template(#bottom-left)
              span(v-if="entity.error")
                div(class="tw-text-red-500") {{ entity.error.message }}
                div(
                  v-if="entity.error.details"
                  class="tw-text-red-500"
                  ) {{ entity.error.details }}
              span(v-else) {{ importMessage(entity, 'preview') }}
        MCallout(
          v-else
          title="Empty Archive"
          ) Entity archive is empty. Are you sure you pasted the right thing?

        div(
          v-if="validationResultDiff"
          class="tw-mt-4"
          )
          div(
            style="max-height: 20.3rem"
            class="code-box text-monospace border rounded bg-light tw-w-full"
            )
            pre {{ validationResultDiff }}

        MErrorCallout(
          v-if="displayError"
          :error="displayError"
          )

      template(#bottom-panel)
        meta-no-seatbelts(class="tw-mx-auto tw-w-7/12")

      //- Results.
      template(#result-panel)
        p(class="tw-mb-4") Import complete, below are the results:
        MList(
          v-if="importResultsObject && importResultsObject.length > 0"
          showBorder
          )
          MListItem(
            v-for="entity in importResultsObject"
            :key="entity.sourceId"
            class="tw-px-3"
            )
            span(v-if="String(entity.destinationId).startsWith('Player')") #[fa-icon(icon="user")] {{ entity.destinationId }}
            span(v-else-if="String(entity.destinationId).startsWith('Guild')") #[fa-icon(icon="chess-rook")] {{ entity.destinationId }}]
            span(v-else) {{ entity.destinationId }}
            template(#top-right)
              MBadge(
                v-if="entity.status === 'Success'"
                variant="success"
                ) Imported
              MBadge(
                v-else-if="entity.status === 'Error'"
                variant="danger"
                ) Error
              MBadge(
                v-else
                variant="warning"
                ) Skipped
            template(#bottom-left) {{ importMessage(entity) }}
            template(#bottom-right)
              span(v-if="entity.status === 'Success'")
                span(v-if="String(entity.destinationId).startsWith('Player')") #[MTextButton(:to="`/players/${entity.destinationId}`") View player]
                span(v-else-if="String(entity.destinationId).startsWith('Guild')") #[MTextButton(:to="`/guilds/${entity.destinationId}`") View guild]
        MCallout(
          v-else
          title="Empty Archive"
          ) Empty entity archive. Nothing was imported.
</template>

<script lang="ts" setup>
import axios from 'axios'
import type { CancelTokenSource } from 'axios'
import { computed, getCurrentInstance, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  DisplayError,
  MActionModalButton,
  MBadge,
  MCallout,
  MCard,
  MErrorCallout,
  MInputSingleSelectSwitch,
  MInputSingleFileContents,
  MInputTextArea,
  MList,
  MListItem,
  MTextButton,
} from '@metaplay/meta-ui-next'

const gameServerApi = useGameServerApi()
const entityArchiveText = ref('')
const entityArchiveFile = ref<string>()
const validationResultsObject = ref()
const validationResultDiff = ref('')
const displayError = ref<DisplayError>()
const overwritePolicy = ref<'ignore' | 'overwrite' | 'createnew'>('ignore')

function updateOverwritePolicy(value: 'ignore' | 'overwrite' | 'createnew'): void {
  overwritePolicy.value = value
  validateArchive(entityArchiveFile.value ?? entityArchiveText.value).catch((error) => {
    console.error(error)
  })
}

const importResultsObject = ref()

const isFormValid = computed(() => {
  const hasArchive = entityArchiveText.value !== '' || entityArchiveFile.value !== null
  if (hasArchive && !validationResultsObject.value) {
    return undefined
  } else if (validationResultsObject.value) {
    return true
  } else {
    return false
  }
})

const overwritePolicyOptions: Array<{
  value: 'ignore' | 'overwrite' | 'createnew'
  label: string
}> = [
  { value: 'ignore', label: 'Ignore' },
  { value: 'overwrite', label: 'Overwrite' },
  { value: 'createnew', label: 'Create New' },
]

function resetModal(): void {
  entityArchiveText.value = ''
  entityArchiveFile.value = undefined
  validationResultsObject.value = undefined
  displayError.value = undefined
  overwritePolicy.value = 'ignore'
}

let cancelTokenSource: CancelTokenSource

async function validateArchive(newText?: string): Promise<void> {
  displayError.value = undefined
  validationResultsObject.value = undefined
  validationResultDiff.value = ''

  if (newText) {
    // Get the payload data that we want to validate.
    let payload
    try {
      payload = calculatePayload(newText)
    } catch (e: any) {
      displayError.value = new DisplayError(
        'Calculating payload failed',
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        e.message
      )
      return
    }

    // Send the payload to the server to validate it.
    try {
      if (cancelTokenSource) {
        cancelTokenSource.cancel('Request canceled by user interaction.')
      }
      cancelTokenSource = axios.CancelToken.source()
      const result = (
        await gameServerApi.post(`/entityArchive/validate?overwritePolicy=${overwritePolicy.value}`, payload)
      ).data
      if (result.error) {
        // If the result has an error object then it failed.
        displayError.value = new DisplayError(
          'Validation failed',
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          result.error.message,
          500,
          undefined,
          [{ title: 'Details', content: result.error.details }]
        )
      } else {
        // Otherwise there must be a data object, indicating success.
        validationResultsObject.value = result.entities
        // Otherwise there must be a data object, indicating success.
        result.entities.map((res: any) => {
          if (res.overwriteDiff !== null) {
            validationResultDiff.value = res.overwriteDiff
          }
          return validationResultDiff.value
        })
      }
    } catch (e: any) {
      if (axios.isCancel(e)) {
        console.log(e)
        // Ignore.
      } else {
        displayError.value = new DisplayError(
          'Validation failed',
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          e.response.data.error.message
        )
      }
    }
  }
}
const vueInstance = getCurrentInstance()?.proxy as any

/**
 * Text to display to let user know what happens after entity is imported.
 * @param entity  The imported entity.
 * @param displayStatus Current stage at which message is being shown. Either during 'Preview' or after import is complete.
 */
function importMessage(entity: any, importStage?: string): string {
  if (entity.status === 'Success' && overwritePolicy.value === 'ignore') {
    return importStage === 'preview' ? 'New entity will be created.' : 'New entity created.'
  } else if (entity.status === 'Ignored') {
    return importStage === 'preview'
      ? 'Existing entity will be preserved.'
      : `Existing entity ${entity.sourceId} has been preserved.`
  }
  if (entity.status === 'Success' && overwritePolicy.value === 'overwrite' && entity.overwriteDiff !== null) {
    return importStage === 'preview'
      ? 'Existing entity will be overwritten.'
      : `Existing entity ${entity.sourceId} has been overwritten.`
  } else if (entity.status === 'Success' && overwritePolicy.value === 'overwrite' && entity.overwriteDiff === null) {
    return importStage === 'preview' ? 'New entity will be created.' : 'New entity created.'
  } else if (entity.status === 'Success' && overwritePolicy.value === 'createnew') {
    return importStage === 'preview'
      ? `A new entity will be created from ${entity.sourceId}.`
      : `A new entity ${entity.destinationId} has been created. `
  } else return ''
}

function onEntityArchiveTextChange(newText: string): void {
  entityArchiveText.value = newText
  void validateArchive(newText)
}

function onEntityArchiveFileChange(newFile: string | undefined): void {
  entityArchiveFile.value = newFile
  void validateArchive(newFile)
}

async function importArchive(data: string): Promise<void> {
  const payload = calculatePayload(data)
  const result = (await gameServerApi.post(`/entityArchive/import?overwritePolicy=${overwritePolicy.value}`, payload))
    .data

  if (result.error) {
    // \todo: custom DisplayError type for API errors?
    throw new Error(`${result.error.message}\n${result.error.details}`)
  }

  importResultsObject.value = result.entities
  vueInstance?.$bvModal.show('import-entities-results-modal')
}

function calculatePayload(data: string): { entities: any } {
  let payload: any
  try {
    payload = JSON.parse(data)
  } catch (e: any) {
    throw new Error(`Could not parse archive. Got '${e.message}'.`)
  }

  // Validate the entity archive somewhat.
  if (typeof payload !== 'object') {
    throw new Error(`Entity archive must be an object. Got '${typeof payload}'.`)
  }
  if (Array.isArray(payload)) {
    throw new Error("Entity archive must be an object. Got 'array'.")
  }
  if (!('entities' in payload)) {
    throw new Error('Entity archive must contain entities but none found.')
  }
  return payload
}
</script>
