<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="d-flex justify-content-between")
  LabelWithLogicVersion(
    v-if="hasFieldName"
    :fieldInfo="fieldInfo"
    :is-header="false"
    ) {{ displayName }}
  div(
    v-if="!value"
    class="text-muted"
    ) undefined
  div(v-else) {{ value }}
</template>

<script lang="ts" setup>
import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'

const props = defineProps(generatedUiFieldBaseProps)
const { displayName, hasFieldName } = useGeneratedUiFieldBase(props)
</script>
