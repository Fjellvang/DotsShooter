<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :variant="!singleNftData?.isMinted ? 'warning' : undefined"
  :is-loading="!singleNftData"
  :error="singleNftError"
  :alerts="alerts"
  )
  template(#overview)
    MPageOverviewCard(
      :id="tokenId"
      :title="singleNftData.name || `Unnamed ${singleNftData.typeName}`"
      :subtitle="singleNftData.description"
      :avatar="singleNftData.imageUrl"
      data-testid="nft-overview-card"
      )
      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Mint Status
            b-td(class="text-right")
              MTooltip(
                v-if="singleNftData.isMinted"
                content="The NFT has been minted in the ledger."
                class="text-success"
                ) Minted
              MTooltip(
                v-else
                content="The NFT hasn't yet been seen in the ledger and for now only exists within the game."
                class="text-warning"
                ) Not minted
          b-tr
            b-td C# type name
            b-td(class="text-right") {{ singleNftData.typeName }}
          b-tr
            b-td Collection
            b-td(class="text-right") #[MTextButton(permission="api.nft.view" :to="`/web3/nft/${collectionId}`") {{ collectionId }}]
          b-tr
            b-td Image URL
            b-td(class="tw-text-right")
              MTextButton(:to="singleNftData.imageUrl") {{ singleNftData.imageUrl }}
          b-tr
            b-td Owning Address
            b-td(class="text-right")
              span(v-if="singleNftData.ownerAddress === 'None' && !singleNftData.isMinted") None (not minted)
              span(v-else-if="singleNftData.ownerAddress === 'None' && singleNftData.isMinted") Unknown
              span(
                v-else
                style="word-break: break-all"
                ) {{ singleNftData.ownerAddress }}
          b-tr
            b-td Owning Player
            b-td(class="text-right")
              MTooltip(
                v-if="singleNftData.owner === 'None'"
                :content="getNoOwningPlayerTooltip(singleNftData.ownerAddress, singleNftData.isMinted)"
                class="text-muted tw-italic"
                ) None
              span(v-else) #[MTextButton(:to="`/players/${singleNftData.owner}`" permission="api.players.view") {{ singleNftData.owner }}]
          b-tr
            b-td Metadata URL
            b-td(class="tw-text-right")
              MTextButton(:to="singleNftData.metadataUrl") {{ singleNftData.metadataUrl }}

      template(#buttons)
        div(class="tw-flex tw-justify-end tw-gap-x-2")
          MActionModalButton(
            modal-title="Refresh External Ownership Status"
            :action="refreshFromLedger"
            trigger-button-label="Refresh Ownership"
            :trigger-button-disabled-tooltip="!singleNftData.hasLedger ? `${singleNftData.ledger} mode has no ledger to refresh from.` : undefined"
            ok-button-label="Refresh"
            permission="api.nft.refresh_from_ledger"
            data-testid="refresh-nft"
            )
            p You can immediately re-fetch this NFT's ownership from the ledger.
            div(class="tw-text-xs+ tw-text-neutral-500") This happens automatically in the background, so manual refreshing is not needed in daily operations.

          MActionModalButton(
            modal-title="Force Re-save NFT's Public Metadata"
            :action="republishMetadata"
            trigger-button-label="Re-save Metadata"
            :trigger-button-disabled-tooltip="cannotPublishMetadataExplanation"
            ok-button-label="Republish"
            permission="api.nft.republish_metadata"
            data-testid="republish-nft-metadata"
            )
            p You can immediately regenerate the NFT's public metadata that gets saved in the NFT collection's S3 bucket.
            div(class="tw-text-xs+ tw-text-neutral-500") This happens automatically in the background, so manual re-saving is not needed in daily operations.

          MActionModalButton(
            modal-title="Edit the NFT"
            :action="editNft"
            trigger-button-label="Edit"
            ok-button-label="Edit"
            variant="danger"
            permission="api.nft.edit"
            @show="prefillNftEditParams"
            data-testid="edit-nft"
            )
            // TODO: UX pass
            p You can manually edit the NFT's state. This also updates the NFT's metadata.
            p(class="tw-text-xs+ tw-text-red-400") This is an advanced developer feature and should be used carefully. If concurrent edits happen from elsewhere in the backend, some of the changes may be lost!
            meta-generated-form(
              v-model="nftEditParams"
              typeName="Metaplay.Server.AdminApi.Controllers.NftController.NftEditParams"
              @status="nftEditParamsValid = $event"
              )

  b-row(
    no-gutters
    align-v="center"
    class="mb-2"
    )
    h3 NFT Data

  b-row(align-h="center")
    // TODO: revisit when generated views support non-MetaMembers (e.g. computed properties), or work around some other way
    //b-col(lg="6").mt-4
      b-card.h-100(data-testid="nft-game-state-card")
        b-card-title
          fa-icon(icon="cube").mr-2
          | NFT Game State

        p TODO: generated view of the metadata?

      meta-generated-card(
        title="NFT Game State"
        icon="cube"
        :value="singleNftData"
        )

    b-col(
      lg="6"
      class="mt-4"
      )
      b-card(
        class="h-100"
        data-testid="nft-public-data-preview-card"
        )
        b-card-title
          fa-icon(
            icon="code"
            class="mr-2"
            )
          | Public Data Preview
        div This is a preview of the NFT's current metadata as sent to 3rd party stores.
        div(class="text-muted small") Note: There may be some latency in how often 3rd party sites re-fetch this data.
        pre(class="code-box border rounded bg-light tw-mt-4") {{ singleNftData.metadata }}

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Admin

  b-row(align-h="center")
    b-col(
      lg="6"
      class="mt-4"
      )
      audit-log-card(
        targetType="$Nft"
        :targetId="`${collectionId}/${tokenId}`"
        class="h-100"
        data-testid="nft-audit-log-card"
        )

  meta-raw-data(
    :kvPair="{ collectionId, tokenId, singleNftData }"
    name="data"
    )/
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MPageOverviewCard,
  MTextButton,
  MTooltip,
  MViewContainer,
  useHeaderbar,
  useNotifications,
  type MViewContainerAlert,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import AuditLogCard from '../components/auditlogs/AuditLogCard.vue'
import MetaGeneratedForm from '../components/generatedui/components/MetaGeneratedForm.vue'
import { isNullOrUndefined } from '../coreUtils'
import { getSingleNftSubscriptionOptions } from '../subscription_options/web3'

const gameServerApi = useGameServerApi()

const props = defineProps<{
  /**
   * ID of the collection this NFT belongs to.
   * @example 'SomeCollection'
   */
  collectionId: string
  /**
   * ID of the NFT.
   * @example '123'
   */
  tokenId: string
}>()

const {
  data: singleNftData,
  error: singleNftError,
  refresh: singleNftRefresh,
} = useSubscription(getSingleNftSubscriptionOptions(props.collectionId, props.tokenId))

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(
  singleNftData,
  (singleNftData) => `Manage ${singleNftData.value?.name || `Unnamed ${singleNftData.value?.typeName}` || 'NFT'}`
)

const alerts = computed(() => {
  const alerts: MViewContainerAlert[] = []

  if (isNullOrUndefined(singleNftData.value)) {
    if (singleNftData.value.queryError?.length > 0) {
      alerts.push({
        variant: 'danger',
        title: 'Failed to load NFT',
        message: `Failed to restore the persisted NFT: ${singleNftData.value.queryError}`,
      })
    }

    if (singleNftData.value.hasPendingMetadataWrite === true) {
      alerts.push({
        title: 'Metadata write pending',
        message:
          'This NFT is currently waiting to have its metadata written to S3. If this NFT was just initialized, you should wait for the write to complete before minting the NFT.',
      })
    }
  }
  return alerts
})

const nftEditParams = ref({})
const nftEditParamsValid = ref(false)

const canPublishMetadata = computed(() => singleNftData.value.metadataManagementMode === 'Authoritative')
const cannotPublishMetadataExplanation = computed(() => {
  const mode = singleNftData.value.metadataManagementMode
  if (mode === 'Authoritative') {
    return undefined
  } else {
    return `To write metadata, the collection must have metadata management mode Authoritative. This collection has mode ${mode}.`
  }
})

const { showSuccessNotification } = useNotifications()

async function refreshFromLedger(): Promise<void> {
  await gameServerApi.post(`nft/${props.collectionId}/${props.tokenId}/refresh`)
  showSuccessNotification('NFT ledger status updated!')
  singleNftRefresh()
}

async function republishMetadata(): Promise<void> {
  await gameServerApi.post(`nft/${props.collectionId}/${props.tokenId}/republishMetadata`)
  showSuccessNotification('NFT metadata republished!')
  // \note Refresh, because this can affect singleNftData.value.hasPendingMetadataWrite.
  singleNftRefresh()
}

async function editNft(): Promise<void> {
  await gameServerApi.post(`nft/${props.collectionId}/${props.tokenId}/edit`, nftEditParams.value)
  showSuccessNotification('NFT game state edited!')
  singleNftRefresh()
}

function prefillNftEditParams(): void {
  nftEditParams.value = { nft: singleNftData.value.model }
}

function getNoOwningPlayerTooltip(ownerAddress: string, isMinted: boolean): string {
  if (ownerAddress === 'None') {
    if (!isMinted) {
      return "The NFT has no owning player because it hasn't yet been minted."
    } else {
      return 'The NFT has no owning player because its owning address is not known to the game backend.'
    }
  } else {
    return 'The NFT has no owning player because its owning address is not linked to any existing player account in the game.'
  }
}
</script>
