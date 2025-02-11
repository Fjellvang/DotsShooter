<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  :modal-title="formOptions.modalTitle"
  :action="() => serverRequestLiveOpsEvent(formMode, false)"
  :trigger-button-label="formOptions.triggerButtonLabel"
  :ok-button-label="formOptions.okButtonLabel"
  :ok-button-disabled-tooltip="okButtonDisabledReason"
  permission="api.liveops_events.edit"
  trigger-button-full-width
  @show="onShow"
  data-testid="live-ops-event-form"
  )
  template(#default)
    div(v-if="formMode === 'edit'")
      div(class="tw-text-sm tw-font-bold tw-leading-6") Event Type
      p {{ selectedEventTypeInfo?.eventTypeName ?? 'Unknown' }}
    div(
      v-else
      class="tw-flex tw-flex-col tw-gap-x-2 @2xl:tw-flex-row"
      )
      MInputSingleSelectDropdown(
        v-bind="getPropsForInputComponent('eventType')"
        label="Event Type"
        :model-value="selectedEventType"
        :options="eventTypeOptions"
        placeholder="Select a type"
        class="tw-w-full"
        @update:model-value="onEventTypeChange"
        )

      MInputSingleSelectDropdown(
        v-bind="getPropsForInputComponent('content')"
        label="Config Preset"
        :model-value="selectedEventPreset"
        :options="eventPresetOptions"
        :placeholder="selectedEventType ? 'Select a preset...' : 'Select a type'"
        :disabled="eventPresetOptions.length === 0"
        class="tw-w-full"
        @update:model-value="(newValue) => (selectedEventPreset = newValue)"
        )

    MInputText(
      v-bind="getPropsForInputComponent('displayName')"
      label="Name"
      :model-value="eventDisplayName"
      class="tw-mb-2"
      placeholder="Double XP Weekend"
      @update:model-value="onEventNameChange"
      )

    MInputTextArea(
      label="Description"
      :model-value="eventDescription"
      placeholder="This event is for players who have completed the tutorial."
      @update:model-value="onEventDescriptionChange"
      )

    MInputSingleSelectDropdown(
      label="Color"
      :model-value="eventColorInHex"
      :options="eventColorOptions"
      placeholder="Select a color"
      @update:model-value="onEventColorChange"
      )
      template(#selection="{ value: option }")
        div(class="align-items-center tw-flex")
          div(
            v-if="option?.value"
            class="tw-h-4 tw-w-4 tw-rounded-sm tw-border tw-border-neutral-800"
            :style="`background-color: ${option?.value}`"
            )
          div(
            v-else
            class="tw-h-4 tw-w-4 tw-rounded tw-border tw-border-neutral-300"
            style="background: linear-gradient(to bottom right, white, white 45%, red 45%, red 55%, white 55%, white)"
            )
          div(class="tw-ml-1") {{ option?.label }}
      template(#option="{ option: option }")
        div(class="align-items-center tw-flex")
          div(
            v-if="option?.value"
            class="tw-h-4 tw-w-4 tw-rounded-sm tw-border tw-border-neutral-800"
            :style="`background-color: ${option?.value}`"
            )
          div(
            v-else
            class="tw-h-4 tw-w-4 tw-rounded tw-border tw-border-neutral-300"
            style="background: linear-gradient(to bottom right, white, white 45%, red 45%, red 55%, white 55%, white)"
            )
          div(class="tw-ml-1") {{ option?.label }}

    div(class="tw-mt-3 tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-50 tw-p-3")
      div(class="tw-flex tw-justify-between")
        //- TODO: Disable this switch when field of schedule is uneditable, even if the server technically allows disabling
        //- the schedule until the event is beyond the 'active' phase?
        span(class="tw-text-sm tw-font-bold tw-leading-6") Enable Schedule
        MInputSwitch(
          v-bind="getPropsForInputComponent('useSchedule')"
          :model-value="eventUseSchedule"
          size="small"
          class="tw-relative tw-top-0.5"
          @update:model-value="(event) => (eventUseSchedule = event)"
          )
      //- TODO: Support hintMessage on MInputSwitch, and use that instead of this ad hoc.
      MInputHintMessage(v-if="tryGetInputComponentInfoMessage('useSchedule')") {{ tryGetInputComponentInfoMessage('useSchedule') }}

      div(
        v-if="eventUseSchedule"
        class="tw-space-y-4"
        )
        div
          div(class="tw-mt-2 tw-flex tw-items-center tw-justify-between tw-border-t tw-border-neutral-200 tw-pt-3")
            div(class="tw-text-sm tw-font-bold") Time Zone
            MInputSingleSelectSwitch(
              v-bind="getPropsForInputComponent('schedule.isPlayerLocalTime')"
              :model-value="eventScheduleTimeMode"
              :options="eventScheduleTimeModeOptions"
              size="small"
              @update:model-value="(newValue) => (eventScheduleTimeMode = newValue)"
              )
          div(class="tw-text-xs tw-text-neutral-400") {{ eventScheduleTimeMode === 'local' ? 'Event will run at a different time for each player.' : 'Event will run at the same time for all players.' }}

        MInputDuration(
          v-bind="getPropsForInputComponent('schedule.previewDuration')"
          label="1. Preview Phase"
          :model-value="eventSchedulePreviewDuration"
          allow-empty
          :hint-message="getPropsForInputComponent('schedule.previewDuration').variant === 'danger' ? getPropsForInputComponent('schedule.previewDuration').hintMessage : eventSchedulePreviewDuration ? `Event will be visible for ${eventSchedulePreviewDuration.toHuman({ listStyle: 'short', unitDisplay: 'short' })} before starting.` : 'Event will not be visible until it starts.'"
          @update:model-value="(newValue) => (eventSchedulePreviewDuration = newValue ?? undefined)"
          @isValid="(newValue) => (eventSchedulePreviewDurationIsValid = newValue)"
          )

        MInputDateTime(
          v-bind="getPropsForInputComponent('schedule.enabledStartTime')"
          :label="`2. Start Time (${eventScheduleTimeMode === 'utc' ? 'UTC' : 'player local'})`"
          :model-value="eventScheduleStartTime"
          :hint-message="getPropsForInputComponent('schedule.enabledStartTime').variant === 'danger' ? getPropsForInputComponent('schedule.enabledStartTime').hintMessage : eventScheduleTimeMode === 'utc' ? `Event will start at ${eventScheduleStartTime?.toLocaleString(DateTime.DATETIME_FULL)}.` : `Event will start at ${eventScheduleStartTime?.toLocaleString(DateTime.DATETIME_FULL).slice(0, -4)} in player local time.`"
          @update:model-value="(newValue) => (eventScheduleStartTime = newValue)"
          )

        MInputDuration(
          v-bind="getPropsForInputComponent('schedule.endingSoonDuration')"
          label="3. Ending Soon Phase"
          :model-value="eventScheduleEndingSoonDuration"
          allow-empty
          :hint-message="getPropsForInputComponent('schedule.endingSoonDuration').variant === 'danger' ? getPropsForInputComponent('schedule.endingSoonDuration').hintMessage : eventScheduleEndingSoonDuration ? `Event will be marked as ending soon ${eventScheduleEndingSoonDuration.toHuman({ listStyle: 'short', unitDisplay: 'short' })} before ending.` : 'Event will not be marked as ending soon before it ends.'"
          @update:model-value="(newValue) => (eventScheduleEndingSoonDuration = newValue ?? undefined)"
          @isValid="(newValue) => (eventScheduleEndingSoonDurationIsValid = newValue)"
          )

        // TODO: Figure out how to get reasonable duration units for this. Months are dynamic size and can't be used here. Should not include seconds and smaller.
        MInputDateTime(
          v-bind="getPropsForInputComponent('schedule.enabledEndTime')"
          :label="`4. End Time (${eventScheduleTimeMode === 'utc' ? 'UTC' : 'player local'})`"
          :model-value="eventScheduleEndTime"
          :minDateTime="eventScheduleStartTime"
          :hint-message="getPropsForInputComponent('schedule.enabledEndTime').variant === 'danger' ? getPropsForInputComponent('schedule.enabledEndTime').hintMessage : eventScheduleTimeMode === 'utc' ? `Event will end at ${eventScheduleEndTime?.toLocaleString(DateTime.DATETIME_FULL)} after running for ${eventScheduleRuntimeDuration?.toHuman()}.` : `Event will end at ${eventScheduleEndTime?.toLocaleString(DateTime.DATETIME_FULL).slice(0, -4)} in player local time.`"
          @update:model-value="(newValue) => (eventScheduleEndTime = newValue)"
          )

        MInputDuration(
          v-bind="getPropsForInputComponent('schedule.reviewDuration')"
          label="5. Review Phase"
          :model-value="eventScheduleReviewDuration"
          allow-empty
          :hint-message="getPropsForInputComponent('schedule.reviewDuration').variant === 'danger' ? getPropsForInputComponent('schedule.reviewDuration').hintMessage : eventScheduleReviewDuration ? `Event will be visible for review ${eventScheduleReviewDuration.toHuman({ listStyle: 'short', unitDisplay: 'short' })} after ending.` : 'Event will not be visible for review after it ends.'"
          @update:model-value="(newValue) => (eventScheduleReviewDuration = newValue ?? undefined)"
          @isValid="(newValue) => (eventScheduleReviewDurationIsValid = newValue)"
          )

      p(
        v-else-if="!getPropsForInputComponent('useSchedule').disabled"
        class="tw-mb-0 tw-mt-0.5 tw-text-xs tw-text-neutral-400"
        ) Enable scheduling to limit when the event is active.

    MessageAudienceForm(
      :model-value="eventTargetingOptions"
      class="tw-mt-3"
      @update:modelValue="(newValue) => (eventTargetingOptions = cloneDeep(newValue))"
      )

  template(#right-panel)
    h6(class="tw-text-sm tw-font-bold tw-leading-6") Event Configuration
    meta-generated-form(
      v-if="selectedEventType !== ''"
      :typeName="selectedEventTypeName"
      :value="eventContent"
      :page="'LiveOpsEventForm'"
      addTypeSpecifier
      is-targeting-multiple-players
      @input="eventContent = $event"
      @status="eventContentValid = $event"
      )
    div(
      v-else
      class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-p-3 tw-text-neutral-500"
      ) Choose an event type to start configuring it.

  // template(#bottom-panel)
    pre(class="tw-text-xs") {{ validationDiagnostics }}
</template>

<script lang="ts" setup>
import { cloneDeep } from 'lodash-es'
import { DateTime, Duration } from 'luxon'
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'

import { useGameServerApi, makeAxiosActionHandler, makeActionDebouncer } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MInputHintMessage,
  MInputSingleSelectDropdown,
  MInputText,
  MInputTextArea,
  MInputDateTime,
  MInputSingleSelectSwitch,
  MInputSwitch,
  MInputDuration,
  useNotifications,
  ColorPickerPalette,
  findClosestColorFromPicketPalette,
  type MInputSingleSelectDropdownOption,
  type Color,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type {
  LiveOpsDiagnostics,
  LiveOpsEventDiagnosticComponentPath,
  LiveOpsEventContent,
  LiveOpsEventParams,
  CreateLiveOpsEventRequest,
  CreateLiveOpsEventResponse,
  UpdateLiveOpsEventRequest,
  UpdateLiveOpsEventResponse,
  LiveOpsEventBriefInfo,
} from '../../liveOpsEventServerTypes'
import {
  getAllLiveOpsEventsSubscriptionOptions,
  getLiveOpsEventTypesSubscriptionOptions,
} from '../../subscription_options/liveOpsEvents'
import MetaGeneratedForm from '../generatedui/components/MetaGeneratedForm.vue'
import MessageAudienceForm from '../mails/MessageAudienceForm.vue'
import type { TargetingOptions } from '../mails/mailUtils'

const gameServerApi = useGameServerApi()
const router = useRouter()

const props = withDefaults(
  defineProps<{
    /**
     * The mode in which the form is to be displayed. Defaults to 'create'.
     */
    formMode: 'create' | 'edit' | 'duplicate'
    /**
     * Optional: Existing LiveOps content which is to be edited or duplicated. Only used in modes 'edit' and 'duplicate'.
     */
    prefillData?: LiveOpsEventParams
    /**
     * Optional: EventId of the event we are editing. Only used in mode 'edit'.
     */
    eventId?: string
  }>(),
  {
    prefillData: undefined,
    eventId: undefined,
  }
)

/**
 * Fetch all available event types.
 */
const { data: liveOpsEventTypesData } = useSubscription(getLiveOpsEventTypesSubscriptionOptions())

const { refresh: liveOpsEventsRefresh } = useSubscription(getAllLiveOpsEventsSubscriptionOptions())

// Form input values --------------------------------------------------------------------------------------------------

const selectedEventType = ref<string>('')
const selectedEventTypeName = computed(() => selectedEventType.value.split(',')[0])
const selectedEventTypeInfo = computed(() =>
  liveOpsEventTypesData.value?.find((x) => x.contentClass === selectedEventType.value)
)
const eventTypeOptions = computed(() => {
  return (
    liveOpsEventTypesData.value?.map((key) => ({
      label: key.eventTypeName,
      value: key.contentClass,
    })) ?? []
  )
})

function resetEventPreset(): void {
  selectedEventPreset.value = eventPresetOptions.value[0]?.value ?? ''
}

const selectedEventPreset = ref<string>('')
const eventPresetOptions = computed(() => {
  return Object.keys(selectedEventTypeInfo.value?.templates ?? {}).map((key) => ({ label: key, value: key }))
})
const selectedEventPresetData = computed(() => selectedEventTypeInfo.value?.templates[selectedEventPreset.value])

watch(
  () => selectedEventPresetData.value,
  (newValue) => {
    if (newValue) {
      eventContent.value = newValue.content
      eventContentValid.value = true
    } else {
      eventContent.value = undefined
      eventContentValid.value = false
    }

    if (props.formMode === 'create') {
      if (!eventDisplayNameSet.value || !eventDisplayName.value) {
        eventDisplayName.value = newValue?.defaultDisplayName ?? ''
      }
      if (!eventDescriptionSet.value || !eventDescription.value) {
        eventDescription.value = newValue?.defaultDescription ?? ''
      }
    }
  }
)

const eventDisplayName = ref<string>('')
const eventDisplayNameSet = ref<boolean>(false)

const eventDescription = ref<string>('')
const eventDescriptionSet = ref<boolean>(false)

const eventColorInHex = ref<string>(ColorPickerPalette.Metaplay)
const eventColorSet = ref<boolean>(false)
const eventColorOptions: Array<MInputSingleSelectDropdownOption<string | undefined>> = Object.entries(
  ColorPickerPalette
).map(([name, hexCode]) => ({
  label: name,
  value: hexCode,
}))

const eventContent = ref<LiveOpsEventContent>()
const eventContentValid = ref<boolean>()

const eventUseSchedule = ref<boolean>(false)

type EventScheduleTimeMode = 'utc' | 'local'
const eventScheduleTimeMode = ref<EventScheduleTimeMode>('utc')
const eventScheduleTimeModeOptions = [
  { label: 'UTC', value: 'utc' as EventScheduleTimeMode },
  { label: 'Player local', value: 'local' as EventScheduleTimeMode },
]

const eventScheduleStartTime = ref<DateTime>()
const eventScheduleEndTime = ref<DateTime>()
const eventScheduleRuntimeDuration = computed(() => {
  if (eventScheduleStartTime.value && eventScheduleEndTime.value) {
    return eventScheduleEndTime.value.diff(eventScheduleStartTime.value, ['days', 'hours', 'minutes'])
  } else {
    return undefined
  }
})

const eventSchedulePreviewDuration = ref<Duration>()
const eventSchedulePreviewDurationIsValid = ref<boolean>(true)

const eventScheduleEndingSoonDuration = ref<Duration>()
const eventScheduleEndingSoonDurationIsValid = ref<boolean>(true)

const eventScheduleReviewDuration = ref<Duration>()
const eventScheduleReviewDurationIsValid = ref<boolean>(true)

const eventTargetingOptions = ref<TargetingOptions>({
  targetPlayers: [],
  targetCondition: null,
  valid: true,
})

// Form handling -------------------------------------------------------------------------

/**
 * Controls how the form looks and behaves based on the `formMode` prop.
 */
const formOptions = computed(() => {
  const options = {
    create: {
      modalTitle: 'Create New LiveOps Event',
      triggerButtonLabel: 'New LiveOps Event',
      okButtonLabel: 'Create Event',
    },
    edit: {
      modalTitle: 'Edit LiveOps Event',
      triggerButtonLabel: 'Edit',
      okButtonLabel: 'Save Changes',
    },
    duplicate: {
      modalTitle: 'Duplicate LiveOps Event',
      triggerButtonLabel: 'Duplicate',
      okButtonLabel: 'Create Duplicate',
    },
  }

  if (props.formMode in options) {
    return options[props.formMode]
  } else {
    throw new Error(`Invalid form mode: ${props.formMode}`)
  }
})

/**
 * Prepare the form for display.
 */
function onShow(): void {
  // Create initial data for the form.
  let eventOptions: LiveOpsEventParams
  if (props.prefillData) {
    // Take a deep clone of the input data so that we don't accidentally edit the wrong thing.
    eventOptions = cloneDeep(props.prefillData)
  } else {
    // Initialize to default values.
    eventOptions = {
      displayName: '',
      description: '',
      color: null,
      eventType: '',
      templateId: '',
      content: {},
      useSchedule: false,
      schedule: null,
      targetPlayers: [],
      targetCondition: null,
    }
  }

  // For 'duplicate' mode we want to append ' (copy)' to the display name.
  if (props.formMode === 'duplicate') {
    eventOptions.displayName += ' (copy)'
  }

  // Deserialize the initial data into the form's inputs.
  deserialize(eventOptions)
}

/**
 * Data for the related events card from validate only response.
 */
const relatedEvents = ref<LiveOpsEventBriefInfo[]>([])

/**
 * Diagnostics from the validate response.
 */
// eslint-disable-next-line @typescript-eslint/no-unsafe-argument
const validationDiagnostics = ref<LiveOpsDiagnostics>({} as any)

/**
 * Attributes to pass directly onto a component to style it according to validation results.
 */
interface InputComponentProps {
  hintMessage?: string
  disabled?: boolean
  variant?: 'danger'
}

/**
 * Create `hintMessage`, `disabled` and `variant` attributes for a component based on the scope.
 * @param componentPath Path to the component.
 */
function getPropsForInputComponent(componentPath: LiveOpsEventDiagnosticComponentPath): InputComponentProps {
  const componentPathParts = Object.keys(validationDiagnostics.value)

  // Is there a specific diagnostic for this component path?
  if (componentPathParts.includes(componentPath)) {
    // Yes, use it. Find the most severe diagnostic.
    const diagnosticSortOrder = {
      Error: 0,
      Warning: 1,
      Uneditable: 2,
      Info: 3,
    }
    const diagnostics = validationDiagnostics.value[componentPath]
    const topDiagnostic = diagnostics.sort((a, b) => diagnosticSortOrder[a.level] - diagnosticSortOrder[b.level])[0]
    // Return attributes based on the diagnostic.
    return {
      hintMessage: topDiagnostic.message ?? undefined,
      disabled: topDiagnostic.level === 'Uneditable',
      variant: topDiagnostic.level === 'Error' ? 'danger' : undefined,
    }
  } else {
    // No, look for a partial match based on component path.
    const scopePath = componentPath.split('.')
    while (scopePath.pop()) {
      const partialPath = scopePath.slice(0).join('.')
      const matches = componentPathParts.filter((x) => x === partialPath) as LiveOpsEventDiagnosticComponentPath[]
      if (matches.length) {
        // If something higher up the path has a diagnostic then disable this child field.
        // This may not be entirely correct behavior but it works with the current paths.
        const diagnostics = validationDiagnostics.value[matches[0]]
        return {
          disabled: diagnostics.some((x) => x.level === 'Uneditable'),
        }
      }
    }
  }

  // No matches, component is open.
  return {
    // variant: 'success'
  }
}

/**
 * Return the message of the first Info-level diagnostic, if any, for the given componentPath.
 * @param componentPath Path to the component.
 */
function tryGetInputComponentInfoMessage(componentPath: LiveOpsEventDiagnosticComponentPath): string | undefined {
  const diagnostics = validationDiagnostics.value[componentPath]
  if (!diagnostics) {
    return undefined
  }

  for (const diagnostic of diagnostics) {
    if (diagnostic.level === 'Info') {
      return diagnostic.message ?? undefined
    }
  }

  return undefined
}

// Validation ---------------------------------------------------------------------------------------------------------

function onEventTypeChange(newValue: string): void {
  selectedEventType.value = newValue
  resetEventPreset()
}

function onEventNameChange(newValue: string): void {
  eventDisplayName.value = newValue
  eventDisplayNameSet.value = true
}

function onEventDescriptionChange(newValue: string): void {
  eventDescription.value = newValue
  eventDescriptionSet.value = true
}

function onEventColorChange(newValue?: string): void {
  eventColorInHex.value = newValue ?? ColorPickerPalette.Metaplay
  eventColorSet.value = true
}

/**
 * Did the server validation pass? Note that `undefined` here means that we are waiting for validation results.
 */
const serverValidationResult = ref<boolean>()

/**
 * Reason for disabling the ok button.
 */
const okButtonDisabledReason = computed<string | undefined>(() => {
  if (serverValidationResult.value === undefined) {
    return 'Validating...'
  } else if (
    !selectedEventType.value ||
    !eventDisplayName.value ||
    !eventSchedulePreviewDurationIsValid.value ||
    !eventScheduleEndingSoonDurationIsValid.value ||
    !eventScheduleReviewDurationIsValid.value
  ) {
    return 'Fill in the required fields to proceed.'
  }
  if (!serverValidationResult.value) {
    return 'Server validation failed. Check that you have filled in all required fields correctly.'
  }
  if (!eventContentValid.value) {
    return 'The event content is invalid. Check that it is filled in correctly to proceed.'
  }
  if (!eventTargetingOptions.value.valid) {
    return 'The audience targeting is invalid. Check that it is filled in correctly to proceed.'
  }
  return undefined // Form is valid, no disabled reason
})

/**
 * Watch the form inputs and trigger server validation when they change.
 */
watch(
  [
    selectedEventType,
    selectedEventPreset,
    eventDisplayName,
    eventDescription,
    eventColorInHex,
    eventContent,
    eventUseSchedule,
    eventScheduleTimeMode,
    eventScheduleStartTime,
    eventScheduleEndTime,
    eventSchedulePreviewDuration,
    eventSchedulePreviewDurationIsValid,
    eventScheduleEndingSoonDuration,
    eventScheduleEndingSoonDurationIsValid,
    eventScheduleReviewDuration,
    eventScheduleReviewDurationIsValid,
    eventTargetingOptions,
  ],
  async () => {
    // Trigger validation.
    void serverRequestLiveOpsEvent(props.formMode, true)
  },
  { deep: true }
)

const { showSuccessNotification, showErrorNotification } = useNotifications()

/**
 * Server call to create or duplicate an existing event. If validateOnly is true, the server will only validate the
 * request and not create/update the event.
 * @param mode The mode of the request.
 * @param validateOnly If true, the server will only validate the request and not create it.
 */
async function serverRequestLiveOpsEvent(mode: 'create' | 'edit' | 'duplicate', validateOnly: boolean): Promise<void> {
  // Immediately set form to invalid because we don't know if the changes are valid yet.
  serverValidationResult.value = undefined

  // Clear previous results.
  relatedEvents.value = []
  validationDiagnostics.value = {} as any

  // Figure out how to make the request.
  let url: string
  let payload: CreateLiveOpsEventRequest | UpdateLiveOpsEventRequest
  let successMessage: string
  let failMessage: string
  switch (mode) {
    case 'create':
    case 'duplicate':
      url = '/createLiveOpsEvent'
      payload = {
        validateOnly,
        parameters: serialize(),
      }
      successMessage = `New ${eventDisplayName.value} event created.`
      failMessage = 'Failed to create event!'
      break

    case 'edit':
      url = '/updateLiveOpsEvent'
      payload = {
        validateOnly,
        occurrenceId: props.eventId!, // eslint-disable-line @typescript-eslint/no-non-null-assertion
        parameters: serialize(),
      }
      successMessage = `${eventDisplayName.value} event updated.`
      failMessage = 'Failed to update event!'
      break
  }

  // Make the request.
  if (validateOnly) {
    // For validation only, we hand the request off to the debouncer. The actual request will happen some time in the
    // future. Any additional requests that occur will cause the previous request to be cancelled.
    serverRequestDebouncedAction.requestAction({
      url,
      method: 'post',
      data: payload,
    })
  } else {
    // For actual creation/updates, we await the response.
    const response = (await gameServerApi.post<CreateLiveOpsEventResponse>(url, payload)).data

    // Show a toast.
    if (response.isValid) {
      showSuccessNotification(successMessage)
      if (mode === 'duplicate') {
        // Navigate to newly created event.
        await router.push(`/liveOpsEvents/${response.eventId}`)
      }
    } else {
      showErrorNotification(failMessage)
    }

    // Update the form with the response.
    relatedEvents.value = response.relatedEvents
    validationDiagnostics.value = response.diagnostics

    // Update the events list.
    liveOpsEventsRefresh()
  }
}

/**
 * How long do we wait before making validation requests?
 */
const validationDebounceTimeoutInMs = 500

/**
 * Action debouncer to make validation requests to the server.
 */
const serverRequestDebouncedAction = makeActionDebouncer(
  makeAxiosActionHandler<
    CreateLiveOpsEventRequest | UpdateLiveOpsEventRequest,
    CreateLiveOpsEventResponse | UpdateLiveOpsEventResponse
  >(),
  (response) => {
    // Set results.
    const responseData = response.data
    relatedEvents.value = responseData.relatedEvents
    validationDiagnostics.value = responseData.diagnostics
    serverValidationResult.value = responseData.isValid
  },
  () => {
    // Errors are unhandled.
  },
  validationDebounceTimeoutInMs
)

// Convert from/to server response to/from input values ---------------------------------------------------------------

/**
 * Deserialize data from the server format to the form inputs.
 * @param input Data from the server.
 */
function deserialize(input: LiveOpsEventParams): void {
  selectedEventType.value = input.eventType
  selectedEventPreset.value = ''
  eventDisplayName.value = input.displayName
  eventDisplayNameSet.value = false
  eventDescription.value = input.description
  eventDescriptionSet.value = false
  eventColorInHex.value = input.color ? findClosestColorFromPicketPalette(input.color) : ColorPickerPalette.Metaplay
  eventColorSet.value = false
  eventContent.value = cloneDeep(input.content)
  eventContentValid.value = true
  eventUseSchedule.value = input.useSchedule
  if (input.schedule) {
    eventScheduleTimeMode.value = input.schedule.isPlayerLocalTime ? 'local' : 'utc'
    eventScheduleStartTime.value =
      input.schedule.enabledStartTime === ''
        ? DateTime.utc().startOf('minute')
        : DateTime.fromISO(input.schedule.enabledStartTime).toUTC()
    eventScheduleEndTime.value =
      input.schedule.enabledEndTime === ''
        ? DateTime.utc().startOf('minute').plus({ days: 1 })
        : DateTime.fromISO(input.schedule.enabledEndTime).toUTC()
    eventSchedulePreviewDuration.value =
      Duration.fromISO(input.schedule.previewDuration).toMillis() === 0
        ? undefined
        : Duration.fromISO(input.schedule.previewDuration)
    eventScheduleEndingSoonDuration.value =
      Duration.fromISO(input.schedule.endingSoonDuration).toMillis() === 0
        ? undefined
        : Duration.fromISO(input.schedule.endingSoonDuration)
    eventScheduleReviewDuration.value =
      Duration.fromISO(input.schedule.reviewDuration).toMillis() === 0
        ? undefined
        : Duration.fromISO(input.schedule.reviewDuration)
  } else {
    eventScheduleTimeMode.value = 'utc'
    eventScheduleStartTime.value = DateTime.utc().startOf('minute')
    eventScheduleEndTime.value = DateTime.utc().startOf('minute').plus({ days: 1 })
    eventSchedulePreviewDuration.value = undefined
    eventScheduleEndingSoonDuration.value = undefined
    eventScheduleReviewDuration.value = undefined
  }
  eventTargetingOptions.value = {
    // The `?? []` is here because it is technically possible to have a null value for targetPlayers, and the
    // MessageAudienceForm component does not handle that.
    targetPlayers: cloneDeep(input.targetPlayers) ?? [],
    targetCondition: cloneDeep(input.targetCondition),
    valid: true,
  }
}

/**
 * Serialize data from the form inputs to server format.
 * @returns Data in server format.
 */
function serialize(): LiveOpsEventParams {
  const output: LiveOpsEventParams = {
    eventType: selectedEventType.value,
    templateId: selectedEventPreset.value,
    displayName: eventDisplayName.value,
    description: eventDescription.value,
    color: eventColorInHex.value ?? null,
    content: cloneDeep(eventContent.value ?? {}),
    useSchedule: eventUseSchedule.value,
    schedule: null,
    targetPlayers: eventTargetingOptions.value.valid ? cloneDeep(eventTargetingOptions.value.targetPlayers) : [],
    targetCondition: eventTargetingOptions.value.valid ? cloneDeep(eventTargetingOptions.value.targetCondition) : null,
  }
  if (output.useSchedule) {
    output.schedule = {
      isPlayerLocalTime: eventScheduleTimeMode.value === 'local',
      enabledStartTime: eventScheduleStartTime.value?.toISO() ?? '',
      enabledEndTime: eventScheduleEndTime.value?.toISO() ?? '',
      previewDuration: eventSchedulePreviewDuration.value?.toISO() ?? Duration.fromMillis(0).toISO(),
      endingSoonDuration: eventScheduleEndingSoonDuration.value?.toISO() ?? Duration.fromMillis(0).toISO(),
      reviewDuration: eventScheduleReviewDuration.value?.toISO() ?? Duration.fromMillis(0).toISO(),
    }
  }

  return output
}
</script>
