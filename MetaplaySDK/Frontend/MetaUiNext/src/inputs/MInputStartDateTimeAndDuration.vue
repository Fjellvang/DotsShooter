<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Note: adding a z-index to the container is necessary to make the date picker's popper show up above sibling elements, as container queries establish a new stacking context.
div(class="tw-relative tw-z-10 tw-@container")
  div(class="tw-w-full tw-space-y-4 @[700px]:tw-flex @[700px]:tw-space-x-4 @[700px]:tw-space-y-0")
    MInputDateTime(
      :model-value="startDateTime"
      label="Start (UTC)"
      :disabled="disabled"
      class="tw-flex-shrink-0"
      @update:model-value="updateStartDateTime($event)"
      )

    MInputDurationOrEndDateTime(
      :model-value="duration"
      :referenceDateTime="startDateTime"
      :disabled="disabled"
      class="tw-grow"
      @update:model-value="$emit('update:duration', $event)"
      )
</template>

<script setup lang="ts">
import { DateTime, Duration } from 'luxon'

import MInputDateTime from './MInputDateTime.vue'
import MInputDurationOrEndDateTime from './MInputDurationOrEndDateTime.vue'

const props = defineProps<{
  /**
   * Start date time as a Luxon `DateTime` object.
   */
  startDateTime: DateTime
  /**
   * Duration as a Luxon `Duration` object.
   */
  duration: Duration
  /**
   * Optional: Disable the inputs. Defaults to false.
   */
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:startDateTime': [value: DateTime]
  'update:duration': [value: Duration]
}>()

function updateStartDateTime(value: DateTime | undefined): void {
  // TODO: Why the non-null assertion? Looks sus.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  emit('update:startDateTime', value!)
}
</script>
