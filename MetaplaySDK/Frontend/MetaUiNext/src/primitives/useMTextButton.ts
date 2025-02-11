import { computed, type ComputedRef, type Ref } from 'vue'
import { useRouter } from 'vue-router'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import { doesHavePermission } from '../composables/usePermissions'
import type { Variant } from '../utils/types'

interface MTextButtonProps {
  /**
   * Optional: The route to navigate to when the button is clicked.
   */
  to?: string
  /**
   * Optional: Disable the button and show a tooltip with the given text.
   */
  disabledTooltip?: string
  /**
   * Set the visual variant of the text button.
   */
  variant: Variant
  /**
   * Optional: The permission required to use this button. If the user does not have this permission the button will be disabled with a tooltip.
   */
  permission?: string
}

interface MTextButtonAttributes {
  is: 'button' | 'a' | 'router-link' | 'nuxt-link'
  href?: string
  to?: string
  target?: '_blank'
  disabled?: boolean
  class?: string | Record<string, boolean>
  tooltipContent?: string
}

export function useMTextButton(
  props: Ref<MTextButtonProps> | ComputedRef<MTextButtonProps>
): ComputedRef<MTextButtonAttributes> {
  const router = import.meta.env.STORYBOOK ? undefined : useRouter()

  /**
   * Figure out what type of link, if any, the button's target is.
   */
  const linkTargetType = computed(() => {
    if (isDisabled.value) return undefined
    if (props.value.to?.startsWith('http') === true) return 'external'
    if (props.value.to !== undefined && router) return 'internal'
    return undefined
  })

  /**
   * The URL to navigate to when the button is clicked. This is also previewed when hovering over the button.
   * If the button is disabled, the link will be `undefined`.
   */
  const linkUrl = computed(() => {
    if (isDisabled.value) return undefined
    // Return the external site url.
    if (props.value.to?.startsWith('http')) return props.value.to
    // Resolve an internal route path into a valid URL.
    if (props.value.to && router) return router.resolve(props.value.to).href
    return undefined
  })

  /**
   * Tooltip content to be shown when the button is disabled.
   */
  const tooltipContent = computed(() => {
    if (props.value.disabledTooltip) return props.value.disabledTooltip
    if (props.value.permission && !hasGotPermission.value) {
      return `You need the '${props.value.permission}' permission to use this feature.`
    }
    return undefined
  })

  /**
   * Prevents disabled buttons from flashing as enabled on first render.
   */
  const { internalDisabled } = useEnableAfterSsr(computed(() => !!props.value.disabledTooltip))

  /**
   * Whether the button is disabled.
   */
  const isDisabled = computed(() => {
    if (internalDisabled.value) return true
    if (props.value.permission && !hasGotPermission.value) return true
    return false
  })

  /**
   * Whether the user has the required permission to use this button.
   */
  const hasGotPermission = computed(() => {
    return doesHavePermission(props.value.permission)
  })

  /**
   * The classes to apply to the button based on its variant and disabled state.
   */
  const textVariantClasses = computed(() => {
    const variants: Record<string, string> = {
      neutral:
        'tw-text-neutral-500 hover:tw-text-neutral-600 active:tw-text-neutral-800 focus:tw-ring-neutral-400 tw-cursor-pointer hover:tw-underline',
      success:
        'tw-text-green-500 hover:tw-text-green-600 active:tw-text-green-800 focus:tw-ring-green-400 tw-cursor-pointer hover:tw-underline',
      warning:
        'tw-text-orange-400 hover:tw-text-orange-500 active:tw-text-orange-600 focus:tw-ring-orange-400 tw-cursor-pointer hover:tw-underline',
      danger:
        'tw-text-red-400 hover:tw-text-red-500 active:tw-text-red-600 focus:tw-ring-red-400 tw-cursor-pointer hover:tw-underline',
      primary:
        'tw-text-blue-500 hover:tw-text-blue-600 active:tw-text-blue-800 focus:tw-ring-blue-400 tw-cursor-pointer hover:tw-underline',
    }

    return isDisabled.value ? 'tw-text-neutral-400 tw-cursor-not-allowed' : variants[props.value.variant]
  })

  return computed((): MTextButtonAttributes => {
    if (linkTargetType.value === 'external') {
      return {
        is: 'a',
        href: linkUrl.value,
        target: '_blank',
        class: textVariantClasses.value,
        disabled: isDisabled.value,
        tooltipContent: tooltipContent.value,
      }
    } else if (linkTargetType.value === 'internal') {
      return {
        is: 'router-link',
        to: props.value.to,
        class: textVariantClasses.value,
        disabled: isDisabled.value,
        tooltipContent: tooltipContent.value,
      }
    } else {
      return {
        is: 'button',
        class: textVariantClasses.value,
        disabled: isDisabled.value,
        tooltipContent: tooltipContent.value,
      }
    }
  })
}
