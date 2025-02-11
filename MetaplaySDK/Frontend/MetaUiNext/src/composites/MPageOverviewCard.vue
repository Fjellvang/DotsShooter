<template lang="pug">
div(
  v-bind="$attrs"
  :class="['tw-relative tw-z-10 tw-rounded-lg tw-shadow tw-border tw-@container', containerVariantClasses]"
  )
  //- Header container.
  div(class="tw-px-4 tw-pb-2 tw-pt-3")
    //- Loading placeholder.
    div(v-if="isLoading")
      div(
        class="tw-my-3 tw-h-6 tw-w-6/12 tw-animate-pulse tw-rounded tw-bg-neutral-200"
        data-testid="overviewcard-loading-indicator"
        )
    div(v-else)
      //- Header.
      div(:class="['sm:tw-flex tw-items-center tw-justify-between tw-gap-2']")
        //- Left side of header.
        div(:class="['tw-grow tw-flex tw-items-center tw-space-x-2 tw-my-1 tw-text-2xl', headerVariantClasses]")
          //- TODO: Avatar.
          div(
            v-if="avatarImageUrl"
            class="tw-flex tw-h-9 tw-w-9 tw-cursor-pointer tw-items-center tw-justify-center tw-rounded-full tw-border tw-border-neutral-200 tw-border-opacity-25 tw-bg-neutral-100 hover:tw-brightness-90 active:tw-brightness-75"
            )
            img(
              :src="avatarImageUrl"
              class="tw-rounded-full"
              )

          //- Title.
          div(
            role="heading"
            :class="['tw-font-bold tw-overflow-ellipsis tw-basis-full', { 'tw-flex tw-items-center tw-gap-x-1': $slots.badge }]"
            data-testid="overviewcard-title"
            )
            <!-- @slot Optional: Slot to add custom title (HTML/component supported).-->
            slot(name="title") {{ title }}
              //- Badge
            span(class="tw-text-lg")
              <!-- @slot Optional: Slot to add a custom badge component -->
              slot(name="badge")

        //- ID.
        div(
          v-if="id"
          class="tw-flex tw-min-w-52 tw-items-center tw-justify-end tw-space-x-1 tw-text-right tw-text-neutral-500"
          )
          span {{ idLabel }}: {{ id }}
          MClipboardCopy(:contents="id")

      //- Subtitle.
      p(
        v-if="$slots.subtitle || subtitle"
        :class="['tw-text-sm tw-mb-2', subtitleVariantClasses]"
        data-testid="overviewcard-subtitle"
        )
        <!-- @slot Optional: Slot to add custom subtitle (HTML/component supported).-->
        slot(name="subtitle") {{ subtitle }}

  //- Body container.
  div(:class="['tw-mt-1 tw-mb-5 tw-px-4 tw-@container', bodyVariantClasses]")
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
      div(class="tw-mb-2 tw-h-4 tw-w-9/12 tw-rounded tw-bg-neutral-200")
      div(class="tw-mb-2 tw-h-4 tw-w-7/12 tw-rounded tw-bg-neutral-200")
      div(class="tw-mb-2 tw-h-4 tw-w-8/12 tw-rounded tw-bg-neutral-200")

    //- Content.
    div(v-else)
      <!-- @slot Default: Slot to add main content (HTML/component supported).-->
      slot

      div(
        v-if="alerts || $slots.alerts"
        class="tw-mt-4"
        )
        //- Programmatic alerts.
        div(v-if="alerts")
          MCallout(
            v-for="alert in alerts"
            :key="alert.key || alert.title"
            :title="alert.title"
            :variant="alert.variant"
            class="tw-mb-2"
            :data-testid="alert.dataTest"
            ) {{ alert.message }}
            span(
              v-if="alert.link && alert.linkText"
              class="tw-ml-1"
              ) #[MTextButton(:to="alert.link") {{ alert.linkText }}]!
        //- Manual alerts.
        div(v-if="$slots.alerts")
          <!-- @slot Optional: Slot to add custom alerts (HTML/component supported).-->
          slot(name="alerts")

      //- Buttons.
      MButtonGroupLayout(
        v-if="$slots.buttons"
        class="tw-mt-6"
        )
        <!-- @slot Optional: Slot to add custom buttons (HTML/component supported).-->
        slot(name="buttons")

div(
  v-if="$slots.caption"
  class="tw-mb-4 tw-mt-2 tw-w-full tw-text-right tw-text-xs tw-text-neutral-500"
  )
  <!-- @slot Optional: Slot to add custom caption (HTML/component supported).-->
  slot(name="caption")
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import MErrorCallout from '../composites/MErrorCallout.vue'
import MButtonGroupLayout from '../layouts/MButtonGroupLayout.vue'
import MCallout from '../primitives/MCallout.vue'
import MTextButton from '../primitives/MTextButton.vue'
import MClipboardCopy from '../unstable/MClipboardCopy.vue'
import type { DisplayError } from '../utils/DisplayErrorHandler'
import type { Variant } from '../utils/types'

export interface MPageOverviewCardAlert {
  /**
   * The title of the alert.
   */
  title: string
  /**
   * The message of the alert.
   */
  message: string
  /**
   * Optional: The visual variant of the alert. Defaults to `warning`.
   */
  variant?: Variant
  /**
   * Optional: The text of the link.
   */
  linkText?: string
  /**
   * Optional: A link to more information.
   */
  link?: string
  /**
   * Optional: A unique key for the alert.
   */
  key?: string
  /**
   * Optional: A data-test-id for the alert.
   */
  dataTest?: string
}

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
     * Optional: A subtitle to show below the title.
     */
    subtitle?: string
    /**
     * Optional: The visual variant of the badge. Defaults to `primary`.
     */
    variant?: Variant
    /**
     * Optional content for an avatar icon.
     * @example: http://placekitten.com/256/256
     */
    avatarImageUrl?: string
    /**
     * Optional: An ID string to be show on the card with a copy-to-clipboard button.
     * @example 'Player:ZArvpuPqNL'
     */
    id?: string
    /**
     * Optional: The label to prefix the ID string with. Defaults to `ID`.
     * @example 'Slug'
     * @example 'Resource Name'
     */
    idLabel?: string
    /**
     * Optional: Alerts to show in the card.
     */
    alerts?: MPageOverviewCardAlert[]
  }>(),
  {
    variant: 'primary',
    error: undefined,
    subtitle: undefined,
    avatarImageUrl: undefined,
    id: undefined,
    idLabel: 'ID',
    alerts: undefined,
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
    danger: 'tw-text-red-500',
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
