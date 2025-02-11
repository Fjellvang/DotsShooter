<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  b-row
    LabelWithLogicVersion(
      v-if="hasFieldName"
      :fieldInfo="fieldInfo"
      :is-header="false"
      ) {{ displayName }}
    b-col(class="tw-text-right")
      meta-time(
        v-if="value.length > 0"
        :date="value"
        )
      div(v-else) undefined
</template>

<script lang="ts" setup>
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'

const props = defineProps(generatedUiFieldBaseProps)
const { displayName, hasFieldName } = useGeneratedUiFieldBase(props)
</script>
