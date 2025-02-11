<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container
div(class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-50 tw-p-3")
  //- Main toggle for enabling targeting
  div(class="tw-flex tw-justify-between")
    div(class="tw-text-sm tw-font-bold tw-leading-6") Enable Targeting
    MInputSwitch(
      :model-value="enableTargeting"
      class="tw-relative tw-top-1"
      name="targetingEnabled"
      size="small"
      @update:model-value="onEnableTargetingChange"
      )

  //- If targeting...
  div(
    v-if="enableTargeting"
    class="tw-mt-2 tw-border-t tw-border-neutral-200 tw-pt-3"
    )
    //- Header
    div(class="tw-text-sm tw-font-bold tw-leading-6") Select Player Segments

    //- Loading spinner
    div(
      v-if="!segmentOptions"
      class="tw-w-full tw-text-center"
      )
      b-spinner(label="Loading segments...")/

    //- Error message (TODO: ship with core segments and get rid of this case)
    div(
      v-else-if="segmentOptions.length === 0"
      class="text-muted tw-w-full tw-py-4 tw-text-center tw-italic"
      ) No segments defined in the currently active game configs.

    //- Segment selector
    div(v-else)
      div(class="tw-flex tw-space-x-2")
        //- Selection type
        MInputSingleSelectSwitch(
          :model-value="segmentMatchingRule"
          :options="segmentMatchingOptions"
          class="tw-shrink-0"
          size="small"
          @update:model-value="segmentMatchingRule = $event"
          )

        //- Segment input
        meta-input-select(
          :value="chosenSegments"
          :options="segmentOptions"
          :searchFields="['displayName']"
          placeholder="Select player segments..."
          multiselect
          @input="chosenSegments = $event"
          )
          template(#option="{ option }")
            div(class="tw-flex tw-justify-between")
              div(class="font-weight-bold") {{ option?.displayName }}
              div(class="tw-text-right") {{ option?.estimatedPlayerCount }}

          template(#selectedOption="{ option }")
            div {{ option?.displayName }}

      //- Hint text
      div(class="tw-mt-1 tw-text-xs tw-text-neutral-400")
        span(v-if="segmentMatchingRule === 'all'") Players must match all of the selected segments.
        span(v-else) Players must match at least one of the selected segments.

    //- Individual players
    div(
      v-if="isPlayerTargetingSupported"
      class="tw-w-full"
      )
      div(class="tw-mb-1 tw-mt-4")
        span(class="tw-text-sm tw-font-bold tw-leading-6") Select Individual Players
        span(class="tw-ml-1 tw-text-xs tw-text-neutral-400") ({{ playerList.length }}/{{ maxTargetPlayerListSize }} selected)

      MInputTextArea(
        :model-value="playerBatchImportString"
        :variant="playerInputTextAreaVariant"
        :disabled="maxTargetPlayerListSize === 0"
        :placeholder="maxTargetPlayerListSize === 0 ? 'To use this feature, set a maximum number of target player in your runtime options.' : 'Comma separated list of IDs: Player:XXXXXXXXXX, Player:YYYYYYYYYY, ...'"
        :hintMessage="playerBatchImportString && !isPlayerInputValid ? playerListValidationError : ''"
        @update:modelValue="(event) => (playerBatchImportString = event)"
        )

      div(
        v-if="invalidPlayerIds && invalidPlayerIds.length > 0"
        class="tw-mt-1 tw-text-xs tw-text-red-400"
        )
        span Invalid player IDs: {{ invalidPlayerIds.slice(0, 20).join(', ') }}
        span(v-if="invalidPlayerIds.length > 20") and {{ invalidPlayerIds.length - 20 }} more.
        MClipboardCopy(:contents="invalidPlayerIds.join(',')")

    div(
      v-if="!isFormValid && !isPlayerListValidationLoading"
      class="tw-mt-1 tw-text-xs tw-text-red-400"
      ) Select at least one target segment or individual player.
</template>

<script lang="ts" setup>
import { uniq } from 'lodash-es'
import { computed, nextTick, ref, watch, onMounted } from 'vue'

import { useGameServerApi, makeActionDebouncer, type ActionHandler } from '@metaplay/game-server-api'
import type { MetaInputSelectOption, BulkListInfo } from '@metaplay/meta-ui'
import { MClipboardCopy, MInputSingleSelectSwitch, MInputTextArea, MInputSwitch } from '@metaplay/meta-ui-next'
import { abbreviateNumber } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { isValidPlayerId } from '../../coreUtils'
import {
  getPlayerSegmentsSubscriptionOptions,
  getRuntimeOptionsSubscriptionOptions,
} from '../../subscription_options/general'
import type { TargetingOptions } from './mailUtils'

const props = withDefaults(
  defineProps<{
    /**
     * Optional: Whether or not the game supports targeting individual players.
     */
    isPlayerTargetingSupported?: boolean
    /**
     * The current targeting options.
     */
    modelValue: TargetingOptions | undefined
  }>(),
  {
    isPlayerTargetingSupported: true,
    modelValue: () => {
      return {
        targetPlayers: [],
        targetCondition: null,
        valid: true,
      }
    },
  }
)

const gameServerApi = useGameServerApi()

/**
 * Subscribe to player segment data.
 */
const { data: playerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

/**
 * Runtime options for the game server.
 */
const { data: runtimeOptionsData, error: runtimeOptionsError } = useSubscription(getRuntimeOptionsSubscriptionOptions())

/**
 * Type definition of segment option
 */
interface SegmentOption {
  displayName: string
  segmentId: string
  estimatedPlayerCount: string
}

/**
 * List of segment options that are to be displayed on the multi-select dropdown.
 */
const segmentOptions = ref<Array<MetaInputSelectOption<SegmentOption>>>()

/**
 * Server type definition of segment information displayed as an option.
 */
interface SegmentInfo {
  info: {
    displayName: string
    segmentId: string
  }
  sizeEstimate: number
}

/**
 * Derive segment option details from segment information supplied.
 * @param segment Segment whose information is to be displayed as an option.
 */
function makeSegmentOptionFromSegmentInfo(segment: SegmentInfo): MetaInputSelectOption<SegmentOption> {
  return {
    id: segment.info.segmentId,
    value: {
      displayName: segment.info.displayName,
      segmentId: segment.info.segmentId,
      estimatedPlayerCount:
        segment.sizeEstimate != null
          ? `~ ${abbreviateNumber(segment.sizeEstimate)} player${segment.sizeEstimate !== 1 ? 's' : ''}`
          : 'Estimate pending...',
    },
  }
}

/**
 * Derive segment options from supplied segment Ids.
 * @param segmentId Id of segment whose information is to be displayed as an option.
 */
function makeSegmentOptionFromSegmentId(segmentId: string): SegmentOption {
  const segment = playerSegmentsData.value.segments.find((segment: SegmentInfo) => segmentId === segment.info.segmentId)
  if (segment) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return makeSegmentOptionFromSegmentInfo(segment).value || null
  } else {
    // Segment is missing from config.
    // todo needs testing
    return {
      displayName: 'missing',
      segmentId: 'missing',
      estimatedPlayerCount: 'missing',
    }
  }
}

// Segment options are only loaded once to avoid breaking the select component's internal state... Not sure if there's a better way to do this.
const playerSegmentsUnwatch = watch(
  playerSegmentsData,
  (newVal: { $type: string; segments: Array<{ $type: string; sizeEstimate: number; info: any }> }) => {
    if (!newVal?.segments) return

    segmentOptions.value = newVal.segments.map((segment) => makeSegmentOptionFromSegmentInfo(segment))
    if (segmentOptions.value.length > 0) {
      // Unwatch the data when we get the first update. If that happens immediately then playerSegmentsUnwatch won't
      // exist yet, so we need to delay this for a frame.
      void nextTick(() => {
        playerSegmentsUnwatch()
      })
    }
  },
  { immediate: true }
)

/**
 * Selected segments and conditions used to target the audience.
 */
const chosenSegments = ref<SegmentOption[]>(
  (
    props.modelValue.targetCondition?.requireAnySegment ??
    props.modelValue.targetCondition?.requireAllSegments ??
    []
  ).map((segment: string) => makeSegmentOptionFromSegmentId(segment))
)
watch(chosenSegments, update)

/**
 * When true that enables targeting of a message to a specific audience.
 */
const enableTargeting = ref(props.modelValue.targetPlayers.length > 0 || !!props.modelValue.targetCondition)

/**
 * Rules that match players to at least one or all segment conditions.
 */
const segmentMatchingRule = ref<string>(props.modelValue.targetCondition?.requireAllSegments ? 'all' : 'any')
watch(segmentMatchingRule, update)

const segmentMatchingOptions = [
  { label: 'All', value: 'all' },
  { label: 'Any', value: 'any' },
]

// MANUAL PLAYER ID's ---------------------------------------------------------

/**
 * List of player IDs as entered by the user.
 */
const playerBatchImportString = ref('')

// Initialize the player list.
onMounted(() => {
  if (props.modelValue.targetPlayers.length > 0) {
    playerBatchImportString.value = props.modelValue.targetPlayers.join(', ')
  }
})

/**
 * List of target player IDs.
 */
const playerList = ref<string[]>(props.modelValue.targetPlayers)

/**
 * List of invalid player IDs.
 */
const invalidPlayerIds = ref<any[]>([])

/**
 * Error returned from validation.
 */
const playerListValidationError = ref<string | undefined>()

/**
 * True if validation is in progress.
 */
const isPlayerListValidationLoading = ref(false)

/**
 * Maximum number of individual players that can be targeted by a notification campaign.
 */
const maxTargetPlayerListSize = computed((): number => {
  const options = runtimeOptionsData.value?.options.find((option: any) => option.name === 'System')
  if (!options) return 0
  return options.values.maxTargetPlayersListSize
})

function onEnableTargetingChange(event: boolean): void {
  enableTargeting.value = event
  update()
}

/**
 * Checks that there are no validation errors.
 */
const isPlayerInputValid = computed(() => {
  if (isPlayerListValidationLoading.value) return false
  if (props.isPlayerTargetingSupported && playerListValidationError.value) {
    return false
  }
  return true
})

/**
 * Checks that the form is not empty.
 */
const isFormValid = computed(() => {
  if (enableTargeting.value) {
    if (chosenSegments.value.length > 0 || playerList.value.length > 0) {
      return true
    }
  }
  return false
})

/**
 * Color variant that indicates whether the input is valid or not.
 */
const playerInputTextAreaVariant = computed(() => {
  if (playerBatchImportString.value) {
    if (isPlayerListValidationLoading.value) return 'loading'
    if (isPlayerInputValid.value) return 'success'
    else return 'danger'
  }
  return 'default'
})

/**
 * Watch the player list input and request validation it when it changes
 */
watch(playerBatchImportString, (input) => {
  // Clear previous state.
  playerListValidationError.value = undefined
  invalidPlayerIds.value = []
  isPlayerListValidationLoading.value = true

  // Request validation.
  validationDebouncedAction.requestAction(input)
})

/**
 * Validation result from the action handler.
 */
interface ValidationResult {
  isValid: boolean
  errorMessage?: string
  invalidIds?: string[]
  validIds?: string[]
}

/**
 * Action handler for the player list input validation to setup a request. Called indirectly by
 * `validationDebouncedAction`. The response from here (a `ValidationResult`) is then passed to the
 * `validationDebouncedAction`'s response handler. Note that this action can complete immediately (if the validation
 * happens locally) or after a round-trip to the server.
 * @param requestData Data to validate.
 * @param response Callback to handle the validation result.
 */
function actionHandlerSetup(requestData: string, response: (response: ValidationResult) => void): undefined {
  // Create list of player IDs and remove duplicates.
  const inputPlayerIdList = uniq(
    requestData
      .trim()
      .split(',')
      .map((id) => id.trim())
  )

  // Remove the last array element if it's empty.
  if (inputPlayerIdList[inputPlayerIdList.length - 1] === '') {
    inputPlayerIdList.pop()
  }

  // Check against the maximum size.
  if (inputPlayerIdList.length > maxTargetPlayerListSize.value) {
    response({
      isValid: false,
      errorMessage: `Too many players! You entered a list of ${inputPlayerIdList.length} players but the maximum limit is ${maxTargetPlayerListSize.value}. Consider using segments instead?`,
    })
    return
  }

  // Check for obviously invalid player IDs.
  const invalidPlayerIds = inputPlayerIdList.filter((id) => !isValidPlayerId(id))
  if (invalidPlayerIds.length > 0) {
    response({
      isValid: false,
      errorMessage: 'Not all listed IDs look like valid player IDs!',
      invalidIds: invalidPlayerIds,
    })
    return
  }

  // Once the local checks has passed we can send the request to the server.
  void gameServerApi.post<BulkListInfo[]>('players/bulkValidate', { PlayerIds: inputPlayerIdList }).then((result) => {
    // Handle response from the server.
    const results = result.data
    const validPlayerIds = results.filter((x) => x.validId && x.playerData != null).map((x) => x.playerIdQuery)
    const invalidPlayerIds = results.filter((x) => !x.validId || x.playerData == null).map((x) => x.playerIdQuery)
    if (invalidPlayerIds.length > 0) {
      response({
        isValid: false,
        errorMessage: 'Invalid player IDs in list!',
        invalidIds: invalidPlayerIds,
      })
    } else {
      response({
        isValid: true,
        validIds: validPlayerIds,
      })
    }
  })
}

/**
 * Note that we don't bother doing any cancel.
 */
function actionHandlerCancel(): void {
  // Do nothing.
}

/**
 * Debounced action to validate the player list input.
 */
const validationDebouncedAction = makeActionDebouncer(
  {
    setup: actionHandlerSetup,
    cancel: actionHandlerCancel,
  },
  (response) => {
    // Update state when response is received.
    playerListValidationError.value = response.errorMessage
    playerList.value = response.validIds ?? []
    invalidPlayerIds.value = response.invalidIds ?? []
    isPlayerListValidationLoading.value = false
    update()
  },
  () => {
    // Errors are unhandled.
  },
  500
)

// FORM INPUT --------------------------------------------------------------

const emits = defineEmits(['update:modelValue'])

/**
 * Update the targeting list when a new segment is added.
 */
function update(): void {
  // Default to no targeting.
  const returnValue: TargetingOptions = {
    targetPlayers: [],
    targetCondition: null,
    valid: true,
  }
  if (enableTargeting.value) {
    // Add individual players.
    returnValue.targetPlayers = playerList.value

    // Add segments.
    if (chosenSegments.value.length > 0) {
      returnValue.targetCondition = {
        $type: 'Metaplay.Core.Player.PlayerSegmentBasicCondition',
      }

      const chosenSegmentIds = chosenSegments.value.map((x) => x.segmentId)
      if (segmentMatchingRule.value === 'all') {
        returnValue.targetCondition.requireAllSegments = chosenSegmentIds
      } else {
        returnValue.targetCondition.requireAnySegment = chosenSegmentIds
      }
    }
    if (!isFormValid.value || !isPlayerInputValid.value) {
      returnValue.valid = false
    }
  }
  emits('update:modelValue', returnValue)
}

// PLAYER COUNT -------------------------------------------------------------

// const databaseItemCounts = useSubscription(getDatabaseItemCountsSubscriptionOptions().data
// const totalPlayerCount = computed(() => databaseItemCounts.value?.totalItemCounts.Players)

// const totalEstimatedSelectedPlayers = computed(() => {
//   return estimateAudienceSize(playerSegmentsData.value?.segments, props.value)
// })
</script>
