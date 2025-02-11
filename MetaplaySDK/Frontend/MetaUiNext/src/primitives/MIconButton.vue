<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTooltip(
  :content="tooltipContent"
  no-underline
  )
  component(
    v-bind="$attrs"
    is="button"
    type="button"
    :class="['tw-inline tw-cursor-pointer tw-rounded-full tw-transition-transform', iconVariantClasses, iconSizeClasses]"
    :disabled="isDisabled"
    :aria-label="ariaLabel"
    @click.stop="$emit('click')"
    )
    <!-- @slot Default: Slot to add the icon HTML/component. -->
    slot
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import { doesHavePermission } from '../composables/usePermissions'
import MTooltip from '../primitives/MTooltip.vue'
import type { Variant } from '../utils/types'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * Optional: Set the visual variant of the button. Defaults to 'primary'.
     */
    variant?: Variant
    /**
     * Optional: A tooltip to show when the button is enabled.
     */
    enabledTooltip?: string
    /**
     * Optional: Disable the button and show a tooltip with the given text.
     */
    disabledTooltip?: string
    /**
     * Optional: The permission required to use this button. If the user does not have this permission the button will be disabled with a tooltip.
     */
    permission?: string
    /**
     * Optional: Set the size of the icon button. Defaults to 'default'.
     */
    size?: 'default' | 'small'
    /**
     * Optional: Text explaining the functionality and purpose of the icon button.
     * When provided, the aria-label attribute will serve as the accessible name for the button when read by screen readers.
     * @example aria-label="Remove this member."
     */
    ariaLabel?: string
    /**
     * Deprecated: Use `disabledTooltip` instead.
     * @deprecated This prop was removed in Release 30. Use `disabledTooltip` instead.
     */
    disabled?: never
  }>(),
  {
    variant: 'primary',
    enabledTooltip: undefined,
    disabledTooltip: undefined,
    permission: undefined,
    ariaLabel: undefined,
    size: 'default',
    disabled: undefined,
  }
)

/**
 * Prevents disabled buttons from flashing as enabled during the mounting of the component.
 */
const { internalDisabled } = useEnableAfterSsr(computed(() => !!props.disabledTooltip))

const emit = defineEmits(['click'])

/**
 * The tooltip content to show when the button is disabled.
 */
const tooltipContent = computed(() => {
  if (props.disabledTooltip) return props.disabledTooltip
  if (props.permission && !hasGotPermission.value) {
    return `You need the '${props.permission}' permission to use this feature.`
  }
  return props.enabledTooltip
})

/**
 * Whether the button is disabled.
 */
const isDisabled = computed(() => {
  if (internalDisabled.value) return true
  if (props.permission && !hasGotPermission.value) return true
  return false
})

/**
 * Whether the user has the required permission to use this button.
 */
const hasGotPermission = computed(() => {
  return doesHavePermission(props.permission)
})

/**
 * The classes to apply to the button based on the variant.
 */
const iconVariantClasses = computed(() => {
  const disabledVariants = {
    neutral: 'tw-text-neutral-300 hover:tw-bg-neutral-100 !tw-cursor-not-allowed',
    success: 'tw-text-green-300 hover:tw-bg-neutral-100 !tw-cursor-not-allowed',
    warning: 'tw-text-orange-300 hover:tw-bg-neutral-100 !tw-cursor-not-allowed',
    danger: 'tw-text-red-30 hover:tw-bg-neutral-100 !tw-cursor-not-allowed',
    primary: 'tw-text-blue-300 hover:tw-bg-neutral-100 !tw-cursor-not-allowed',
  }

  const variants = {
    neutral:
      'tw-text-neutral-500 hover:tw-text-neutral-600 active:tw-text-neutral-700 hover:tw-bg-neutral-100 active:tw-bg-neutral-200 focus:tw-ring-2 focus:tw-ring-neutral-400',
    success:
      'tw-text-green-500 hover:tw-text-green-600 active:tw-text-green-700 hover:tw-bg-green-100 active:tw-bg-green-200 focus:tw-ring-2 focus:tw-ring-green-400',
    warning:
      'tw-text-orange-500 hover:tw-text-orange-600 active:tw-text-orange-700 hover:tw-bg-orange-100 active:tw-bg-orange-200 focus:tw-ring-2 focus:tw-ring-orange-400',
    danger:
      'tw-text-red-500 hover:tw-text-red-600 active:tw-text-red-700 hover:tw-bg-red-100 active:tw-bg-red-200 focus:tw-ring-2 focus:tw-ring-red-400',
    primary:
      'tw-text-blue-500 hover:tw-text-blue-600 active:tw-text-blue-700 hover:tw-bg-blue-100 active:tw-bg-blue-200 focus:tw-ring-2 focus:tw-ring-blue-400',
  }
  return isDisabled.value ? disabledVariants[props.variant] : variants[props.variant]
})
const iconSizeClasses = computed(() => {
  const classes: Record<string, string> = {
    default: 'tw-px-1',
    small: 'tw-p-0.5',
  }
  return classes[props.size]
})
</script>
