<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTooltip(
  :content="tooltipContent"
  no-underline
  )
  //- Container.
  label(
    v-bind="api.getRootProps()"
    class="tw-mb-0"
    :data-testid="`${dataTestid}-label`"
    )
    //- Hidden input for forms.
    input(
      v-bind="api.getHiddenInputProps()"
      :data-testid="`${dataTestid}-switch-input`"
      )

    //- Switch body.
    span(
      v-bind="api.getControlProps()"
      :class="['tw-relative tw-inline-flex tw-flex-shrink-0 tw-rounded-full tw-border-2 tw-border-transparent tw-transition-colors tw-duration-200 tw-ease-in-out focus:tw-outline-none focus:tw-ring-2 focus:tw-ring-indigo-500 focus:tw-ring-offset-2', variantClasses, bodySizeClasses, { 'tw-cursor-pointer': !internalDisabled, 'tw-cursor-not-allowed': internalDisabled }]"
      :data-testid="`${dataTestid}-switch-control`"
      )
      //- Switch thumb.
      span(
        v-bind="api.getThumbProps()"
        :class="['tw-pointer-events-none tw-inline-block tw-transform tw-rounded-full tw-bg-white tw-shadow tw-ring-0 tw-transition tw-duration-200 tw-ease-in-out', thumbSizeClasses, { 'tw-bg-neutral-300': internalDisabled }]"
        )

    //- Screen reader label.
    span(
      v-bind="api.getLabelProps()"
      class="tw-sr-only"
      )
      span {{ api.checked ? 'On' : 'Off' }}
</template>

<script setup lang="ts">
import { computed } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as zagSwitch from '@zag-js/switch'
import type { Context } from '@zag-js/switch'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import { usePermissions } from '../composables/usePermissions'
import MTooltip from '../primitives/MTooltip.vue'

const props = withDefaults(
  defineProps<{
    /**
     * The current value of the switch.
     */
    modelValue: boolean
    /**
     * Optional: Disable the switch. Defaults to false.
     */
    disabled?: boolean
    /**
     * Optional: The visual variant of the switch. Defaults to 'primary'.
     */
    variant?: 'primary' | 'success' | 'warning' | 'danger'
    /**
     * Optional: The size of the switch. Defaults to 'default'.
     */
    size?: 'extraSmall' | 'small' | 'default'
    /**
     * Optional: The name of the switch for HTML form submission. Defaults to `undefined`.
     */
    name?: string
    /**
     * Optional: The permission required to use this switch. If the user does not have this permission the switch will be disabled.
     */
    permission?: string
    /**
     * Optional: The data-testid attribute to be applied to the switch.
     */
    dataTestid?: string
  }>(),
  {
    disabled: false,
    variant: 'primary',
    size: 'default',
    name: undefined,
    permission: undefined,
    dataTestid: undefined,
  }
)

const { doesHavePermission } = usePermissions()

/**
 * Disabled state of the switch based on the permission and the disabled prop.
 */
const { internalDisabled } = useEnableAfterSsr(
  computed(() => (props.permission ? !doesHavePermission(props.permission) : props.disabled))
)

/**
 * The tooltip content to show when the switch is disabled.
 */
const tooltipContent = computed(() => {
  if (props.permission && !doesHavePermission(props.permission)) {
    return `You need the '${props.permission}' permission to use this feature.`
  }
  return undefined
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

// Zag Switch ---------------------------------------------------------------------------------------------------------
/**
 * Context values to be passed to the state machine.
 */
const transientContext = computed(
  (): Partial<Context> => ({
    disabled: internalDisabled.value,
    name: props.name,
    checked: props.modelValue,
  })
)

/**
 * The state machine for the switch.
 */
const [state, send] = useMachine(
  zagSwitch.machine({
    id: makeIntoUniqueKey('switch'),
    onCheckedChange: ({ checked }) => {
      emit('update:modelValue', checked)
    },
  }),
  {
    context: transientContext,
  }
)

/**
 * The API object that contains all the props, methods and event handlers to interact with the switch.
 */
const api = computed(() => zagSwitch.connect(state.value, send, normalizeProps))

// UI visuals ---------------------------------------------------------------------------------------------------------

/**
 * Variant class to be applied to the switch body.
 */
const variantClasses = computed(() => {
  const variants = {
    primary: {
      enabled: 'tw-bg-blue-500',
      disabled: 'tw-bg-blue-200',
    },
    success: {
      enabled: 'tw-bg-green-500',
      disabled: 'tw-bg-green-200',
    },
    warning: {
      enabled: 'tw-bg-orange-500',
      disabled: 'tw-bg-orange-200',
    },
    danger: {
      enabled: 'tw-bg-red-500',
      disabled: 'tw-bg-red-200',
    },
  }

  const variant = variants[props.variant]
  if (internalDisabled.value) {
    return api.value.checked ? variant.disabled : 'tw-bg-neutral-200'
  } else {
    return api.value.checked ? variant.enabled : 'tw-bg-neutral-300'
  }
})

/**
 * Size classes to be applied to the switch body.
 */
const bodySizeClasses = computed(() => {
  if (props.size === 'extraSmall') return 'tw-w-6 tw-h-3'
  else if (props.size === 'small') return 'tw-w-8 tw-h-4.5'
  else return 'tw-w-11 tw-h-6'
})

/**
 * Size classes to be applied to the switch thumb.
 */
const thumbSizeClasses = computed(() => {
  const thumbSizes = {
    extraSmall: { size: 'tw-w-2 tw-h-2', translate: 'tw-translate-x-3' },
    small: { size: 'tw-w-3.5 tw-h-3.5', translate: 'tw-translate-x-3.5' },
    default: { size: 'tw-w-5 tw-h-5', translate: 'tw-translate-x-5' },
  }
  if (api.value.checked) {
    return `${thumbSizes[props.size].size} ${thumbSizes[props.size].translate}`
  } else {
    return `${thumbSizes[props.size].size} tw-transalate-x-0`
  }
})
</script>
