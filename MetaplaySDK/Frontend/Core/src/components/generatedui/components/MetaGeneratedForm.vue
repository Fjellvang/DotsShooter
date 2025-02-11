<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  v-if="!schema || !gameData || !staticConfigData"
  class="pt-5 tw-w-full tw-text-center"
  )
  b-spinner(
    label="Loading..."
    class="mt-5"
    )/
div(v-else)
  div(v-if="schema.isLocalized && !forcedLocalization")
    div(class="font-weight-bold tw-mb-1") Selected Locales
    MInputMultiSelectCheckbox(
      :model-value="selectedLocales"
      :options="localeOptions"
      :variant="selectedLocales.length === 0 ? 'danger' : 'default'"
      class="mb-2"
      :logic-version="logicVersion"
      @update:model-value="updateLocalizations"
      data-testid="localizations-selection-input"
      )

    div(class="font-weight-bold tw-mb-1") Current Locale
    meta-input-select(
      :value="currentLocale"
      :options="selectedLocaleOptions"
      seachable
      no-clear
      class="mb-2"
      @input="currentLocale = $event"
      data-testid="currentlocale-input"
      )

  generated-ui-form-dynamic-component(
    :key="typeName"
    :fieldInfo="rootField"
    :value="value"
    :editLocales="selectedLocales"
    :previewLocale="currentLocale === 'all' ? undefined : currentLocale"
    :fieldPath="''"
    :serverValidationResults="validationResults"
    :page="page"
    :abstractTypeFilter="abstractTypeFilter"
    :contextObj="value"
    :logicVersion="props.logicVersion"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    @input="update"
    )
  div(
    v-if="!isServerValid"
    class="tw-text-red-500"
    ) {{ serverValidationError }}
</template>

<script lang="ts" setup>
import { computed, onMounted, ref, watch, type PropType } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MInputMultiSelectCheckbox } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import {
  getGameDataSubscriptionOptions,
  getStaticConfigSubscriptionOptions,
} from '../../../subscription_options/general'
import GeneratedUiFormDynamicComponent from '../fields/forms/GeneratedUiFormDynamicComponent.vue'
import type {
  IGeneratedUiFieldInfo,
  IGeneratedUiFieldTypeSchema,
  IGeneratedUiFormAbtractTypeFilter,
} from '../generatedUiTypes'
import { EGeneratedUiTypeKind } from '../generatedUiTypes'
import { findLanguages } from '../generatedUiUtils'
import { PreloadAllSchemasForTypeName, stripNonMetaFields } from '../getGeneratedUiTypeSchema'

const props = defineProps({
  /**
   * The namespace qualified type name of the C# type we are about to visualize.
   * @example 'Metaplay.Core.InGameMail.MetaInGameMail'
   */
  typeName: {
    type: String,
    required: true,
  },
  /**
   * The value of the current form data of the object.
   */
  value: {
    type: null,
    required: false,
    default: undefined,
  },
  /**
   * This can be used to tell the form to add a $type specifier to the output object.
   * @example 'BroadcastForm'
   */
  page: {
    type: String,
    default: undefined,
  },
  /**
   * This can be used to tell the form to add a $type specifier to the output object.
   */
  addTypeSpecifier: {
    type: Boolean,
    default: false,
  },
  /**
   * Can be used to force a form to only show a single localization option, eg. the player's locale.
   * @example 'en'
   */
  forcedLocalization: {
    type: String,
    default: undefined,
  },
  /**
   * A custom type filter for abtract types. Can be used to filter derived types inside a type dropdown.
   * @example see the Metaplay documentation for an example
   */
  abstractTypeFilter: {
    type: Function as PropType<IGeneratedUiFormAbtractTypeFilter>,
    default: undefined,
  },
  /**
   * The logic verison of the player that we are currently targetting, e.g. in the player details view, we pass the logic version of the current player model.
   * This is used to disable components that are not supported by the player's logic version.
   */
  logicVersion: {
    type: Number,
    default: undefined,
  },
  /**
   * Enabling this will show warnings if any of the members/types are only available in a subset of the player base.
   * This can happen if the SupportedLogicVersions is a range (e.g. 4..6) and members were added or removes in between these versions.
   */
  isTargetingMultiplePlayers: {
    type: Boolean,
    default: false,
  },
})

const schema = ref<IGeneratedUiFieldTypeSchema>()
const validationResults = ref()
const validationTimeout = ref<any>(null)

const showAllLocales = ref<boolean>()

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())
const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())
const gameServerApi = useGameServerApi()

const emit = defineEmits<{
  (e: 'input', value: any): void
  (e: 'status', value: boolean): void
}>()

function initializeSchema(): void {
  if (props.typeName !== '') {
    PreloadAllSchemasForTypeName(props.typeName)
      .then((loadedSchema: IGeneratedUiFieldTypeSchema) => {
        schema.value = loadedSchema
      })
      .catch((err: any) => {
        console.error(err)
      })
  }
}

async function update(newValue: any): Promise<void> {
  if (schema.value) {
    if (props.addTypeSpecifier) {
      newValue.$type = schema.value.jsonType
    }
    const stripped = await stripNonMetaFields(newValue, schema.value)
    emit('input', stripped)
  }
}

// TODO: This changed in Vue 3 migration -> check if it still works as expected
const rootField = computed<IGeneratedUiFieldInfo>(() => ({
  fieldName: undefined,
  fieldType: props.typeName,
  typeKind: schema.value?.typeKind ?? EGeneratedUiTypeKind.Class,
  isLocalized: schema.value?.isLocalized,
  addedInVersion: schema.value?.addedInVersion,
  removedInVersion: undefined,
}))

// -- Localization ---

const currentLocale = ref('')
const selectedLocales = ref<string[]>([])

function updateLocalizations(newValues: string[]): void {
  selectedLocales.value = newValues
  if (!newValues.includes(currentLocale.value)) {
    currentLocale.value = newValues[0]
  }
}

function initialSelectedLocalizations(): string[] {
  let selectedLocales: string[] = []

  if (!gameData.value || !staticConfigData.value) {
    return selectedLocales
  }

  const defaultLang = staticConfigData.value.defaultLanguage

  // always add default language
  if (defaultLang && !selectedLocales.includes(defaultLang)) {
    selectedLocales.push(defaultLang)
  }

  const oldLocales = findLanguages(props.value, gameData.value)
  for (const lang of oldLocales) {
    if (!selectedLocales.includes(lang)) {
      selectedLocales.push(lang)
    }
  }

  if (oldLocales.length === 0) {
    for (const lang in gameData.value.gameConfig.Languages) {
      if (!selectedLocales.includes(lang)) {
        selectedLocales.push(lang)
      }
    }
  }

  if (props.forcedLocalization) {
    selectedLocales = [props.forcedLocalization]
  }

  currentLocale.value = props.forcedLocalization ?? staticConfigData.value.defaultLanguage

  return selectedLocales
}

const allLanguages = computed(() => {
  return gameData.value?.gameConfig.Languages ?? {}
})

const localeOptions = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const a = Object.values(allLanguages.value)
    .sort((a: any, b: any) => {
      if (a.languageId === staticConfigData.value?.defaultLanguage) {
        return -1
      } else if (b.languageId === staticConfigData.value?.defaultLanguage) {
        return 1
      } else {
        return 0
      }
    })
    .filter((lang: any) => !props.forcedLocalization || lang.languageId === props.forcedLocalization)
    .map((lang: any) => {
      return {
        label: lang.displayName,
        value: lang.languageId,
        disabled: lang.languageId === staticConfigData.value?.defaultLanguage || !!props.forcedLocalization,
      }
    })
  return a
})

const selectedLocaleOptions = computed((): Array<MetaInputSelectOption<string>> => {
  const options = selectedLocales.value.map((lang) => {
    return {
      id: gameData.value.gameConfig.Languages[lang].displayName,
      value: lang,
    }
  })

  options.unshift({
    id: 'Show all',
    value: 'all',
  })

  return options
})

// When either gameData or config changes, find localizations again.
watch(
  [gameData, staticConfigData],
  (newValue) => {
    selectedLocales.value = initialSelectedLocalizations()
  },
  {
    deep: false,
  }
)

// --- Validation shenanigans ---

const isServerValid = computed(() => {
  return validationResults.value ? validationResults.value.length === 0 : undefined
})

const serverValidationError = computed(() => {
  if (validationResults.value?.length > 0) {
    return validationResults.value[0].path + ': ' + validationResults.value[0].reason
  } else {
    return ''
  }
})

async function validate(value: any): Promise<void> {
  // Don't validate empty objects.
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  if (typeof value === 'object' && Object.keys(value).length === 0) {
    return
  }

  let uri = `forms/schema/${props.typeName}/validate`
  if (props.logicVersion !== undefined) {
    uri += `/${props.logicVersion}`
  }
  // \todo #mail-refactoring: listen to fields status event to support client side validation
  // server side validate.
  const response = await gameServerApi.post(uri, value)
  if (response.status === 200) {
    validationResults.value = response.data
    emit('status', validationResults.value.length === 0)
  } else {
    console.error('Server validation failed: ', response)
    emit('status', false)
  }
}

watch(
  () => props.value,
  (newValue, oldValue) => {
    if (validationTimeout.value !== null) {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      clearTimeout(validationTimeout.value)
    }
    validationTimeout.value = setTimeout(() => {
      validate(newValue)
        .then(() => {
          validationTimeout.value = null
        })
        .catch((err: any) => {
          console.error(err)
        })
    }, 200)
  }
)

watch(
  () => props.typeName,
  (newValue, oldValue) => {
    initializeSchema()
  }
)

// --- Lifecycle hooks ---

onMounted(() => {
  initializeSchema()
  selectedLocales.value = initialSelectedLocalizations()
})
</script>
