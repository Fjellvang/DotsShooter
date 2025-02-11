<template lang="pug">
div(class="tw-px-4")
  //- Header row.
  div(class="tw-items-top tw-flex tw-justify-between tw-space-x-1 tw-pt-4")
    //- Title.
    h2(class="tw-font-bold")
      MInputText(
        v-if="showTitleEditor"
        class="tw-basis-2/3"
        :model-value="title"
        placeholder="No title."
        :debounce="1000"
        @update:model-value="(value) => onChangeName(value)"
        )
      span(
        v-else
        class="tw-mr-1"
        ) {{ title }}

      //- Optional title badge slot.
      slot(name="title-badge")

    //- Close button.
    button(
      class="tw-relative -tw-top-0.5 tw-inline-flex tw-h-7 tw-w-7 tw-shrink-0 tw-items-center tw-justify-center tw-rounded tw-font-semibold hover:tw-bg-neutral-100 active:tw-bg-neutral-200"
      @click="$emit('close')"
      )
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="tw-w-6 tw-h-6">
        <path fill-rule="evenodd" d="M5.47 5.47a.75.75 0 011.06 0L12 10.94l5.47-5.47a.75.75 0 111.06 1.06L13.06 12l5.47 5.47a.75.75 0 11-1.06 1.06L12 13.06l-5.47 5.47a.75.75 0 01-1.06-1.06L10.94 12 5.47 6.53a.75.75 0 010-1.06z" clip-rule="evenodd" />
      </svg>

  //- Description.
  div(class="tw-text-sm") {{ description }}

  //- Color picker.
  MInputSingleSelectDropdown(
    v-if="showColorPicker"
    label="Color"
    :model-value="itemColor"
    :options="itemColorOptions"
    placeholder="Select a color"
    @update:model-value="onItemColorChange"
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

  //- Main content, default slot.
  div(class="tw-mt-2")
    slot(name="default")

  //- Actions buttons slot.
  MButtonGroupLayout(class="tw-mt-4")
    slot(name="buttons")
</template>

<script setup lang="ts">
import { ref } from 'vue'

import MInputSingleSelectDropdown, {
  type MInputSingleSelectDropdownOption,
} from '../../inputs/MInputSingleSelectDropdown.vue'
import MInputText from '../../inputs/MInputText.vue'
import MButtonGroupLayout from '../../layouts/MButtonGroupLayout.vue'
import { ColorPickerPalette, findClosestColorFromPicketPalette } from '../../unstable/timeline/MEventTimelineColorUtils'
import type { ToServerCommand, ToServerCommandChangeMetaData } from './timelineCommands'

const emit = defineEmits({
  close: () => ({ type: 'close' as const }),
  invokeCommand: (command: ToServerCommand) => ({ type: 'command', command }),
})

const props = defineProps<{
  id: string
  version: number
  title: string
  description?: string
  showTitleEditor?: boolean
  showDescriptionEditor?: boolean
  showColorPicker?: boolean
  currentColor?: string
}>()

function onChangeName(value: string): void {
  const command: ToServerCommandChangeMetaData = {
    commandType: 'changeMetadata',
    items: [
      {
        targetId: props.id,
        currentVersion: props.version,
        changes: [
          {
            property: 'displayName',
            newValue: value,
          },
        ],
      },
    ],
  }
  emit('invokeCommand', command)
}

const itemColorOptions: Array<MInputSingleSelectDropdownOption<string | undefined>> = Object.entries(
  ColorPickerPalette
).map(([name, hexCode]) => ({
  label: name,
  value: hexCode,
}))
itemColorOptions.unshift({ label: 'None', value: undefined })

const itemColor = ref<string | undefined>(findClosestColorFromPicketPalette(props.currentColor))

function onItemColorChange(color: string | undefined): void {
  const command: ToServerCommandChangeMetaData = {
    commandType: 'changeMetadata',
    items: [
      {
        targetId: props.id,
        currentVersion: props.version,
        changes: [
          {
            property: 'color',
            newValue: color,
          },
        ],
      },
    ],
  }
  emit('invokeCommand', command)
}
</script>
