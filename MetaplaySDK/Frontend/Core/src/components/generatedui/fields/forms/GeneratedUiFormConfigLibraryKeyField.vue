<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  LabelWithLogicVersion(
    :fieldInfo="fieldInfo"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    ) {{ displayName }}
    MTooltip(
      v-if="displayHint"
      :content="displayHint"
      noUnderline
      class="ml-2"
      ): MBadge(shape="pill") ?
  div
    meta-input-select(
      v-if="fieldSchema.configLibrary && fieldSchema.configLibrary.length > 0"
      :value="stringValue"
      :options="possibleValues"
      :class="isValid ? 'border-success' : ''"
      no-clear
      :disabled="disabled"
      @input="updateValue"
      :data-testid="dataTestid + '-input'"
      )
    MInputText(
      v-else
      :model-value="stringValue"
      :placeholder="formInputPlaceholder"
      :variant="isValid !== undefined ? (isValid ? 'success' : 'danger') : 'default'"
      :disabled="props.disabled"
      @update:model-value="updateValue"
      :data-testid="dataTestid + '-input'"
      )
  div(
    v-if="!isValid"
    class="tw-text-red-500"
    ) {{ validationError }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MTooltip, MBadge, MInputText } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../../../coreStore'
import { getGameDataSubscriptionOptions } from '../../../../subscription_options/general'
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: null,
    required: true,
    default: undefined,
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { displayName, displayHint, isValid, validationError, update, dataTestid, formInputPlaceholder, useDefault } =
  useGeneratedUiFieldForm(props, emit)

const stringValue = computed<string>(() => String(props.value ?? possibleValues.value.find(() => true)?.value))

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())
const coreStore = useCoreStore()

function updateValue(value: any): void {
  update(value)
}

const possibleValues = computed(() => {
  // TODO: Improve the prop typings so we don't need to use non-null assertions.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const libraryKey = props.fieldSchema.configLibrary!
  if (gameData.value.gameConfig[libraryKey]) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return Object.keys(gameData.value.gameConfig[libraryKey]).map((key) => {
      // Look up if there is a prettier display name for this string id.
      const id = coreStore.stringIdDecorators[props.fieldInfo.fieldType]
        ? coreStore.stringIdDecorators[props.fieldInfo.fieldType](key)
        : key
      return {
        id,
        value: key,
      }
    })
  } else {
    return []
  }
})

useDefault(undefined, stringValue) // Use first value if available, or undefined
</script>
