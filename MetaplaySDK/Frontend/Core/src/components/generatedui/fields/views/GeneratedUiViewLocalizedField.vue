<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MListItem(
  v-if="staticConfigData"
  class="!tw-py-0"
  condensed
  )
  LabelWithLogicVersion(
    :fieldInfo="fieldInfo"
    :is-header="false"
    ) {{ displayName }}
  template(
    v-if="value.localizations"
    #top-right
    )
    meta-language-label(
      :language="previewLocale && previewLocale in value.localizations ? previewLocale : staticConfigData.defaultLanguage"
      variant="span"
      )
  template(#bottom-left)
    generated-ui-view-dynamic-component(
      v-if="localizedField && value.localizations"
      v-bind="$props"
      :fieldInfo="localizedField"
      :value="previewLocale && previewLocale in value.localizations ? value.localizations[previewLocale] : value.localizations[staticConfigData.defaultLanguage]"
      :logic-version="logicVersion"
      )
    generated-ui-view-dynamic-component(
      v-else-if="localizedField && fieldSchema.typeName === 'Metaplay.Core.Localization.LocalizedString' && value.localizationKey"
      v-bind="$props"
      :fieldInfo="localizedField"
      :value="`[${value.localizationKey}]`"
      :logic-version="logicVersion"
      )
    MBadge(v-else) null
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { MBadge, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getStaticConfigSubscriptionOptions } from '../../../../subscription_options/general'
import MetaLanguageLabel from '../../../MetaLanguageLabel.vue'
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'
import type { IGeneratedUiFieldInfo } from '../../generatedUiTypes'
import GeneratedUiViewDynamicComponent from './GeneratedUiViewDynamicComponent.vue'

const props = defineProps(generatedUiFieldBaseProps)

const { displayName } = useGeneratedUiFieldBase(props)

const localizedType = ref('')
const localizedField = ref<IGeneratedUiFieldInfo>()
const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

onMounted(() => {
  const lField = props.fieldSchema.fields?.find((field) => field.fieldName === 'Localizations')
  localizedType.value = lField?.typeParams?.[1] ?? ''

  localizedField.value = {
    ...props.fieldInfo,
    fieldName: undefined,
    fieldType: localizedType.value,
  }
})
</script>
