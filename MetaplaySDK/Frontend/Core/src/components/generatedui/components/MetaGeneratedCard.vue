<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Loading animation
//- TODO: Could we change this to block rendering until all schemas etc. have been loaded -> no child component would need loading state handling?
b-card(
  v-if="!schema || !gameData || !staticConfigData"
  class="h-100"
  )
  b-skeleton(width="85%")
  b-skeleton(width="55%")
  b-skeleton(width="70%")

meta-list-card(
  v-else-if="list"
  :title="title"
  :icon="icon || 'list'"
  :itemList="value ? value : []"
  class="h-100"
  )
  template(#item-card="row")
    generated-ui-view-card-list-item(
      v-if="rootField && schema"
      :fieldInfo="rootField"
      :fieldSchema="schema"
      :value="row.item"
      :index="Number(row.index)"
      :previewLocale="currentLocale"
      :logic-version="logicVersion"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      )
meta-list-card(
  v-else-if="dictionary"
  :title="title"
  :icon="icon || 'list'"
  :itemList="Object.keys(value || {}).map((key) => ({ key, value: value[key] }))"
  class="h-100"
  )
  template(#item-card="{ item: dictionaryItem }")
    MCollapse(extraMListItemMargin)
      template(#header)
        MListItem(noLeftPadding) {{ dictionaryItem.key }}
      template(#default)
        generated-ui-view-dynamic-component(
          v-if="rootField"
          :fieldInfo="rootField"
          :value="dictionaryItem.value"
          :previewLocale="currentLocale"
          :logic-version="logicVersion"
          :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
          )
b-card(
  v-else
  no-body
  class="h-100 shadow-sm"
  )
  //- Header
  b-card-title(class="d-flex align-items-center")
    fa-icon(
      v-if="icon"
      :icon="icon"
      class="mr-2"
      )
    | {{ title }}
  generated-ui-view-dynamic-component(
    v-if="rootField"
    :fieldInfo="{ ...rootField }"
    :value="value"
    :previewLocale="currentLocale"
    :logic-version="logicVersion"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    )
//- TODO: some cool error box?
</template>

<script lang="ts" setup>
import { onMounted, ref, computed } from 'vue'

import { MCollapse, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import {
  getGameDataSubscriptionOptions,
  getStaticConfigSubscriptionOptions,
} from '../../../subscription_options/general'
import GeneratedUiViewDynamicComponent from '../fields/views/GeneratedUiViewDynamicComponent.vue'
import GeneratedUiViewCardListItem from '../fields/views/special/GeneratedUiViewCardListItem.vue'
import type { IGeneratedUiFieldTypeSchema, IGeneratedUiFieldInfo } from '../generatedUiTypes'
import { EGeneratedUiTypeKind } from '../generatedUiTypes'
import { PreloadAllSchemasForTypeName } from '../getGeneratedUiTypeSchema'

const props = defineProps({
  // TODO: If we could generate types from C# to TS, this could an enum or something?
  /**
   * The namespace qualified type name of the C# type we are about to visualize.
   * @example 'Metaplay.Core.InGameMail.MetaInGameMail'
   */
  typeName: {
    type: String,
    default: undefined,
  },
  /**
   * Whether this card is meant to expect a list as a value.
   * If this is set, the typeName prop should be the type of the items inside the list.
   */
  list: {
    type: Boolean,
    default: false,
  },
  /**
   * Whether this card is meant to expect a dictionary as a value.
   * If this is set, the typeName prop should be the type of the value of the keyvalue pair.
   */
  dictionary: {
    type: Boolean,
    default: false,
  },
  /**
   * An icon for this card.
   * @example 'list'
   */
  icon: {
    type: String,
    default: undefined,
  },
  /**
   * The raw data of the object.
   */
  value: {
    type: null,
    required: false,
    default: undefined,
  },
  /**
   * The title of this card.
   * @example 'Content'
   */
  title: {
    type: String,
    required: true,
  },
  /**
   * In case of localised content, this is the one locale to show as a preview. Should be set to the player's active locale to preview what they would see.
   * @example 'en'
   */
  previewLocale: {
    type: String,
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
const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())
const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

const rootField = computed<IGeneratedUiFieldInfo>(() => {
  return {
    fieldName: undefined,
    fieldType: schema.value?.typeName ?? '',
    typeKind: schema.value?.typeKind ?? EGeneratedUiTypeKind.Class,
    isLocalized: schema.value?.isLocalized,
    addedInVersion: schema.value?.addedInVersion,
    removedInVersion: undefined,
  }
})

const currentLocale = computed(() => {
  if (schema.value?.isLocalized) {
    return props.previewLocale ? props.previewLocale : staticConfigData.value?.defaultLanguage
  } else return undefined
})

// Preload all schemas for this type.
onMounted(async () => {
  if (props.typeName) {
    schema.value = await PreloadAllSchemasForTypeName(props.typeName)
  } else if (props.value && typeof props.value === 'object' && '$type' in props.value) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    schema.value = await PreloadAllSchemasForTypeName(props.value.$type)
  } else {
    throw Error('No typeName or value with $type given')
  }
})
</script>
