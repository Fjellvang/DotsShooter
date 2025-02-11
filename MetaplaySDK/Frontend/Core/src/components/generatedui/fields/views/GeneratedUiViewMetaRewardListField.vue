<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="hasValue")
  meta-reward-badge(
    v-for="(reward, index) in value"
    :key="index"
    :reward="reward"
    :data-testid="`generated-ui-reward-badge-${index}`"
    )
div(
  v-else
  class="text-muted small tw-italic"
  ) No {{ displayName }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { generatedUiFieldBaseProps, useGeneratedUiFieldBase } from '../../generatedFieldBase'

const props = defineProps(generatedUiFieldBaseProps)
const { displayName } = useGeneratedUiFieldBase(props)

const hasValue = computed(() => {
  return props.value && Array.isArray(props.value) && props.value.length > 0
})
</script>
