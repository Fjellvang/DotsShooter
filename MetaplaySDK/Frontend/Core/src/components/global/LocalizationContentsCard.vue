<template lang="pug">
MCard(
  title="Localization Contents"
  :badgeVariant="badgeVariant"
  :badge="badgeValue"
  :isLoading="!localizationTableData"
  data-testid="localization-contents-card"
  )
  //- Header.
  template(#icon)
    fa-icon(icon="language")

  template(#header-right)
    MInputText(
      :model-value="searchString"
      :placeholder="'Search...'"
      @update:modelValue="updateSearchString"
      )
  template(#subtitle)
    div(
      class="tw-mt-2 tw-flex tw-gap-x-10 tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-px-5 tw-py-2 tw-text-neutral-600"
      )
      div
        MInputCheckbox(
          :model-value="showMissingValuesOnly"
          label="Filter"
          name="Show only keys with missing values"
          :variant="totalMissingLocalizationValues > 0 ? 'danger' : 'default'"
          size="small"
          @update:modelValue="showMissingValuesOnly = $event"
          ) Keys with missing values ({{ totalMissingLocalizationValues }})
      div
        MInputSingleSelectRadio(
          :model-value="sortKeyOption"
          label="Sort"
          :options="sortKeyOptions"
          size="small"
          @update:model-value="sortKeyOption = $event"
          )
  div(class="tw-overflow-x-auto")
    table(
      v-if="!(searchString && filteredItemList.length === 0) && (!showMissingValuesOnly || totalMissingLocalizationValues > 0)"
      class="tw-w-full"
      )
      thead
        tr(class="tw-border tw-border-neutral-200 tw-text-xs+")
          th(class="tw-pl-2") Localization Key
          th(
            v-for="languageId in languageIds"
            :key="languageId"
            class="tw-border-l tw-border-neutral-200 tw-pl-2"
            ) {{ languageIdToName(languageId) }}
      tbody
        //- Library is empty.
        tr(
          v-if="localizationTableData?.table.length === 0"
          class="tw-border-x tw-border-b tw-border-neutral-200"
          )
          td(
            v-for="col in localizationTableData.table.length + 2"
            :class="['tw-px-2 tw-text-xs tw-italic tw-text-neutral-400 tw-border-neutral-200 tw-border-t', { 'tw-border-l': col > 1 }]"
            ) Library is empty.
        tr(
          v-else
          v-for="localizationEntry in currentPageItemList"
          :key="localizationEntry.title"
          :class="['tw-text-xs tw-border-neutral-200 tw-border odd:tw-bg-neutral-100 tw-align-top', { 'hover:tw-cursor-pointer hover:tw-bg-neutral-300 active:tw-bg-neutral-400': localizationEntry.isAnyLanguageValueTruncated }]"
          @click="onLocalizationEntryClicked(localizationEntry.title)"
          )
          //- Row has values that are too long -> show truncated elements.
          template(v-if="localizationEntry.isAnyLanguageValueTruncated")
            td(
              :class="['tw-pl-2 tw-py-1 tw-font-mono tw-whitespace-nowrap', { 'tw-bg-orange-200': containsSearchTerm(localizationEntry.title) }]"
              )
              div(style="min-width: 14rem")
                fa-icon(
                  icon="angle-right"
                  :class="['tw-mr-1', { 'tw-rotate-90': expandedLocalizationKeys[localizationEntry.title] }]"
                  )
                span {{ localizationEntry.title }}
                fa-icon(
                  v-if="localizationEntry.isAnyLanguageValueMissing"
                  icon="circle-exclamation"
                  class="tw-relative -tw-bottom-[1px] tw-ml-1 tw-text-red-500"
                  )
            td(
              v-for="languageId in languageIds"
              :key="languageId"
              :class="['tw-border-l tw-border-neutral-200 tw-pl-2 tw-py-1.5', { 'tw-bg-orange-200': containsSearchTerm(localizationEntry.values[languageId]) }]"
              )
              div(
                :class="['tw-w-56 tw-break-words', { 'tw-truncate': !expandedLocalizationKeys[localizationEntry.title], 'tw-whitespace-pre-wrap': expandedLocalizationKeys[localizationEntry.title], 'tw-text-red-500 tw-italic': localizationEntry.values[languageId].startsWith('#missing') }]"
                ) {{ localizationEntry.values[languageId].startsWith('#missing') ? 'Missing value' : localizationEntry.values[languageId] }}
              //- Alternative render style for reference.
              //- div(:class="['tw-w-56 tw-break-words', { 'tw-line-clamp-3': !expandedLocalizationKeys[localizationEntry.title], 'tw-whitespace-pre-wrap': expandedLocalizationKeys[localizationEntry.title], 'tw-text-red-500 tw-italic': !localizationEntry.values[languageId] }]") {{ localizationEntry.values[languageId] ? localizationEntry.values[languageId] : 'Missing value' }}
          template(v-else)
            td(
              :class="['tw-pl-2 tw-py-1 tw-font-mono tw-whitespace-nowrap', { 'tw-bg-orange-200': containsSearchTerm(localizationEntry.title) }]"
              )
              div(style="min-width: 14rem")
                span {{ localizationEntry.title }}
                fa-icon(
                  v-if="localizationEntry.isAnyLanguageValueMissing"
                  icon="circle-exclamation"
                  class="tw-relative -tw-bottom-[1px] tw-ml-1 tw-text-red-500"
                  )
            td(
              v-for="languageId in languageIds"
              :key="languageId"
              :class="['tw-border-l tw-border-neutral-200 tw-pl-2 tw-py-1', { 'tw-bg-orange-200': containsSearchTerm(localizationEntry.values[languageId]) }]"
              )
              div(
                :class="['tw-w-56 tw-overflow-x-hidden', { 'tw-text-red-500 tw-italic': localizationEntry.values[languageId].startsWith('#missing') }]"
                ) {{ localizationEntry.values[languageId].startsWith('#missing') ? 'Missing value' : localizationEntry.values[languageId] }}
    div(
      v-else
      class="tw-italic tw-text-neutral-500"
      ) No results. Try a different search?

  //- Pagination controls.
  //- TODO: remove bootstrap styles in favor of tailwind like the outmost div
  div(
    v-if="showPagingControls"
    class="tw-mt-4 tw-text-center"
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
</template>

<script lang="ts" setup>
import { BButtonGroup, BButton } from 'bootstrap-vue'
import { clamp, debounce } from 'lodash-es'
import { computed, ref, watch } from 'vue'

import { MCard, MInputText, MInputCheckbox, MInputSingleSelectRadio } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type { LocalizationTable, LocalizationTableItem } from '../../localizationServerTypes'
import { getGameDataSubscriptionOptions } from '../../subscription_options/general'
import { getSingleLocalizationTableSubscriptionOptions } from '../../subscription_options/localization'

const props = defineProps<{ localizationId: string }>()

interface DecoratedLocalizationTableItem extends LocalizationTableItem {
  /**
   * Indicates true if the localization entry has a value that is missing.
   */
  isAnyLanguageValueMissing?: boolean
  /**
   * Indicates true if the localization entry has a value that is too long to display in the table.
   */
  isAnyLanguageValueTruncated?: boolean
}

/**
 * Initializing a collator object for alphanumeric sorting of localization keys later on.
 */
const collator = new Intl.Collator('en', { numeric: true })

/**
 * Search string entered by user.
 */
const searchString = ref<string>()

// Data ---------------------------------------------------------------------------------------------------------------

const { data: localizationTableData } = useSubscription(
  getSingleLocalizationTableSubscriptionOptions(props.localizationId)
)

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())

const languageIds = computed(() => localizationTableData.value?.languageIds)

const languageIdToName = function (languageId: string): string {
  if (gameData.value?.gameConfig.Languages) {
    const language: any = Object.values(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      gameData.value.gameConfig.Languages
    ).find((language: any) => language.languageId === languageId)
    if (language) {
      return `${language.displayName} (${language.languageId})`
    }
  }
  return languageId
}

/**
 * A computed list of all items, each enhanced with a flag indicating if any language value is missing.
 */
const decoratedItemList = computed((): DecoratedLocalizationTableItem[] => {
  return localizationTableData.value
    ? localizationTableData.value.table.map((item) => ({
        ...item,
        isAnyLanguageValueMissing:
          languageIds.value?.some(
            (languageId) =>
              item.values[languageId] === undefined ||
              item.values[languageId] === '' ||
              item.values[languageId].startsWith('#missing')
          ) ?? false,
      }))
    : []
})

/**
 * Value for missing values toggle. When true, filters the displayed items to only those with missing language values.
 */
const showMissingValuesOnly = ref(false)

/**
 * Total number of missing localization values.
 */
const totalMissingLocalizationValues = computed(() => {
  return filteredItemList.value.filter((item) => item.isAnyLanguageValueMissing).length
})

/**
 * Default sort option.
 */
const sortKeyOption = ref('unsorted')

/**
 * Options for sorting the displayed items.
 */
const sortKeyOptions = [
  { label: 'Unsorted', value: 'unsorted' },
  { label: 'Keys alphabetically ↑', value: 'alphabeticalAsc' },
  { label: 'Keys alphabetically ↓', value: 'alphabeticalDesc' },
]

// Search -------------------------------------------------------------------------------------------------------------

/**
 * Filters the displayed item list based on the search string and other filter/sort options.
 */
const filteredItemList = computed((): DecoratedLocalizationTableItem[] => {
  let returnedItemList = [...decoratedItemList.value]

  if (showMissingValuesOnly.value) {
    returnedItemList = returnedItemList.filter((item) => item.isAnyLanguageValueMissing)
  }

  if (lowerCaseSearchString.value && lowerCaseSearchString.value.length > 0) {
    returnedItemList = returnedItemList.filter((item) => {
      if (containsSearchTerm(item.title)) return true
      if (Object.values(item.values).some((value: string) => containsSearchTerm(value))) {
        return true
      }
      return false
    })
  }

  if (sortKeyOption.value === 'alphabeticalAsc') {
    returnedItemList = returnedItemList.slice().sort((a, b) => collator.compare(a.title, b.title))
  } else if (sortKeyOption.value === 'alphabeticalDesc') {
    returnedItemList = returnedItemList.slice().sort((a, b) => collator.compare(b.title, a.title))
  }

  return returnedItemList
})

/**
 * Checks if the provided value contains the search string.
 */
function containsSearchTerm(value: string): boolean {
  if (!lowerCaseSearchString.value) return false
  return value.toLowerCase().includes(lowerCaseSearchString.value)
}

/**
 * Updates the search string and triggers a debounced item filtering.
 * @param input - The new search string.
 */
function updateSearchString(input: string | undefined): void {
  searchString.value = input
  debounceSearchStringToLowerCase()
}

/**
 * The search string in lower case.
 */
const lowerCaseSearchString = ref<string>()

/**
 * Debounced function to update the lower case search string.
 */
const debounceSearchStringToLowerCase = debounce(() => {
  lowerCaseSearchString.value = searchString.value?.toLowerCase()
}, 200)

// UI -----------------------------------------------------------------------------------------------------------------

/**
 * Badge variant for the status badge.
 */
const badgeVariant = computed(() => {
  return localizationTableData.value?.info.bestEffortStatus === 'Success' ? 'primary' : 'danger'
})

/**
 * When unfiltered, shows total number of rows, and when filtered, shows number of currently visible rows.
 */
const badgeValue = computed(() => {
  const tableLength = localizationTableData.value?.table.length
  if (localizationTableData.value?.info.bestEffortStatus === 'Success') {
    if (filteredItemList.value.length === tableLength) {
      return tableLength
    } else {
      return filteredItemList.value.length + ' / ' + tableLength
    }
  } else {
    return 'Error'
  }
})

/**
 * List of localization keys that are expanded (ie: showing the full text of all values).
 */
const expandedLocalizationKeys = ref<Record<string, boolean>>({})

/**
 * Toggle the expansion state of a localization key.
 */
function onLocalizationEntryClicked(keyId: string): void {
  expandedLocalizationKeys.value[keyId] = !expandedLocalizationKeys.value[keyId]
}

// Pagination ---------------------------------------------------------------------------------------------------------

const pageSize = 100

/**
 * A computed list of items for the current page, each enhanced with a flag indicating if any language value is truncated.
 */
const currentPageItemList = computed((): DecoratedLocalizationTableItem[] => {
  if (showPagingControls.value) {
    const start = currentPage.value * pageSize
    const pageItems = filteredItemList.value.slice(start, start + pageSize)
    return pageItems.map((item) => ({
      ...item,
      isAnyLanguageValueTruncated: Object.values(item.values).some(
        (value) => !value.startsWith('#missing') && value.length > 37
      ),
    }))
  } else {
    return filteredItemList.value.map((item) => ({
      ...item,
      isAnyLanguageValueTruncated: Object.values(item.values).some((value) => value.length > 37),
    }))
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
  const itemCount = filteredItemList.value.length
  return Math.ceil(itemCount / pageSize)
})

/**
 * This is the page that we have chosen to view. Sometimes this page ends up being outside the valid page range (eg:
 * when a search/filter changes the total number of items/pages) but we keep track of the original number to prevent
 * surprising page changes as searches/filters are changed.
 */
const desiredCurrentPage = ref(0)

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
}

/**
 * Jump to a relative page.
 * @param offset Offset to current page, eg +1 or -1. Input is clamped to valid range.
 */
function gotoPageRelative(offset: number): void {
  desiredCurrentPage.value = clamp(currentPage.value + offset, 0, totalPageCount.value - 1)
}
</script>
