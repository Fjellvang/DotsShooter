<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- TODO: Transition and responsive design.
//- Teleporting dialog to body is necessary to avoid inheriting styles from the parent component.
Teleport(to="body")
  Transition(name="backdrop-fade")
    div(
      v-bind="api.getBackdropProps()"
      class="tw-pointer-events-none tw-fixed tw-inset-0"
      )
  Transition(name="modal-fade")
    div(
      v-if="api.open"
      class="tw-pointer-events-auto tw-fixed tw-inset-0 tw-overflow-y-auto tw-@container"
      )
      div(
        v-bind="api.getPositionerProps()"
        class="tw-relative tw-p-1 sm:tw-px-5"
        :data-testid="dataTestid"
        )
        //- TODO: Why does the size not animate?
        div(
          v-bind="api.getContentProps()"
          :class="modalSizeClasses"
          class="tw-mx-auto tw-overflow-x-hidden tw-overflow-y-visible tw-rounded-lg tw-bg-white tw-p-4 tw-shadow-xl tw-transition-transform tw-duration-1000 tw-@container sm:tw-mt-24 sm:tw-w-full sm:tw-px-5 sm:tw-pt-3.5"
          )
          //- Header.
          div(
            v-bind="api.getTitleProps()"
            class="tw-mb-2 tw-flex tw-justify-between"
            )
            span(
              role="heading"
              class="tw-overflow-hidden tw-overflow-ellipsis tw-font-bold tw-text-neutral-900"
              ) {{ title }}
              //- Badge.
              MBadge(
                v-if="badge"
                class="tw-ml-1"
                style="bottom: 1px"
                :variant="badgeVariant"
                shape="pill"
                :data-testid="`${dataTestid}-badge`"
                ) {{ badge }}

            <!-- @slot Optional: Sub-title of the modal. -->
            slot(name="modal-subtitle")

            button(
              class="tw-relative -tw-top-0.5 tw-inline-flex tw-h-7 tw-w-7 tw-shrink-0 tw-items-center tw-justify-center tw-rounded tw-font-semibold hover:tw-bg-neutral-100 active:tw-bg-neutral-200"
              @click="close"
              )
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="tw-w-6 tw-h-6">
                <path fill-rule="evenodd" d="M5.47 5.47a.75.75 0 011.06 0L12 10.94l5.47-5.47a.75.75 0 111.06 1.06L13.06 12l5.47 5.47a.75.75 0 11-1.06 1.06L12 13.06l-5.47 5.47a.75.75 0 01-1.06-1.06L10.94 12 5.47 6.53a.75.75 0 010-1.06z" clip-rule="evenodd" />
              </svg>

          //- Divider.
          div(class="-tw-mx-10 tw-mb-3 tw-border-b tw-border-neutral-200")

          //- Modal content.
          div
            //- Body.
            div(class="tw-mb-4 sm:tw-flex sm:tw-space-x-8")
              //- Left panel.
              div(class="tw-overflow-x-auto sm:tw-flex-1")
                <!-- @slot Default: Main modal content (HTML/components supported). -->
                slot Default modal content goes here...

              //- Right panel (optional).
              div(
                v-if="$slots['right-panel']"
                class="tw-overflow-x-auto sm:tw-flex-1"
                )
                <!-- @slot Optional: Right-side content for a large two column modal (HTML/components supported). -->
                slot(name="right-panel")

            div(class="-tw-mx-10 tw-mb-2 tw-border-b tw-border-neutral-200")

            //- Buttons.
            MButtonGroupLayout
              //- Custom buttons (optional).
              <!-- @slot Optional: Scoped slot for custom buttons (HTML/components supported). -->
              slot(name="buttons")

              //- Close button.
              MButton(
                :variant="variant"
                @click="close"
                :data-testid="dataTestid ? dataTestid + '-close' : undefined"
                ) {{ closeButtonLabel }}
</template>

<script setup lang="ts">
import { computed, Teleport, useSlots } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as dialog from '@zag-js/dialog'
import { normalizeProps, useMachine } from '@zag-js/vue'

import MButtonGroupLayout from '../layouts/MButtonGroupLayout.vue'
import MBadge from '../primitives/MBadge.vue'
import MButton from '../primitives/MButton.vue'
import type { Variant } from '../utils/types'

const props = withDefaults(
  defineProps<{
    /**
     * The title of the modal.
     */
    title: string
    /**
     * Optional: The badge to display next to the title.
     */
    badge?: string
    /**
     * Optional: The visual variant of the badge. Defaults to "neutral".
     */
    badgeVariant?: Variant
    /**
     * Optional: Label for the close button. Defaults to "Close".
     */
    closeButtonLabel?: string
    /**
     * Optional: The visual variant of the modal. Currently only affects the color of the OK button. Defaults to "primary".
     */
    variant?: Variant
    /**
     * Optional: Set a custom size for the modal. Defaults to 'default'.
     * @example 'large'
     */
    modalSize?: 'default' | 'large'
    /**
     * Optional: Whether the modal can be dismissed by clicking outside of it. Defaults to false.
     */
    dismissable?: boolean
    /**
     * Optional: Unique Id to apply to the modal.
     * This is useful for testing, as it allows you to easily find the component and related children in the DOM.
     * Note: Test ids for child elements are generated by appending '-close' to the testId.
     * @example 'simple-modal'. Child element test IDs would be'simple-modal-close'
     */
    dataTestid?: string
  }>(),
  {
    badge: undefined,
    badgeVariant: 'neutral',
    variant: 'neutral',
    closeButtonLabel: 'Close',
    modalSize: 'default',
    dismissable: false,
    dataTestid: undefined,
  }
)

// Visibility controls ------------------------------------------------------------------------------------------------
const emit = defineEmits(['show', 'hide'])

/**
 * Opens the modal.
 */
function open(): void {
  api.value.setOpen(true)
  emit('show')
}

/**
 * Closes the modal.
 */
function close(): void {
  api.value.setOpen(false)
}

defineExpose({
  open,
  close,
})

// Styles -------------------------------------------------------------------------------------------------------------
const slots = useSlots()

const modalSizeClasses = computed(() => {
  const classes = {
    default: 'tw-max-w-lg',
    large: 'tw-max-w-5xl',
  }

  if (slots['right-panel']) {
    return classes.large
  } else {
    return classes[props.modalSize]
  }
})

// Zag ----------------------------------------------------------------------------------------------------------------

const [state, send] = useMachine(
  dialog.machine({
    id: makeIntoUniqueKey('modal'),
    closeOnInteractOutside: props.dismissable,
    onOpenChange: (details) => {
      if (!details.open) {
        emit('hide')
      }
    },
  })
)

const api = computed(() => dialog.connect(state.value, send, normalizeProps))
</script>

<style>
[data-part='backdrop'][data-state='open'] {
  background: rgba(107, 114, 128, 0.45);
}

[data-part='backdrop'] {
  background-color: rgb(0 0 0 / 0);
}

.modal-fade-enter-active,
.modal-fade-leave-active {
  transition:
    opacity 0.2s ease-out,
    transform 0.2s ease-out;
}

.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
  transform: translateY(-1rem);
}

.backdrop-fade-enter-active,
.backdrop-fade-leave-active {
  transition: opacity 0.2s;
}

.backdrop-fade-enter-from,
.backdrop-fade-leave-to {
  opacity: 0;
}
</style>
