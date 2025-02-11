<template lang="pug">
div(class="tw-relative")
  h3 {{ title }}

  div(v-if="!schema || !gameData || !staticConfigData")
    b-skeleton(width="85%")
    b-skeleton(width="55%")
    b-skeleton(width="70%")

  generated-ui-view-section-content(
    v-else
    class="tw-@container"
    :fieldInfo="rootField"
    :fieldSchema="schema"
    :value="value"
    :previewLocale="previewLocale"
    :logic-version="logicVersion"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    )
</template>

<script lang="ts" setup>
import { onMounted, ref, computed } from 'vue'

import { useSubscription } from '@metaplay/subscriptions'

import {
  getGameDataSubscriptionOptions,
  getStaticConfigSubscriptionOptions,
} from '../../../subscription_options/general'
import GeneratedUiViewSectionContent from '../fields/views/special/GeneratedUiViewSectionContent.vue'
import { EGeneratedUiTypeKind } from '../generatedUiTypes'
import type { IGeneratedUiFieldTypeSchema, IGeneratedUiFieldInfo } from '../generatedUiTypes'
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
   * The raw data of the object.
   */
  value: {
    type: null,
    required: false,
    default: undefined,
  },
  /**
   * The title of this section.
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
   * The logic version of the player that we are currently targeting, e.g. in the player details view, we pass the logic version of the current player model.
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

const rootField = computed<IGeneratedUiFieldInfo>(() => ({
  fieldName: undefined,
  fieldType: schema.value?.typeName ?? '',
  typeKind: schema.value?.typeKind ?? EGeneratedUiTypeKind.Class,
  isLocalized: schema.value?.isLocalized,
  addedInVersion: schema.value?.addedInVersion,
  removedInVersion: undefined,
}))

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
