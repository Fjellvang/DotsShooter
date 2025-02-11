<template lang="pug">
div(:class="['tw-rounded-lg tw-shadow tw-border tw-flex tw-flex-col tw-@container', containerVariantClasses]")
  //- Header container.
  div(
    :class="['tw-px-4 tw-pt-3 tw-pb-2 tw-rounded-t', { 'hover:tw-bg-neutral-200 active:tw-bg-neutral-300 tw-cursor-pointer': clickableHeader }]"
    @click="onHeaderClick"
    )
    //- Loading.
    div(v-if="isLoading")
      //- Title.
      span(
        role="heading"
        :class="['tw-font-bold tw-overflow-ellipsis', headerVariantClasses]"
        )
        span(data-testid="card-title") {{ title }}

    div(v-else)
      //- Header.
      div(:class="['sm:tw-flex tw-items-center tw-justify-between tw-space-x-2']")
        //- Left side of header.
        div(:class="['tw-flex tw-items-center tw-space-x-2 tw-my-1 tw-text-lg', headerVariantClasses]")
          //- Icon.
          <!-- @slot Optional: Slot to add an icon (HTML/components supported). -->
          slot(name="icon")

          //- Title.
          span(
            role="heading"
            :class="['tw-font-bold tw-overflow-ellipsis']"
            )
            span(data-testid="card-title") {{ title }}

            //- Badge.
            MBadge(
              v-if="badge !== undefined"
              class="tw-relative tw-ml-1"
              style="bottom: 1px"
              :variant="badgeVariant"
              shape="pill"
              data-testid="card-badge"
              ) {{ badge }}

        //- Right side of header.
        div(
          v-if="$slots['header-right']"
          class="tw-grow tw-text-right"
          style="min-width: 10rem; max-width: 40rem"
          )
          <!-- @slot Optional: Slot to add custom header content. -->
          slot(name="header-right")

      //- Subtitle.
      p(
        v-if="$slots.subtitle || subtitle"
        :class="['tw-text-sm tw-mb-2', subtitleVariantClasses]"
        data-testid="card-subtitle"
        )
        <!-- @slot Optional: Subtitle. -->
        slot(name="subtitle") {{ subtitle }}

  //- Body container.
  div(:class="['tw-grow', bodyVariantClasses, { 'tw-mt-1 tw-mb-5 tw-px-4': !noBodyPadding }]")
    //- Error state.
    MErrorCallout(
      v-if="error"
      :error="error"
      class="tw-shadow"
      )

    //- Loading state.
    div(
      v-else-if="isLoading"
      class="tw-animate-pulse"
      )
      div(:class="['tw-h-4 tw-w-9/12 tw-mb-2 tw-bg-neutral-200 tw-rounded', { 'tw-ml-4': noBodyPadding }]")
      div(:class="['tw-h-4 tw-w-7/12 tw-mb-2 tw-bg-neutral-200 tw-rounded', { 'tw-ml-4': noBodyPadding }]")
      div(:class="['tw-h-4 tw-w-8/12 tw-mb-2 tw-bg-neutral-200 tw-rounded', { 'tw-ml-4': noBodyPadding }]")

    //- Content.
    div(
      v-else
      class="tw-flex tw-h-full tw-flex-col tw-justify-between"
      )
      //- TODO: Use a child selector to add side margins to children instad of padding the container. This would make overflow look nicer.
      div(class="tw-grow tw-overflow-x-auto")
        <!-- @slot Default: The main card content to be displayed (HTML/components supported). -->
        slot

      //- Buttons.
      MButtonGroupLayout(
        v-if="$slots.buttons"
        class="tw-mt-3"
        :class="{ 'tw-px-4 tw-pb-2': noBodyPadding }"
        )
        <!-- @slot Optional: Slot to add custom buttons. -->
        slot(name="buttons")
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import MErrorCallout from '../composites/MErrorCallout.vue'
import MButtonGroupLayout from '../layouts/MButtonGroupLayout.vue'
import type { DisplayError } from '../utils/DisplayErrorHandler'
import type { Variant } from '../utils/types'
import MBadge from './MBadge.vue'

const props = withDefaults(
  defineProps<{
    /**
     * The title of the card.
     */
    title: string
    /**
     * Optional: Show a loading state.
     */
    isLoading?: boolean
    /**
     * Optional: Show an error. You can directly pass in the `error` property from subscriptions.
     */
    error?: Error | DisplayError
    /**
     * Optional: Content to show in a pill shaped badge on the right side of the title. Usually a number.
     */
    badge?: string | number
    /**
     * Optional: A subtitle to show below the title.
     */
    subtitle?: string
    /**
     * Optional: The visual variant of the badge. Defaults to `primary`.
     */
    variant?: Variant
    /**
     * Optional: Set the visual style of the header badge. Defaults to `neutral`.
     */
    badgeVariant?: Variant
    /**
     * Optional: Set the header to have visual clickable feedback.
     */
    clickableHeader?: boolean
    /**
     * Optional: Remove the default padding from the body. Good for making cards where the content should go from edge to edge.
     */
    noBodyPadding?: boolean
  }>(),
  {
    variant: 'primary',
    badgeVariant: 'neutral',
    error: undefined,
    badge: undefined,
    subtitle: undefined,
  }
)

const emit = defineEmits(['headerClick'])

function onHeaderClick(): void {
  emit('headerClick')
}

const internalVariant = computed(() => {
  if (props.error) return 'danger'
  else return props.variant
})

const containerVariantClasses = computed(() => {
  const loadingClasses = 'tw-border-neutral-200 tw-bg-neutral-50'

  const classes = {
    primary: 'tw-border-neutral-200 tw-bg-white',
    neutral: 'tw-border-neutral-200 tw-bg-neutral-50',
    success: 'tw-border-green-200 tw-bg-green-100',
    warning: 'tw-border-orange-200 tw-bg-orange-100',
    danger: 'tw-border-red-200 tw-bg-red-200',
  }

  return props.isLoading ? loadingClasses : classes[internalVariant.value]
})

const headerVariantClasses = computed(() => {
  const loadingClasses = 'tw-text-neutral-300'

  const classes = {
    primary: 'tw-text-neutral-800',
    neutral: 'tw-text-neutral-500',
    success: 'tw-text-green-800',
    warning: 'tw-text-orange-800',
    danger: 'tw-text-red-800',
  }

  return props.isLoading ? loadingClasses : classes[internalVariant.value]
})

const subtitleVariantClasses = computed(() => {
  const classes = {
    primary: 'tw-text-neutral-500',
    neutral: 'tw-text-neutral-400',
    success: 'tw-text-green-500',
    warning: 'tw-text-orange-500',
    danger: 'tw-text-red-700',
  }

  return classes[internalVariant.value]
})

const bodyVariantClasses = computed(() => {
  const classes = {
    primary: 'tw-text-neutral-900',
    neutral: 'tw-text-neutral-500',
    success: 'tw-text-green-900',
    warning: 'tw-text-orange-900',
    danger: 'tw-text-red-900',
  }

  return classes[internalVariant.value]
})
</script>
