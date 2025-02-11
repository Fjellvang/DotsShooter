<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTooltip(
  :content="internalDisabledTooltip"
  trigger-html-element="div"
  :class="pointerClasses"
  no-underline
  )
  li(
    v-bind="$attrs"
    role="link"
    :class="['tw-py-2.5 tw-px-5 tw-flex tw-space-x-1', conditionalClasses]"
    :disabled="internalDisabled"
    @click="onClick"
    )
    div(
      v-if="$slots.icon"
      class="tw-h-6 tw-w-6 tw-shrink-0"
      )
      slot(name="icon")
    div {{ label }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { doesHavePermission } from '../composables/usePermissions'
import MTooltip from '../primitives/MTooltip.vue'

defineOptions({
  inheritAttrs: false,
})

const props = defineProps<{
  /**
   * The text label of the link. Keep these as short as possible.
   * @example 'Players'
   */
  label: string
  /**
   * Optional: If this string matches any part of the current url, the link will be highlighted.
   * @example '/players'
   */
  activePathFragment?: string
  /**
   * Optional: If the current url is exactly this string, the link will be highlighted.
   * @example '/'
   */
  activeExactPath?: string
  /**
   * Optional: The permission required to view this link. If the user does not have this permission, the link will be disabled.
   * @example 'api.players.view'
   */
  permission?: string
  /**
   * Optional: Disable the link. Defaults to false.
   */
  disabled?: boolean
  /**
   * Optional: Disables the link and shows a tooltip with this text. Defaults to undefined.
   */
  disabledTooltip?: string
}>()

const emit = defineEmits(['click'])

const route = import.meta.env.STORYBOOK ? undefined : useRoute()

const active = computed(() => {
  if (!route) return false // This makes storybook work without a router.

  if (props.activeExactPath === route.fullPath) return true
  if (!props.activePathFragment) return false
  return route.fullPath.includes(props.activePathFragment)
})

const pointerClasses = computed(() => {
  if (internalDisabled.value) {
    return 'tw-cursor-not-allowed'
  } else if (doesHavePermission(props.permission)) {
    return 'tw-cursor-pointer'
  } else {
    return undefined
  }
})

const conditionalClasses = computed(() => {
  const classes = []

  if (internalDisabled.value) {
    // Disabled state.
    classes.push('tw-bg-neutral-100 tw-text-neutral-400 tw-pointer-events-none')
  } else if (doesHavePermission(props.permission)) {
    // Enabled state.
    classes.push('tw-text-neutral-700')

    if (active.value) {
      classes.push('tw-bg-blue-500 tw-text-white')
    } else {
      classes.push('hover:tw-bg-neutral-100 active:tw-bg-neutral-200')
    }
  }

  return classes.join(' ')
})

const internalDisabledTooltip = computed(() => {
  if (props.disabledTooltip) return props.disabledTooltip
  if (!doesHavePermission(props.permission)) {
    return `You need the '${props.permission}' permission to view this page.`
  }
  return undefined
})

const internalDisabled = computed(() => {
  if (props.disabled) return true
  if (internalDisabledTooltip.value) return true
  return false
})

function onClick(): void {
  emit('click')
}
</script>
