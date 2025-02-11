<template lang="pug">
//- Immutable or locked state.
div(
  v-if="isLocked"
  class="tw-flex tw-items-center tw-space-x-0.5 tw-pr-1 tw-opacity-0 tw-transition-opacity group-hover:tw-opacity-100"
  )
  //- Lock icon.
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512" class="tw-size-3 tw-fill-neutral-300">
    <!-- Font Awesome Free 6.7.1 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license/free Copyright 2024 Fonticons, Inc. -->
    <path d="M144 144l0 48 160 0 0-48c0-44.2-35.8-80-80-80s-80 35.8-80 80zM80 192l0-48C80 64.5 144.5 0 224 0s144 64.5 144 144l0 48 16 0c35.3 0 64 28.7 64 64l0 192c0 35.3-28.7 64-64 64L64 512c-35.3 0-64-28.7-64-64L0 256c0-35.3 28.7-64 64-64l16 0z"/>
  </svg>

  //- Inspect button.
  MIconButton(
    v-if="allowInspect !== false"
    :enabledTooltip="`Inspect ${inspectTargetName}.`"
    :aria-label="`Inspect ${inspectTargetName}.`"
    permission="api.liveops_events.view"
    :disabledTooltip="typeof allowInspect === 'string' ? allowInspect : undefined"
    variant="neutral"
    @click="() => (isSelected ? $emit('close') : $emit('itemInspected', id))"
    )
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" fill="currentColor" class="tw-size-3">
      <!-- Font Awesome Free 6.7.1 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license/free Copyright 2024 Fonticons, Inc. -->
      <path d="M495.9 166.6c3.2 8.7 .5 18.4-6.4 24.6l-43.3 39.4c1.1 8.3 1.7 16.8 1.7 25.4s-.6 17.1-1.7 25.4l43.3 39.4c6.9 6.2 9.6 15.9 6.4 24.6c-4.4 11.9-9.7 23.3-15.8 34.3l-4.7 8.1c-6.6 11-14 21.4-22.1 31.2c-5.9 7.2-15.7 9.6-24.5 6.8l-55.7-17.7c-13.4 10.3-28.2 18.9-44 25.4l-12.5 57.1c-2 9.1-9 16.3-18.2 17.8c-13.8 2.3-28 3.5-42.5 3.5s-28.7-1.2-42.5-3.5c-9.2-1.5-16.2-8.7-18.2-17.8l-12.5-57.1c-15.8-6.5-30.6-15.1-44-25.4L83.1 425.9c-8.8 2.8-18.6 .3-24.5-6.8c-8.1-9.8-15.5-20.2-22.1-31.2l-4.7-8.1c-6.1-11-11.4-22.4-15.8-34.3c-3.2-8.7-.5-18.4 6.4-24.6l43.3-39.4C64.6 273.1 64 264.6 64 256s.6-17.1 1.7-25.4L22.4 191.2c-6.9-6.2-9.6-15.9-6.4-24.6c4.4-11.9 9.7-23.3 15.8-34.3l4.7-8.1c6.6-11 14-21.4 22.1-31.2c5.9-7.2 15.7-9.6 24.5-6.8l55.7 17.7c13.4-10.3 28.2-18.9 44-25.4l12.5-57.1c2-9.1 9-16.3 18.2-17.8C227.3 1.2 241.5 0 256 0s28.7 1.2 42.5 3.5c9.2 1.5 16.2 8.7 18.2 17.8l12.5 57.1c15.8 6.5 30.6 15.1 44 25.4l55.7-17.7c8.8-2.8 18.6-.3 24.5 6.8c8.1 9.8 15.5 20.2 22.1 31.2l4.7 8.1c6.1 11 11.4 22.4 15.8 34.3zM256 336a80 80 0 1 0 0-160 80 80 0 1 0 0 160z"/>
    </svg>

//- Display (possibly disabled) buttons.
div(
  v-else-if="allowInspect !== false || allowAdd !== false || allowRemove !== false"
  class="tw-opacity-0 tw-transition-opacity group-hover:tw-opacity-100"
  )
  //- Add button.
  MIconButton(
    v-if="allowAdd !== false"
    :enabledTooltip="`Add ${addTargetName}.`"
    :aria-label="`Add ${addTargetName}.`"
    permission="api.liveops_events.edit"
    :disabledTooltip="typeof allowAdd === 'string' ? allowAdd : undefined"
    variant="neutral"
    @click="$emit('add', id)"
    )
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512" fill="currentColor" class="tw-size-3">
      <!-- Font Awesome Free 6.7.1 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license/free Copyright 2024 Fonticons, Inc. -->
      <path d="M256 80c0-17.7-14.3-32-32-32s-32 14.3-32 32l0 144L48 224c-17.7 0-32 14.3-32 32s14.3 32 32 32l144 0 0 144c0 17.7 14.3 32 32 32s32-14.3 32-32l0-144 144 0c17.7 0 32-14.3 32-32s-14.3-32-32-32l-144 0 0-144z"/>
    </svg>

  //- Remove button.
  MIconButton(
    v-if="allowRemove !== false"
    :enabledTooltip="`Remove ${removeTargetName}.`"
    :aria-label="`Remove ${removeTargetName}.`"
    permission="api.liveops_events.edit"
    :disabledTooltip="typeof allowRemove === 'string' ? allowRemove : undefined"
    variant="neutral"
    @click="() => $emit('remove', id)"
    )
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512" fill="currentColor" class="tw-size-3">
      <!-- Font Awesome Free 6.7.1 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license/free Copyright 2024 Fonticons, Inc. -->
      <path d="M135.2 17.7L128 32 32 32C14.3 32 0 46.3 0 64S14.3 96 32 96l384 0c17.7 0 32-14.3 32-32s-14.3-32-32-32l-96 0-7.2-14.3C307.4 6.8 296.3 0 284.2 0L163.8 0c-12.1 0-23.2 6.8-28.6 17.7zM416 128L32 128 53.2 467c1.6 25.3 22.6 45 47.9 45l245.8 0c25.3 0 46.3-19.7 47.9-45L416 128z"/>
    </svg>

  //- Inspect button.
  MIconButton(
    v-if="allowInspect !== false"
    :enabledTooltip="`Inspect ${inspectTargetName}.`"
    :aria-label="`Inspect ${inspectTargetName}.`"
    permission="api.liveops_events.view"
    :disabledTooltip="typeof allowInspect === 'string' ? allowInspect : undefined"
    variant="neutral"
    @click="() => (isSelected ? $emit('close') : $emit('itemInspected', id))"
    )
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" fill="currentColor" class="tw-size-3">
      <!-- Font Awesome Free 6.7.1 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license/free Copyright 2024 Fonticons, Inc. -->
      <path d="M495.9 166.6c3.2 8.7 .5 18.4-6.4 24.6l-43.3 39.4c1.1 8.3 1.7 16.8 1.7 25.4s-.6 17.1-1.7 25.4l43.3 39.4c6.9 6.2 9.6 15.9 6.4 24.6c-4.4 11.9-9.7 23.3-15.8 34.3l-4.7 8.1c-6.6 11-14 21.4-22.1 31.2c-5.9 7.2-15.7 9.6-24.5 6.8l-55.7-17.7c-13.4 10.3-28.2 18.9-44 25.4l-12.5 57.1c-2 9.1-9 16.3-18.2 17.8c-13.8 2.3-28 3.5-42.5 3.5s-28.7-1.2-42.5-3.5c-9.2-1.5-16.2-8.7-18.2-17.8l-12.5-57.1c-15.8-6.5-30.6-15.1-44-25.4L83.1 425.9c-8.8 2.8-18.6 .3-24.5-6.8c-8.1-9.8-15.5-20.2-22.1-31.2l-4.7-8.1c-6.1-11-11.4-22.4-15.8-34.3c-3.2-8.7-.5-18.4 6.4-24.6l43.3-39.4C64.6 273.1 64 264.6 64 256s.6-17.1 1.7-25.4L22.4 191.2c-6.9-6.2-9.6-15.9-6.4-24.6c4.4-11.9 9.7-23.3 15.8-34.3l4.7-8.1c6.6-11 14-21.4 22.1-31.2c5.9-7.2 15.7-9.6 24.5-6.8l55.7 17.7c13.4-10.3 28.2-18.9 44-25.4l12.5-57.1c2-9.1 9-16.3 18.2-17.8C227.3 1.2 241.5 0 256 0s28.7 1.2 42.5 3.5c9.2 1.5 16.2 8.7 18.2 17.8l12.5 57.1c15.8 6.5 30.6 15.1 44 25.4l55.7-17.7c8.8-2.8 18.6-.3 24.5 6.8c8.1 9.8 15.5 20.2 22.1 31.2l4.7 8.1c6.1 11 11.4 22.4 15.8 34.3zM256 336a80 80 0 1 0 0-160 80 80 0 1 0 0 160z"/>
    </svg>
</template>

<script setup lang="ts">
import MIconButton from '../../primitives/MIconButton.vue'

const emit = defineEmits({
  itemInspected: (id: string) => ({ type: 'itemInspected', id }),
  close: () => ({ type: 'close' as const }),
  add: (id: string) => ({ type: 'add', id }),
  remove: (id: string) => ({ type: 'remove', id }),
})

const props = withDefaults(
  defineProps<{
    id: string
    allowInspect?: boolean | string
    allowAdd?: boolean | string
    allowRemove?: boolean | string
    inspectTargetName?: string
    addTargetName?: string
    removeTargetName?: string
    isSelected: boolean
    isLocked: boolean
  }>(),
  {
    allowInspect: false,
    allowAdd: false,
    allowRemove: false,
    inspectTargetName: 'item',
    addTargetName: 'item',
    removeTargetName: 'item',
  }
)
</script>
