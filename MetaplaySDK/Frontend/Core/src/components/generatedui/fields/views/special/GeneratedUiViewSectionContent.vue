<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-m-auto tw-flex tw-flex-wrap tw-justify-items-center")
  div(
    v-if="primitiveCard"
    class="tw-w-full tw-p-2 @2xl:tw-w-1/2"
    )
    b-card(
      :title="primitiveCard.title"
      class="h-100 shadow-sm"
      )
      //- Below not tested in dashboard locally.
      MList(
        v-if="primitiveCard.fields && primitiveCard.fields.length > 0"
        showBorder
        )
        MListItem(
          v-for="primfield in primitiveCard.fields"
          :key="primfield.field.fieldName"
          )
          template(#bottom-left)
            generated-ui-view-dynamic-component(
              v-bind="$props"
              :fieldInfo="primfield.field"
              :value="primfield.value"
              :logic-version="logicVersion"
              )

  div(
    v-if="localizedCard"
    class="tw-w-full tw-p-2 @2xl:tw-w-1/2"
    )
    meta-list-card(
      title="Localizations"
      icon="language"
      :itemList="localizedCard.languages"
      class="h-100"
      )
      template(#item-card="{ item: localization }")
        MCollapse(extraMListItemMargin)
          template(#header)
            MListItem(noLeftPadding) {{ localization.displayName }}
          template(#default)
            MList(
              v-if="localizedCard.fields && localizedCard.fields.length > 0"
              showBorder
              noLeftPadding
              )
              div(
                v-for="locfield in localizedCard.fields"
                :key="locfield.field.fieldName"
                class="tw-py-2"
                )
                //- TODO: Below is not working properly. Look into styling later.
                generated-ui-view-dynamic-component(
                  v-bind="$props"
                  :fieldInfo="locfield.field"
                  :value="locfield.value"
                  :previewLocale="localization.lang"
                  :logic-version="logicVersion"
                  )

  div(
    v-for="othfield in otherFields"
    :key="othfield.field.fieldName"
    class="tw-w-full tw-p-2 @2xl:tw-w-1/2"
    )
    meta-generated-card(
      v-if="othfield.arrayType"
      :typeName="othfield.arrayType"
      :value="othfield.value"
      :title="fieldTitle(othfield.field)"
      list
      :logic-version="logicVersion"
      )
    meta-generated-card(
      v-else-if="othfield.dictionaryType"
      :typeName="othfield.dictionaryType"
      :value="othfield.value"
      :title="fieldTitle(othfield.field)"
      dictionary
      :logic-version="logicVersion"
      )
    meta-generated-card(
      v-else
      :typeName="othfield.field.fieldType"
      :value="othfield.value"
      :title="fieldTitle(othfield.field)"
      :logic-version="logicVersion"
      )
</template>

<script lang="ts" setup>
import { isEqual } from 'lodash-es'
import { computed, onMounted, ref, watch } from 'vue'

import { getLanguageName } from '@metaplay/meta-ui'
import { MCollapse, MList, MListItem } from '@metaplay/meta-ui-next'
import { camelCaseToTitleCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { getGameDataSubscriptionOptions } from '../../../../../subscription_options/general'
import MetaGeneratedCard from '../../../components/MetaGeneratedCard.vue'
import { generatedUiFieldBaseProps } from '../../../generatedFieldBase'
import { EGeneratedUiTypeKind } from '../../../generatedUiTypes'
import type { IGeneratedUiFieldInfo, IGeneratedUiFieldTypeSchema } from '../../../generatedUiTypes'
import { findLanguages, newtonsoftCamelCase } from '../../../generatedUiUtils'
import { GetTypeSchemaForTypeName } from '../../../getGeneratedUiTypeSchema'
import GeneratedUiViewDynamicComponent from '../GeneratedUiViewDynamicComponent.vue'

export interface ISectionField {
  field: IGeneratedUiFieldInfo
  value: any
  arrayType?: string
  dictionaryType?: string
}

const props = defineProps(generatedUiFieldBaseProps)

// --- Schema resolving for Abstract types ---
const resolvedSchema = ref<IGeneratedUiFieldTypeSchema>()

onMounted(async () => {
  if (props.fieldInfo.typeKind === EGeneratedUiTypeKind.Abstract) {
    resolvedSchema.value = props.value.$type
      ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        await GetTypeSchemaForTypeName(props.value.$type)
      : undefined
  } else {
    resolvedSchema.value = props.fieldSchema
  }
})

// --- Field filtering ---

// TODO: use different prop notViewable
const viewableFilter = (x: IGeneratedUiFieldInfo): boolean => !x.notEditable

const primitiveFilter = (x: IGeneratedUiFieldInfo): boolean =>
  x.typeKind === EGeneratedUiTypeKind.Primitive ||
  x.typeKind === EGeneratedUiTypeKind.StringId ||
  x.typeKind === EGeneratedUiTypeKind.ConfigLibraryItem ||
  x.typeKind === EGeneratedUiTypeKind.Enum ||
  x.typeKind === EGeneratedUiTypeKind.DynamicEnum ||
  x.typeKind === EGeneratedUiTypeKind.Nullable ||
  x.fieldType === 'Metaplay.Core.MetaTime'

const localizedFilter = (x: IGeneratedUiFieldInfo): boolean => x.typeKind === EGeneratedUiTypeKind.Localized

const primitiveFields = ref<ISectionField[]>([])
const localizedFields = ref<ISectionField[]>([])
const otherFields = ref<ISectionField[]>([])

// Find all fields of the class recursively and sort them based on their type.
watch(
  () => [resolvedSchema.value, props.value],
  async ([newResolvedSchema, newValue], [oldSchema, oldValue]) => {
    if (!newResolvedSchema) {
      return
    }
    if (
      (primitiveFields.value.length > 0 || localizedFields.value.length > 0 || otherFields.value.length > 0) &&
      isEqual(newValue, oldValue)
    ) {
      return
    }
    const schemasTraversed: string[] = []
    const traverseQueue: Array<{
      schema: IGeneratedUiFieldTypeSchema
      value: any
      namePrefix?: string
    }> = [
      {
        schema: newResolvedSchema as IGeneratedUiFieldTypeSchema,
        value: newValue,
      },
    ]

    primitiveFields.value = []
    localizedFields.value = []
    otherFields.value = []

    while (traverseQueue.length > 0) {
      // TODO: Improve prop typings to avoid this non-null assertion.
      const {
        schema: currentSchema,
        value: currentValue,
        namePrefix,
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      } = traverseQueue.pop()!

      // Avoid recursive reference looping
      if (schemasTraversed.find((x) => currentSchema.typeName === x)) {
        continue
      }

      schemasTraversed.push(currentSchema.typeName)

      // TODO: Improve prop typings to avoid this non-null assertion.
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      for (const field of currentSchema.fields!) {
        if (!viewableFilter(field)) {
          continue
        }
        const fieldValue = currentValue ? currentValue[newtonsoftCamelCase(field.fieldName)] : undefined

        const constructedField: ISectionField = {
          field: {
            ...field,
            fieldName: (namePrefix ?? '') + field.fieldName,
          },
          value: fieldValue,
        }

        if (field.typeKind === EGeneratedUiTypeKind.ValueCollection) {
          // TODO: Improve prop typings to avoid this non-null assertion.
          // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
          constructedField.arrayType = field.typeParams![0]
        } else if (field.typeKind === EGeneratedUiTypeKind.KeyValueCollection) {
          // TODO: Improve prop typings to avoid this non-null assertion.
          // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
          constructedField.dictionaryType = field.typeParams![1]
        }

        // Deep traverse into classes and abstracts
        if (field.typeKind === EGeneratedUiTypeKind.Class) {
          const fieldSchema = await GetTypeSchemaForTypeName(field.fieldType)
          traverseQueue.push({
            schema: fieldSchema,
            value: fieldValue,
            // TODO: Improve prop typings to avoid this non-null assertion.
            namePrefix:
              // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
              currentSchema.fields!.length > 1 ? field.fieldName : namePrefix, // Add name prefix if current schema has more than one field
          })
        } else if (field.typeKind === EGeneratedUiTypeKind.Abstract && fieldValue && '$type' in fieldValue) {
          const fieldType = fieldValue.$type
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          const fieldSchema = await GetTypeSchemaForTypeName(fieldType)
          traverseQueue.push({
            schema: fieldSchema,
            value: fieldValue,
            // TODO: Improve prop typings to avoid this non-null assertion.
            namePrefix:
              // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
              currentSchema.fields!.length > 1 ? field.fieldName : namePrefix, // Add name prefix if current schema has more than one field
          })
        } else if (primitiveFilter(field)) {
          primitiveFields.value.push(constructedField)
        } else if (localizedFilter(field)) {
          localizedFields.value.push(constructedField)
        } else {
          otherFields.value.push(constructedField)
        }
      }
    }
  },
  {
    deep: true,
  }
)

// --- Section members ---

const primitiveCard = computed(() => {
  if (primitiveFields.value.length === 0) {
    return null
  }
  return {
    title: 'General',
    fields: primitiveFields.value,
  }
})

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())

const localizedCard = computed(() => {
  if (localizedFields.value.length === 0) {
    return null
  }

  return {
    title: 'Localizations',
    languages: findLanguages(props.value, gameData.value).map((x) => ({
      displayName: getLanguageName(x, gameData.value),
      lang: x,
    })),
    fields: localizedFields.value,
  }
})

function fieldTitle(field: IGeneratedUiFieldInfo): string {
  return field.fieldName ? camelCaseToTitleCase(field.fieldName) : 'Untitled'
}
</script>
