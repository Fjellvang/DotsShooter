<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Loading indicator
b-skeleton(v-if="!resolvedSchema")

div(v-else-if="!value")
  MBadge null

div(v-else-if="resolvedSchema.typeKind === 'Class' && resolvedSchema.fields")
  //- List layout
  MList(
    v-if="viewableFields && (viewableFields.length > 1 || abstractTitle)"
    showBorder
    class="tw-mb-3"
    )
    div(
      v-if="abstractTitle"
      class="tw-flex tw-justify-between tw-space-x-4 tw-px-5 tw-py-3"
      )
      div Type
      div {{ abstractTitle }}
    div(
      v-for="field in viewableFields"
      :key="field.fieldName"
      class="tw-px-5 tw-py-3"
      )
      generated-ui-view-dynamic-component(
        v-bind="$props"
        :fieldInfo="field"
        :value="newtonsoftCamelCase(field.fieldName) in value ? value[newtonsoftCamelCase(field.fieldName)] : undefined"
        :logic-version="logicVersion"
        )
  //- Inline rendering
  div(v-else-if="viewableFields && viewableFields.length === 1")
    span(
      v-for="field in viewableFields"
      :key="field.fieldName"
      )
      generated-ui-view-dynamic-component(
        v-bind="$props"
        :fieldInfo="field"
        :value="newtonsoftCamelCase(field.fieldName) in value ? value[newtonsoftCamelCase(field.fieldName)] : undefined"
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
import { computed, onMounted, ref, watch } from 'vue'

import { MBadge, MList, MListItem } from '@metaplay/meta-ui-next'

import { generatedUiFieldBaseProps } from '../../generatedFieldBase'
import type { IGeneratedUiFieldInfo, IGeneratedUiFieldTypeSchema } from '../../generatedUiTypes'
import { EGeneratedUiTypeKind } from '../../generatedUiTypes'
import { isVersionInRange, newtonsoftCamelCase } from '../../generatedUiUtils'
import { GetTypeSchemaForTypeName } from '../../getGeneratedUiTypeSchema'
import GeneratedUiViewDynamicComponent from './GeneratedUiViewDynamicComponent.vue'

const props = defineProps(generatedUiFieldBaseProps)

// --- Schema resolving for Abstract types ---
const resolvedSchema = ref<IGeneratedUiFieldTypeSchema>()

onMounted(async () => {
  if (props.fieldInfo.typeKind === EGeneratedUiTypeKind.Abstract && props.value) {
    resolvedSchema.value = props.value.$type
      ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        await GetTypeSchemaForTypeName(props.value.$type)
      : undefined
  } else {
    resolvedSchema.value = props.fieldSchema
  }
})

// Update resolved schema if value changes type
watch(
  () => props.value?.$type,
  async (newValue) => {
    if (props.fieldInfo.typeKind === EGeneratedUiTypeKind.Abstract) {
      resolvedSchema.value = newValue
        ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          await GetTypeSchemaForTypeName(newValue)
        : undefined
    }
  }
)

const abstractTitle = computed(() => {
  if (resolvedSchema.value && resolvedSchema.value.typeName !== props.fieldInfo.fieldType) {
    return resolvedSchema.value.typeName.split('.').at(-1)
  } else {
    return undefined
  }
})

// --- Field filtering ---

// TODO: should be different prop notViewable?
const viewableFields = computed(() =>
  resolvedSchema.value?.fields?.filter(
    (x: IGeneratedUiFieldInfo) => !x.notEditable && isVersionInRange(x, props.logicVersion)
  )
)
</script>
