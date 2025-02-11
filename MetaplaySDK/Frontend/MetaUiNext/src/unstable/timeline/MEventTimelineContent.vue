<template lang="pug">
div(
  ref="timelineContainer"
  class="tw-grow tw-overflow-x-scroll"
  data-testid="timeline-content"
  )
  //- Timeline element that is bigger than the container to allow for horizontal scrolling.
  div(
    ref="timelineElement"
    :class="`tw-relative tw-h-full tw-overflow-x-hidden ${isTimelineMouseDragging ? 'tw-cursor-grabbing' : 'tw-cursor-grab'}`"
    :style="{ width: `${timelineDayWidthInRem * timelineLengthInDays}rem` }"
    @mousedown="isTimelineMouseDragging = true"
    )
    //- Timeline header with time units for the selected time scale.
    //- Add this to enable zooming: @wheel.prevent="onTimelineHeaderWheel"
    div(
      :class="`tw-relative tw-z-10 tw-flex tw-h-14 tw-select-none tw-border-b tw-border-neutral-300 tw-drop-shadow-sm  ${isTimelineMouseDragging ? 'tw-cursor-grabbing' : 'tw-cursor-grab'}`"
      )
      //- Days with hours.
      template(v-if="timelineTimeScale === 'hours' || timelineTimeScale === 'days'")
        div(
          v-for="index in timelineLengthInDays"
          class="tw-relative tw-text-center"
          :style="{ width: `${timelineDayWidthInRem}rem` }"
          )
          //- Overlay month name to top left corner if this is the first day of the month.
          div(
            v-if="timelineStartInstant.plus({ days: index - 1 }).day === 1"
            class="tw-absolute tw-left-0.5 tw-top-0 tw-text-xs tw-text-neutral-400"
            ) {{ timelineStartInstant.plus({ days: index - 1 }).toFormat('LLL') }}

          //- Overlay week number to top right corner if this is the first day of the week.
          div(
            v-if="timelineStartInstant.plus({ days: index - 1 }).weekday === 1"
            class="tw-absolute tw-right-0.5 tw-top-0.5 tw-size-4 tw-rounded-sm tw-bg-neutral-200 tw-text-xs tw-text-neutral-500"
            ) {{ timelineStartInstant.plus({ days: index - 1 }).weekNumber }}

          //- Day name
          div(class="tw-mt-2 tw-space-x-1")
            span(class="tw-text-xs tw-text-neutral-600") {{ timelineStartInstant.plus({ days: index - 1 }).toFormat('ccc') }}
            //- Day number
            span(class="tw-font-semibold") {{ timelineStartInstant.plus({ days: index - 1 }).toFormat('d') }}

          //- Every other hour name.
          div(
            v-if="timelineTimeScale === 'days'"
            class="tw-mt-1 tw-flex tw-justify-between tw-text-xs tw-text-neutral-600"
            )
            template(v-for="index in 23")
              div(
                v-if="index % 2 === 0"
                :style="{ width: `${timelineDayWidthInRem / 11}rem` }"
                ) {{ index < 10 ? '0' + index : index }}

          //- Every hour name.
          div(
            v-else
            class="tw-mt-1 tw-flex tw-justify-between tw-text-xs tw-text-neutral-600"
            )
            div(
              v-for="index in 23"
              :style="{ width: `${timelineDayWidthInRem / 23}rem` }"
              ) {{ index < 10 ? '0' + index : index }}

      //- Weeks.
      template(v-else-if="timelineTimeScale === 'weeks'")
        div(
          v-for="index in timelineLengthInDays"
          class="tw-relative tw-text-center"
          :style="{ width: `${timelineDayWidthInRem}rem` }"
          )
          //- Overlay month name to top left corner if this is the first day of the month.
          div(
            v-if="timelineStartInstant.plus({ days: index - 1 }).day === 1"
            class="tw-absolute tw-left-0.5 tw-top-0 tw-text-xs tw-text-neutral-400"
            ) {{ timelineStartInstant.plus({ days: index - 1 }).toFormat('LLL') }}

          //- Overlay week number to top right corner if this is the first day of the week.
          div(
            v-if="timelineStartInstant.plus({ days: index - 1 }).weekday === 1"
            class="tw-absolute tw-right-0.5 tw-top-0.5 tw-size-4 tw-rounded-sm tw-bg-neutral-200 tw-text-xs tw-text-neutral-500"
            ) {{ timelineStartInstant.plus({ days: index - 1 }).weekNumber }}

          //- Day name
          div(class="-tw-mb-1 tw-mt-3 tw-text-xs tw-text-neutral-600") {{ timelineStartInstant.plus({ days: index - 1 }).toFormat('ccc') }}
          //- Day number
          div(class="tw-font-semibold") {{ timelineStartInstant.plus({ days: index - 1 }).toFormat('d') }}

      //- Months.
      template(v-else-if="timelineTimeScale === 'months'")
        div(
          v-for="index in weeksInTimeline"
          class="tw-relative"
          :style="{ width: `${timelineDayWidthInRem * getWeekLengthInDays(index - 1)}rem` }"
          )
          div Week {{ timelineStartInstant.plus({ weeks: index - 1 }).weekNumber }}

          div(class="tw-mt-1 tw-flex tw-text-center tw-text-xs tw-text-neutral-600")
            //- Day numbers
            div(
              v-for="dayIndex in getWeekLengthInDays(index - 1)"
              class="tw-basis-full"
              ) {{ timelineStartInstant.plus({ weeks: index - 1, days: dayIndex - 1 - 7 + getWeekLengthInDays(0) }).toFormat('d') }}

    //- Backgrounds for weekends and vertical grid lines.
    div(class="tw-pointer-events-none tw-absolute tw-inset-0 tw-z-0")
      //- Darker backgrounds for weekends.
      div(v-for="(number, index) in timelineLengthInDays")
        div(
          v-if="timelineStartInstant.plus({ days: index }).weekday > 5"
          class="tw-pointer-events-none tw-absolute tw-bottom-0 tw-top-0 tw-bg-neutral-500 tw-opacity-10"
          :style="{ left: `${(index * 100) / timelineLengthInDays}%`, width: `${100 / timelineLengthInDays}%` }"
          )

      //- Vertical grid lines between hours. Only visible when zoomed in.
      template(v-if="timelineTimeScale === 'hours'")
        div(
          v-for="(number, index) in timelineLengthInDays * 23"
          class="tw-pointer-events-none tw-absolute tw-bottom-0 tw-top-14 tw-w-[1px] tw-bg-neutral-200"
          :style="{ left: `${(index * 100) / (timelineLengthInDays * 23)}%` }"
          )

      template(v-else-if="timelineTimeScale === 'days'")
        div(
          v-for="(number, index) in timelineLengthInDays * 11"
          class="tw-pointer-events-none tw-absolute tw-bottom-0 tw-top-14 tw-w-[1px] tw-bg-neutral-200"
          :style="{ left: `${(index * 100) / (timelineLengthInDays * 11)}%` }"
          )

      //- Vertical grid lines between days. Only visible when zoomed in.
      template(v-if="timelineTimeScale === 'hours' || timelineTimeScale === 'days' || timelineTimeScale === 'weeks'")
        div(
          v-for="(number, index) in timelineLengthInDays"
          class="tw-pointer-events-none tw-absolute tw-h-full tw-w-[1px] tw-bg-neutral-300"
          :style="{ left: `${(index * 100) / timelineLengthInDays}%` }"
          )

      //- Vertical grid lines between weeks. Only visible when zoomed out.
      template(v-if="timelineTimeScale === 'years'")
        template(v-for="(number, index) in timelineLengthInDays")
          div(
            v-if="timelineStartInstant.plus({ days: index - 1 }).weekday === 1"
            class="tw-pointer-events-none tw-absolute tw-h-full tw-w-[1px] tw-bg-neutral-300"
            :style="{ left: `${((index - 1) * 100) / timelineLengthInDays}%` }"
            )

    //- Overlay current time indicator.
    div(
      v-if="currentTimeIndicatorPosition"
      class="tw-pointer-events-none tw-absolute tw-inset-0 tw-z-30"
      )
      div(
        class="tw-pointer-events-none tw-absolute tw-h-full tw-w-0.5 tw-bg-red-500"
        :style="{ left: `${currentTimeIndicatorPosition}%` }"
        )

    //- Overlay loading indicator to days in the beginning of the timeline that are not part of the fetched data range.
    div(
      class="tw-pointer-events-none tw-absolute tw-bottom-0 tw-left-0 tw-top-14 tw-z-20 tw-overflow-clip tw-bg-neutral-300"
      :style="{ width: `${numberOfTimelineBufferDays * timelineDayWidthInRem}rem` }"
      )
      div(class="active-event-overlay")

    //- Overlay loading indicator to days in the end of the timeline that are not part of the fetched data range.
    div(
      class="tw-pointer-events-none tw-absolute tw-bottom-0 tw-right-0 tw-top-14 tw-z-20 tw-overflow-clip tw-bg-neutral-300"
      :style="{ width: `${numberOfTimelineBufferDays * timelineDayWidthInRem}rem` }"
      )
      div(class="active-event-overlay")

    //- Sections.
    span(v-if="visibleTimelineData && timelineRoot")
      div(v-for="[sectionId, section] in calculateSectionsFromRoot(visibleTimelineData.items, timelineRoot)")
        //- Section header row.
        div(:style="{ height: `${sectionHeightInRem}rem` }")

        //- Timeline groups.
        div(
          v-for="[groupId, group] in calculateGroupsFromSection(visibleTimelineData.items, section)"
          class="tw-border-t tw-border-neutral-300 odd:tw-bg-neutral-100 even:tw-bg-neutral-50"
          :style="{ backgroundColor: group.metadata.color ? getWashedColor(group.metadata.color, 0.8) : undefined }"
          @wheel="onTimelineContentWheel"
          )
          //- Group header row.
          div
            //- Open group header row.
            div(
              v-if="isGroupExpanded(groupId)"
              class="tw-relative tw-z-10"
              :style="{ height: `${groupHeightInRem}rem` }"
              )
              //- Render each event in the group on top of each other but on the right position.
              //- Use opacity to make the events distinguishable.
              //div(
                v-for="event in group.events"
                class="tw-absolute tw-h-2 tw-mt-3 tw-opacity-30 tw-border tw-border-white tw-rounded-md"
                :style="getTimelineEventMinimapStyles(event)"
                )

            //- Closed group header row.
            div(
              v-else
              class="tw-relative tw-z-10 tw-py-[1px]"
              :style="{ height: `${groupHeightInRem}rem` }"
              )
              //- Render each item in the group as a single line.
              template(v-for="[rowId, row] in calculateRowsFromGroup(visibleTimelineData.items, group)")
                template(v-for="[itemId, item] in calculateItemsFromRow(visibleTimelineData.items, row)")
                  template(v-if="visibleTimelineEventsRenderInfos[itemId]")
                    //- LiveOps Event.
                    <!-- @vue-expect-error - We know the render info type is correct -->
                    MEventTimelineItemLiveopsEvent(
                      v-if="item.itemType === 'liveopsEvent'"
                      :renderInfo="visibleTimelineEventsRenderInfos[itemId]"
                      :selected="!!selectedItemIds?.includes(itemId)"
                      @click="$emit('eventClicked', itemId)"
                      )
                    //- Instant Event.
                    <!-- @vue-expect-error - We know the render info type is correct -->
                    MEventTimelineItemInstantEvent(
                      v-else-if="item.itemType === 'instantEvent'"
                      :renderInfo="visibleTimelineEventsRenderInfos[itemId]"
                      :selected="!!selectedItemIds?.includes(itemId)"
                      @click="$emit('eventClicked', itemId)"
                      )
                    //- TODO: Other item types.

          //- Group event rows.
          div(v-if="isGroupExpanded(groupId)")
            div(
              v-for="[rowId, row] in calculateRowsFromGroup(visibleTimelineData.items, group)"
              class="tw-relative tw-z-10 tw-py-[1px]"
              :style="{ height: `${rowHeightInRem}rem` }"
              )
              span(v-for="[itemId, item] in calculateItemsFromRow(visibleTimelineData.items, row)")
                template(v-if="visibleTimelineEventsRenderInfos[itemId]")
                  //- LiveOps Event.
                  <!-- @vue-expect-error - We know the render info type is correct -->
                  MEventTimelineItemLiveopsEvent(
                    v-if="item.itemType === 'liveopsEvent'"
                    :renderInfo="visibleTimelineEventsRenderInfos[itemId]"
                    :selected="!!selectedItemIds?.includes(itemId)"
                    @click="$emit('eventClicked', itemId)"
                    )
                  //- Instant Event.
                  <!-- @vue-expect-error - We know the render info type is correct -->
                  MEventTimelineItemInstantEvent(
                    v-else-if="item.itemType === 'instantEvent'"
                    :renderInfo="visibleTimelineEventsRenderInfos[itemId]"
                    :selected="!!selectedItemIds?.includes(itemId)"
                    @click="$emit('eventClicked', itemId)"
                    )
                  //- TODO: Other item types.
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

import MEventTimelineItemInstantEvent from './MEventTimelineItemInstantEvent.vue'
import type { TimelineItemInstantEvent } from './MEventTimelineItemInstantEventTypes'
import MEventTimelineItemLiveopsEvent from './MEventTimelineItemLiveopsEvent.vue'
import type { TimelineItemLiveopsEvent } from './MEventTimelineItemLiveopsEventTypes'
import type { TimelineItem, TimelineData, TimelineItemRenderInfo } from './MEventTimelineTypes'
import {
  findRoot,
  calculateSectionsFromRoot,
  calculateGroupsFromSection,
  calculateRowsFromGroup,
  calculateItemsFromRow,
} from './MEventTimelineUtils'
import {
  getWashedColor,
  groupHeightInRem,
  pixelsToRem,
  remToPixels,
  rowHeightInRem,
  sectionHeightInRem,
  getRenderInfosForVisibleRange,
} from './MEventTimelineVisibleDataUtils'

const emit = defineEmits<{
  'update:timelineFirstVisibleInstant': [instant: DateTime]
  'update:timelineLastVisibleInstant': [instant: DateTime]
  'update:timelineDayWidthInRem': [widthInRem: number]
  eventClicked: [eventId: string]
}>()

const props = defineProps<{
  visibleTimelineData?: TimelineData
  timelineStartInstant: DateTime
  timelineEndInstant: DateTime
  timelineLengthInDays: number
  timelineFirstVisibleInstant: DateTime
  numberOfTimelineBufferDays: number
  timelineDayWidthInRem: number
  expandedGroups: string[]
  selectedItemIds: string[]
}>()

/**
 * Helper to find the root item from visibleTimelineData.
 * Undefined if visibleTimelineData is undefined.
 */
const timelineRoot = computed(() =>
  props.visibleTimelineData === undefined ? undefined : findRoot(props.visibleTimelineData.items)
)

/**
 * A reference to the timeline container element.
 */
const timelineContainer = ref<HTMLDivElement>()

/**
 * The number of full days visible in timeline container.
 * Depends on the width of the timeline container and the width of a single day.
 */
const timelineVisibleDaysCount = ref<number>(0)

/**
 * Internal copy of the initial timeline day width in REM.
 * This is faster to react to changes than the prop value, as there is no emit cycle.
 */
const internalTimelineDayWidthInRem = ref(props.timelineDayWidthInRem)

/**
 * Computed property to determine the header mode based on the current timeline day width.
 */
const timelineTimeScale = computed((): 'hours' | 'days' | 'weeks' | 'months' | 'years' => {
  if (internalTimelineDayWidthInRem.value < 1) return 'years'
  if (internalTimelineDayWidthInRem.value < 3) return 'months'
  if (internalTimelineDayWidthInRem.value < 11) return 'weeks'
  if (internalTimelineDayWidthInRem.value < 20) return 'days'
  return 'hours'
})

const weeksInTimeline = computed(() => props.timelineLengthInDays / 7)

function getWeekLengthInDays(weekIndex: number): number {
  if (weekIndex === 0) {
    return 7 - props.timelineStartInstant.weekday + 1
  }
  if (weekIndex === weeksInTimeline.value - 1) {
    return props.timelineEndInstant.weekday
  }
  return 7
}

/**
 * Function to recalculate the number of visible days based on container size and zoom level.
 * This should be called after browser resize events and similar user inputs.
 */
function updateTimelineVisibleDaysCount(): void {
  if (!timelineContainer.value) {
    return
  }

  const containerWidth = timelineContainer.value.clientWidth
  const containerWidthInRem = pixelsToRem(containerWidth)
  timelineVisibleDaysCount.value = containerWidthInRem / internalTimelineDayWidthInRem.value
}

// Recalculate visible days after zoom level changes.
watch(() => internalTimelineDayWidthInRem.value, updateTimelineVisibleDaysCount)

// Recalculate visible days after browser resizes.
onMounted(() => {
  // Calculate the initial number of visible days.
  updateTimelineVisibleDaysCount()

  // Subscribe to resize events to update the number of visible days.
  window.addEventListener('resize', updateTimelineVisibleDaysCount)
})

/**
 * The full timeline element that is wider than the screen.
 * Note: the parent of this element is responsible for scrolling.
 */
const timelineElement = ref<HTMLDivElement>()

// Add a listener to the width of the timeline element to jump the container to the initial value.
// Note: This is needed because the elements get lazy rendered after data fetch and we must wait for the first layout shift before scrolling.
// Note2: This is also useful for when we lazy-load more data and the timeline gets wider. This will keep the scroll position.
let resizeObserver: ResizeObserver | undefined
onMounted(() => {
  if (!timelineElement.value) {
    console.warn('Timeline element not found.')
    return
  }

  resizeObserver = new ResizeObserver(() => {
    jumpTimelineToInstant(props.timelineFirstVisibleInstant)
    resizeObserver?.disconnect() // Only do this once.
  })

  resizeObserver.observe(timelineElement.value)
})
onUnmounted(() => {
  if (resizeObserver) {
    resizeObserver.disconnect()
  }
})

/**
 * Current right edge position in time of the visible timeline.
 * This updates automatically when the total number of visible days changes due to container resizing.
 */
const lastVisibleInstant = computed(() => {
  if (!timelineContainer.value) return undefined
  return props.timelineFirstVisibleInstant.plus({
    days: timelineVisibleDaysCount.value,
  })
})

watch(lastVisibleInstant, (newValue) => {
  if (!newValue) return
  emit('update:timelineLastVisibleInstant', newValue)
})

onMounted(() => {
  if (!timelineContainer.value) {
    return
  }

  // Watch for changes in the scroll position and update the selected time window start date.
  timelineContainer.value.addEventListener('scroll', (event) => {
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- We know the value is set here.
    const scrollPositionInRem = pixelsToRem(timelineContainer.value!.scrollLeft)
    const scrollPositionInDays = scrollPositionInRem / internalTimelineDayWidthInRem.value
    const newFirstVisibleInstant = props.timelineStartInstant.plus({
      days: scrollPositionInDays,
    })

    // Emit the new selection. It loops back to this component via the props.
    emit('update:timelineFirstVisibleInstant', newFirstVisibleInstant)
  })
})

// Time indicator -----------------------------------------------------------------------------------------------------

const currentTimeIndicatorPosition = computed(() => {
  const now = DateTime.utc()
  if (now < props.timelineStartInstant || now > props.timelineEndInstant) {
    // Return undefined if the current time is outside the visualized data range.
    return undefined
  } else {
    // Otherwise calculate the offset.
    const daysSinceStart = now.diff(props.timelineStartInstant, 'days').days
    return (daysSinceStart * 100) / props.timelineLengthInDays
  }
})

// Timeline scrolling --------------------------------------------------------------------------------------------------

/**
 * This function is called when the user scrolls the timeline content.
 * It hijacks the scroll event and scrolls the timeline container horizontally.
 */
function onTimelineContentWheel(event: WheelEvent): void {
  if (event.shiftKey) {
    event.preventDefault()
    timelineContainer.value?.scrollBy(event.deltaX, 0)
  }
}

/**
 * This function is called when the user scrolls the timeline header.
 * It hijacks the scroll event and changes the width of a single day in the selected time window.
 */
function onTimelineHeaderWheel(event: WheelEvent): void {
  const direction = event.deltaY > 0 ? 1 : -1

  let newWidth = internalTimelineDayWidthInRem.value + direction * internalTimelineDayWidthInRem.value * 0.05

  // Clamp the width to a minimum of 1.
  if (newWidth < 0.1) {
    newWidth = 0.1
  }

  // Clamp the width to a maximum of 10.
  if (newWidth > 40) {
    newWidth = 40
  }

  // Save current timeline location.
  const currentInstant = props.timelineFirstVisibleInstant

  internalTimelineDayWidthInRem.value = newWidth
  emit('update:timelineDayWidthInRem', newWidth)

  // Jump to the previous timeline location now that things have shifted.
  jumpTimelineToInstant(currentInstant)
}

// Timeline dragging --------------------------------------------------------------------------------------------------

const isTimelineMouseDragging = ref(false)

onMounted(() => {
  // Register the mouse move event on the document to allow dragging the timeline.
  document.addEventListener('mousemove', (event) => {
    if (isTimelineMouseDragging.value) {
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- We know the value is set here.
      timelineContainer.value!.scrollBy(-event.movementX, 0)
    }
  })

  // Register the mouse up event on the document to stop dragging the timeline.
  document.addEventListener('mouseup', () => {
    isTimelineMouseDragging.value = false
  })
})

// Hotkey listeners ---------------------------------------------------------------------------------------------------

// Register a global event listener for keys.
onMounted(() => {
  document.addEventListener('keydown', (event) => {
    if (!props.visibleTimelineData) {
      return
    }

    // Arrow keys -----------------------------------------------------------------------------------------------------

    // UP
    /*
    if (event.key === 'ArrowUp') {
      event.preventDefault()
      const allRows = props.visibleTimelineData.sections.flatMap((section) =>
        section.groups.flatMap((group) => group.rows)
      )

      // If nothing is selected, select the last visible event from the last row with data.
      if (props.selectedItemIds.length === 0) {
        let lastItem: TimelineItemData

        // Loop through the rows in reverse order to find the last row with events.
        for (let i = allRows.length - 1; i >= 0; i--) {
          if (allRows[i].items.length > 0) {
            lastItem = allRows[i].items[allRows[i].items.length - 1]
            scrollTimelineToItem(lastItem)
            break
          }
        }
        return
      }

      // If something is selected, select the closest event in time from the first above row with events. Loop to the last row if needed.
      const selectedItem = props.selectedItemIds[0]
      //todo      const currentRowIndex = allRows.findIndex((row) => row.items.includes(selectedItem))

      // TODO: Implement this.
    }
      */

    // DOWN
    /*
    if (event.key === 'ArrowDown') {
      event.preventDefault()
      const allRows = props.visibleTimelineData.sections.flatMap((section) =>
        section.groups.flatMap((group) => group.rows)
      )

      // If nothing is selected, select the first visible event from the first row with data.
      if (props.selectedItemIds.length === 0) {
        for (const row of allRows) {
          if (row.items.length > 0) {
            scrollTimelineToItem(row.items[0])
            break
          }
        }
        return
      }

      // If something is selected, select the closest event in time from the first below row with events. Loop to the first row if needed.
      const selectedItem = props.selectedItemIds[0]
      //todo      const currentRowIndex = allRows.findIndex((row) => row.items.includes(selectedItem))

      // TODO: Implement this.
    }
    */

    // RIGHT
    // if (event.key === 'ArrowRight') {
    //   event.preventDefault()

    //   // If nothing is selected, select the earliest visible event from any row.
    //   if (props.selectedItems.length === 0) {
    //     const sortedEvents = Object.entries(props.visibleTimelineEventsRenderInfos).sort((a, b) => {
    //       if (!a[1].eventStartInstant || !b[1].eventStartInstant) {
    //         return 0
    //       }
    //       return a[1].eventStartInstant.toMillis() - b[1].eventStartInstant.toMillis()
    //     })

    //     const earliestEventId = sortedEvents[0][1].id

    //     const earliestEvent = props.visibleTimelineData.sections
    //       .flatMap((section) => section.groups.flatMap((group) => group.rows.flatMap((row) => row.items)))
    //       .find((item) => item.id === earliestEventId)

    //     if (earliestEvent) {
    //       scrollTimelineToItem(earliestEvent)
    //     }
    //   }

    //   // If something is selected and there are events on the same row, select the next event on the same row.
    //   const selectedItem = props.selectedItems[0]
    //   const allRows = props.visibleTimelineData.sections.flatMap((section) =>
    //     section.groups.flatMap((group) => group.rows)
    //   )
    //   const currentRowIndex = allRows.findIndex((row) => row.items.includes(selectedItem))
    //   const currentRowEventsAfterSelectedItem = allRows[currentRowIndex].items.slice(
    //     allRows[currentRowIndex].items.indexOf(selectedItem) + 1
    //   )
    //   if (currentRowEventsAfterSelectedItem.length > 0) {
    //     scrollTimelineToItem(currentRowEventsAfterSelectedItem[0])
    //   }
    // }

    // LEFT
    // if (event.key === 'ArrowLeft') {
    //   event.preventDefault()

    //   // If nothing is selected, select the latest visible event from any row.
    //   if (props.selectedItems.length === 0) {
    //     const sortedEvents = Object.entries(props.visibleTimelineEventsRenderInfos).sort((a, b) => {
    //       if (!a[1].eventStartInstant || !b[1].eventStartInstant) {
    //         return 0
    //       }
    //       return b[1].eventStartInstant.toMillis() - a[1].eventStartInstant.toMillis()
    //     })

    //     const latestEventId = sortedEvents[0][1].id

    //     const latestEvent = props.visibleTimelineData.sections
    //       .flatMap((section) => section.groups.flatMap((group) => group.rows.flatMap((row) => row.items)))
    //       .find((item) => item.id === latestEventId)

    //     if (latestEvent) {
    //       scrollTimelineToItem(latestEvent)
    //     }
    //   }

    //   // If something is selected and there are events on the same row, select the previous event on the same row.
    //   const selectedItem = props.selectedItems[0]
    //   const allRows = props.visibleTimelineData.sections.flatMap((section) =>
    //     section.groups.flatMap((group) => group.rows)
    //   )
    //   const currentRowIndex = allRows.findIndex((row) => row.items.includes(selectedItem))
    //   const currentRowEventsBeforeSelectedItem = allRows[currentRowIndex].items.slice(
    //     0,
    //     allRows[currentRowIndex].items.indexOf(selectedItem)
    //   )
    //   if (currentRowEventsBeforeSelectedItem.length > 0) {
    //     scrollTimelineToItem(currentRowEventsBeforeSelectedItem[currentRowEventsBeforeSelectedItem.length - 1])
    //   }
    // }

    // CTRL + T to scroll to today.
    // if (event.key === 't' && event.ctrlKey) {
    //   scrollTimelineToToday()
    // }

    // CTRL + E to scroll to the first selected event.
    if (event.key === 'e' && event.ctrlKey) {
      if (props.selectedItemIds.length > 0) {
        // todo        scrollTimelineToItem(props.selectedItemIds[0])
      }
    }
  })
})

// Exposed timeline control functions ---------------------------------------------------------------------------------

/**
 * Utility function to scroll the timeline to the current day.
 * @param smooth Whether to scroll smoothly or not.
 */
function scrollTimelineToToday(smooth = true): void {
  if (!timelineContainer.value) {
    return
  }

  const instant = DateTime.utc()

  const daysSinceStart = instant.diff(props.timelineStartInstant, 'days').days
  const offset = daysSinceStart * remToPixels(internalTimelineDayWidthInRem.value)

  // todo: make this a hard jump (even is smooth is true) if today is outside the visible range.

  if (smooth) {
    timelineContainer.value.scrollTo({
      left: offset,
      behavior: 'smooth',
    })
  } else {
    timelineContainer.value.scrollLeft = offset
  }
}

/**
 * Utility function to scroll the timeline to the day of a specific timeline item.
 * @param item The item to scroll to.
 * @param selectItem Whether to also select the item in addition to scrolling. Default is true.
 * @param smooth Whether to scroll smoothly or not.
 */
function scrollTimelineToItem(item: TimelineItem, selectItem = true, smooth = true): void {
  if (!timelineContainer.value) return

  let targetInstant: DateTime

  if (item.itemType === 'liveopsEvent') {
    const liveopsEvent = item as TimelineItemLiveopsEvent
    // Can't scroll to events without a start date.
    if (!liveopsEvent.renderData.timelinePosition.startInstantIsoString) return
    targetInstant = DateTime.fromISO(liveopsEvent.renderData.timelinePosition.startInstantIsoString)
  } else if (item.itemType === 'instantEvent') {
    const instantEvent = item as TimelineItemInstantEvent
    targetInstant = DateTime.fromISO(instantEvent.renderData.instantIsoString)
  } else {
    // This can't happen but makes TypeScript happy.
    return
  }

  // Add some margin to the target time.
  targetInstant = targetInstant.minus({ days: 1 })

  const daysSinceTimelineStart = targetInstant.diff(props.timelineStartInstant, 'days').days

  if (smooth) {
    timelineContainer.value.scrollTo({
      left: daysSinceTimelineStart * remToPixels(internalTimelineDayWidthInRem.value),
      behavior: 'smooth',
    })
  } else {
    timelineContainer.value.scrollLeft = daysSinceTimelineStart * remToPixels(internalTimelineDayWidthInRem.value)
  }

  if (selectItem) {
    // emit('eventClicked', item.id)
  }
}

function jumpTimelineToInstant(instant: DateTime): void {
  if (!timelineContainer.value) {
    return
  }

  const daysSinceStart = instant.diff(props.timelineStartInstant, 'days').days
  const offset = daysSinceStart * remToPixels(internalTimelineDayWidthInRem.value)

  timelineContainer.value.scrollLeft = offset
}

defineExpose({
  scrollTimelineToToday,
  scrollTimelineToItem,
  jumpTimelineToInstant,
})

// Data ---------------------------------------------------------------------------------------------------------------

/**
 * Render data for visible timeline events. Created from the source `visibleTimelineData`.
 */
const visibleTimelineEventsRenderInfos = ref<Record<string, TimelineItemRenderInfo>>({})

/**
 * Update render data when any source data changes.
 */
watch(
  [
    (): TimelineData | undefined => props.visibleTimelineData,
    (): DateTime => props.timelineStartInstant,
    (): DateTime => props.timelineEndInstant,
    (): DateTime => props.timelineFirstVisibleInstant,
    lastVisibleInstant,
    (): number => props.timelineDayWidthInRem,
    (): string[] => props.expandedGroups,
  ],
  ([
    newVisibleTimelineData,
    newTimelineStartInstant,
    newTimelineEndInstant,
    newTimelineFirstVisibleInstant,
    newLastVisibleInstant,
    newDayWidth,
    newExpandedGroups,
  ]) => {
    if (newVisibleTimelineData && newLastVisibleInstant) {
      visibleTimelineEventsRenderInfos.value = getRenderInfosForVisibleRange(
        newVisibleTimelineData,
        newTimelineStartInstant,
        newTimelineEndInstant,
        newTimelineFirstVisibleInstant,
        newLastVisibleInstant,
        newDayWidth,
        newExpandedGroups
      )
    }
  }
)

/**
 * Force timeline to match expected position. This feels like a kludge right now...
 */
watch(
  [
    (): DateTime => props.timelineStartInstant,
    (): DateTime => props.timelineEndInstant,
    (): number => props.timelineDayWidthInRem,
  ],
  () => {
    jumpTimelineToInstant(props.timelineFirstVisibleInstant)
  }
)

// Utility functions --------------------------------------------------------------------------------------------------

/**
 * Utility function that checks if a group is currently expanded.
 */
function isGroupExpanded(groupId: string): boolean {
  return props.expandedGroups.includes(groupId)
}
</script>
