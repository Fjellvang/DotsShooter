<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Database Items"
  data-testid="database-items"
  )
  span(class="tw-font-semibold") Totals
  b-table-simple(
    class="tw-mt-1"
    small
    )
    b-tbody
      b-tr(
        v-for="(itemCount, tableName) in totalItemCounts"
        :key="tableName"
        )
        b-td {{ tableName }}
        b-td(class="tw-text-right") #[meta-abbreviate-number(:value="itemCount")]

  div(class="tw-flex tw-justify-end tw-space-x-2")
    MButton(
      v-if="databaseStatusData"
      @click="databaseStatusModal?.open"
      ) Show Items per Shard

    MModal(
      ref="databaseStatusModal"
      title="Database Items per Shard"
      modal-size="large"
      )
      ul(
        class="tw-divide-y tw-divide-neutral-200 tw-rounded-md tw-border tw-border-neutral-300"
        style="font-size: 0.8rem"
        )
        li(
          v-for="(shardSpec, shardIndex) in databaseOptions.shards"
          :key="shardIndex"
          class="tw-px-3 tw-py-3 even:tw-bg-neutral-100"
          )
          div(v-if="shardIndex < databaseStatusData.numShards")
            div(class="tw-text-base tw-font-semibold") Shard &#35{{ shardIndex }}
            div(class="tw-grid tw-gap-x-6 tw-space-y-0.5 sm:tw-grid-cols-2 lg:tw-grid-cols-3")
              div(
                v-for="(shardItemCounts, tableName) in databaseStatusData.shardItemCounts"
                :key="`counts-${tableName}`"
                )
                div(
                  class="tw-flex tw-flex-wrap tw-justify-between tw-gap-x-2"
                  :class="shardItemCounts[shardIndex] > 0 ? '' : 'text-muted'"
                  )
                  span(class="tw-break-word") {{ tableName }}
                  span(class="text-monospace tw-flex-none") #[meta-abbreviate-number(:value="shardItemCounts[shardIndex]")]
          div(
            v-else
            class="tw-text-sm tw-font-semibold tw-text-neutral-500"
            ) Shard &#35{{ shardIndex }} inactive

    MActionModalButton(
      modal-title="Inspect an Entity"
      :action="inspectEntity"
      trigger-button-label="Inspect Entity"
      ok-button-label="Inspect"
      :ok-button-disabled-tooltip="!isEntityIdValid ? 'Enter a valid Entity ID to proceed.' : undefined"
      disable-safety-lock
      permission="api.database.inspect_entity"
      @show="resetModal"
      data-testid="inspect-entity"
      )
      p(class="tw-mb-3") Entity inspection loads the stored data for the chosen entity and deserializes it into a human readable form without affecting the actual data.
      span(class="tw-text-xs+ tw-text-neutral-500") Inspecting the raw data of an entity may help you spot problems with the related actor, as you can see the data before any potential mutations during wake-up.

      //- TODO: replace this with a generic entity search & select component once we have one.
      MInputText(
        label="Entity ID"
        :model-value="entityId"
        :variant="variant"
        :hint-message="isEntityIdValid === false ? isEntityIdValidReason : undefined"
        placeholder="Player:000000003v"
        :debounce="300"
        class="tw-mt-4"
        @update:model-value="entityId = $event"
        data-testid="entity-id-input"
        )
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModalButton, MButton, MCard, MInputText, MModal } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { isValidEntityId } from '../../coreUtils'
import { getDatabaseStatusSubscriptionOptions } from '../../subscription_options/general'

const gameServerApi = useGameServerApi()

const {
  data: databaseStatusData,
  hasPermission: databaseStatusPermission,
  error: databaseStatusError,
} = useSubscription(getDatabaseStatusSubscriptionOptions())

const databaseOptions = computed(() => {
  return databaseStatusData.value?.options.values
})

const databaseStatusModal = ref<typeof MModal>()

/**
 * The total number of items in the database per item type.
 */
const totalItemCounts = computed((): Record<string, number> => {
  if (!databaseStatusData.value) return {}

  const totalItemCounts: Record<string, number> = {}
  for (const tableName of Object.keys(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    databaseStatusData.value.shardItemCounts
  )) {
    const shardItemCounts = databaseStatusData.value.shardItemCounts[tableName]
    totalItemCounts[tableName] = shardItemCounts.reduce((sum: number, v: number) => sum + v, 0)
  }
  return totalItemCounts
})
/**
 * Entity ID as entered into the modal.
 */
const entityId = ref('')

/**
 * Validation state for the entity ID input.
 */
const isEntityIdValid = ref<boolean | null>(false)

/**
 * Human-readable reason for the validation state.
 */
const isEntityIdValidReason = ref('')

/**
 * The visual variant of the entity ID input.
 */
const variant = ref<'default' | 'danger' | 'success' | 'loading'>('default')

/**
 * Validate 'entityId' when user input changes.
 */
watch(entityId, () => {
  isEntityIdValid.value = null
  isEntityIdValidReason.value = ''

  if (!entityId.value) {
    isEntityIdValid.value = null
    isEntityIdValidReason.value = ''
    variant.value = 'default'
  } else if (!isValidEntityId(entityId.value)) {
    isEntityIdValid.value = false
    isEntityIdValidReason.value = 'Not a valid entity ID.'
    variant.value = 'danger'
  } else {
    variant.value = 'loading'
    void gameServerApi.get(`/entities/${entityId.value}/exists`).then((result) => {
      const exists: boolean = result.data
      isEntityIdValid.value = exists
      variant.value = exists ? 'success' : 'danger'
      isEntityIdValidReason.value = exists ? '' : 'Entity does not exist.'
    })
  }
})

/**
 * Reset contents of the modal when it is opened.
 */
function resetModal(): void {
  entityId.value = ''

  // Note: These two will be set to these values by `debouncedValidation` in response to `entityId` being reset, but
  // that takes a few hundred millisecond so we'll see a flash if we don't also initialize them here.
  isEntityIdValid.value = null
  isEntityIdValidReason.value = ''
}

const router = useRouter()
async function inspectEntity(): Promise<void> {
  void router.push(`/entities/${entityId.value}/dbinfo`)
}
</script>
