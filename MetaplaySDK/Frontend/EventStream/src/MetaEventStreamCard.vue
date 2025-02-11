<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->
<!-- eslint-disable @typescript-eslint/no-unsafe-argument - The 'any' types in this file suck. Rewrite later. -->

<template lang="pug">
b-card(
  :class="{ 'bg-light': !hasContent || !hasPermission }"
  style="min-height: 12rem"
  no-body
  class="shadow-sm"
  )
  //- Header
  div(
    class="tw-cursor-pointer tw-px-4 tw-flex tw-justify-between tw-items-center tw-pt-4 tw-cursor-pointer"
    @click="utilsOpen = !utilsOpen"
    )
    b-card-title(class="tw-flex tw-items-center")
      fa-icon(
        v-if="icon"
        :icon="icon"
        class="mr-2"
        )
      | {{ title }}
      //- MBadge styling TODO: "1/701" type of pill number looks off.
      MBadge(
        v-if="hasContent && hasPermission"
        shape="pill"
        :variant="badgeVariant"
        class="tw-ml-1"
        data-testid="badge-text"
        ) {{ badgeText }}
      svg(
        v-if="eventStreamLoading"
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 512 512"
        class="tw-pointer-events-none tw-ml-2 tw-w-5 tw-h-5 tw-fill-blue-500 tw-animate-spin"
        aria-hidden="true"
        )
        <!--! Font Awesome Free 6.4.2 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license (Commercial License) Copyright 2023 Fonticons, Inc. -->
        path(
          d="M304 48a48 48 0 1 0 -96 0 48 48 0 1 0 96 0zm0 416a48 48 0 1 0 -96 0 48 48 0 1 0 96 0zM48 304a48 48 0 1 0 0-96 48 48 0 1 0 0 96zm464-48a48 48 0 1 0 -96 0 48 48 0 1 0 96 0zM142.9 437A48 48 0 1 0 75 369.1 48 48 0 1 0 142.9 437zm0-294.2A48 48 0 1 0 75 75a48 48 0 1 0 67.9 67.9zM369.1 437A48 48 0 1 0 437 369.1 48 48 0 1 0 369.1 437z"
          )

    span(style="margin-top: -1rem")
      span(class="text-muted tw-italic small mr-2") {{ topRightHintLabel }}
      MIconButton(
        :disabled-tooltip="!hasContent || !eventList.length ? 'There are no events to search.' : undefined"
        aria-label="Toggle the utilities menu"
        @click="utilsOpen = !utilsOpen"
        )
        fa-icon(
          icon="angle-right"
          size="sm"
          class="tw-mr-1"
          )
        fa-icon(
          icon="search"
          size="sm"
          )

  //- Permission handling
  div(
    v-if="!hasPermission"
    class="small text-muted tw-text-center mb-4 pt-4"
    ) You need the #[MBadge {{ permission }}] permission to view this card.

  //- Loading
  div(
    v-else-if="!hasContent"
    class="tw-w-full card-manual-content-padding pt-2"
    )
    b-skeleton(width="85%")
    b-skeleton(width="55%")
    b-skeleton(width="70%")
    b-skeleton(
      width="80%"
      class="tw-mt-4"
      )
    b-skeleton(width="65%")

  //- Empty
  b-row(
    v-else-if="!eventList.length"
    align-h="center"
    no-gutters
    class="tw-px-4 tw-text-center my-auto mb-4 pb-4"
    )
    span(class="text-muted") {{ emptyMessage }}

  div(v-else)
    //- Utilities menu
    MTransitionCollapse
      div(
        v-if="eventList.length && utilsOpen"
        :id="title"
        class="tw-bg-neutral-100 tw-w-full tw-border-t tw-border-b tw-border-neutral-200 tw-px-4 tw-py-3"
        )
        div(
          v-if="keywordPreFilters.length > 0 || eventTypePreFilters.length > 0 || searchPreHighlight"
          class="tw-border-b tw-border-neutral-200 tw-mb-3"
          )
          div(v-if="keywordPreFilters.length > 0 || eventTypePreFilters.length > 0")
            div(class="tw-text-sm tw-leading-6 tw-font-bold") Event Data Filters
            p Event data has been pre-filtered down to events that match the following types OR keywords: #[span {{ eventTypePreFilters.concat(keywordPreFilters).join(', ') }}]

          div(v-if="searchPreHighlight")
            div(class="tw-text-sm tw-leading-6 tw-font-bold") Pre-Highlight
            p Events matching the search string #[MBadge {{ searchPreHighlight }}] are highlighted.

        div(class="sm:tw-flex tw-items-center sm:tw-space-x-2")
          div(class="tw-text-sm tw-leading-6 tw-font-bold") Utilities Mode
          MInputSingleSelectSwitch(
            :model-value="internalUtilitiesMode"
            :options="[ { label: 'Highlight', value: 'highlight' }, { label: 'Filter', value: 'filter' }, ]"
            size="small"
            @update:model-value="(event) => (internalUtilitiesMode = event)"
            )

          div(class="tw-text-sm tw-leading-6 tw-font-bold") Logic Condition
          MInputSingleSelectSwitch(
            :model-value="internalUtilitiesCondition"
            :options="[ { label: 'And', value: 'and' }, { label: 'Or', value: 'or' }, ]"
            size="small"
            @update:model-value="(event) => (internalUtilitiesCondition = event)"
            )

        div(class="tw-text-sm tw-leading-6 tw-font-bold") Search
        MInputText(
          :model-value="userSearchString"
          placeholder="Type your search here..."
          :variant="userSearchActive ? 'success' : 'default'"
          :debounce="300"
          class="tw-mb-2"
          showClearButton
          @update:model-value="(event) => (userSearchString = event)"
          )

        div(class="tw-text-sm tw-leading-6 tw-font-bold") Event Types
        MInputMultiSelectCheckbox(
          v-if="filters.eventTypes.length > 0"
          :model-value="selectedEventTypes"
          :options="filters.eventTypes.map((type) => ({ label: `${type} (${countTypeMatches(type)})`, value: type }))"
          size="small"
          @update:model-value="(event) => (selectedEventTypes = event)"
          )
        div(
          v-else
          class="text-muted small tw-italic"
          ) None available for this event stream.

        div(class="tw-text-sm tw-leading-6 tw-font-bold") Keywords
        MInputMultiSelectCheckbox(
          v-if="filters.eventKeywords.length > 0"
          :model-value="selectedKeywords"
          :options="filters.eventKeywords.map((keyword) => ({ label: `${keyword} (${countKeywordMatches(keyword)})`, value: keyword }))"
          size="small"
          @update:model-value="(event) => (selectedKeywords = event)"
          )
        div(
          v-else
          class="text-muted small tw-italic"
          ) None available for this event stream.

        div(
          v-if="userSearchActive || filtersActive"
          class="tw-text-sm tw-mt-3"
          ) #[span(class="tw-font-semibold") {{ internalUtilitiesMode === 'filter' ? 'Only showing' : 'Highlighting' }}] events that match the
          span(
            v-for="(filter, index) in activeFilters"
            :class="{ 'tw-ml-1': index > 0 }"
            ) #[span(v-if="index > 0" class="tw-font-semibold") {{ internalUtilitiesCondition === 'or' ? 'OR' : 'AND' }}] {{ filter }}
          span .

    //- Main body
    div(
      :style="`max-height: ${maxHeight}; overflow-y: auto;`"
      class="d-flex flex-column justify-content-between h-100"
      )
      //- Sticks to the top
      div
        //- Pause stream
        div(
          v-if="allowPausing"
          class="tw-text-center tw-text-neutral-500 tw-text-xs+ tw-pb-2 tw-space-x-1"
          )
          span(v-show="!isPaused") Last update #[meta-time(:date="lastUpdated")].
          span(v-show="isPaused") Updates paused #[meta-time(:date="lastUpdated")].
          MTextButton(
            @click="togglePlayPauseState"
            data-testid="play-pause-button"
            ) {{ isPaused ? 'Resume' : 'Pause' }} updates
          div
            MBadge(
              v-if="updateAvailable"
              shape="pill"
              variant="primary"
              ) New data available

        //- List of events
        div(
          v-if="decoratedSearchedAndUnfoldedEventList.length > 0"
          class="group-element-borders group-element-stripes tw-mb-3"
          )
          //- Loop through all events, giving each a test Id
          div(
            v-for="(event, index) in decoratedSearchedAndUnfoldedEventList"
            class="group"
            :data-testid="dataTestIdFromEvent(event)"
            )
            //- Render each event
            meta-lazy-loader(
              :key="index"
              :class="eventBackgroundClass(event)"
              :placeholderEventHeight="50"
              )
              //- Session header row
              div(
                v-if="event.type === 'Session'"
                :class="{ 'group-open': event.decorations.isPathUnfolded }"
                class="card-manual-content-padding tw-flex clickable-list-group-item tw-py-3 tw-gap-2"
                @click="toggleFoldedPath(event.path)"
                )
                fa-icon(
                  icon="angle-right"
                  class="tw-mt-1 tw-text-sm"
                  )
                div(class="tw-grow")
                  div(class="tw-flex tw-justify-between")
                    div
                      MTooltip(
                        :content="`Session ID: ${event.id}`"
                        noUnderline
                        class="font-weight-bold"
                        ) Session \#{{ event.typeData.sessionNumber }}
                      span(class="tw-text-sm text-muted tw-ml-1") {{ event.typeData.numEvents }} events
                    div(class="tw-text-sm")
                      meta-time(:date="event.typeData.startTime")
                  div(class="tw-flex tw-justify-between")
                    div(class="tw-text-sm text-muted") {{ event.typeData.deviceName }}
                    div(class="tw-text-sm") Lasted for #[meta-duration(:duration="event.typeData.duration")]

              //- Day header row
              div(
                v-if="event.type === 'Day'"
                :class="{ 'group-open': event.decorations.isPathUnfolded }"
                class="card-manual-content-padding tw-flex clickable-list-group-item tw-py-3 tw-gap-2"
                @click="toggleFoldedPath(event.path)"
                )
                fa-icon(
                  icon="angle-right"
                  class="tw-mt-1 tw-text-sm"
                  )
                div(class="tw-grow")
                  div(class="tw-flex tw-justify-between")
                    div
                      meta-time(
                        :date="event.typeData.date"
                        showAs="date"
                        class="font-weight-bold"
                        )
                    div(class="tw-text-sm")
                      meta-time(:date="event.typeData.date")
                  div(class="tw-text-sm text-muted") {{ event.typeData.numEvents }} events

              //- Aggregated events header row
              div(
                v-else-if="event.type === 'RepeatedEvents'"
                :class="{ 'group-open': event.decorations.isPathUnfolded }"
                class="card-manual-content-padding clickable-list-group-item"
                @click="toggleFoldedPath(event.path)"
                )
                div(v-if="false") Collapsed events: {{ event.typeData.numEvents }} x #[span(class="font-weight-bold") {{ event.typeData.repeatedTitle }}] #[span(class="small text-muted") Lasted #[meta-duration(:duration="event.typeData.duration")]]
                div(
                  v-else
                  :class="`${event.decorations.lineVariant !== 'none' ? 'three' : 'two'}-column-layout`"
                  )
                  //- Timestamps
                  div(class="py-1 pr-2 text-right")
                    div(v-if="!event.decorations.isPathUnfolded")
                      div #[meta-time(:date="event.time" showAs="time")]
                      div(
                        v-if="showTimeDeltas"
                        class="small text-muted"
                        )
                        span(v-if="event.decorations.timeBetweenEvents") + #[meta-duration(:duration="event.decorations.timeBetweenEvents" showAs="top-two")]
                        span(v-else) Oldest event
                  div(v-if="event.decorations.lineVariant !== 'none'")
                    meta-event-stream-card-group-line(:variant="event.decorations.lineVariant")
                  div(class="pl-2")
                    div(
                      v-if="!event.decorations.isPathUnfolded"
                      class="py-2"
                      )
                      fa-icon(
                        icon="angle-right"
                        class="mr-2"
                        )
                      span {{ event.typeData.numEvents }} x #[span(class="font-weight-bold") {{ event.typeData.repeatedTitle }}] #[span(class="tw-text-xs tw-text-blue-500 hover:tw-underline") Expand]
                    div(v-else)
                      span(class="text-muted small") {{ event.typeData.numEvents }} repeating events. #[span(class="tw-text-xs tw-text-blue-500 hover:tw-underline") Collapse]

              //- Individual event row (when it is renderable)
              div(
                v-else-if="event.type === 'Event'"
                ref="event-item"
                :class="`${event.decorations.lineVariant !== 'none' ? 'three' : 'two'}-column-layout`"
                class="card-manual-content-padding"
                )
                //- Timestamps
                div(class="py-1 pr-2 text-right")
                  div #[meta-time(:date="event.time" showAs="time")]
                  div(
                    v-if="showTimeDeltas"
                    class="small text-muted"
                    )
                    span(v-if="event.decorations.timeBetweenEvents") + #[meta-duration(:duration="event.decorations.timeBetweenEvents" showAs="top-two")]
                    span(v-else) Oldest event

                //- Group line
                div(v-if="event.decorations.lineVariant !== 'none'")
                  meta-event-stream-card-group-line(:variant="event.decorations.lineVariant")

                //- Event payload
                div(class="!tw-overflow-x-hidden")
                  div(
                    class="clickable-list-group-item d-flex rounded-sm py-1 pr-1"
                    @click="toggleEventExpanded(event)"
                    )
                    div(
                      v-if="event.decorations.isEventExpandable || true"
                      :class="{ 'not-collapsed': isEventExpanded(event) }"
                      )
                      fa-icon(
                        icon="angle-right"
                        class="tw-text-sm mx-2"
                        )
                    div(class="tw-w-full")
                      div(class="d-flex justify-content-between")
                        div
                          span(class="font-weight-bold") {{ event.typeData.title }}
                          span(class="font-weight-normal small text-muted tw-ml-1")
                            meta-username(
                              v-if="event.typeData.author"
                              :username="event.typeData.author"
                              )
                        div(
                          style="padding-top: 0.2rem"
                          class="small"
                          )
                          MTextButton(
                            v-if="showViewMoreLink && event.typeData.viewMoreLink"
                            :to="event.typeData.viewMoreLink"
                            data-testid="view-more-link"
                            ) View {{ event.typeData.viewMore }}
                      div(class="text-muted small text-break-word") {{ event.typeData.description }}
                  transition(name="collapse-transition")
                    div(
                      v-if="isEventExpanded(event)"
                      class="tw-my-1"
                      )
                      slot(
                        name="event-details"
                        v-bind:event="event"
                        )
                        pre(class="tw-bg-neutral-100 tw-text-xs tw-p-3 tw-border-neutral-200 tw-border tw-rounded-md")
                          div(class="tw-text-neutral-500") // raw_event_payload
                          div(class="tw-text-neutral-600") {{ event.typeData.sourceData }}

        //- Empty list after filtering
        div(
          v-else
          class="small text-muted tw-text-center mb-4 pt-4"
          ) {{ noResultsMessage }}
</template>

<script lang="ts" setup>
import { DateTime, Duration } from 'luxon'
import { computed, ref, shallowRef, watch } from 'vue'

import {
  MBadge,
  MIconButton,
  MInputMultiSelectCheckbox,
  MInputSingleSelectSwitch,
  MInputText,
  MTextButton,
  MTooltip,
  MTransitionCollapse,
  usePermissions,
} from '@metaplay/meta-ui-next'

import MetaEventStreamCardGroupLine from './MetaEventStreamCardGroupLine.vue'
import type { EventStreamItemBase } from './eventStreamItems'
// import { camelCaseToSentenceCase, sentenceCaseToKebabCase } from '@metaplay/meta-ui'
import { getFiltersForEventStream } from './eventStreamUtils'

// Props ------------------------

const props = withDefaults(
  defineProps<{
    /**
     * Title of the card.
     */
    title: string
    /**
     * Optional Font-Awesome icon shown before the card's title (for example: 'table').
     */
    icon?: string
    /**
     * Stream of events to be shown. Must be supplied in the order of oldest to newest.
     */
    eventStream?: EventStreamItemBase[] | null
    /**
     * Optional: If true then the card will show a loading spinner to indicate that events are still being streamed in
     * from the server. Defaults to false.
     */
    eventStreamLoading?: boolean
    /**
     * Optional: Setting this to 'highlight' will show events that match the search & filters with a different background color.
     * Setting this to 'filter' will hide events that do not match the search & filters.
     * Defaults to 'filter'.
     */
    utilitiesMode?: 'highlight' | 'filter'
    /**
     * Optional: Setting this to 'or' will show events that match at least one of the selected filters. Setting this to 'and' will
     * show events that match all of the selected filters. Defaults to 'or'.
     */
    utilitiesCondition?: 'or' | 'and'
    /**
     * Optional: Permanent highlighting of events that match this search string. This is intended to be used with an event ID so it matches one event. The user can not change this search string.
     */
    searchPreHighlight?: string
    /**
     * Optional: Pre-filtering based on an array of keywords. This is an OR filter on the source data before it is shown in the UI.
     */
    keywordPreFilters?: string[]
    /**
     * Optional: Pre-filtering based on an array of event types. This is an OR filter on the source data before it is shown in the UI.
     */
    eventTypePreFilters?: string[]
    /**
     * Optional: Custom message to be shown when event array is null or empty.
     */
    emptyMessage?: string
    /**
     * Optional: Custom message to be shown when there are no search results.
     */
    noResultsMessage?: string
    /**
     * Optional: Limit height of the card.
     */
    maxHeight?: string
    /**
     * Optional: Permission needed to view this card's data.
     */
    permission?: string
    /**
     * Optional: If true then each event shows a "view more" link.
     */
    showViewMoreLink?: boolean
    /**
     * Optional: If true then show delta time between events. Defaults to true.
     */
    showTimeDeltas?: boolean
    /**
     * Optional: Show the controls to pause and resume the event stream.
     */
    allowPausing?: boolean
  }>(),
  {
    icon: '',
    utilitiesMode: 'filter',
    utilitiesCondition: 'and',
    searchPreHighlight: undefined,
    eventTypePreFilters: () => [],
    keywordPreFilters: () => [],
    emptyMessage: 'No events in this stream.',
    noResultsMessage: 'No events found. Try a different search string? 🤔',
    eventStream: null,
    maxHeight: '',
    permission: '',
    showViewMoreLink: false,
    showTimeDeltas: true,
  }
)

// Searching & filtering ----------------------------------------------------------------------------------------------

/**
 * A subset of the event stream that has been pre-filtered based on the pre-filter props.
 */
const preFilteredEvents = computed(() => {
  if (props.eventTypePreFilters.length === 0 && props.keywordPreFilters.length === 0) {
    return props.eventStream
  }
  return props.eventStream?.filter((event) => {
    if (event.type !== 'Event' && event.type !== 'RepeatedEvents') {
      return true
    }
    if (props.eventTypePreFilters.length > 0 && props.eventTypePreFilters.includes(event.getEventDisplayType() ?? '')) {
      return true
    }
    const eventKeywords = event.getEventKeywords()
    if (
      props.keywordPreFilters.length > 0 &&
      !!eventKeywords &&
      props.keywordPreFilters.some((keyword) => eventKeywords.includes(keyword))
    ) {
      return true
    }
    return false
  })
})

/**
 * Filters available for this event stream.
 */
const filters = computed(() => {
  return getFiltersForEventStream(preFilteredEvents.value ?? [])
})

/**
 * User's search string as entered from the utilities menu. Can be passed in as a prop but is purposefully /not/
 * reactive to changes in that prop.
 */
const userSearchString = ref('')

/**
 * Used as a v-model to remember which event types the user has selected.
 */
const selectedEventTypes = ref<string[]>([])

/**
 * Used as a v-model to remember which event keywords the user has selected.
 */
const selectedKeywords = ref<string[]>([])

/**
 * Internal flag to remember if the user is highlighting or filtering. Defaults to the prop value but can be toggled by
 * the user.
 */
const internalUtilitiesMode = ref(props.utilitiesMode)
watch(
  () => props.utilitiesMode,
  (newValue) => {
    internalUtilitiesMode.value = newValue
  }
)

/**
 * Internal flag to remember if the user is using 'or' or 'and' condition for filters. Defaults to the prop value but can
 * be toggled by the user.
 */
const internalUtilitiesCondition = ref(props.utilitiesCondition)
watch(
  () => props.utilitiesCondition,
  (newValue) => {
    internalUtilitiesCondition.value = newValue
  }
)

/**
 * Transforms the event list by marking entries that match against any search strings with `highlightXXX'. If filtering is
 * active then any non-matches are filtered out of the list.
 */
const filteredOrHighlightedEventList = computed(() => {
  if (anyUtilityActive.value) {
    // TODO: This any here is really magical. Things get wonky if you try to type this. Not great.
    let events: any[] = eventList.value

    // Apply highlighting.
    if (
      // eslint-disable-next-line @typescript-eslint/prefer-nullish-coalescing
      props.searchPreHighlight ||
      userSearchActive.value ||
      filtersActive.value
    ) {
      events = events.map((event) => {
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        const preHighlightMatch = doesEventMatchPreHighlightSearch(event)
        let userHighlightMatch = false
        if (internalUtilitiesCondition.value === 'and' && (userSearchActive.value || filtersActive.value)) {
          userHighlightMatch =
            // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
            (userSearchString.value ? doesEventMatchUserSearch(event) : true) &&
            (selectedEventTypes.value.length > 0
              ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
                doesEventMatchCurrentEventTypeFilters(event)
              : true) &&
            (selectedKeywords.value.length > 0
              ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
                doesEventMatchCurrentKeywordFilters(event)
              : true)
        } else {
          userHighlightMatch =
            // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
            doesEventMatchUserSearch(event) ||
            // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
            doesEventMatchCurrentEventTypeFilters(event) ||
            // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
            doesEventMatchCurrentKeywordFilters(event)
        }
        event.preHighlightMatch = preHighlightMatch
        event.userHighlightMatch = userHighlightMatch
        return event
      })
    }

    // Apply filtering.
    if (internalUtilitiesMode.value === 'filter' && (userSearchActive.value || filtersActive.value)) {
      events = events.filter((x) => x.userHighlightMatch)
    }

    return events
  } else {
    return eventList.value
  }
})

const activeFilters = computed(() => {
  const activeFilters: string[] = []
  if (userSearchActive.value) {
    activeFilters.push('search string')
  }
  if (selectedEventTypes.value.length > 0) {
    activeFilters.push('selected event type(s)')
  }
  if (selectedKeywords.value.length > 0) {
    activeFilters.push('selected keyword(s)')
  }

  return activeFilters
})

/**
 * True if any type of pre- or user utility is active.
 */
const anyUtilityActive = computed(() => {
  return userSearchActive.value || filtersActive.value || !!props.searchPreHighlight
})

/**
 * True if user search string is active.
 */
const userSearchActive = computed(() => {
  // Search string must be at least 1 chars long before it is considered to be usable.
  return userSearchString.value.length >= 1
})

/**
 * True if any event types or keywords are selected.
 */
const filtersActive = computed(() => {
  return selectedEventTypes.value.length > 0 || selectedKeywords.value.length > 0
})

/**
 * Utility to check if a given event in the stream matches the pre-highlight search.
 */
function doesEventMatchPreHighlightSearch(event: EventStreamItemBase): boolean {
  return !!props.searchPreHighlight && event.search(props.searchPreHighlight.toLocaleLowerCase())
}

/**
 * Utility to check if a given event in the stream matches the currently possibly active search.
 */
function doesEventMatchUserSearch(event: EventStreamItemBase): boolean {
  return !!userSearchString.value && event.search(userSearchString.value.toLocaleLowerCase())
}

/**
 * Utility to check if a given event in the stream matches the currently possibly active type filter.
 */
function doesEventMatchCurrentEventTypeFilters(event: EventStreamItemBase): boolean {
  return selectedEventTypes.value.includes(event.getEventDisplayType() ?? '')
}

/**
 * Utility to check if a given event in the stream matches the currently possibly active keyword filter.
 */
function doesEventMatchCurrentKeywordFilters(event: EventStreamItemBase): boolean {
  const eventKeywords = event.getEventKeywords()
  return selectedKeywords.value.some((keyword) => eventKeywords?.includes(keyword))
}

/**
 * Count the number of events in the stream that match a given type.
 */
function countTypeMatches(eventType: string): number {
  if (eventType) {
    return eventList.value.reduce((pre, cur) => {
      return cur.getEventDisplayType() === eventType ? pre + 1 : pre
    }, 0)
  } else {
    return 0
  }
}

/**
 * Count the number of events in the stream that match a given keyword.
 */
function countKeywordMatches(keyword: string): number {
  if (keyword) {
    return eventList.value.reduce((pre, cur) => {
      return (cur.getEventKeywords() ?? []).includes(keyword) ? pre + 1 : pre
    }, 0)
  } else {
    return 0
  }
}

function eventBackgroundClass(event: DecoratedEventStreamItem): string {
  if (event.preHighlightMatch) {
    return 'presearch-highlight'
  }
  if (internalUtilitiesMode.value === 'filter') {
    return ''
  }
  if (event.userHighlightMatch) {
    return 'user-highlight'
  }
  return ''
}

/**
 * When search strings change, ensure that all search results are made visible.
 */
watch([userSearchString, selectedEventTypes, selectedKeywords], openPathsToHighlightedOrFilteredEvents)

// Folding paths ------------------------------------------------------------------------------------------------------

/**
 * List of paths that are unfolded, ie: events with children that have been expanded.
 */
const unfoldedPaths = ref<string[]>([])

/**
 * Open paths so that any searched events are visible.
 */
function openPathsToHighlightedOrFilteredEvents(): void {
  // TODO: refactor to also apply filters.
  if (anyUtilityActive.value) {
    const uniqueSearchPaths = new Set<string>(
      filteredOrHighlightedEventList.value
        .filter((event) => event.preHighlightMatch || event.userHighlightMatch)
        .map((event) => event.path)
    )

    // Open those paths and all paths above them.
    uniqueSearchPaths.forEach((searchPath) => {
      let openPath = ''
      searchPath.split('.').forEach((searchPathSegment) => {
        openPath += searchPathSegment
        openFoldedPath(openPath)
        openPath += '.'
      })
    })
  }
}

/**
 * Open or close a folded path.
 * @param path Path to toggle.
 */
function toggleFoldedPath(path: string): void {
  if (path) {
    if (!unfoldedPaths.value.includes(path)) {
      let pathToUnfold = ''
      path.split('.').forEach((pathSegment) => {
        pathToUnfold += pathSegment
        openFoldedPath(pathToUnfold)
        pathToUnfold += '.'
      })
    } else {
      closeFoldedPath(path)
    }
  }
}

/**
 * Opens a path. Safe to call on an already opened path.
 * @param path Path to open.
 */
function openFoldedPath(path: string): void {
  if (!unfoldedPaths.value.includes(path)) {
    unfoldedPaths.value.push(path)
  }
}

/**
 * Closes a path. Safe to call on an already closed path.
 * @param path Path to open.
 */
function closeFoldedPath(path: string): void {
  const index = unfoldedPaths.value.indexOf(path)
  if (index !== -1) {
    unfoldedPaths.value.splice(index, 1)
  }
}

/**
 * Returns true if a path is opened.
 * @param path Path to open.
 */
function isPathUnfolded(path: string): boolean {
  return unfoldedPaths.value.includes(path)
}

// Event expansion ----------------------------------------------------------------------------------------------------

/**
 * List of expanded event IDs, ie: events whose contents has been expanded for viewing.
 */
const expandedEvents = ref<string[]>([])

/**
 * Can an event be expanded?
 * @param event Event to test.
 */
function isEventExpandable(event: EventStreamItemBase): boolean {
  return !!event.id
}

/**
 * Toggle an event so that the details can be seen (or hidden) in the UI.
 * @param event Event to expand.
 */
function toggleEventExpanded(event: EventStreamItemBase): void {
  const { id } = event
  if (id) {
    const index = expandedEvents.value.indexOf(id)
    if (index === -1) {
      expandedEvents.value.push(id)
    } else {
      expandedEvents.value.splice(index, 1)
    }
  }
}

/**
 * Is an event expanded?
 * @param event Event to expand.
 */
function isEventExpanded(event: EventStreamItemBase): boolean {
  return expandedEvents.value.includes(event.id)
}

// Events -------------------------------------------------------------------------------------------------------------

/**
 * Stream events are decorated for the UI.
 */
interface DecoratedEventStreamItem extends EventStreamItemBase {
  decorations: {
    isPathUnfolded?: boolean
    lineVariant:
      | 'none'
      | 'newest-terminated'
      | 'newest-unterminated'
      | 'oldest-terminated'
      | 'oldest-unterminated'
      | 'both-terminated'
      | 'line'
      | 'skip'
      | 'grouped-event'
      | 'root-event'
      | 'error'
    timeBetweenEvents: number
    isEventExpandable?: boolean
  }
  preHighlightMatch?: boolean
  userHighlightMatch?: boolean
}

/**
 * List of all events before searching, filtering and decorating.
 */
const eventList = shallowRef<EventStreamItemBase[]>([])

const searchedAndUnfoldedEventList = computed(() => {
  return filteredOrHighlightedEventList.value.filter((event) => {
    let path = event.path ?? ''
    if (event.type !== 'Event') {
      path = path.substring(0, path.lastIndexOf('.'))
    }
    if (!path) {
      return true
    }
    let pathToCheck = ''
    let isUnfolded = true
    const pathSegments = path.split('.')
    for (const pathSegment of pathSegments) {
      pathToCheck += pathSegment
      if (!isPathUnfolded(pathToCheck)) {
        isUnfolded = false
        break
      }
      pathToCheck += '.'
    }
    return isUnfolded
  })
})

/**
 * After searching and filtering, events are decorated for the UI.
 */
const decoratedSearchedAndUnfoldedEventList = computed((): DecoratedEventStreamItem[] => {
  let index = 0
  return searchedAndUnfoldedEventList.value.map((event: any) => {
    const decoratedEvent = {
      ...event,
      decorations: {
        timeBetweenEvents: timeBetweenEvents(
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          searchedAndUnfoldedEventList.value[index],
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          searchedAndUnfoldedEventList.value[index + 1]
        ),
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        lineVariant: calculateLineVariant(event),
      },
    }

    // Spare setting of this data.
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    if (isPathUnfolded(event.path)) {
      decoratedEvent.decorations.isPathUnfolded = true
    }
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    if (isEventExpandable(event)) {
      decoratedEvent.decorations.isEventExpandable = true
    }

    ++index
    return decoratedEvent
  })
})

/**
 * Return the time between two events.
 * @param firstEvent First event to consider.
 * @param secondEvent Second event to consider
 * @return Time between events or null if either event is missing or has no timestamp.
 */
function timeBetweenEvents(firstEvent?: EventStreamItemBase, secondEvent?: EventStreamItemBase): Duration | null {
  const firstEventTime = firstEvent?.time
  const secondEventTime = secondEvent?.time
  if (firstEventTime && secondEventTime) {
    const diffTime = DateTime.fromISO(firstEventTime).diff(DateTime.fromISO(secondEventTime))
    return diffTime
  } else {
    return null
  }
}

/**
 * When paused is true, `eventList` does not update when new stream data arrives.
 */
const isPaused = ref(false)

/**
 * True when the stream is paused but we have received new data.
 */
const updateAvailable = ref(false)

/**
 * Remembers the last time that we updated `eventList` from `eventStream`.
 */
const lastUpdated = ref(DateTime.now())

/**
 * Toggle pause state.
 */
function togglePlayPauseState(): void {
  isPaused.value = !isPaused.value
}

let hasReceivedEventStream = false

/**
 * Triggered when new stream data arrives or pause state changes.
 */
watch(
  [preFilteredEvents, isPaused],
  ([eventStream, isPaused], [_previousEventStream, previousIsPaused]) => {
    if (!isPaused) {
      // If data updates but we are paused, then we just don't update `eventList` with the new data. If the user
      // unpauses then this watcher will fire and the data will get updated immediately.
      eventList.value = eventStream?.slice().reverse() ?? []
      updateAvailable.value = false
      lastUpdated.value = DateTime.now()
    } else if (previousIsPaused) {
      // If we are are paused now and we were paused before this watcher fired then it must be `eventStream` that
      // changed. Now we know that we are paused and new data has arrived.
      updateAvailable.value = true
    }

    // When the event stream loads for the first time we need to trigger opening the search string paths - otherwise
    // we don't see the initialSearchString opening the paths.
    if (!hasReceivedEventStream && eventList.value.length > 0) {
      hasReceivedEventStream = true
      openPathsToHighlightedOrFilteredEvents()
    }
  },
  {
    immediate: true,
    deep: false,
  }
)

/**
 * True once stream data has arrived.
 */
const hasContent = computed(() => {
  return !!preFilteredEvents.value
})

// UI -----------------------------------------------------------------------------------------------------------------

/**
 * Model for when the utilities window is open.
 */
const utilsOpen = ref(false)

/**
 * Does the user have the required permissions to view the data?
 */
const hasPermission = computed(() => {
  if (props.permission) {
    return usePermissions().doesHavePermission(props.permission)
  } else return true
})

/**
 * Test used in the badge that follows the card's title.
 */
const badgeText = computed(() => {
  // If the shown list is smaller than the total list then show the number of both. Otherwise just show the total.
  const total = eventList.value.length
  const count = filteredOrHighlightedEventList.value.length

  if (count !== total) {
    return `${count} / ${total}`
  } else {
    return `${total}`
  }
})

const topRightHintLabel = computed(() => {
  const stringSections: string[] = []
  if (props.keywordPreFilters.length > 0 || props.eventTypePreFilters.length > 0) {
    stringSections.push('pre-filtered data')
  }
  if (props.searchPreHighlight) stringSections.push('pre-highlighted data')
  if (internalUtilitiesMode.value === 'filter' && (userSearchActive.value || filtersActive.value)) {
    stringSections.push('filter active')
  } else if (internalUtilitiesMode.value === 'highlight' && (userSearchActive.value || filtersActive.value)) {
    stringSections.push('highlight active')
  }
  return stringSections.join(' & ')
})

/**
 * Which variant (ie: color) to use when rendering the badge.
 */
const badgeVariant = computed(() => {
  return eventList.value.length === 0 ? 'neutral' : 'primary'
})

/**
 * Calculate the UI style to use when drawing the connection line for this event.
 * @param event Event to examine.
 * @return String representing the variant type.
 */
function calculateLineVariant(event: EventStreamItemBase): string {
  if (event.type === 'Event') {
    if (event.typeData.terminatorStyle) return event.typeData.terminatorStyle
    else if (!event.path) return 'none'
    else if (event.path.startsWith('repeat')) return 'none'
    else return 'grouped-event'
  } else if (event.type === 'RepeatedEvents') {
    if (!event.path) return 'error'
    else if (event.path.startsWith('repeat')) return 'none'
    else return isPathUnfolded(event.path) ? 'line' : 'skip'
  } else {
    return 'error'
  }
}

/**
 * Create a `data-test-id` from an event. The Id is based on the event type and is kebab-cased. For example,
 * `"Metaplay.Server.AdminApi.Controllers.PlayerDetailsController+PlayerEventViewed"` becomes
 * `event-player-details-controller-player-event-viewed`.
 * @param event Event to create a data-test-id for.
 * @return `data-test-id` string or undefined if the event has no type.
 */
function dataTestIdFromEvent(event: EventStreamItemBase): string | undefined {
  const fullType = event.typeData.sourceData?.payload?.$type as string
  if (fullType) {
    const eventType = fullType.split('.').pop()
    if (eventType) {
      const sanitizedEventType = eventType.replace(/[^a-zA-Z0-9]/g, '-')
      return `event-${sanitizedEventType}`
    }
  }
  return undefined
}
</script>

<style scoped>
.group-open .fa-angle-right {
  transform: rotateZ(90deg);
}

.group-element-stripes .group:nth-child(even) {
  background: #f7f7f7;
}

.group-element-stripes .presearch-highlight:nth-child(even) {
  background: #fde8aa;
}

.group-element-stripes .presearch-highlight:nth-child(odd) {
  background: #fdefc3;
}

.group-element-stripes .user-highlight:nth-child(even) {
  background: #ffe4cc;
}

.group-element-stripes .user-highlight:nth-child(odd) {
  background: #ffca99;
}

.group-element-borders .group {
  border-bottom: solid 1px var(--metaplay-grey-light);
}

.group-element-borders .group:last-child {
  border-bottom: none;
}

.three-column-layout {
  display: grid;
  grid-template-columns: 5rem 1.7rem 1fr;
  grid-template-rows: 1fr;
}

.two-column-layout {
  display: grid;
  grid-template-columns: 5rem 1fr;
  grid-template-rows: 1fr;
}

.collapse-transition-enter-active {
  transition: all 0.25s ease-out;
}

.collapse-transition-enter-from,
.collapse-transition-leave-to {
  height: 0;
  opacity: 0;
}
</style>
