<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  v-if="!triggerOptions"
  class="tw-w-full tw-text-center"
  )
  b-spinner(label="Loading trigger events...")

div(
  v-else-if="triggerOptions.length === 0"
  class="text-muted text-small tw-w-full tw-text-center tw-italic"
  ) No trigger events defined!

meta-input-select(
  v-else
  :value="myValue"
  :searchFields="['displayName', 'eventTypeCode']"
  :options="triggerOptions"
  placeholder="No trigger condition"
  @input="myValue = $event"
  )
  template(#option="{ option }")
    div {{ option?.eventTypeCode }}: {{ option?.displayName }}
</template>

<script lang="ts" setup>
import { isEqual } from 'lodash-es'
import { computed, ref, watch } from 'vue'

import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllAnalyticsEventsSubscriptionOptions } from '../../subscription_options/analyticsEvents'
import type { EventInfo, TriggerInfo } from './mailUtils'

const props = defineProps<{
  modelValue?: TriggerInfo
}>()

const emits = defineEmits(['update:modelValue'])

const { data: analyticsEventsData } = useSubscription(getAllAnalyticsEventsSubscriptionOptions())

/**
 * Look up the display name from an event type code.
 * @param typeCode Type code.
 */
const nameFromTypeCode = (typeCode: number): string => {
  const event = analyticsEventsData.value?.find((event) => event.typeCode === typeCode)
  return event?.displayName ?? typeCode.toString()
}

/**
 * List of conditions options that can be used to trigger a broadcast.
 */
const triggerOptions = computed((): Array<MetaInputSelectOption<TranslatedTriggerInfo>> => {
  if (!analyticsEventsData.value) return []

  const triggers = analyticsEventsData.value.filter((event) => event.categoryName === 'Player' && event.canTrigger)
  const options = triggers.map((event): MetaInputSelectOption<TranslatedTriggerInfo> => {
    return {
      id: event.typeCode.toString(),
      value: {
        displayName: nameFromTypeCode(event.typeCode),
        eventTypeCode: event.typeCode,
      },
    }
  })
  return options
})

/**
 * This is what our internal view of a trigger info looks like.
 */
interface TranslatedTriggerInfo {
  displayName: string
  eventTypeCode: number
}

/**
 * Our locally translated value.
 */
const myValue = ref<TranslatedTriggerInfo>()

/**
 * When the external value changes, translate and update our internal value.
 */
watch(
  () => props.modelValue,
  (newValue) => {
    let newValueTranslated: TranslatedTriggerInfo | undefined
    if (newValue) {
      newValueTranslated = {
        displayName: nameFromTypeCode(newValue.eventTypeCode),
        eventTypeCode: newValue.eventTypeCode,
      }
    }
    if (!isEqual(myValue.value, newValueTranslated)) {
      myValue.value = newValueTranslated
    }
  },
  { immediate: true }
)

/**
 * When our internal value changes, translate and emit the external value.
 */
watch(
  () => myValue.value,
  (newValue) => {
    let newValueTranslated: TriggerInfo | undefined
    if (newValue) {
      newValueTranslated = {
        $type: 'Metaplay.Server.PlayerTriggerConditionByTriggerType',
        eventTypeCode: newValue.eventTypeCode,
      }
    }
    if (!isEqual(props.modelValue, newValueTranslated)) {
      emits('update:modelValue', newValueTranslated)
    }
  },
  { immediate: true }
)
</script>
