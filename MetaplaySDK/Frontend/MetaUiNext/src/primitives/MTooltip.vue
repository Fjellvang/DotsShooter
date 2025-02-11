<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- https://zagjs.com/components/vue-sfc/tooltip -->

<template lang="pug">
template(v-if="$slots.content !== undefined || content !== undefined")
  //- Tooltip.
  Teleport(to="body")
    Transition
      //- Note: Bootstrap forces us to put in this z-index so this element appears on top of modals.
      div(
        v-if="api.open"
        v-bind="api.getPositionerProps()"
        class="tw-rounded-lg tw-border-neutral-600 tw-bg-neutral-800 tw-text-xs+ tw-font-normal tw-text-white tw-shadow-sm tw-transition-opacity"
        style="z-index: 2000"
        )
        //- Arrow.
        div(v-bind="api.getArrowProps()")
          div(
            v-bind="api.getArrowTipProps()"
            class="-tw-z-10 tw-border-l tw-border-t tw-border-neutral-600"
            )

        //- Content.
        div(
          v-bind="api.getContentProps()"
          class="tw-relative tw-z-20 tw-max-w-fit tw-whitespace-pre-wrap tw-px-2 tw-py-1.5 tw-text-center"
          style="max-width: 12.5rem"
          )
          slot(name="content")
            span {{ contentToDisplay }}

  //- Trigger.
  component(
    v-bind="{ ...$attrs, ...api.getTriggerProps() }"
    :class="[props.class, { 'tw-border-b tw-border-dashed tw-border-neutral-400': !noUnderline }]"
    :is="triggerHtmlElement ?? 'span'"
    )
    <!-- @slot Default: Content to display in the tooltip. -->
    slot

//- Fallback for when the tooltip is disabled. Attributes can´t be passed to a slot so we need this wrapper.
component(
  v-else
  :is="triggerHtmlElement ?? 'span'"
  )
  slot
</template>

<script lang="ts" setup>
import { computed, nextTick, watch } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as tooltip from '@zag-js/tooltip'
import { normalizeProps, useMachine } from '@zag-js/vue'

const props = defineProps<{
  /**
   * The content to display in the tooltip. You can also use the `content` slot. The tooltip's visibility is
   * controlled by the presence of content. When both the content prop and the `content` slot are undefined the tooltip
   * component will not render, but everything inside its child slot will render.
   * @example 'This is a tooltip.'
   */
  content?: string
  /**
   * Optional: Disable the underline on the trigger.
   */
  noUnderline?: boolean
  /**
   * Optional: The HTML element to use for the trigger. Defaults to `span`.
   * @example 'div'
   */
  triggerHtmlElement?: string
  /**
   * Optional: Classes to apply to the tooltip's trigger element. This prop is fixing a Vue bug where classes are not inherited correctly.
   */
  class?: string | Record<string, boolean>
}>()

const [state, send] = useMachine(
  tooltip.machine({
    id: makeIntoUniqueKey('tooltip'),
    openDelay: 100,
    closeDelay: 100,
    interactive: true,
    positioning: {
      placement: 'top',
    },
  })
)

const api = computed(() => {
  return tooltip.connect(state.value, send, normalizeProps)
})

const contentToDisplay = computed((): string | undefined => {
  // Truncate the content if it starts with a word that's over 25 characters.
  if (props.content && props.content.split(' ')[0].length > 25) {
    return props.content.split(' ')[0].slice(0, 25) + '...'
  }

  // Truncate the content if it's over 180 characters.
  if (props.content && props.content.length > 180) {
    return props.content.slice(0, 180) + '...'
  }

  return props.content
})

/**
 * Reposition the tooltip when the content changes.
 * This fixes a bug where the position of the tooltip can become incorrect when the content changes.
 */
watch(contentToDisplay, () => {
  // Reposition needs to be delayed until the next frame.
  void nextTick(() => {
    api.value.reposition({ placement: 'top' })
  })
})
</script>

<style scoped>
[data-part='arrow'] {
  --arrow-background: rgb(38 38 38);
  --arrow-size: 10px;
}

.v-enter-active,
.v-leave-active {
  transition: opacity 0.1s ease;
}

.v-enter-from,
.v-leave-to {
  opacity: 0;
}
</style>
