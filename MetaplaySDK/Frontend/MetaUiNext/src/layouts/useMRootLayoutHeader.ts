// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { type Ref, type ComputedRef, ref, watch } from 'vue'

const title = ref('Loading...')

/**
 * A utility function that sets a watcher to dynamically change the title of the headerbar.
 * @param dynamicObject Source object of the dynamic title.
 * @param titleGetter Function that returns the title from the source object.
 */
function setDynamicTitle<T>(
  dynamicObject: Ref<T> | ComputedRef<T>,
  titleGetter: (dynamicObject: Ref<T> | ComputedRef<T>) => string
): void {
  watch(
    dynamicObject,
    (newValue) => {
      if (newValue) title.value = titleGetter(dynamicObject)
    },
    { immediate: true }
  )
}

const rightBadgeLabel = ref<string>()
const rightAvatarImageUrl = ref<string>()

/**
 * A Vue composition function to access and update the headerbar title.
 */
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export function useHeaderbar() {
  return {
    title,
    setDynamicTitle,
    rightBadgeLabel,
    rightAvatarImageUrl,
  }
}
