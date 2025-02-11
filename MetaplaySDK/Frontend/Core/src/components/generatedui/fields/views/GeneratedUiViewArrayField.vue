<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Collapsed list layout
MCollapse(
  v-if="hasContent"
  :id="String(fieldInfo.fieldName)"
  class="-tw-ml-5"
  )
  //- Collapse header
  template(#header)
    LabelWithLogicVersion(
      :fieldInfo="fieldInfo"
      :is-header="false"
      ) {{ displayName }}

  //- Loading
  b-skeleton(v-if="!arrayField")
  //- Collapse content
  div(v-else)
    div(
      v-for="(val, idx) in value"
      :key="idx"
      class="tw-mb-1"
      )
      generated-ui-view-dynamic-component(
        v-bind="$props"
        :fieldInfo="arrayField"
        :value="val"
        :logic-version="logicVersion"
        )
div(
  v-else
  class="text-muted small tw-italic"
  ) No {{ displayName }}
</template>

<script lang="ts" setup>
import { onMounted, ref, computed } from 'vue'

import { MCollapse } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'
import type { IGeneratedUiFieldInfo } from '../../generatedUiTypes'
import { GetTypeSchemaForTypeName } from '../../getGeneratedUiTypeSchema'
import GeneratedUiViewDynamicComponent from './GeneratedUiViewDynamicComponent.vue'

const props = defineProps(generatedUiFieldBaseProps)
const { displayName } = useGeneratedUiFieldBase(props)

const hasContent = computed(() => Array.isArray(props.value) && props.value.length > 0)

/**
 * FieldInfo constructed from the array type parameter.
 */
const arrayField = ref<IGeneratedUiFieldInfo>()

onMounted(async () => {
  // Get the schema for the type contained in the array and construct a type info from it.
  if (props.fieldInfo.fieldType === '[]' && Array.isArray(props.value)) {
    // TODO: Improve the prop typings so we don't need to use non-null assertions.
    const typeSchema = await GetTypeSchemaForTypeName(
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      props.fieldInfo.typeParams![0]
    )
    arrayField.value = {
      fieldName: '',
      fieldType: typeSchema.typeName,
      typeKind: typeSchema.typeKind,
      isLocalized: typeSchema.isLocalized,
      addedInVersion: typeSchema.addedInVersion,
      removedInVersion: undefined,
    }
  }
})
</script>
