<template lang="pug">
div(
  ref="inspectorRef"
  class="tw-shrink-0 tw-basis-80 tw-overflow-y-scroll tw-rounded-br-md tw-border-l tw-border-neutral-300 tw-bg-white tw-shadow-[rgba(0,0,0,0.1)_-1px_0px_3px_0px]"
  :class="!isEmpty(selectedItemAndDetails) ? 'tw-block' : 'tw-hidden'"
  )
  //- Single event inspector.
  div(v-if="Object.keys(selectedItemAndDetails).length === 1")
    //- Loading.
    //- Note: Make this a skeleton loader later.
    div(
      v-if="!singleSelectedItem?.details"
      class="tw-h-32 tw-content-center tw-text-center"
      )
      div(class="tw-animate-bounce tw-italic tw-text-neutral-500") Loading...

    span(v-else)
      MEventTimelineInspectorSingle(
        v-if="!singleSelectedItem.data"
        title="Deleted Item"
        description="This item has been deleted."
        :id="singleSelectedItem.id"
        :version="0"
        @close="$emit('close')"
        )
      MEventTimelineItemSectionInspectorSingle(
        v-else-if="singleSelectedItem.details.itemType === 'section'"
        :id="singleSelectedItem.id"
        :version="singleSelectedItem.data.version"
        :visible-timeline-data="visibleTimelineData"
        :raw-details="singleSelectedItem.details"
        @close="$emit('close')"
        @invoke-command="(command) => emit('invokeCommand', command)"
        )
      MEventTimelineItemGroupInspectorSingle(
        v-else-if="singleSelectedItem.details.itemType === 'group'"
        :id="singleSelectedItem.id"
        :version="singleSelectedItem.data.version"
        :visible-timeline-data="visibleTimelineData"
        :raw-details="singleSelectedItem.details"
        @close="$emit('close')"
        @invoke-command="(command) => emit('invokeCommand', command)"
        )
      MEventTimelineItemRowInspectorSingle(
        v-else-if="singleSelectedItem.details.itemType === 'row'"
        :id="singleSelectedItem.id"
        :version="singleSelectedItem.data.version"
        :visible-timeline-data="visibleTimelineData"
        :is-immutable="!!singleSelectedItem.data.isImmutable"
        :raw-details="singleSelectedItem.details"
        @close="$emit('close')"
        @invoke-command="(command) => emit('invokeCommand', command)"
        )
      MEventTimelineItemLiveopsEventInspectorSingle(
        v-else-if="singleSelectedItem.details.itemType === 'liveopsEvent'"
        :id="singleSelectedItem.id"
        :version="singleSelectedItem.data.version"
        :visible-timeline-data="visibleTimelineData"
        :raw-details="singleSelectedItem.details"
        @close="$emit('close')"
        @invoke-command="(command) => emit('invokeCommand', command)"
        )
      MEventTimelineItemInstantEventInspectorSingle(
        v-else-if="singleSelectedItem.details.itemType === 'instantEvent'"
        :id="singleSelectedItem.id"
        :version="singleSelectedItem.data.version"
        :raw-details="singleSelectedItem.details"
        @close="$emit('close')"
        @invoke-command="(command) => emit('invokeCommand', command)"
        )
      div(v-else) No inspector defined..

    //- div(v-else)
      //- Header row.
      div(class="tw-items-top tw-flex tw-justify-between tw-space-x-1 tw-px-4 tw-pt-4")
        //- Event title.
        div(class="tw-flex tw-grow")
          h2(class="tw-font-bold") {{ singleSelectedItem.details?.displayName }}

          //- Event state (for LiveOps events).
          MBadge(
            v-if="singleSelectedItem.details.itemType === 'liveopsEvent'"
            class="tw-ml-1 tw-shrink-0"
            :variant="singleSelectedItem.data.renderData?.state === 'scheduled' ? 'primary' : singleSelectedItem.data.renderData?.state === 'active' ? 'success' : 'neutral'"
            ) {{ singleSelectedItem.data.renderData?.state }}

        //- Close button.
        button(
          class="tw-relative -tw-top-0.5 tw-inline-flex tw-h-7 tw-w-7 tw-shrink-0 tw-items-center tw-justify-center tw-rounded tw-font-semibold hover:tw-bg-neutral-100 active:tw-bg-neutral-200"
          @click="$emit('close')"
          )
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="tw-w-6 tw-h-6">
            <path fill-rule="evenodd" d="M5.47 5.47a.75.75 0 011.06 0L12 10.94l5.47-5.47a.75.75 0 111.06 1.06L13.06 12l5.47 5.47a.75.75 0 11-1.06 1.06L12 13.06l-5.47 5.47a.75.75 0 01-1.06-1.06L10.94 12 5.47 6.53a.75.75 0 010-1.06z" clip-rule="evenodd" />
          </svg>

      //- Description.
      div(class="tw-px-4 tw-text-sm tw-italic tw-text-neutral-500") {{ singleSelectedItem.details.description ?? 'No description.' }}

      //- Event details.
      div(
        v-if="singleSelectedItem"
        class="tw-space-y-3 tw-px-4 tw-pb-4"
        )
        //- div(class="tw-flex tw-flex-row-reverse tw-items-center tw-justify-between")
          //- Lock toggle.
          span(class="tw-inline-flex tw-items-center tw-space-x-1")
            span(class="tw-mr-1 tw-text-sm tw-italic tw-text-neutral-600") {{ singleSelectedItem.data.isImmutable ? 'Immutable' : singleSelectedItem.data.isLocked ? 'Locked' : 'Unlocked' }}
            //- TODO: This modify server state instead.
            MInputSwitch(
              v-if="!singleSelectedItem.data.isImmutable"
              v-model="singleSelectedItem.data.isLocked"
              class="tw-relative tw-top-[2px]"
              size="small"
              )

        //- Participant count for liveops events.
        //- div(
          v-if="singleSelectedItem.details.itemType === 'liveopsEvent' && singleSelectedItem.data.participantCount"
          class="tw-flex tw-items-center tw-space-x-1 tw-text-sm tw-text-neutral-600"
          )
          div(class="tw-relative tw-bottom-[1px] tw-size-2.5 tw-fill-neutral-600")
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512">
              <path d="M224 256A128 128 0 1 0 224 0a128 128 0 1 0 0 256zm-45.7 48C79.8 304 0 383.8 0 482.3C0 498.7 13.3 512 29.7 512H418.3c16.4 0 29.7-13.3 29.7-29.7C448 383.8 368.2 304 269.7 304H178.3z"/>
            </svg>

          MAbbreviateNumber(
            :number="singleSelectedItem.data.participantCount ?? 0"
            unit="participant"
            )

        //- Event schedule (for LiveOps events).
        //- div(
          v-if="singleSelectedItem.timelineItemType === 'liveopsEvent' && singleSelectedItem.data.schedule"
          class="tw-rounded-md tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-text-sm"
          )
          div(class="tw-mb-1 tw-mr-1 tw-font-bold") Schedule
            span(class="tw-ml-1 tw-text-xs tw-font-normal tw-text-neutral-600") {{ singleSelectedItem.data.schedule?.timeMode === 'utc' ? 'UTC' : 'Player local' }}

          ul
            li(class="tw-flex tw-justify-between")
              div Preview
              div TBD
              //div {{ singleSelectedItem.details.schedule.preview.toHuman() }}
            li(class="tw-flex tw-justify-between")
              div Event start
              div TBD
              //div {{ singleSelectedItem.details.schedule.start.toFormat('yyyy-MM-dd HH:mm') }}
            li(class="tw-flex tw-justify-between")
              div Ending soon
              div TBD
              //div {{ singleSelectedItem.details.schedule.endingSoon.toHuman() }}
            li(class="tw-flex tw-justify-between")
              div Event end
              div TBD
              //div {{ singleSelectedItem.details.schedule.end.toFormat('yyyy-MM-dd HH:mm') }}
            li(class="tw-flex tw-justify-between")
              div Review
              div TBD
              //div {{ singleSelectedItem.details.schedule.review.toHuman() }}

            //p(class="tw-text-xs tw-text-neutral-600 tw-mt-1") Active for {{ singleSelectedItem.details.schedule.end.diff(singleSelectedItem.details.schedule.start, 'hours').toHuman() }}.
            p(class="tw-text-xs tw-text-neutral-600") Visible for {{ getTotalVisibleDurationForEvent(singleSelectedItem) }} total.

        //- Event targeting.

        //- Event parameters.
        //- For LiveOps events.
        //- div(
          v-if="singleSelectedItem.timelineItemType === 'liveopsEvent' && singleSelectedItem.details.parameters"
          class="tw-text-sm"
          )
          div(class="tw-mr-1 tw-font-bold") Parameters
          ul(class="tw-list-inside tw-list-disc")
            li(v-for="(value, key) in singleSelectedItem.details.parameters") #[span(class="tw-font-bold") {{ key }}]: {{ value }}
        //- For Instant events.
        //- div(
          v-else-if="singleSelectedItem.timelineItemType === 'instantEvent' && singleSelectedItem.details.rawPayload"
          class="tw-text-sm"
          )
          div(class="tw-mr-1 tw-font-bold") Parameters
          ul(class="tw-list-inside tw-list-disc")
            li(v-for="(value, key) in singleSelectedItem.details.rawPayload") #[span(class="tw-font-bold") {{ key }}]: {{ value }}

        //- Event actions (for LiveOps events).
        //- div(
          v-if="singleSelectedItem.timelineItemType === 'liveopsEvent'"
          class="tw-flex tw-justify-end tw-space-x-2 tw-pt-4"
          )
          MButton(
            :disabled-tooltip="singleSelectedItem.data.state === 'concluded' ? 'Cannot edit concluded events.' : singleSelectedItem.data.isLocked ? 'Event editing is locked.' : undefined"
            ) Edit
          MButton Duplicate
          MButton(
            variant="warning"
            :disabled-tooltip="singleSelectedItem.data.isLocked ? 'Event editing is locked.' : undefined"
            ) Conclude

  //- Multiple events inspector.
  div(v-else)
    div TBD

  //- Debug box.
  MCollapse(class="tw-mb-2 tw-mt-4 tw-text-neutral-500")
    template(#header)
      div(class="tw-text-sm") Raw Data
    pre(class="tw-overflow-x-auto tw-rounded tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-text-xs")
      div {{ selectedItemAndDetails }}
</template>

<script setup lang="ts">
import { isEmpty } from 'lodash-es'
import { onMounted, ref, computed } from 'vue'

import MCollapse from '../../primitives/MCollapse.vue'
import MEventTimelineInspectorSingle from './MEventTimelineInspectorSingle.vue'
import MEventTimelineItemGroupInspectorSingle from './MEventTimelineItemGroupInspectorSingle.vue'
import MEventTimelineItemInstantEventInspectorSingle from './MEventTimelineItemInstantEventInspectorSingle.vue'
import MEventTimelineItemLiveopsEventInspectorSingle from './MEventTimelineItemLiveopsEventInspectorSingle.vue'
import MEventTimelineItemRowInspectorSingle from './MEventTimelineItemRowInspectorSingle.vue'
import MEventTimelineItemSectionInspectorSingle from './MEventTimelineItemSectionInspectorSingle.vue'
import type { TimelineData, TimelineItem, TimelineItemDetails } from './MEventTimelineTypes'
import type { ToServerCommand } from './timelineCommands'

const emit = defineEmits({
  close: () => ({ type: 'close' as const }),
  invokeCommand: (command: ToServerCommand) => ({ type: 'invokeCommand', command }),
})

// TODO: Shouldn't be here..
const props = defineProps<{
  /**
   * Currently selected items.
   */
  selectedItemAndDetails: Record<string, { data: TimelineItem; details: TimelineItemDetails | undefined }>
  visibleTimelineData?: TimelineData
}>()

const inspectorRef = ref<HTMLElement>()

onMounted(() => {
  if (!inspectorRef.value) return

  // Create a new mutation observer to track class changes on the inspector root. This means it opens or closes.
  const observer = new MutationObserver(() => {
    // Dispatch a browser resize event so the timeline knows to re-layout.
    window.dispatchEvent(new Event('resize'))
  })

  observer.observe(inspectorRef.value, {
    attributes: true,
    attributeFilter: ['class'],
  })
})

const singleSelectedItem = computed(
  (): { id: string; data: TimelineItem; details: TimelineItemDetails | undefined } | undefined => {
    const selectedItemAndDetails = Object.entries(props.selectedItemAndDetails)
    return selectedItemAndDetails.length === 1
      ? {
          id: selectedItemAndDetails[0][0],
          data: selectedItemAndDetails[0][1].data,
          details: selectedItemAndDetails[0][1].details,
        }
      : undefined
  }
)

/*
function getTotalVisibleDurationForEvent(event: TimelineItemDetailsLiveopsEvent): string {
  if (!event.data.timelinePosition.startInstantIsoString) {
    return 'manually controlled'
  }

  if (!event.data.timelinePosition.endInstantIsoString) {
    return 'manually controlled'
  }

  const start = DateTime.fromISO(event.data.timelinePosition.startInstantIsoString)
  const end = DateTime.fromISO(event.data.timelinePosition.endInstantIsoString)

  return end.diff(start, 'hours').normalize().toHuman()
}
*/

// Hotkey listeners ---------------------------------------------------------------------------------------------------

// Register a global event listener for keys.
onMounted(() => {
  document.addEventListener('keydown', (event) => {
    // Close the inspector when the user presses the escape key.
    if (event.key === 'Escape') {
      emit('close')
    }
  })
})
</script>
