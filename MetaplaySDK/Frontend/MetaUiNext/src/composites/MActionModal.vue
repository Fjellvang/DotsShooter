<template lang="pug">
//- TODO: Transition and responsive design.
//- Teleporting dialog to body is necessary to avoid inheriting styles from the parent component.
Teleport(to="body")
  Transition(name="backdrop-fade")
    div(
      v-if="api.open"
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
        class="tw-relative tw-p-1 @md:tw-px-5"
        :data-testid="dataTestid"
        )
        //- TODO: Why does the size not animate?
        div(
          v-bind="api.getContentProps()"
          :class="modalSizeClasses"
          class="tw-mx-auto tw-overflow-x-hidden tw-overflow-y-visible tw-rounded-lg tw-bg-white tw-p-4 tw-shadow-xl tw-transition-transform tw-duration-1000 tw-@container @md:tw-mt-24 @md:tw-w-full @md:tw-px-5 @md:tw-pt-3.5"
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
            span
              <!-- @slot Optional: Sub-title for the modal. -->
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

          //- Loading state.
          div(
            v-if="visualState === 'loading'"
            class="tw-my-14 tw-flex tw-animate-bounce tw-justify-center tw-italic tw-text-neutral-500"
            ) Loading, please hold...

          //- Error state.
          div(v-else-if="visualState === 'error'")
            p Oh snap, something went wrong while trying to perform the action. Here's what we know:
            MErrorCallout(
              v-if="okPromiseError"
              :error="okPromiseError"
              class="tw-my-4"
              )
            div(class="tw-flex tw-justify-end")
              MButton(
                variant="neutral"
                @click="close"
                ) Close

          //- Success state.
          div(v-else-if="visualState === 'success'")
            <!-- @slot Optional: Content shown after a successful action (HTML/components supported). -->
            slot(name="result-panel")
              p(class="tw-my-14 tw-flex tw-justify-center tw-italic tw-text-neutral-500") Success!

            div(
              v-if="$slots['result-panel']"
              class="tw-mt-4 tw-flex tw-justify-end"
              )
              MButton(
                v-bind="$attrs"
                variant="neutral"
                @click="close"
                :data-testid="dataTestid ? dataTestid + '-close' : undefined"
                ) Close

          //- Default state.
          div(v-else)
            //- Body.
            div(class="tw-mb-4 tw-gap-8 tw-space-y-8 @4xl:tw-flex @4xl:tw-space-y-0")
              //- Left panel.
              div(class="tw-overflow-x-auto @4xl:tw-flex-1")
                <!-- @slot Default: Main content displayed in the modal body (HTML/components supported). -->
                slot Default modal content goes here...

              //- Right panel (optional).
              div(
                v-if="$slots['right-panel']"
                class="tw-overflow-x-auto @4xl:tw-flex-1"
                )
                <!-- @slot Optional: Right-side content for a two-column modal (HTML/components supported). -->
                slot(name="right-panel")

            //- Bottom panel (optional).
            div(
              v-if="$slots['bottom-panel']"
              class="tw-overflow-x-auto tw-pb-3 @4xl:tw-flex-1"
              )
              <!-- @slot Optional: Additional content area for a three-column modal (HTML/components supported). -->
              slot(name="bottom-panel")

            div(class="-tw-mx-10 tw-mb-2 tw-border-b tw-border-neutral-200")

            //- Buttons.
            MButtonGroupLayout
              MButton(
                :variant="variant"
                :disabled-tooltip="okButtonDisabledTooltip"
                :safetyLock="!disableSafetyLock"
                @click="ok"
                :data-testid="dataTestid ? dataTestid + '-ok-button' : undefined"
                )
                template(
                  v-if="$slots['ok-button-icon']"
                  #icon
                  )
                  span(class="tw-mr-1")
                    <!-- @slot Optional: Icon for the OK button (HTML/components supported). -->
                    slot(name="ok-button-icon")
                template(#default) {{ okButtonLabel }}

              MButton(
                variant="neutral"
                @click="cancel"
                :data-testid="dataTestid ? dataTestid + '-cancel-button' : undefined"
                ) Cancel
</template>

<script setup lang="ts">
import { computed, Teleport, ref, useSlots } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as dialog from '@zag-js/dialog'
import { normalizeProps, useMachine } from '@zag-js/vue'

import MErrorCallout from '../composites/MErrorCallout.vue'
import MButtonGroupLayout from '../layouts/MButtonGroupLayout.vue'
import MButton from '../primitives/MButton.vue'
import type { Variant } from '../utils/types'

const props = withDefaults(
  defineProps<{
    /**
     * The title of the modal.
     */
    title: string
    /**
     * The action to perform when the user clicks the OK button. A loading screen is shown while the async action is
     * pending. If the action throws an error, the error is shown instead.
     */
    action: () => Promise<void>
    /**
     * Optional: The visual variant of the modal. Currently only affects the color of the OK button. Defaults to "primary".
     */
    variant?: Variant
    /**
     * Optional: The label of the OK button. Defaults to "Ok".
     */
    okButtonLabel?: string
    /**
     * Optional: Disable the ok-button and show a tooltip.
     * @example 'Please fill out all required fields to proceed.'
     */
    okButtonDisabledTooltip?: string
    /**
     * Optional: 'Remove' the safety lock from the OK button. Defaults to false.
     */
    disableSafetyLock?: boolean
    /**
     * Optional: Set a custom size for the modal. Defaults to 'default'.
     * @example 'large'
     */
    modalSize?: 'default' | 'large'
    /**
     * Optional: Unique Id to apply to the modal.
     * This is useful for testing, as it allows you to easily find the component and related children in the DOM.
     * Note: Test ids for child elements are generated by appending '-ok' or '-cancel' to the testId.
     * @example 'simple-modal'. Child element test IDs would be'simple-modal-ok' and 'simple-modal-cancel'
     */
    dataTestid?: string
    /**
     * Optional: Disable the 'Ok' button. Good for form validation.
     * Set this prop to `true` to disable the ok-button or use a `string` to disable it and display a tooltip.
     * @example true // Disables the ok-button.
     * @deprecated This prop was removed in Release 31. Use `okButtonDisabledTooltip` instead.
     */
    okButtonDisabled?: never
  }>(),
  {
    variant: 'primary',
    okButtonLabel: 'Ok',
    okButtonDisabledTooltip: undefined,
    okButtonDisabled: undefined,
    disableSafetyLock: false,
    modalSize: 'default',
    dataTestid: undefined,
  }
)

const okPromiseTriggered = ref(false)

const visualState = computed(() => {
  if (okPromisePending.value) {
    return 'loading'
  } else if (okPromiseError.value) {
    return 'error'
  } else if (okPromiseTriggered.value) {
    return 'success'
  } else {
    return 'default'
  }
})

// Visibility controls ------------------------------------------------------------------------------------------------

const emit = defineEmits(['ok', 'cancel', 'show', 'hide'])

function open(): void {
  okPromisePending.value = false
  okPromiseError.value = undefined
  okPromiseTriggered.value = false
  api.value.setOpen(true)
  emit('show')
}
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

// Actions ------------------------------------------------------------------------------------------------------------

const okPromisePending = ref(false)
const okPromiseError = ref<Error>()
async function ok(): Promise<void> {
  okPromisePending.value = true
  try {
    okPromiseTriggered.value = true
    await props.action()
    okPromisePending.value = false
    emit('ok')
    if (!slots['result-panel']) {
      close()
    }
  } catch (error) {
    okPromisePending.value = false
    okPromiseError.value = error as Error
    console.error(error)
  }
}

function cancel(): void {
  emit('cancel')
  close()
}

// Zag ----------------------------------------------------------------------------------------------------------------

const [state, send] = useMachine(
  dialog.machine({
    id: makeIntoUniqueKey('modal'),
    closeOnInteractOutside: false,
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
