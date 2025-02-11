<template lang="pug">
//- Plain time container for playerLocal events where the possible event range is bigger than the event itself.
div(
  v-if="renderInfo.plainTimeContainerStyles"
  class="tw-pointer-events-none tw-absolute tw-rounded-lg"
  :style="renderInfo.plainTimeContainerStyles"
  )

//- Event container.
div(
  v-bind="$attrs"
  class="tw-absolute tw-flex tw-cursor-pointer tw-rounded-lg tw-border tw-border-neutral-50 tw-shadow tw-filter hover:tw-brightness-90 active:tw-brightness-75"
  :class="{ 'tw-ring tw-ring-blue-500 tw-ring-offset-1': selected }"
  :style="renderInfo.containerStyles"
  @click="$emit('click')"
  )
  //- Preview phase element.
  div(
    v-if="renderInfo.previewStyles"
    class="tw-relative tw-rounded-l-md tw-bg-neutral-500"
    :style="renderInfo.previewStyles"
    )
    //- Active phase overlay.
    div(
      v-if="renderInfo.currentPhase === 'preview'"
      class="tw-absolute tw-inset-0 tw-overflow-clip tw-rounded-md"
      )
      div(class="active-event-overlay tw-rounded-l-md")

  //- Main event element. This expands to fill all empty space.
  div(
    class="tw-relative tw-grow"
    :style="renderInfo.activePhasesStyles"
    )
    //- Active phase overlay.
    div(
      v-if="renderInfo.currentPhase === 'active'"
      class="tw-absolute tw-inset-0 tw-overflow-clip tw-rounded-md"
      )
      div(class="active-event-overlay")

  //- Review phase element.
  div(
    v-if="renderInfo.reviewStyles"
    class="tw-relative tw-rounded-r-md tw-bg-neutral-500"
    :style="renderInfo.reviewStyles"
    )
    //- Active phase overlay.
    div(
      v-if="renderInfo.currentPhase === 'review'"
      class="tw-absolute tw-inset-0 tw-overflow-clip tw-rounded-md"
      )
      div(class="active-event-overlay tw-rounded-r-md")

//- Event text container.
//- NOTE: This element seems off by a few pixels.
div(
  class="tw-pointer-events-none tw-absolute tw-flex tw-select-none tw-flex-col tw-content-center tw-justify-between tw-px-1.5 tw-text-xs"
  :style="renderInfo.textContainerStyles"
  )
  //- Event title.
  div(class="tw-truncate") {{ renderInfo.displayName }} #[span(v-if="renderInfo.eventState === 'draft'" class="tw-text-xs tw-italic") {{ renderInfo.eventState }}]

  //- Details row.
  div(
    v-if="renderInfo.isGroupExpanded"
    class="tw-flex tw-h-3 tw-items-center tw-justify-between tw-space-x-1 tw-overflow-hidden"
    )
    div(class="tw-flex tw-items-center tw-space-x-1 tw-pl-[1px]")
      //- Locked symbol.
      div(
        v-if="renderInfo.isLocked"
        class="tw-relative tw-bottom-[1px] tw-size-2"
        )
        //- Icon from Font Awesome.
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512">
          <path d="M144 144v48H304V144c0-44.2-35.8-80-80-80s-80 35.8-80 80zM80 192V144C80 64.5 144.5 0 224 0s144 64.5 144 144v48h16c35.3 0 64 28.7 64 64V448c0 35.3-28.7 64-64 64H64c-35.3 0-64-28.7-64-64V256c0-35.3 28.7-64 64-64H80z"/>
        </svg>
      //- Recurring symbol.
      div(
        v-if="renderInfo.isRecurring"
        class="tw-size-2.5"
        )
        //- Icon from Font Awesome.
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
          <path d="M0 224c0 17.7 14.3 32 32 32s32-14.3 32-32c0-53 43-96 96-96H320v32c0 12.9 7.8 24.6 19.8 29.6s25.7 2.2 34.9-6.9l64-64c12.5-12.5 12.5-32.8 0-45.3l-64-64c-9.2-9.2-22.9-11.9-34.9-6.9S320 19.1 320 32V64H160C71.6 64 0 135.6 0 224zm512 64c0-17.7-14.3-32-32-32s-32 14.3-32 32c0 53-43 96-96 96H192V352c0-12.9-7.8-24.6-19.8-29.6s-25.7-2.2-34.9 6.9l-64 64c-12.5 12.5-12.5 32.8 0 45.3l64 64c9.2 9.2 22.9 11.9 34.9 6.9s19.8-16.6 19.8-29.6V448H352c88.4 0 160-71.6 160-160z"/>
        </svg>
      //- Targeting symbol.
      div(
        v-if="renderInfo.isTargeted"
        class="tw-size-2"
        )
        //- Icon from Font Awesome.
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
          <path d="M448 256A192 192 0 1 0 64 256a192 192 0 1 0 384 0zM0 256a256 256 0 1 1 512 0A256 256 0 1 1 0 256zm256 80a80 80 0 1 0 0-160 80 80 0 1 0 0 160zm0-224a144 144 0 1 1 0 288 144 144 0 1 1 0-288zM224 256a32 32 0 1 1 64 0 32 32 0 1 1 -64 0z"/>
        </svg>

    div(class="tw-text-xs")
      //- Participant count.
      div(
        v-if="renderInfo.participantCount"
        class="tw-flex tw-items-center tw-space-x-1"
        )
        //- Participant count icon.
        div(class="tw-relative tw-bottom-[1px] tw-size-2")
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512">
            <path d="M224 256A128 128 0 1 0 224 0a128 128 0 1 0 0 256zm-45.7 48C79.8 304 0 383.8 0 482.3C0 498.7 13.3 512 29.7 512H418.3c16.4 0 29.7-13.3 29.7-29.7C448 383.8 368.2 304 269.7 304H178.3z"/>
          </svg>
        //- Participant count.
        MAbbreviateNumber(
          :number="renderInfo.participantCount"
          disableTooltip
          )
</template>

<script setup lang="ts">
import MAbbreviateNumber from '../../composites/MAbbreviateNumber.vue'
import type { TimelineItemRenderInfoLiveopsEvent } from './MEventTimelineItemLiveopsEventTypes'

defineProps<{
  renderInfo: TimelineItemRenderInfoLiveopsEvent
  selected?: boolean
}>()

defineOptions({
  inheritAttrs: false,
})
</script>
