<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :variant="singleNftCollectionData?.configWarning ? 'warning' : undefined"
  :is-loading="!singleNftCollectionData"
  :error="singleNftCollectionError"
  :alerts="alerts"
  )
  template(#overview)
    MPageOverviewCard(
      :id="singleNftCollectionData.collectionId"
      :title="singleNftCollectionData?.ledgerInfo?.name ?? 'Name unknown'"
      :subtitle="singleNftCollectionData?.ledgerInfo?.description"
      :avatar="singleNftCollectionData?.ledgerInfo?.iconUrl"
      data-testid="nft-collection-overview-card"
      )
      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Ledger
            b-td(class="text-right") {{ singleNftCollectionData.ledger }}
          b-tr
            b-td Contract Address
            b-td(
              v-if="singleNftCollectionData.hasLedger"
              style="word-break: break-all"
              class="text-right"
              ) {{ singleNftCollectionData.contractAddress }}
            b-td(
              v-else
              class="text-right text-muted"
              ) {{ singleNftCollectionData.ledger }} mode has no contracts
          b-tr
            b-td Metadata API URL
            b-td(class="text-right") {{ singleNftCollectionData.metadataApiUrl }}
          b-tr
            b-td Metadata Management
            b-td(class="text-right")
              MTooltip(:content="getMetadataManagementModeDescription(singleNftCollectionData.metadataManagementMode)") {{ singleNftCollectionData.metadataManagementMode }}

      //- Ledger-specific info. TODO: use a generated view instead?
      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="cubes")] Ledger Metadata
      div(
        v-if="!singleNftCollectionData.hasLedger"
        class="tw-w-full tw-text-center"
        )
        p(class="text-muted") {{ singleNftCollectionData.ledger }} mode has no associated ledger.
      div(
        v-else-if="!singleNftCollectionData.ledgerInfo"
        class="tw-w-full tw-text-center"
        )
        p(class="text-muted") Collection not configured in {{ singleNftCollectionData.ledgerName }}.
      div(v-else)
        b-table-simple(
          small
          responsive
          )
          b-tbody
            b-tr
              b-td Icon URL
              b-td(class="tw-text-right")
                MTextButton(:to="singleNftCollectionData.ledgerInfo.iconUrl") {{ singleNftCollectionData.ledgerInfo.iconUrl }}
            b-tr
              b-td Image URL
              b-td(class="tw-text-right")
                MTextButton(:to="singleNftCollectionData.ledgerInfo.collectionImageUrl") {{ singleNftCollectionData.ledgerInfo.collectionImageUrl }}

      template(#buttons)
        div(class="tw-flex tw-flex-wrap tw-justify-end tw-gap-1")
          MActionModalButton(
            modal-title="Initialize a New NFT"
            :action="initializeNft"
            trigger-button-label="Init single NFT"
            ok-button-label="Initialize"
            :ok-button-disabled-tooltip="!nftInitializationParamsValid ? 'Missing required fields.' : undefined"
            permission="api.nft.initialize"
            data-testid="initialize-nft"
            )
            p To mint a new NFT, its metadata need to be first initialized on the game server.
            meta-generated-form(
              v-model="nftInitializationParams"
              typeName="Metaplay.Server.AdminApi.Controllers.NftController.NftInitializationParams"
              @status="nftInitializationParamsValid = $event"
              )

          MActionModalButton(
            modal-title="Batch Initialize new NFTs"
            :action="batchInitializeNfts"
            trigger-button-label="Init NFTs from CSV"
            ok-button-label="Batch Initialize"
            @show="clearBatchInitializationState"
            data-testid="batch-initialize-nfts"
            )
            template(#default)
              h6 Upload NFT Data
              p You can upload a list of NFTs in a CSV format to initialize them all in one go.

              div(class="tw-flex tw-flex-col tw-gap-y-2")
                MInputTextArea(
                  label="Paste in a CSV string..."
                  :model-value="csvString"
                  :placeholder="csvFile != null ? 'File upload selected' : singleNftCollectionData.batchInitPlaceholderText"
                  :variant="csvString !== '' ? (csvString ? 'success' : 'danger') : 'default'"
                  :rows="5"
                  :disabled="csvFile != null"
                  @update:model-value="onCsvStringChange"
                  data-testid="entity-archive-text"
                  )

                MInputSingleFileContents(
                  label="...or upload as a file"
                  :model-value="csvFile"
                  :placeholder="csvString ? 'Manual paste selected' : 'Choose or drop a CSV file'"
                  :variant="csvFile ? (isCsvFormValid ? 'success' : 'danger') : 'default'"
                  :disabled="csvString !== ''"
                  accepted-file-types=".csv"
                  @update:model-value="csvFile = $event"
                  )

                MInputCheckbox(
                  :model-value="batchInitAllowOverwrite"
                  name="allowBatchInitializationOverwrite"
                  @update:model-value="onAllowOverwriteChange"
                  ) Allow Overwrite
                  MBadge(
                    tooltip="If overwriting is allowed, NFTs from this batch will overwrite the state of any existing NFTs with the same ids. Use with caution!"
                    shape="pill"
                    class="tw-ml-1"
                    ) ?

            template(#right-panel)
              h6 Preview Incoming Data
              b-alert(
                v-if="!nftPreview"
                show
                variant="secondary"
                ) Paste in a valid list of NFTs compatible with the game server's version to see a preview of the import results.
              b-spinner(v-if="nftPreviewIsLoading")
              MErrorCallout(
                v-if="csvValidationError"
                :error="csvValidationError"
                )

              div(v-if="nftPreview")
                div {{ maybePluralString(nftPreview.nfts.length, 'NFT') }} will be initialized.
                div(v-if="batchInitAllowOverwrite") {{ maybePluralString(nftPreview.numNftsOverwritten, 'existing NFT') }} will be overwritten.
                MList(
                  class="tw-mt-2"
                  show-border
                  )
                  MListItem(
                    v-for="nft in nftPreview.nfts.slice(0, batchInitPreviewMaxLength)"
                    :key="nft.tokenId"
                    :avatarUrl="nft.imageUrl"
                    )
                    span(v-if="nft.name") {{ nft.name }}
                    span(
                      v-else
                      class="tw-italic"
                      ) Unnamed {{ nft.typeName }}
                    span(class="small text-muted tw-ml-1") {{ collectionId }}/{{ nft.tokenId }}

                    template(#top-right)
                      MBadge(:variant="getItemStatusText(nft) === 'Minted' ? 'success' : undefined") {{ getItemStatusText(nft) }}

                    template(#bottom-left)
                      div(v-if="nft.description") {{ nft.description }}
                      span(
                        v-else
                        class="tw-italic"
                        ) No description.
                  MListItem(
                    v-if="nftPreview.nfts.length > batchInitPreviewMaxLength"
                    key="nfts-omitted-dummy"
                    )
                    span(class="small text-muted ml-2") ... and {{ maybePluralString(nftPreview.nfts.length - batchInitPreviewMaxLength, 'more NFT') }} omitted from this preview.

          MActionModalButton(
            modal-title="Initialize NFTs from existing metadata"
            :action="batchInitializeNftsFromMetadata"
            trigger-button-label="Init NFTs from metadata"
            ok-button-label="Initialize"
            :ok-button-disabled-tooltip="!nftInitializationFromMetadataParamsValid ? 'Missing required fields.' : undefined"
            permission="api.nft.initialize"
            @show="resetMetadataInitializationParams"
            data-testid="batch-initialize-nfts-from-metadata"
            )
            p You can initialize a batch of NFTs (with sequential ids) based on existing metadata publicly served at the NFTs' metadata URLs.

            meta-generated-form(
              v-model="nftInitializationFromMetadataParams"
              typeName="Metaplay.Server.AdminApi.Controllers.NftController.NftBatchInitializationFromMetadataParams"
              @status="nftInitializationFromMetadataParamsValid = $event"
              )

          MActionModalButton(
            modal-title="Refresh NFT Collection Metadata"
            :action="refreshCollectionLedgerInfo"
            trigger-button-label="Refresh metadata"
            :trigger-button-disabled-tooltip="!singleNftCollectionData.hasLedger ? `${singleNftCollectionData.ledger} mode has no ledger to refresh from.` : undefined"
            permission="api.nft.refresh_from_ledger"
            data-testid="refresh-nft-collection"
            )
            p You can immediately trigger a refresh of the collection's ledger metadata from {{ singleNftCollectionData.ledgerName }}.
            div(class="text-muted small") This happens automatically in the background, so manual refreshing is not needed in daily operations.

  //- BODY CONTENT -------------------------------
  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Collection Data

  b-row(align-h="center")
    b-col(
      lg="6"
      class="tw-mb-4"
      )
      meta-list-card(
        :itemList="allNfts"
        :searchFields="searchFields"
        :filterSets="filterSets"
        title="NFTs"
        emptyMessage="No NFTs in this collection"
        class="h-100"
        data-testid="nft-collection-nft-list"
        )
        template(#item-card="{ item }")
          MListItem(:avatarUrl="item.imageUrl")
            span(v-if="item.queryError !== null") 🛑 Failed to load!
            span(v-else-if="item.name") {{ item.name }}
            span(
              v-else
              class="tw-italic"
              ) Unnamed {{ item.typeName }}
            span(class="small text-muted tw-ml-1") {{ collectionId }}/{{ item.tokenId }}

            template(
              v-if="item.queryError === null"
              #top-right
              ) {{ getItemStatusText(item) }}

            template(
              v-if="item.queryError === null"
              #bottom-left
              )
              div(v-if="item.description") {{ item.description }}
              span(
                v-else
                class="tw-italic"
                ) No description.

            template(#bottom-right)
              MTextButton(
                :to="`/web3/nft/${collectionId}/${item.tokenId}`"
                permission="api.nft.view"
                data-testid="view-nft"
                ) View NFT

    b-col(
      lg="6"
      class="tw-mb-4"
      )
      meta-list-card(
        :itemList="allUninitializedNfts"
        :searchFields="uninitializedNftsSearchFields"
        title="Recent orphan NFTs"
        emptyMessage="No orphan NFTs encountered for this collection."
        :description="`The most recently-encountered NFTs that have been minted in ${singleNftCollectionData.ledgerName} but haven't been initialized in the game. Ideally, this should never happen in production.`"
        dangerous
        class="h-100"
        data-testid="nft-collection-uninitialized-nfts-card"
        )
        template(#item-card="{ item }")
          MListItem
            | {{ collectionId }}/{{ item.tokenId }}
            template(#top-right)
              span(v-if="item.owner === 'None'") No owning player
              span(v-else) Owning player: #[MTextButton(:to="`/players/${item.owner}`" permission="api.players.view") {{ item.owner }}]
            template(#bottom-left) Owner: {{ item.ownerAddress }}

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Admin

  b-row(align-h="center")
    b-col(
      lg="6"
      class="tw-mb-4"
      )
      audit-log-card(
        targetType="$NftCollection"
        :targetId="collectionId"
        class="h-100"
        data-testid="nft-collection-audit-log-card"
        )

  meta-raw-data(
    :kvPair="singleNftCollectionData"
    name="collection"
    )
</template>

<script lang="ts" setup>
/* eslint-disable @typescript-eslint/no-unsafe-argument */
import { debounce } from 'lodash-es'
import { computed, ref, watch } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaListFilterOption, MetaListFilterSet } from '@metaplay/meta-ui'
import {
  DisplayError,
  MActionModalButton,
  MBadge,
  MErrorCallout,
  MInputCheckbox,
  MInputTextArea,
  MInputSingleFileContents,
  MList,
  MListItem,
  MTextButton,
  MTooltip,
  useHeaderbar,
  MViewContainer,
  MPageOverviewCard,
  type MViewContainerAlert,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import AuditLogCard from '../components/auditlogs/AuditLogCard.vue'
import MetaGeneratedForm from '../components/generatedui/components/MetaGeneratedForm.vue'
import { isNullOrUndefined } from '../coreUtils'
import { getSingleNftCollectionSubscriptionOptions } from '../subscription_options/web3'

const props = defineProps<{
  /**
   * ID of the collection this NFT belongs to.
   * @example 'SomeCollection'
   */
  collectionId: string
}>()

const {
  data: singleNftCollectionData,
  error: singleNftCollectionError,
  refresh: singleNftCollectionRefresh,
} = useSubscription(getSingleNftCollectionSubscriptionOptions(props.collectionId))

const allNfts = computed((): any[] | undefined => singleNftCollectionData.value?.nfts)
const allUninitializedNfts = computed((): any[] | undefined => singleNftCollectionData.value?.uninitializedNfts)

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(
  singleNftCollectionData,
  (singleNftCollectionData) => `Manage ${singleNftCollectionData.value.collectionId || 'Collection'}`
)

const alerts = computed(() => {
  const alerts: MViewContainerAlert[] = []

  if (singleNftCollectionData.value?.configWarning?.length > 0) {
    alerts.push({
      title: singleNftCollectionData.value.configWarning.title as string,
      message: singleNftCollectionData.value.configWarning.message as string,
    })
  }

  const numPendingMetadataWrites = getNumPendingMetadataWrites()
  if (numPendingMetadataWrites !== 0) {
    alerts.push({
      title: 'Metadata write operation in progress',
      message: `The metadata files of ${maybePluralString(numPendingMetadataWrites, 'NFT')} in this collection are currently being written in the background. If new NFTs were just initialized, you should wait for this operation to complete before minting the NFTs.`,
    })
  }

  if (isNullOrUndefined(singleNftCollectionData.value)) {
    for (const ongoingMetadataDownload of singleNftCollectionData.value.ongoingMetadataDownloads) {
      const firstToken = ongoingMetadataDownload.firstTokenId
      const lastToken = ongoingMetadataDownload.lastTokenId
      const numDownloaded = ongoingMetadataDownload.numDownloaded
      const numTotal = ongoingMetadataDownload.numTotal

      alerts.push({
        key: ongoingMetadataDownload.taskId,
        title: 'Metadata download in progress',
        message: `The server is currently downloading the metadata of tokens ${firstToken} to ${lastToken}. When the download is finished, the NFTs will get initialized in the server. Progress: ${numDownloaded}/${numTotal}.`,
      })
    }
  }

  return alerts
})

function getNumPendingMetadataWrites(): number {
  if (!singleNftCollectionData.value) {
    return 0
  }

  let count = 0
  for (const nft of singleNftCollectionData.value.nfts) {
    if (nft.hasPendingMetadataWrite === true) {
      count += 1
    }
  }

  return count
}

// INDIVIDUAL NFT INIT STUFF ----------------------------------------------------

// TODO: form validation, error handling

const nftInitializationParams = ref<any>({})
const nftInitializationParamsValid = ref(false)

const { showSuccessNotification, showErrorNotification } = useNotifications()

async function initializeNft(): Promise<void> {
  // \todo #nft #nft-init-token-id-kludge
  //       Getting tokenId from nftInitializationParams is hacky:
  //       really it shouldn't be a member of nftInitializationParams at all, but
  //       should be a separate variable with its own input in the form. I only
  //       did it this way to piggyback on the automatic form generation, to
  //       easily get the input for the tokenId.
  let tokenIdUrlPart = nftInitializationParams.value.tokenId
  // Empty token id means we want the server to auto-generate the id.
  // However, an empty url part seems to cause trouble, so encode differently.
  if (tokenIdUrlPart === '' || tokenIdUrlPart === null || tokenIdUrlPart === undefined) {
    tokenIdUrlPart = 'automaticTokenId'
  }

  await useGameServerApi().post(`nft/${props.collectionId}/${tokenIdUrlPart}/initialize`, nftInitializationParams.value)
  showSuccessNotification('New NFT initialized!')
  singleNftCollectionRefresh()
  nftInitializationParams.value = {}
}

// BATCH INIT STUFF ----------------------------------------------------

const batchInitPreviewMaxLength = 5

const csvString = ref('')
const csvFile = ref<string>()
const nftPreview = ref()
const nftPreviewIsLoading = ref<boolean>(false)
const csvValidationError = ref<DisplayError>()
const batchInitAllowOverwrite = ref<boolean>(false)
const isCsvFormValid = computed(() => {
  const hasContent = getCsvContent() != null
  if (hasContent && !csvValidationError.value && !nftPreview.value) {
    return null
  } else if (nftPreview.value) {
    return true
  } else {
    return false
  }
})

function onCsvStringChange(value: string): void {
  csvString.value = value
  void validateCsvContentDebounced()
}

function onAllowOverwriteChange(value: boolean): void {
  batchInitAllowOverwrite.value = value
  void validateCsvContentNow()
}

watch(csvFile, validateCsvContentNow)

function getCsvContent(): string | null {
  if (csvString.value.length > 0) {
    return csvString.value
  } else if (csvFile.value) {
    return csvFile.value
  } else {
    return null
  }
}

function clearBatchInitializationState(): void {
  csvString.value = ''
  csvFile.value = undefined
  nftPreview.value = null
  nftPreviewIsLoading.value = false
  csvValidationError.value = undefined
  batchInitAllowOverwrite.value = false
}

async function validateCsvContentNow(): Promise<void> {
  const csvContent = getCsvContent()
  if (csvContent === null) {
    return
  }

  nftPreviewIsLoading.value = true
  csvValidationError.value = undefined
  nftPreview.value = null

  try {
    const response = await useGameServerApi().post(`nft/${props.collectionId}/batchInitialize`, {
      csv: csvContent,
      allowOverwrite: batchInitAllowOverwrite.value,
      validateOnly: true,
    })
    if (response.data.isSuccess) {
      csvValidationError.value = undefined
      nftPreview.value = {
        nfts: response.data.nfts,
        numNftsOverwritten: response.data.numNftsOverwritten,
      }
    } else {
      csvValidationError.value = new DisplayError(
        'Validation Error',
        response.data.error.message,
        undefined,
        undefined,
        [{ title: 'Details', content: response.data.error.details }]
      )
      nftPreview.value = null
    }
  } catch (ex: any) {
    const error = ex.response.data.error
    csvValidationError.value = new DisplayError('Validation Error', error.message, undefined, undefined, [
      { title: 'Details', content: error.details },
    ])
    nftPreview.value = null
  } finally {
    nftPreviewIsLoading.value = false
  }
}
const validateCsvContentDebounced = debounce(validateCsvContentNow, 500)

async function batchInitializeNfts(): Promise<void> {
  const csvContent = getCsvContent()
  const response = await useGameServerApi().post(`nft/${props.collectionId}/batchInitialize`, {
    csv: csvContent,
    allowOverwrite: batchInitAllowOverwrite.value,
  })
  if (response.data.isSuccess) {
    showSuccessNotification(`Batch of ${maybePluralString(response.data.nfts.length, 'NFT')} initialized!`)
    singleNftCollectionRefresh()
  } else {
    showErrorNotification(response.data.error.message + ' ' + response.data.error.details)
  }
}

// BATCH INIT FROM METADATA STUFF ----------------------------------------------------

const nftInitializationFromMetadataParams = ref<any>({})
const nftInitializationFromMetadataParamsValid = ref(false)

async function batchInitializeNftsFromMetadata(): Promise<void> {
  const response = await useGameServerApi().post(
    `nft/${props.collectionId}/batchInitializeFromMetadata`,
    nftInitializationFromMetadataParams.value
  )
  showSuccessNotification(
    `Batch of ${maybePluralString(response.data.nfts.length, 'NFT')} initialized based on existing metadata!`
  )
  singleNftCollectionRefresh()
}

function resetMetadataInitializationParams(): void {
  nftInitializationFromMetadataParams.value = {}
}

// REFRESH ----------------------------------------------------

async function refreshCollectionLedgerInfo(): Promise<void> {
  await useGameServerApi().post(`nft/${props.collectionId}/refresh`, nftInitializationParams.value)
  showSuccessNotification('Ledger metadata refreshed!')
  singleNftCollectionRefresh()
}

// UI FILTERS, SEARCH, ETC. ----------------------------------------------------

const searchFields = ['name', 'description', 'tokenId', 'owner', 'ownerAddress', 'typeName']

const uninitializedNftsSearchFields = ['tokenId', 'owner', 'ownerAddress']

const filterSets = [
  new MetaListFilterSet('status', [
    new MetaListFilterOption('Player-owned', (item: any) => item.owner !== 'None'),
    new MetaListFilterOption('Non-player-owned', (item: any) => item.owner === 'None' && item.isMinted),
    new MetaListFilterOption('Not minted', (item: any) => !item.isMinted),
  ]),
]

function getItemStatusText(item: any): string {
  if (item.owner !== 'None') {
    return 'Player-owned'
  } else if (item.ownerAddress !== 'None') {
    return 'Non-player-owned'
  } else {
    return 'Not minted'
  }
}

function getMetadataManagementModeDescription(mode: string): string | undefined {
  if (mode === 'Authoritative') {
    return 'NFT metadata is written by this game.'
  } else if (mode === 'Foreign') {
    return 'NFT metadata is written externally, and this game only reads the metadata.'
  } else {
    return undefined
  }
}
</script>
