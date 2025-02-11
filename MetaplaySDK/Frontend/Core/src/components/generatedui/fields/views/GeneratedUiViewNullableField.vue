<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Loading indicator
b-skeleton(v-if="!fieldSchema || !valueField")

div(
  v-else-if="!value"
  class="tw-flex tw-items-center tw-justify-between"
  )
  LabelWithLogicVersion(
    :fieldInfo="fieldInfo"
    :is-header="false"
    ) {{ fieldInfo.fieldName ? camelCaseToSentenceCase(fieldInfo.fieldName) : undefined }}
  MBadge null

div(v-else-if="fieldSchema.typeKind === 'Nullable' && valueField")
  div(v-if="valueField.typeKind === 'Class'") {{ fieldInfo.fieldName ? camelCaseToSentenceCase(fieldInfo.fieldName) : 'Missing field name' }}
  generated-ui-view-dynamic-component(
    v-bind="$props"
    :fieldInfo="valueField"
    :value="valueField.typeKind === 'Class' ? { ...value, $type: fieldSchema.valueType } : value"
    :logic-version="logicVersion"
    )

//- Error
div(
  v-else
  class="small"
  )
  div(class="text-danger") Failed to find out how to visualize the fields for this object:
  pre {{ value }}
</template>

<script lang="ts" setup>
import { onMounted, ref } from 'vue'

import { MBadge } from '@metaplay/meta-ui-next'
import { camelCaseToSentenceCase } from '@metaplay/meta-utilities'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps } from '../../generatedFieldBase'
import { GetTypeSchemaForTypeName } from '../../getGeneratedUiTypeSchema'
import GeneratedUiViewDynamicComponent from './GeneratedUiViewDynamicComponent.vue'

const props = defineProps(generatedUiFieldBaseProps)

const valueField = ref()

onMounted(async () => {
  // Get the schema for the type contained in the nullable and construct a type info from it.
  // TODO: Improve the prop typings so we don't need to use non-null assertions.
  const typeSchema = await GetTypeSchemaForTypeName(
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    props.fieldSchema.valueType!
  )
  valueField.value = {
    ...props.fieldInfo,
    fieldType: typeSchema.typeName,
    typeKind: typeSchema.typeKind,
    isLocalized: typeSchema.isLocalized,
  }
})
</script>
