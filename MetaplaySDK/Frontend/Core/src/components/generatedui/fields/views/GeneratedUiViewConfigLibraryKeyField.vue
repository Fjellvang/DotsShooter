<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="d-flex justify-content-between")
  LabelWithLogicVersion(
    v-if="hasFieldName"
    :is-header="false"
    :fieldInfo="fieldInfo"
    ) {{ displayName }}
  div(
    v-if="prettyValue === undefined || prettyValue === null"
    class="text-muted"
    ) undefined
  div(v-else) {{ prettyValue }}
</template>

<script setup lang="ts">
import { computed } from 'vue'

import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../../../coreStore'
import { getGameDataSubscriptionOptions } from '../../../../subscription_options/general'
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())
const coreStore = useCoreStore()

const props = defineProps(generatedUiFieldBaseProps)
const { displayName, hasFieldName } = useGeneratedUiFieldBase(props)

const prettyValue = computed(() => {
  if (props.value === undefined || props.value === null) {
    return undefined
  }
  return coreStore.stringIdDecorators[props.fieldInfo.fieldType]
    ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      coreStore.stringIdDecorators[props.fieldInfo.fieldType](props.value)
    : props.value
})
</script>
