<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="previewLocale")
  generated-ui-form-dynamic-component(
    v-if="localizedFields"
    v-bind="props"
    :fieldInfo="localizedFields[previewLocale]"
    :key="fieldInfo.fieldName+'-'+previewLocale"
    :value="value.localizations && value.localizations[previewLocale] || undefined"
    @input="update"
    :fieldPath="fieldPath+'/Localizations/'+previewLocale"
    :contextObj="fieldSchema.useAsContext ? value : contextObj"
    :logic-version="logicVersion"
    :disabled="disabled"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    )
div(v-else)
  div(v-for="locale in editLocales" :key="fieldInfo.fieldName+'-'+locale")
    generated-ui-form-dynamic-component(
      v-if="localizedFields"
      v-bind="props"
      :fieldInfo="localizedFields[locale]"
      :value="value.localizations && value.localizations[locale] || undefined"
      @input="(value: any) => updateLocale(value, locale)"
      :fieldPath="fieldPath+'/Localizations/'+locale"
      :contextObj="fieldSchema.useAsContext ? value : contextObj"
      :logic-version="logicVersion"
      :disabled="disabled"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    )
</template>

<script lang="ts" setup>
import { difference } from 'lodash-es'
import { computed, watch, ref } from 'vue'

import { getLanguageName } from '@metaplay/meta-ui'
import { camelCaseToTitleCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { getGameDataSubscriptionOptions } from '../../../../subscription_options/general'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import GeneratedUiFormDynamicComponent from './GeneratedUiFormDynamicComponent.vue'

// Use form props but override default value
const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Object,
    default: () => ({ localizations: [] }),
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { update: emitUpdate } = useGeneratedUiFieldForm(props, emit)
// eslint-disable-next-line @typescript-eslint/no-unsafe-argument
const localizationCache = ref<Record<string, any>>(props.value.localizations)

/**
 * Removes locales that are not selected from the editLocales.
 * @param localizations
 */
const pruneLocalizations = (localizations: Record<string, any>): Record<string, any> => {
  // TODO: Improve prop typings to avoid this non-null assertion.
  return Object.fromEntries(
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    props.editLocales!.map((locale: string) => {
      return locale in localizations ? [locale, localizations[locale]] : [locale, '']
    })
  )
}

/**
 * Update the currently visible locale values when editing only a single locale.
 * @param newValue New text for the locale.
 */
const update = (newValue: any): void => {
  // TODO: Improve prop typings to avoid this non-null assertion.
  localizationCache.value = {
    ...localizationCache.value,
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    [props.previewLocale!]: newValue,
  }
  // TODO: Improve prop typings to avoid this non-null assertion.
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const newLocalizations = pruneLocalizations({
    ...props.value.localizations,
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    [props.previewLocale!]: newValue,
  })
  const newVal = { ...props.value, localizations: newLocalizations }
  emitUpdate(newVal)
}

/**
 * Update a single locale value when editing multiple locales.
 * This mode is used for example when the show all flag is checked.
 * @param newValue New text for the locale.
 * @param locale The locale that is currently being edited.
 */
const updateLocale = (newValue: any, locale: string): void => {
  localizationCache.value = { ...localizationCache.value, [locale]: newValue }
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const newLocalizations = pruneLocalizations({
    ...props.value.localizations,
    [locale]: newValue,
  })
  const newVal = { ...props.value, localizations: newLocalizations }
  emitUpdate(newVal)
}

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())

const localizedFields = computed((): any => {
  // TODO: Improve prop typings to avoid this non-null assertion.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const lField: any = props.fieldSchema.fields!.find((field: any) => field.fieldName === 'Localizations')
  const result: Record<string, any> = {}
  // TODO: Improve prop typings to avoid this non-null assertion.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  for (const locale of props.editLocales!) {
    result[locale] = {
      ...props.fieldInfo,
      fieldName: props.fieldInfo.fieldName,
      fieldType: lField.typeParams[1],
      displayProps: {
        displayName:
          camelCaseToTitleCase(props.fieldInfo.fieldName ?? '') + ' (' + getLanguageName(locale, gameData.value) + ')',
      },
    }
  }
  return result
})

watch(
  () => props.editLocales,
  (newValue, oldValue) => {
    // Check if a new locale was added to the edited locales.
    const newArray = difference(newValue, oldValue ?? [])
    if (newArray.length) {
      // setTimeout is used to prevent two updates in the same event from cancelling each other.
      setTimeout(() => {
        if (localizationCache.value[newArray[0]]) {
          updateLocale(localizationCache.value[newArray[0]], newArray[0])
        }
      })
    }

    setTimeout(() => {
      update(
        props.previewLocale && props.previewLocale in props.value.localizations
          ? props.value.localizations[props.previewLocale]
          : ''
      )
    }, 10)
  },
  { deep: true, flush: 'post' }
)
</script>
