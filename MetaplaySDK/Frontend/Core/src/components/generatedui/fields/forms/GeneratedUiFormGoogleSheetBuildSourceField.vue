<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  generated-ui-form-container-field(
    typeName="Metaplay.Core.Config.GoogleSheetBuildSource"
    :value="value"
    :field-info="fieldInfo"
    :field-schema="fieldSchema"
    :server-validation-results="serverValidationResults"
    :field-path="fieldInfo.fieldName"
    :logic-version="logicVersion"
    :disabled="disabled"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    @input="onUpdate"
    )
</template>

<script lang="ts" setup>
import { onMounted } from 'vue'

import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import GeneratedUiFormContainerField from './GeneratedUiFormContainerField.vue'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Object,
    default: null,
  },
})

// Matches the spreadsheet Id from a google sheets Url, i.e. https://docs.google.com/spreadsheets/d/{SPREADSHEET_ID}/edit#gid=0
const spreadsheetIdRegex = /d\/(?<id>[a-zA-Z0-9-_]*?)(\/|$)/

function onUpdate(value: { name: string; spreadsheetId: string }): void {
  const match = spreadsheetIdRegex.exec(value.spreadsheetId)
  if (match != null) {
    update({ name: value.name, spreadsheetId: match.groups?.id })
  } else {
    update(value)
  }
}

const emit = defineEmits(generatedUiFieldFormEmits)

const { update } = useGeneratedUiFieldForm(props, emit)

onMounted(() => {
  update(props.value)
})
</script>
