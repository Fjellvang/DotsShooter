<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  span(
    role="heading"
    :class="'tw-mb-1 tw-text-sm tw-leading-6' + (isHeader ? ' tw-font-bold' : '')"
    )
    <!-- @slot Default: Main callout content to be displayed (HTML/components supported). --> 
    slot
  MBadge(
    v-if="hasLogicVersionLabel"
    variant="warning"
    :tooltip="logicVersionMismatchDescription"
    class="tw-pl-1"
    ) {{ logicVersionLabel }}
</template>
<script lang="ts" setup>
import { computed, type PropType } from 'vue'

import { MBadge } from '@metaplay/meta-ui-next'

import type { IGeneratedUiFieldInfo } from '../generatedUiTypes'
import { hasPotentialLogicVersionMismatch } from '../generatedUiUtils'

const props = defineProps({
  fieldInfo: {
    type: Object as PropType<IGeneratedUiFieldInfo>, // Possibly null if current field is an array or a dictionary.

    default: () => ({}),
    required: true,
  },
  isHeader: {
    type: Boolean,
    default: true,
  },
  /**
   * Enabling this will show warnings if any of the members/types are only available in a subset of the player base.
   * This can happen if the SupportedLogicVersions is a range (e.g. 4..6) and members were added or removes in between these versions.
   */
  isTargetingMultiplePlayers: {
    type: Boolean,
    default: true,
  },
})

const hasLogicVersionLabel = computed(() => {
  // We only want to show the logic version label if we're targeting multiple players, otherwise the field is already disabled by the parent.
  if (!props.isTargetingMultiplePlayers) return false

  if (hasPotentialLogicVersionMismatch(props.fieldInfo)) {
    return true
  }

  return false
})

const logicVersionLabel = computed(() => {
  if (props.fieldInfo.addedInVersion !== undefined && props.fieldInfo.removedInVersion !== undefined) {
    return `${props.fieldInfo.addedInVersion} to ${props.fieldInfo.removedInVersion}`
  } else if (props.fieldInfo.removedInVersion !== undefined) {
    return `${props.fieldInfo.removedInVersion} and lower`
  } else if (props.fieldInfo.addedInVersion !== undefined) {
    return `${props.fieldInfo.addedInVersion} and higher`
  }

  return ''
})

const logicVersionMismatchDescription = computed(() => {
  if (props.isTargetingMultiplePlayers) {
    if (props.fieldInfo.addedInVersion === undefined || props.fieldInfo.addedInVersion == null) {
      return `${props.fieldInfo.fieldName} is only supported in logic versions up to ${props.fieldInfo.removedInVersion}. While the server supports a larger range, a subsection of your users might not support this feature.`
    } else if (props.fieldInfo.removedInVersion === undefined || props.fieldInfo.removedInVersion == null) {
      return `${props.fieldInfo.fieldName} is only supported in logic versions ${props.fieldInfo.addedInVersion} and higher. While the server supports a larger range, a subsection of your users might not support this feature.`
    }

    return `${props.fieldInfo.fieldName} is only supported in logic versions ${props.fieldInfo.addedInVersion} to ${props.fieldInfo.removedInVersion}. While the server supports a larger range, a subsection of your users might not support this feature.`
  } else {
    return `${props.fieldInfo.fieldName} is not available in this player's logic version.`
  }
})
</script>
