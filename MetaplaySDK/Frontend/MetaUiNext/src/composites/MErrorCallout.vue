<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A simple component to visualize the standard error message format in an alert. -->

<template lang="pug">
MCallout(
  :title="displayError.title"
  variant="danger"
  data-testid="error-callout"
  )
  template(
    v-if="displayError?.badgeText"
    #badge
    )
    MBadge(variant="danger") {{ displayError.badgeText }}

  //- Error message. Should ideally never be empty or very long. Something immediately actionable and useful. Max height of 110px.
  div(class="tw-max-h-[110px] tw-overflow-y-auto")
    p(class="tw-mb-2") {{ displayError.message }}

  //- Full error details for people that really care.
  MList(
    v-if="displayError?.details && displayError?.details.length > 0"
    variant="danger"
    showBorder
    )
    div(v-for="detail in displayError.details")
      MCollapse(
        extraMListItemMargin
        variant="danger"
        )
        template(#header)
          MListItem(noLeftPadding)
            span(v-if="detail.title") {{ detail.title }}
            //- TODO: Removed for now, pending future redesign.
            // template(#bottom-left)
              span(class="tw-font-mono tw-text-red-900") {{ getPreviewString(detail.content) }}

        //- Content
        div(
          v-if="detail.content"
          class="tw-max-h-80 tw-overflow-y-auto tw-rounded tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-font-mono tw-text-xs tw-text-neutral-600"
          )
          pre(class="tw-mb-0") {{ getDisplayString(detail.content) }}

  div(
    v-else
    class="tw-ml-0.5 tw-mt-2 tw-font-mono tw-text-xs tw-text-red-600"
    ) No details available.

  template(#buttons)
    <!-- @slot Optional: A scoped slot for custom buttons (HTML/components supported). -->
    slot(name="buttons")
    MClipboardCopy(
      fullSize
      :contents="JSON.stringify(displayError, null, 2)"
      class="tw-ml-2"
      ) Copy to Clipboard
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import MBadge from '../primitives/MBadge.vue'
import MCallout from '../primitives/MCallout.vue'
import MCollapse from '../primitives/MCollapse.vue'
import MList from '../primitives/MList.vue'
import MListItem from '../primitives/MListItem.vue'
import MClipboardCopy from '../unstable/MClipboardCopy.vue'
import { createDisplayError, DisplayError } from '../utils/DisplayErrorHandler'

const props = defineProps<{
  /**
   * The error object that we are interested in.
   */
  error: Error | DisplayError
}>()

/**
 * The error that is to be display.
 * If the error is in the correct format it will be displayed as is.
 * Otherwise we map it to 'DisplayError' format.
 */
const displayError = computed(() => {
  if (props.error instanceof Error) {
    return createDisplayError(props.error)
  } else if (props.error instanceof DisplayError) {
    return props.error
  } else {
    // console.error('ErrorCallout: Invalid error format', JSON.stringify(props.error, null, 2))
    return createDisplayError(new Error(JSON.stringify(props.error, null, 2)))
  }
})

/**
 * Transforms the raw error into a string format for display.
 * If the error is an array or object, it joins the elements into a single string.
 */
function getDisplayString(rawError: string | string[] | Record<string, any>): string {
  if (typeof rawError === 'string') return rawError
  if (Array.isArray(rawError)) return rawError.join('\n')
  return Object.entries(rawError)
    .map((row) => {
      const key = row[0]
      const rawValue = row[1] as unknown
      if (rawValue !== undefined) {
        return `${key}: ${JSON.stringify(rawValue)}`
      } else {
        return `${key}: undefined`
      }
    })
    .join('\n')
}

// TODO: Review design for preview text.
/**
 * Returns a preview of the error message, truncating it to 100 characters if it exceeds this length.
function getPreviewString (rawError: string | string[] | { [key: string]: any }): string {
  const displayString = getDisplayString(rawError)

  if (displayString.length > 100) {
    return displayString.slice(0, 100) + '...'
  } else {
    return displayString
  }
} */
</script>
