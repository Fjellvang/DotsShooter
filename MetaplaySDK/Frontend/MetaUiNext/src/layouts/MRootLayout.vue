<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div#root-content(class="tw-flex tw-h-dvh")
  //- Sidebar
  div(
    :class="['tw-basis-56 tw-flex tw-flex-col tw-shrink-0 tw-border-r-2 tw-border-neutral-200 tw-bg-white tw-transition-all tw-duration-300 tw-overflow-y-auto', { '-tw-mr-56 -tw-translate-x-56': !showSidebar }]"
    )
    //- Sidebar header
    div(class="tw-flex tw-shrink-0 tw-basis-14 tw-items-center tw-space-x-2 tw-px-4")
      slot(name="sidebar-header")
        div(class="tw-flex tw-h-10 tw-w-10 tw-items-center tw-justify-center tw-rounded-lg tw-bg-green-500")
          MetaplayMonogram(class="tw-w-6 tw-fill-white")
        span(
          role="heading"
          class="tw-text-xl tw-font-semibold"
          ) {{ projectName }}

    //- Sidebar content
    div(class="tw-flex tw-grow tw-flex-col tw-justify-between tw-space-y-2 tw-py-3")
      slot(
        name="sidebar"
        :closeSidebarOnNarrowScreens="closeSidebarOnNarrowScreens"
        )

      //- Bottom.
      div(class="tw-pt-6")
        MetaplayLogo(class="tw-mx-auto tw-mb-3 tw-h-9 tw-fill-neutral-200")

  //- Right side.
  div(class="tw-relative tw-z-0 tw-flex tw-grow tw-flex-col tw-overflow-hidden sm:tw-overflow-auto")
    //- Overlay on mobile.
    div(
      v-show="showSidebar"
      class="tw-absolute tw-inset-0 tw-z-50 tw-cursor-pointer tw-touch-none tw-bg-black tw-opacity-50 tw-transition-colors sm:tw-hidden"
      @click.stop="showSidebar = !showSidebar"
      )

    //- Header bar.
    div(
      class="tw-flex tw-shrink-0 tw-basis-14 tw-items-center tw-justify-between tw-space-x-3 tw-border-b-2 tw-border-neutral-200 tw-px-3 tw-shadow"
      :style="'min-width: 375px; background-color: ' + headerBackgroundColorString"
      )
      //- Left side.
      div(
        :class="['tw-flex tw-space-x-3 tw-min-w-0 tw-items-center', { 'tw-text-neutral-800': !headerLightTextColor, 'tw-text-neutral-50': headerLightTextColor }]"
        )
        //- Burger button.
        div(
          class="tw-relative tw-size-8 tw-shrink-0 tw-cursor-pointer tw-rounded hover:tw-bg-neutral-200 active:tw-bg-neutral-300"
          @click="showSidebar = !showSidebar"
          )
          button(
            v-show="showSidebar"
            title="sidebar"
            class="tw-absolute tw-inset-1.5 tw-flex"
            )
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke-width="2.4" :class="['tw-size-5', { 'tw-stroke-neutral-600': !headerLightTextColor, 'tw-stroke-neutral-50': headerLightTextColor}]">
              <path d="M21.97 15V9C21.97 4 19.97 2 14.97 2H8.96997C3.96997 2 1.96997 4 1.96997 9V15C1.96997 20 3.96997 22 8.96997 22H14.97C19.97 22 21.97 20 21.97 15Z" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M7.96997 2V22" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M14.97 9.43994L12.41 11.9999L14.97 14.5599"  stroke-linecap="round" stroke-linejoin="round"/>
            </svg>

          button(
            v-show="!showSidebar"
            title="sidebar"
            class="tw-absolute tw-inset-1.5 tw-flex"
            )
            <svg viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg" fill="none" stroke-width="2.4" class="tw-size-5 tw-stroke-neutral-600">
              <path d="M21.97 15V9C21.97 4 19.97 2 14.97 2H8.96997C3.96997 2 1.96997 4 1.96997 9V15C1.96997 20 3.96997 22 8.96997 22H14.97C19.97 22 21.97 20 21.97 15Z" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M14.97 2V22" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M7.96997 9.43994L10.53 11.9999L7.96997 14.5599" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>

        //- Title label.
        // TODO: How to limit this to not grow with content?
        div(
          role="heading"
          class="tw-min-w-0 tw-truncate tw-text-lg tw-font-semibold"
          data-testid="header-bar-title"
          ) {{ title }}

      //- Right side.
      div(class="tw-flex tw-shrink-0 tw-items-center tw-space-x-3")
        slot(name="header-right")
          //- User name.
          span(
            v-if="headerBadgeLabel"
            class="tw-hidden tw-cursor-pointer sm:tw-inline"
            @click="$emit('headerAvatarClick')"
            )
            MBadge(variant="neutral") {{ headerBadgeLabel }}

        //- User avatar.
        div(
          v-if="headerAvatarImageUrl || headerBadgeLabel"
          class="tw-flex tw-h-9 tw-w-9 tw-cursor-pointer tw-items-center tw-justify-center tw-rounded-full tw-border tw-border-neutral-200 tw-border-opacity-25 tw-bg-neutral-100 hover:tw-brightness-90 active:tw-brightness-75"
          role="link"
          @click="$emit('headerAvatarClick')"
          data-testid="header-avatar"
          )
          img(
            v-if="localHeaderAvatarImageUrl"
            :src="localHeaderAvatarImageUrl"
            class="tw-rounded-full"
            @error="onHeaderAvatarImageError"
            )
          div(v-else)
            <svg xmlns="http://www.w3.org/2000/svg" height="1em" viewBox="0 0 448 512" class="tw-fill-neutral-800">
              <!--! Font Awesome Free 6.4.2 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license (Commercial License) Copyright 2023 Fonticons, Inc. -->
              <path d="M224 256A128 128 0 1 0 224 0a128 128 0 1 0 0 256zm-45.7 48C79.8 304 0 383.8 0 482.3C0 498.7 13.3 512 29.7 512H418.3c16.4 0 29.7-13.3 29.7-29.7C448 383.8 368.2 304 269.7 304H178.3z"/>
            </svg>
    //- Content container.
    div(
      class="tw-relative tw-grow tw-overflow-scroll tw-bg-neutral-50 tw-@container"
      style="min-width: 375px"
      data-testid="page-content-container"
      )
      slot

//- Containers that can be used as teleport targets for modals, popovers, and tooltips.
div#root-modals
div#root-popovers
div#root-tooltips
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'

import MetaplayLogo from '../assets/MetaplayLogo.vue'
import MetaplayMonogram from '../assets/MetaplayMonogram.vue'
import MBadge from '../primitives/MBadge.vue'
import { useHeaderbar } from './useMRootLayoutHeader'

const props = withDefaults(
  defineProps<{
    /**
     * The name of the project to display in the sidebar header.
     */
    projectName?: string
    headerBadgeLabel?: string
    headerAvatarImageUrl?: string
    headerBackgroundColorString?: string
  }>(),
  {
    projectName: undefined,
    headerBadgeLabel: undefined,
    headerAvatarImageUrl: undefined,
    headerBackgroundColorString: '#FFFFFF',
  }
)

defineEmits(['headerAvatarClick'])

const { title } = useHeaderbar()

/**
 * Whether to show the sidebar or not. On narrow screens we hide it. If the URL has `showSidebar=false` then we also hide it.
 * In this case we also set the initial value to hidden so that it doesn't appear briefly before being hidden.
 */
const showSidebar = ref(!hideSidebarRequested())

/**
 * Look for `showSidebar=false` in the URL query string.
 */
function hideSidebarRequested(): boolean {
  const route = import.meta.env.STORYBOOK ? undefined : useRoute()
  const showSidebar = route?.query.showSidebar
  if (showSidebar !== null && showSidebar !== undefined && showSidebar.toString() === 'false') {
    return true
  } else {
    return false
  }
}

onMounted(() => {
  // Start with the sidebar closed on narrow browsers.
  // Note: It's important to do this in a mounted hook so that the SSR output does not break.
  showSidebar.value = Math.max(document.documentElement.clientWidth || 0, window.innerWidth || 0) > 768

  // If request has been made to hide the sidebar then hide it.
  if (hideSidebarRequested()) {
    showSidebar.value = false
  }
})

/**
 * Locally cached copy of the header avatar URL. Replaced with a placeholder if the image fails to load.
 */
const localHeaderAvatarImageUrl = ref<string>()

/**
 * Watch for changes to the header avatar URL and update the local copy.
 */
watch(
  () => props.headerAvatarImageUrl,
  (newVal) => {
    localHeaderAvatarImageUrl.value = newVal
  },
  { immediate: true }
)

/**
 * If the avatar image fails to load then replace it with a placeholder.
 */
function onHeaderAvatarImageError(): void {
  localHeaderAvatarImageUrl.value = undefined
}

function closeSidebarOnNarrowScreens(): void {
  if (Math.max(document.documentElement.clientWidth || 0, window.innerWidth || 0) < 768) {
    showSidebar.value = false
  }
}

const headerLightTextColor = computed(() => {
  // Return true if the header background color is dark enough to have better contrast with light text.
  // See https://stackoverflow.com/a/41491220/1243212
  if (!props.headerBackgroundColorString) {
    return false
  }
  const hex = props.headerBackgroundColorString.replace('#', '')
  const c = hex.length === 3 ? hex.split('').map((x) => x + x) : hex.match(/.{2}/g)
  if (!c) {
    return false
  }
  const r = parseInt(c[0], 16)
  const g = parseInt(c[1], 16)
  const b = parseInt(c[2], 16)
  const brightness = (r * 299 + g * 587 + b * 114) / 1000
  return brightness < 125
})
</script>
