<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!--
This is a reusable utility component for easily rendering nice looking lists in the dashboard.
All lists will come with sensible defaults like paging and a rudimentary default renderer.

You can optionally configure features like searching, sorting and filtering in the utilities menu.

Have a look at the Vue 'props' object to see the various configurations that can be used to customise the look and feel for various use-cases!
-->

<template lang="pug">
b-card(
  :class="{ 'bg-light': !hasContent || !hasPermission }"
  style="min-height: 12rem"
  no-body
  class="shadow-sm"
  )
  //- Header.
  b-row(
    align-h="between"
    align-v="center"
    no-gutters
    :class="{ 'table-row-link': hasContent && showUtilitiesMenu }"
    class="card-manual-title-padding"
    @click="toggleUtilitiesMenu"
    )
    MTooltip(
      :content="tooltip"
      no-underline
      )
      b-card-title(class="d-flex align-items-center")
        fa-icon(
          v-if="icon"
          :icon="icon"
          class="mr-2"
          )
        | {{ title }}
        MBadge(
          v-if="itemList && hasPermission"
          shape="pill"
          :variant="itemCountClass"
          class="tw-ml-1"
          ) {{ itemCountText }}
    span(style="margin-top: -1rem")
      small(
        v-if="filtersActive && sortActive"
        class="text-muted mr-2 tw-italic"
        ) filtered & sorted
      small(
        v-else-if="filtersActive"
        class="text-muted mr-2 tw-italic"
        ) filtered
      small(
        v-else-if="sortActive"
        class="text-muted mr-2 tw-italic"
        ) sorted
      MIconButton(
        v-if="showUtilitiesMenu"
        :disabled-tooltip="!hasContent ? 'There is nothing to search, filter or sort.' : undefined"
        :variant="filtersOrSortActive ? 'primary' : 'neutral'"
        aria-label="Toggle the utilities menu"
        @click="toggleUtilitiesMenu"
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

  //- Permission handling.
  div(
    v-if="!hasPermission"
    class="mb-4 pt-4 small text-muted tw-text-center"
    ) You need the #[MBadge {{ permission }}] permission to view this card.

  //- Loading.
  div(
    v-else-if="!itemList"
    class="card-manual-content-padding pt-2 tw-w-full"
    )
    b-skeleton(width="85%")
    b-skeleton(width="55%")
    b-skeleton(width="70%")

    b-skeleton(
      width="80%"
      class="tw-mt-4"
      )
    b-skeleton(width="65%")

  //- Main body.
  div(
    v-else-if="hasContent"
    class="d-flex flex-column justify-content-between h-100"
    )
    //- Sticks to the top.
    div
      //- Utilities menu.
      MTransitionCollapse
        div(
          v-if="showUtilitiesMenu && utilitiesMenuExpanded"
          class="bg-light card-manual-content-padding border-top border-bottom tw-w-full"
          )
          div(class="mb-3 tw-mt-4")
            div(class="mb-2")
              div(class="tw-mb-1 tw-text-sm tw-font-semibold") Search
              MInputText(
                v-if="searchFields"
                :model-value="searchString"
                :placeholder="searchPlaceholder"
                :variant="searchActive ? 'success' : 'default'"
                :debounce="200"
                showClearButton
                @update:model-value="searchString = $event"
                )
              span(
                v-else
                class="tw-relative tw-bottom-1 tw-text-xs+ tw-text-neutral-500"
                ) Search not available for this card.
            b-row
              b-col
                div(class="tw-mb-1 tw-text-sm tw-font-semibold") Filter
                template(v-if="filterSets")
                  MInputMultiSelectCheckbox(
                    v-for="(filterSet, filterSetIndex) in filterSets"
                    :key="filterSetIndex"
                    :model-value="selectedFilters"
                    :options="getFilterSetOptions(filterSet)"
                    size="small"
                    :class="['tw-mb-0.5', { 'tw-text-neutral-500': !isFilterSetActive(filterSet, selectedFilters) }]"
                    @update:model-value="selectedFilters = $event"
                    )
                span(
                  v-else
                  class="tw-relative tw-bottom-1 tw-text-xs+ tw-text-neutral-500"
                  ) Filters not available for this card.

              b-col
                div(class="tw-mb-1 tw-text-sm tw-font-semibold") Sort
                MInputSingleSelectRadio(
                  v-if="sortOptions"
                  :model-value="sortOption"
                  :options="radioSortOptions"
                  size="small"
                  @update:model-value="sortOption = $event"
                  )
                span(
                  v-else
                  class="tw-relative tw-bottom-1 tw-text-xs+ tw-text-neutral-500"
                  ) Sorting not available for this card.

      //- Description.
      div(
        v-if="description"
        class="small card-manual-content-padding"
        ) {{ description }}
      div(class="small card-manual-content-padding")
        slot(name="description")

      //- Pause stream.
      div(
        v-if="allowPausing"
        class="tw-mt-1 tw-space-x-1 tw-pb-2 tw-text-center tw-text-xs+ tw-text-neutral-500"
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

      //- List of items.
      div(v-if="currentPageItemList.length > 0")
        MList(
          v-if="listLayout === 'list'"
          data-testid="meta-list-card-list-container"
          )
          //- Render actual items.
          div(
            v-for="(item, index) in currentPageItemList"
            :key="getItemKey ? getItemKey(item) : index"
            ref="itemsElements"
            )
            slot(
              name="item-card"
              v-bind:item="item"
              v-bind:index="getItemIndex(item)"
              ) {{ item }}
          //- Render empty slots if needed.
          template(v-if="currentPage > 0")
            div(
              v-for="index in pageSize - currentPageItemList.length"
              :key="uniqueKeyPrefix + index.toString()"
              :style="`min-height: ${emptyListItemHeight}px`"
              )

        //- Flex layout.
        div(
          v-else-if="listLayout === 'flex'"
          class="d-flex flex-wrap card-manual-content-padding tw-my-4"
          )
          div(
            v-for="(item, index) in currentPageItemList"
            :key="index"
            class="mr-2 mb-2"
            )
            slot(
              name="item-card"
              v-bind:item="item"
              v-bind:index="getItemIndex(item)"
              )
              pre(style="font-size: 0.7rem") {{ item }}

      //- Empty list after filtering.
      b-row(
        v-else
        align-h="center"
        no-gutters
        class="mb-4 pt-4 tw-text-center"
        )
        span(
          class="small text-muted"
          data-testid="meta-list-card-no-results-message"
          ) {{ noResultsMessage }}

    //- Sticks to the bottom.
    div(
      v-if="moreInfoUri || showPagingControls"
      class="tw-mb-4 tw-mt-4"
      )
      //- "View more" link.
      div(
        v-if="moreInfoUri"
        class="mb-2 tw-w-full tw-text-center"
        )
        span(class="small text-muted")
          | View more items at the&nbsp;
          MTextButton(
            :to="moreInfoUri"
            :permission="moreInfoPermission"
            ) {{ moreInfoLabel }} page
          | !

      //- Pagination controls.
      div(
        v-if="showPagingControls"
        class="tw-mb-1 tw-text-center"
        )
        b-button-group(size="sm")
          b-button(
            :disabled="currentPage == 0"
            @click="gotoPageAbsolute(0)"
            )
            fa-icon(:icon="['fas', 'fast-backward']")
          b-button(
            :disabled="currentPage == 0"
            @click="gotoPageRelative(-1)"
            )
            fa-icon(icon="backward")
          div(
            style="padding-top: 0.3rem; min-width: 4rem"
            class="px-3 bg-secondary text-light pagination-shadow"
            )
            small {{ currentPage + 1 }} / {{ totalPageCount }}
          b-button(
            :disabled="currentPage == totalPageCount - 1"
            @click="gotoPageRelative(+1)"
            )
            fa-icon(icon="forward")
          b-button(
            :disabled="currentPage == totalPageCount - 1"
            @click="gotoPageAbsolute(totalPageCount - 1)"
            )
            fa-icon(:icon="['fas', 'fast-forward']")

  //- Empty.
  div(v-else)
    //- Description.
    div(
      v-if="description"
      class="small card-manual-content-padding"
      ) {{ description }}
    div(class="mb-2")
      slot(name="description")
    div(class="pb-4 my-5 text-muted tw-px-4 tw-text-center") {{ emptyMessage }}
</template>

<script lang="ts" setup generic="T">
import { BButtonGroup } from 'bootstrap-vue'
import { clamp, clone, isEqual, meanBy } from 'lodash-es'
import { DateTime } from 'luxon'
import { computed, onMounted, ref, watch } from 'vue'

import {
  MBadge,
  MInputMultiSelectCheckbox,
  MInputSingleSelectRadio,
  MInputText,
  MList,
  MTextButton,
  MTooltip,
  MTransitionCollapse,
  MIconButton,
  type Variant,
  usePermissions,
} from '@metaplay/meta-ui-next'
import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import type { MetaListFilterOption, MetaListFilterSet, MetaListSortOption } from '../utils/metaListUtils'
import { MetaListSortDirection } from '../utils/metaListUtils'
import { resolve } from '../utils/utils'

// Props ------------------------

const props = withDefaults(
  defineProps<{
    /**
     * Title of the card.
     */
    title: string
    /**
     * Optional: Font-Awesome icon shown before the card's title.
     * @example 'table'
     */
    icon?: string
    /**
     * Optional: Tooltip to be shown when hovering over the card's title.
     */
    tooltip?: string
    /**
     * Optional: Use red styling for the card.
     */
    dangerous?: boolean
    /**
     * Array of items to be shown.
     */
    itemList?: T[]
    /**
     * By default, the items in the list are uniquely keyed by their index. This is adequate in most cases, but sometimes
     * the items can be reordered and sometimes this causes rendering issues. For example, items that can be
     * collapsed/expanded will get confused if itemList is changed while items are expanded.
     * @example (item) => item.id
     */
    getItemKey?: (item: T) => string
    /**
     * Optional: Maximum amount of list items shown on one page. Defaults 8.
     */
    pageSize?: number
    /**
     * Optional: Array of list item property names that can be searched.
     * @example: ['id', 'contents.title']
     */
    searchFields?: string[]
    /**
     * Optional: Placeholder text for the search box.
     */
    searchPlaceholder?: string
    /**
     * Optional: List of filter sets that can be applied.
     */
    filterSets?: MetaListFilterSet[]
    /**
     * Optional: List of sort types that can be applied.
     */
    sortOptions?: MetaListSortOption[]
    /**
     * Optional: Index of sort option to choose by default.
     */
    defaultSortOption?: number
    /**
     * Optional: Message to be shown when itemList is null or empty.
     */
    emptyMessage?: string
    /**
     * Optional: Message to be shown when there are no search results.
     */
    noResultsMessage?: string
    /**
     * Optional: URL to show a call-to-action link at the bottom of the card.
     */
    moreInfoUri?: string
    /**
     * Optional: Custom word to use in the call-to-action link at the bottom of the card.
     */
    moreInfoLabel?: string
    /**
     * Optional: Permissions needed by the call-to-action link at the bottom of the card.
     */
    moreInfoPermission?: string
    /**
     * Optional: Alternative layout for the list. Default is `list`.
     */
    listLayout?: 'flex' | 'list'
    /**
     * Optional: Apply pointer cursor style to the list.
     */
    clickable?: boolean
    /**
     * Optional: Permission needed to view the card's data.
     */
    permission?: string
    /**
     * Optional: Header subtitle for the card.
     */
    description?: string
    /**
     * Optional: Show the controls to pause and resume the event stream.
     */
    allowPausing?: boolean
  }>(),
  {
    icon: undefined,
    tooltip: undefined,
    itemList: undefined,
    getItemKey: undefined,
    pageSize: 8,
    searchFields: undefined,
    searchPlaceholder: 'Type your search here...',
    filterSets: undefined,
    sortOptions: undefined,
    defaultSortOption: 0,
    emptyMessage: 'Nothing to list!',
    noResultsMessage: 'No items found. Try a different search string or filters? 🤔',
    moreInfoUri: undefined,
    moreInfoLabel: 'relevant',
    moreInfoPermission: undefined,
    listLayout: 'list',
    clickable: false,
    permission: undefined,
    description: undefined,
    allowPausing: false,
  }
)

// Setup and utils ------------------------

const permissions = usePermissions()

/**
 * Allows us to prefix each item on the page with a unique key to prevent clashes between multiple instances of
 * MetaListCard on the same page.
 */
const uniqueKeyPrefix = makeIntoUniqueKey

onMounted(() => {
  // Calculate empty line height once the card has loaded.
  setTimeout(updateEmptyLineHeight, 0)

  // Check that filters hae unique keys.
  if (props.filterSets) {
    const allFilterKeys = props.filterSets.map((filterSet) => filterSet.key)
    const uniqueFilterKeys = Array.from(new Set(allFilterKeys))
    if (allFilterKeys.length !== uniqueFilterKeys.length) {
      console.warn(`Duplicate filter keys found in MetaListCard '${props.title}'`)
    }
  }

  // Enable initially active filter options.
  if (props.filterSets) {
    props.filterSets.forEach((filterSet) => {
      filterSet.filterOptions.forEach((filterOption) => {
        if (filterOption.initiallyActive) {
          selectedFilters.value.push(makeFilterKey(filterSet, filterOption))
        }
      })
    })
  }

  // Initial sort option copied from props.
  sortOption.value = props.defaultSortOption

  // When the card loads for the first time we need to copy across the data.
  deferredItemList.value = clone(props.itemList) ?? []
  updateAvailable.value = false
  lastUpdated.value = DateTime.now()
})

/**
 * Store a reference to all items. Used to calculate empty list item height.
 */
const itemsElements = ref<HTMLDivElement[]>([])

/**
 * Height to render empty items at, as calculated by `updateEmptyLineHeight`.
 */
const emptyListItemHeight = ref(10)

/**
 * Calculate a sensible default for the empty list items. Items with no content are set to this height, which makes
 * the layout more pleasing.
 */
function updateEmptyLineHeight(): void {
  // Set to average.
  const newHeight = meanBy(itemsElements.value, (ref) => ref.clientHeight)

  // Only grow, don't shrink - this makes the empty list height behave more consistently.
  if (newHeight > emptyListItemHeight.value) {
    emptyListItemHeight.value = newHeight
  }
}

/**
 * Does user have permission to view the card?
 */
const hasPermission = computed((): boolean => {
  if (props.permission) return permissions.doesHavePermission(props.permission)
  else return true
})

/**
 * Is there any content available at all, ie: does `deferredItemList` contain any items?
 */
const hasContent = computed((): boolean => {
  return (deferredItemList.value?.length ?? 0) > 0
})

// Utilities menu ------------------------

/**
 * Only need to show the utilities menu if we have something to show in it (ie: search, sort of filtering).
 */
const showUtilitiesMenu = computed((): boolean => {
  return (
    (props.searchFields && props.searchFields.length > 0) ??
    (props.sortOptions && props.sortOptions.length > 1) ??
    !!(props.filterSets && props.filterSets.length > 0)
  )
})

/**
 * Is the utilities menu expanded or not?
 */
const utilitiesMenuExpanded = ref(false)

/**
 * Expand or collapse the utilities menu.
 */
function toggleUtilitiesMenu(): void {
  utilitiesMenuExpanded.value = !utilitiesMenuExpanded.value
}

// Searching, filtering and sorting ------------------------

/**
 * Search string entered by user.
 */
const searchString = ref('')

/**
 * Utility function to create a list of options for the filter set.
 */
function getFilterSetOptions(filterSet: MetaListFilterSet): Array<{ label: string; value: string }> {
  return filterSet.filterOptions.map((filter) => {
    const key = makeFilterKey(filterSet, filter)
    return {
      label: `${filter.displayName} (${getSearchFilteredItemList([key]).length})`,
      value: key,
    }
  })
}

/**
 * Array of active filters.
 */
const selectedFilters = ref<string[]>([])

/**
 * Index into `sortOptions` for currently selected sort option.
 */
const sortOption = ref(0)

/**
 * Calculate a list of all items after searching, filtering and sorting. This is the list of all items, and it is
 * further cropped by `currentPageItemList` before being rendered.
 */
const searchedFilteredSortedItemList = computed((): T[] => {
  let items = getSearchFilteredItemList(selectedFilters.value)
  if (props.sortOptions) {
    const selectedSortOption = props.sortOptions[sortOption.value]
    const sortKey = selectedSortOption.sortKey
    let getSortKey: (a: any) => string | number
    if (sortKey === null) {
      // Unsorted.
      getSortKey = (a: any): number => {
        return 0
      }
    } else if (typeof sortKey === 'string') {
      // Sort by named field.
      getSortKey = (a: any): string => {
        let key = sortKey ? resolve(a, sortKey) : a
        if (typeof key === 'string') key = key.toLowerCase()
        return key
      }
    } else if (typeof sortKey === 'function') {
      // Sort by function.
      getSortKey = (a: any): string | number => {
        let key = sortKey(a)
        if (typeof key === 'string') key = key.toLowerCase()
        return key
      }
    }
    const sortDirection = selectedSortOption.direction
    items = [...items].sort((a, b) => {
      let order = 0
      const valueA = getSortKey(a)
      const valueB = getSortKey(b)
      if (valueA > valueB) {
        order = +1
      } else if (valueA < valueB) {
        order = -1
      }
      order = order * sortDirection
      return order
    })
  }
  return items
})

/**
 * Create details for the sort options radio group.
 */
const radioSortOptions = computed((): Array<{ label: string; value: number }> => {
  if (props.sortOptions) {
    return props.sortOptions.map((item, index) => {
      return {
        value: index,
        label:
          item.displayName +
          (item.direction === MetaListSortDirection.Descending
            ? ' ↓'
            : item.direction === MetaListSortDirection.Ascending
              ? ' ↑'
              : ''),
      }
    })
  } else {
    return []
  }
})

/**
 * Is search active?
 */
const searchActive = computed((): boolean => {
  return searchString.value.length > 0
})

/**
 * Are any filters active?
 */
const filtersActive = computed((): boolean => {
  return selectedFilters.value.length > 0 || searchActive.value
})

/**
 * Is a sort option active? If we have `sortOptions` then one will always be active.
 */
const sortActive = computed((): boolean => {
  return hasContent.value && props.sortOptions !== undefined
})

/**
 * Filters or sort active?
 */
const filtersOrSortActive = computed((): boolean => {
  return filtersActive.value || sortActive.value
})

/**
 * Create a unique id string for a given filter.
 * @param filterSet
 * @param filter
 */
function makeFilterKey(filterSet: MetaListFilterSet, filter: MetaListFilterOption): string {
  return `${filterSet.key}-${filter.displayName}`
}

/**
 * Determines whether the given filter is currently active or not.
 * @param filterSet
 * @param selectedFilters
 */
function isFilterSetActive(filterSet: MetaListFilterSet, selectedFilters: string[]): boolean {
  return filterSet.filterOptions.some((filter) => selectedFilters.includes(makeFilterKey(filterSet, filter)))
}

/**
 * Return a list of items after searching and filtering.
 * @param selectedFilters
 */
function getSearchFilteredItemList(selectedFilters: string[]): T[] {
  let items = deferredItemList.value ?? []

  // Filter first..
  const filterSets = props.filterSets
  if (filterSets) {
    items = items.filter((item) => {
      let passAll = true
      filterSets.forEach((filterSet) => {
        if (isFilterSetActive(filterSet, selectedFilters)) {
          let passFilter = false
          filterSet.filterOptions.forEach((filter) => {
            if (selectedFilters.includes(makeFilterKey(filterSet, filter)) && filter.filterFn(item as object)) {
              passFilter = true
            }
          })

          if (!passFilter) {
            passAll = false
          }
        }
      })
      return passAll
    })
  }

  // ..then search
  const searchFields = props.searchFields
  if (searchActive.value && searchFields) {
    const searchStringLower = searchString.value.toLowerCase()
    items = items.filter((item) => {
      return searchFields.some((searchField) => {
        if (searchField) {
          const value = resolve(item, searchField)
          if (value === undefined) {
            if (!warnedMissingSearchFields.value.includes(searchField)) {
              // Warn the first time we try to search on a non-existent search field.

              console.warn(
                `Could not find search field '${searchField}' inside item for meta-list-card '${props.title}'`
              )
              warnedMissingSearchFields.value.push(searchField)
            }
            return false
          } else {
            const valueLower = String(value).toLowerCase()
            return valueLower.includes(searchStringLower)
          }
        } else {
          const valueLower = String(item).toLowerCase()
          return valueLower.includes(searchStringLower)
        }
      })
    })
  }

  return items
}

/**
 * Cache of search fields that we have warned about. We keep this so that we can only warn about each missing search
 * field once.
 */
const warnedMissingSearchFields = ref<string[]>([])

/**
 * Item count to show in the card's header.
 */
const itemCountText = computed((): string => {
  const total = deferredItemList.value?.length ?? 0
  if (searchActive.value || filtersActive.value) {
    const current = searchedFilteredSortedItemList.value.length || 0
    return `${current} / ${total}`
  } else {
    return `${total}`
  }
})

/**
 * Highlight style for the item count.
 */
const itemCountClass = computed((): Variant => {
  if ((deferredItemList.value?.length ?? 0) === 0) {
    return 'neutral'
  } else {
    return props.dangerous ? 'danger' : 'primary'
  }
})

// Paging ------------------------

/**
 * This is the page that we have chosen to view. Sometimes this page ends up being outside the valid page range (eg:
 * when a search/filter changes the total number of items/pages) but we keep track of the original number to prevent
 * surprising page changes as searches/filters are changed.
 */
const desiredCurrentPage = ref(0)

/**
 * Returns one page of items. This is the data that actually gets rendered.
 */
const currentPageItemList = computed((): T[] => {
  if (showPagingControls.value) {
    const start = currentPage.value * props.pageSize
    return searchedFilteredSortedItemList.value.slice(start, start + props.pageSize)
  } else {
    return searchedFilteredSortedItemList.value
  }
})

/**
 * Only show the paging controls if we have >1 page of items.
 */
const showPagingControls = computed((): boolean => {
  return totalPageCount.value > 1
})

/**
 * How many pages we would need to show all of the items.
 */
const totalPageCount = computed((): number => {
  const itemCount = searchedFilteredSortedItemList.value.length
  return Math.ceil(itemCount / props.pageSize)
})

/**
 * The current page that we are viewing. Takes `desiredCurrentPage` and clamps it to a valid range.
 */
const currentPage = computed((): number => {
  return Math.min(desiredCurrentPage.value, totalPageCount.value - 1)
})

/**
 * Jump directly to a specific page. Input is clamped to valid range.
 * @param page
 */
function gotoPageAbsolute(page: number): void {
  desiredCurrentPage.value = clamp(page, 0, totalPageCount.value - 1)
  updateEmptyLineHeight()
}

/**
 * Jump to a relative page.
 * @param offset Offset to current page, eg +1 or -1. Input is clamped to valid range.
 */
function gotoPageRelative(offset: number): void {
  desiredCurrentPage.value = clamp(currentPage.value + offset, 0, totalPageCount.value - 1)
  updateEmptyLineHeight()
}

// Pausing ------------------------

/**
 * When paused is true, `deferredItemList` does not update when the 'itemList' prop changes.
 */
const isPaused = ref(false)

/**
 * True when the stream is paused but we have received new data.
 */
const updateAvailable = ref(false)

/**
 * Remembers the last time that we updated `deferredItemList` from `itemList`.
 */
const lastUpdated = ref(DateTime.now())

/**
 * Toggle pause state.
 */
function togglePlayPauseState(): void {
  isPaused.value = !isPaused.value
}

/**
 * To handle pausing, we don't deal with 'itemList' directly. Instead we copy that data to `deferredItemList` when it
 * changes. If we are paused then those updates are simply not copied.
 */
const deferredItemList = ref<T[]>()

/**
 * Watch `itemList` and `isPaused` so that we can react to changes in them.
 */
watch(
  [(): T[] | undefined => props.itemList, isPaused],
  ([itemList, isPaused], [_previousItemList, previousIsPaused]) => {
    if (!isPaused) {
      // If data updates but we are paused, then we just don't update `itemList` with the new data. If the user
      // unpauses then this watcher will fire and the data will get updated immediately.
      deferredItemList.value = clone(itemList) ?? []
      updateAvailable.value = false
      lastUpdated.value = DateTime.now()
    } else if (previousIsPaused) {
      // If we are are paused now and we were paused before this watcher fired then it must be `itemList` that
      // changed. Now we know that we are paused and new data has arrived.
      if (!isEqual(deferredItemList.value, itemList)) {
        updateAvailable.value = true
      }
    }
  }
)

/**
 * Return index of given item in the list of items.
 * @param item Item to look up.
 * @returns Index of item in list, or -1 if not found.
 */
function getItemIndex(item: T): number {
  const index = deferredItemList.value?.findIndex((i) => isEqual(i, item)) ?? -1
  console.assert(index >= 0, 'Cannot find index for item that cannot be found in the list.')
  return index
}
</script>

<style scoped>
.pagination-shadow {
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.15),
    inset 0 -4px 1px #494949;
}
</style>
