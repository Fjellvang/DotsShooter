<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-flex tw-items-center tw-justify-between")
  LabelWithLogicVersion(
    v-if="hasFieldName"
    :is-header="false"
    :fieldInfo="fieldInfo"
    ) {{ displayName }}
  MBadge(:variant="value ? 'success' : 'danger'") {{ value }}
</template>

<script lang="ts" setup>
import { MBadge } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'

const props = defineProps(generatedUiFieldBaseProps)
const { displayName, hasFieldName } = useGeneratedUiFieldBase(props)
</script>
