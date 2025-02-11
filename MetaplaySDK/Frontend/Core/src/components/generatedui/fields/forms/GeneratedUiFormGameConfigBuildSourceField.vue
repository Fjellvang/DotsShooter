<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  LabelWithLogicVersion(
    :fieldInfo="fieldInfo"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    ) {{ displayName }}
  div(
    v-if="displayHint"
    class="text-muted small"
    ) {{ displayHint }}

  div(
    class="tw-mt-1"
    :data-testid="dataTestid + 'source-slot'"
    )
    meta-input-select(
      :no-clear="true"
      :value="selectedSourceOption"
      :options="sourceOptions"
      placeholder="Select a source..."
      :disabled="disabled"
      @input="onSourceSelectionChanged"
      )

  div(
    v-if="isCustomSourceSelected"
    class="mt-2 border p-2 rounded bg-light"
    )
    generated-ui-form-abstract-field(
      :typeName="'Metaplay.Core.Config.GameConfigBuildSource'"
      :value="customSource"
      :field-info="fieldInfo"
      :field-schema="fieldSchema"
      :server-validation-results="serverValidationResults"
      :logic-version="logicVersion"
      :disabled="disabled"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      @input="onCustomSourceChanged"
      )
</template>

<script lang="ts" setup>
import { ref, type ComputedRef, computed, watch } from 'vue'

import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { useSubscription } from '@metaplay/subscriptions'

import { getStaticConfigSubscriptionOptions } from '../../../../subscription_options/general'
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import GeneratedUiFormAbstractField from './GeneratedUiFormAbstractField.vue'

const { data: staticConfig } = useSubscription(getStaticConfigSubscriptionOptions())

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Object,
    default: null,
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { displayName, displayHint, update, dataTestid } = useGeneratedUiFieldForm(props, emit)

function onSourceSelectionChanged(newSource: any): void {
  if (newSource.displayName === customSourceName) {
    isCustomSourceSelected = true
    update(customSource.value)
  } else {
    isCustomSourceSelected = false
    update(newSource)
  }
}

function onCustomSourceChanged(newSource: any): void {
  customSource.value = newSource
  if (isCustomSourceSelected) {
    update(newSource)
  }
}

function onSourceValidityChanged(valid: boolean): void {
  // TODO: use frontend validation when possible
  // sourceValidationState.value = isValid
}

let isCustomSourceSelected = false
const customSourceName = 'Custom Source'
const customSourcePlaceholderForList = { displayName: customSourceName }
const customSource = ref()

const predefinedSources = computed<Array<{ displayName: string }>>(() => {
  let config = null
  if (props.page === 'GameConfigBuildCard') {
    config = staticConfig.value?.gameConfigBuildInfo
  } else if (props.page === 'LocalizationsBuildCard') {
    config = staticConfig.value?.localizationsBuildInfo
  }
  return config?.slotToAvailableSourcesMapping[props.fieldInfo.fieldName ?? ''] ?? []
})
const availableSources = computed<Array<{ displayName: string }>>(() =>
  predefinedSources.value.concat(customSourcePlaceholderForList)
)
const selectedSourceOption = computed(() =>
  isCustomSourceSelected ? customSourcePlaceholderForList : { ...props.value, displayName: props.value?.name }
)
const sourceOptions: ComputedRef<Array<MetaInputSelectOption<any>>> = computed(() =>
  availableSources.value.map((element) => ({
    id: element.displayName,
    value: element,
  }))
)
const defaultValue = computed(() => predefinedSources.value[0])
const currentValue = computed(() => props.value)

watch(
  currentValue,
  (val, oldVal) => {
    if (!val) {
      setTimeout(() => {
        update(defaultValue.value)
      }, 0)
    }
  },
  { immediate: true }
)

const customSourceValidationState = ref<boolean>()
</script>
