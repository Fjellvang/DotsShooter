<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCollapse(
  v-if="fieldSchema && (fieldSchema.typeKind === 'Class' || fieldSchema.typeKind === 'Abstract')"
  extraMListItemMargin
  )
  template(#header)
    MListItem(noLeftPadding) {{ resolvedTitle }}
  template(#default)
    generated-ui-view-dynamic-component(
      v-bind="$props"
      :fieldInfo="fieldInfo"
      :value="value"
      :logic-version="logicVersion"
      )
generated-ui-view-dynamic-component(
  v-else
  v-bind="$props"
  :fieldInfo="fieldInfo"
  :value="value"
  class="tw-mx-5 tw-my-3"
  :logic-version="logicVersion"
  )
</template>

<script lang="ts" setup>
import { ref, onMounted, watch } from 'vue'

import { MCollapse, MListItem } from '@metaplay/meta-ui-next'

import { generatedUiFieldBaseProps } from '../../../generatedFieldBase'
import { EGeneratedUiTypeKind } from '../../../generatedUiTypes'
import { newtonsoftCamelCase } from '../../../generatedUiUtils'
import { GetTypeSchemaForTypeName } from '../../../getGeneratedUiTypeSchema'
import GeneratedUiViewDynamicComponent from '../GeneratedUiViewDynamicComponent.vue'

const props = defineProps({
  ...generatedUiFieldBaseProps,
  index: {
    type: Number,
    default: 0,
  },
})

// --- Finding the title magics! ---

const resolvedTitle = ref<any>(null)

async function tryFindTitle(typeName: string, value: any): Promise<string | undefined> {
  const schema = await GetTypeSchemaForTypeName(typeName)
  if (schema.typeKind === EGeneratedUiTypeKind.Abstract && '$type' in value) {
    // TODO: Improve prop typings to avoid this non-null assertion.
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    const derivedType = schema.derived!.find((d: any) => d.jsonType === value.$type)!.typeName
    const derivedTitle = await tryFindTitle(derivedType, value)

    if (!derivedTitle) {
      return derivedType.split('.').at(-1)
    }
  } else if (schema.typeKind === EGeneratedUiTypeKind.Class) {
    // TODO: Improve prop typings to avoid this non-null assertion.
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    for (const field of schema.fields!) {
      if (field.fieldType === '[]' || field.fieldType === '{}') {
        continue
      }
      const fieldValue =
        newtonsoftCamelCase(field.fieldName) in value ? value[newtonsoftCamelCase(field.fieldName)] : undefined

      if (fieldValue) {
        const childTitle: any = await tryFindTitle(field.fieldType, fieldValue)
        if (childTitle) {
          return childTitle
        }
      }
    }
    return undefined
  } else if (schema.typeKind === EGeneratedUiTypeKind.Localized) {
    return 'Localized' // \todo find localized title
  } else if (schema.typeName === 'System.String') {
    return value as string
  }
  return undefined
}

async function findTitle(value: any): Promise<void> {
  await tryFindTitle(props.fieldInfo.fieldType, value).then((title) => {
    resolvedTitle.value = title ?? String(props.index)
  })
}

watch(
  () => props.value,
  async (newValue) => {
    await findTitle(newValue)
  }
)

onMounted(async () => {
  await findTitle(props.value)
})
</script>
