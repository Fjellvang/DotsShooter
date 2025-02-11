// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { onMounted, ref, computed, type Ref, type ComputedRef } from 'vue'

/**
 * Utility composable to keep the input disabled until the component has mounted. This fixes issues with SSR + slow hydration on poor connections.
 * @param disabled Whether the component should be disabled after SSR.
 */
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export function useEnableAfterSsr(disabled: ComputedRef<boolean> | Ref<boolean>) {
  /**
   * Has the component mounted yet.
   */
  const hasMounted = ref(false)

  /**
   * This hook gets called on the client after hydration but before the component is mounted.
   */
  onMounted(() => {
    hasMounted.value = true
  })

  /**
   * Is the component disabled. If the component has not mounted yet, it will be disabled regardless of the value of `disabled`.
   */
  const internalDisabled = computed(() => (!hasMounted.value ? true : disabled.value))

  return {
    /**
     * Is the component disabled. If the component has not mounted yet, it will be disabled regardless of the value of `disabled`.
     */
    internalDisabled,
  }
}
