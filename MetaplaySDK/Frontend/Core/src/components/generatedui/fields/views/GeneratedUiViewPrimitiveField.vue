<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-flex tw-justify-between tw-space-x-4")
  LabelWithLogicVersion(
    v-if="hasFieldName"
    :is-header="false"
    :fieldInfo="fieldInfo"
    ) {{ displayName }}
  div(
    v-if="value === undefined || value === null"
    class="text-muted"
    ) undefined
  div(v-else) {{ value }}
</template>

<script setup lang="ts">
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'

const props = defineProps(generatedUiFieldBaseProps)
const { displayName, hasFieldName } = useGeneratedUiFieldBase(props)
</script>
