<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.localization.view"
  :is-loading="!activeLocalizationIdData || !allLocalizationData"
  :error="loadingError"
  full-width-overview
  )
  template(#overview)
    //- Extra wide header card to fit the UI elements.
    MPageOverviewCard(title="Compare Localizations Versions")
      p(class="tw-mb-3")
        | Select two localizations to compare the differences between them. You can view the differences in the
        | full diff, or filter them to only show additions, removals, or modifications.

      b-row(align-v="center")
        //- Baseline
        b-col(class="tw-mb-4")
          div(class="hacked-height")
            h6 Baseline localization
            meta-input-select(
              :value="baselineLocalizationId"
              :options="localizationOptions"
              no-clear
              style="height: 5rem"
              @input="baselineLocalizationId = $event"
              )
              template(#option="{ option }")
                localization-select-option(
                  v-if="option"
                  :localizationId="option"
                  )

              template(#selectedOption="{ option }")
                div(style="line-height: 1.5")
                  localization-select-option(
                    v-if="option"
                    :localizationId="option"
                    )

            div(class="text-right tw-w-full")
              small #[MTextButton(:to="`/localizations/${baselineLocalizationId}`") View baseline localization]

        //- Swap
        b-col(
          md="1"
          class="tw-mb-4 tw-text-center"
          )
          MIconButton(
            variant="primary"
            aria-label="Swap the baseline and new localizations"
            @click="swapLocalizations"
            ): fa-icon(
            icon="right-left"
            )

        //- New
        //- TODO - Would be nice to not have to repeat this.
        b-col(class="tw-mb-4")
          div(class="hacked-height")
            h6 New localization
            meta-input-select(
              :value="newLocalizationId"
              :options="localizationOptions"
              no-clear
              style="height: 5rem"
              @input="newLocalizationId = $event"
              )
              template(#option="{ option }")
                localization-select-option(
                  v-if="option"
                  :localizationId="option"
                  )

              template(#selectedOption="{ option }")
                div(style="line-height: 1.5")
                  localization-select-option(
                    v-if="option"
                    :localizationId="option"
                    )

            div(class="text-right tw-w-full")
              small #[MTextButton(:to="`/localizations/${newLocalizationId}`") View new localization]

  core-ui-placement(placementId="Localizations/Diff")

  //- Overview of changes.
  b-row(
    align-h="center"
    class="tw-mb-3"
    )
    b-col(
      md="7"
      class="tw-z-0"
      )
      meta-list-card(
        title="Overview of changes"
        :itemList="loadingState === LoadingState.Loading ? undefined : items"
        :searchFields="['displayName']"
        :filterSets="overviewFilterSets"
        :sortOptions="overviewSortOptions"
        :emptyMessage="emptyChangeOverviewMessage"
        :page-size="15"
        )
        template(#item-card="slot")
          MListItem
            MBadge(
              v-if="slot.item.reason === DiffItemReason.Added"
              variant="success"
              tooltip="Added"
              class="mr-2"
              ) A
            MBadge(
              v-if="slot.item.reason === DiffItemReason.Removed"
              variant="danger"
              tooltip="Removed"
              class="mr-2"
              ) R
            MBadge(
              v-if="slot.item.reason === DiffItemReason.Modified"
              variant="primary"
              tooltip="Modified"
              class="mr-2"
              ) M
            span {{ slot.item.displayName }}

  //- List of changes.
  b-card(
    v-if="items.length > 0"
    no-body
    class="shadow-sm mb-3 tw-mx-3"
    )
    //- Title
    b-card-body
      div(class="tw-flex tw-items-center tw-justify-between")
        b-card-title(class="tw-pt-3") Detailed Changes
        MInputSingleSelectSwitch(
          :model-value="diffViewType"
          :options="diffViewTypeOptions"
          @update:model-value="diffViewType = $event"
          )

    //- Body
    b-list-group(
      flush
      class="list-group-stripes"
      )
      b-list-group-item(
        v-for="(item, index) in items"
        :key="item.anchorName"
        )
        meta-lazy-loader(:renderAfterDelayInMs="50")
          //- Header
          div(
            :id="item.anchorName"
            style="cursor: pointer"
            @click="toggleItemCollapsed(item.anchorName)"
            )
            span(:class="{ 'not-collapsed': !isItemCollapsed(item.anchorName) }")
              fa-icon(
                v-if="shouldShowDiffItemBody(item.reason)"
                icon="angle-right"
                class="mr-2"
                )
            span(class="font-weight-bold mr-2") {{ item.languageName }}{{ item.itemName !== '' ? '.' : '' }}{{ item.itemName }}
            MBadge(
              v-if="item.reason === DiffItemReason.Added"
              variant="success"
              ) Added
            MBadge(
              v-if="item.reason === DiffItemReason.Removed"
              variant="danger"
              ) Removed
            MBadge(
              v-if="item.reason === DiffItemReason.Modified"
              variant="primary"
              ) Modified
            span(
              v-if="shouldShowDiffItemBody(item.reason)"
              class="tw-ml-1 tw-text-xs tw-text-blue-400"
              ) {{ isItemCollapsed(item.anchorName) ? 'Show' : 'Hide' }}

            //- Body. We skip unnecessary items based on the diff mode
            div(
              v-if="!isItemCollapsed(item.anchorName) && shouldShowDiffItemBody(item.reason)"
              class="mt-2"
              )
              //- Draw each line with line reason specific rendering
              div(
                v-for="(line, lineNumber) in itemDiffText(item.languageName, item.itemName)"
                class="diff text-monospace"
                )
                //- No changes
                div(
                  v-if="[DiffViewType.Diff, DiffViewType.Baseline, DiffViewType.Destination].includes(diffViewType) && line.reason == DiffTextReason.Unchanged"
                  )
                  | {{ line.text }}

                //- Additions
                div(
                  v-if="[DiffViewType.Diff, DiffViewType.Additions, DiffViewType.Destination].includes(diffViewType) && line.reason == DiffTextReason.Added"
                  )
                  |#[span(class="text-success") +] #[span(class="added") {{ line.text.slice(2, line.text.length) }}]

                //- Removed
                div(
                  v-else-if="[DiffViewType.Diff, DiffViewType.Baseline].includes(diffViewType) && line.reason == DiffTextReason.Removed"
                  )
                  |#[span(class="text-danger") -] #[span(class="removed") {{ line.text.slice(2, line.text.length) }}]

  meta-raw-data(
    :kvPair="baselineLocalization"
    name="baselineLocalization"
    )
  meta-raw-data(
    :kvPair="newLocalization"
    name="newLocalization"
    )
</template>

<script lang="ts" setup>
import { diffLines } from 'diff'
import { computed, onBeforeMount, ref, watch, watchEffect } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  type MetaInputSelectOption,
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortDirection,
  MetaListSortOption,
} from '@metaplay/meta-ui'
import {
  DisplayError,
  MBadge,
  MIconButton,
  MInputSingleSelectSwitch,
  MListItem,
  MPageOverviewCard,
  MTextButton,
  MViewContainer,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import LocalizationSelectOption from '../components/localization/LocalizationSelectOption.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { extractSingleValueFromQueryStringOrUndefined } from '../coreUtils'
import type { LocalizationContent } from '../localizationServerTypes'
import {
  getSingleLocalizationSubscriptionOptions,
  getAllLocalizationsSubscriptionOptions,
} from '../subscription_options/localization'

// Subscriptions and core systems -------------------------------------------------------------------------------------

const gameServerApi = useGameServerApi()
const route = useRoute()
const router = useRouter()

const { data: activeLocalizationIdData } = useSubscription(getSingleLocalizationSubscriptionOptions('$active'))
const { data: allLocalizationData } = useSubscription(getAllLocalizationsSubscriptionOptions())

/**
 * Localizations are loaded from the game server and stored in here, keyed by their id. Because loading of these is
 * async, we need to be able to indicate that loading is in progress. We do this by setting the value to `null`, so a
 * `null` value here means "still loading".
 */
const sourceLocalizations: Record<string, LocalizationContent | null> = {}

// Localization Ids for baseline and new -------------------------------------------------------------------------------

/**
 * Id of the baseline (left-hand) localization. Initially this comes from the query string. If the query string is not
 * found then see the `watchEffect` for how the Id gets resolved.
 */
const baselineLocalizationId = ref<string | undefined>(
  extractSingleValueFromQueryStringOrUndefined(route.query, 'baselineRoot')
)

/**
 * Id of the new (right-hand) localization. Initially this comes from the query string. If the query string is not found
 * then see the `watchEffect` for how the Id gets resolved.
 */
const newLocalizationId = ref<string | undefined>(extractSingleValueFromQueryStringOrUndefined(route.query, 'newRoot'))

/**
 * When the component loads we need to kick off loads for any localizations that were specified in the query string.
 */
onBeforeMount(async () => {
  await Promise.all([loadLocalization(baselineLocalizationId.value), loadLocalization(newLocalizationId.value)])
})

/**
 * Watch for changes in the `baseline` localization Id. This can happen either from the multi-select or by the resolution
 * inside the `WatchEffect`.
 */
watch(baselineLocalizationId, async () => {
  // Try to fetch the localization. If that fails then we need to load it.
  baselineLocalization.value = fetchLoadedLocalization(baselineLocalizationId.value)
  if (!baselineLocalization.value) {
    await loadLocalization(baselineLocalizationId.value)
  }
  updateBrowserAddressBar()
})

/**
 * Watch for changes in the `new` localization Id. This can happen either from the multi-select or by the resolution
 * inside the `WatchEffect`.
 */
watch(newLocalizationId, async () => {
  // Try to fetch the localization. If that fails then we need to load it.
  newLocalization.value = fetchLoadedLocalization(newLocalizationId.value)
  if (!newLocalization.value) {
    await loadLocalization(newLocalizationId.value)
  }
  updateBrowserAddressBar()
})

/**
 * When the page initially loads, if the base/new localization Ids are not loaded from the query string then they will default
 * to undefined. We want them to default to the active localization Id. We kick off a subscription to get this id and
 * resolve the unknown Ids when it loads.
 */
watchEffect(() => {
  if (activeLocalizationIdData.value) {
    sourceLocalizations[activeLocalizationIdData.value.info.id] = activeLocalizationIdData.value
    if (!baselineLocalizationId.value) {
      baselineLocalizationId.value = activeLocalizationIdData.value.info.id
    }
    if (!newLocalizationId.value) {
      newLocalizationId.value = activeLocalizationIdData.value.info.id
    }
  }
})

/**
 * Update the URL in the browser address bar to show the currently selected localizations.
 */
function updateBrowserAddressBar(): void {
  const permalink = route.path + `?baselineRoot=${baselineLocalizationId.value}` + `&newRoot=${newLocalizationId.value}`
  const urlParts = permalink.split('?')
  const url = urlParts[0]
  const queryParams = Object.fromEntries(urlParts[1].split('&').map((x) => x.split('=')))
  router.replace({ path: url, query: queryParams }).catch((error: any) => {
    // Vue will sometimes complain that we are navigating to the same page, but we can safely
    // catch and ignore this.
    if (error.name !== 'NavigationDuplicated') {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      throw Error(error)
    }
  })
}

const { showSuccessNotification } = useNotifications()

/**
 * Swap the localizations - `new` becomes `baseline`, `baseline` becomes `new`.
 */
function swapLocalizations(): void {
  const temp = baselineLocalizationId.value
  baselineLocalizationId.value = newLocalizationId.value
  newLocalizationId.value = temp
  showSuccessNotification('Localizations swapped.')
}

// Async loading of localizations from the game server -----------------------------------------------------------------

/**
 * Used to track the state of localizations loading.
 */
enum LoadingState {
  'Loading' = 'Loading',
  'Loaded' = 'Loaded',
  'Error' = 'Error',
}

/**
 * Localizations are loaded in the background. This function starts and then manages their loading.
 * @param localizationId Id of the localization that we want to load.
 */
async function loadLocalization(localizationId: string | undefined): Promise<void> {
  // Don't try to load empty Ids, and don't try to load if we're already in an error state.
  if (localizationId !== undefined && loadingState.value !== LoadingState.Error) {
    // Is this localization loading or already loaded? Remember the value will be `null` if it's loading, `something` if it's
    // loaded, or `undefined` if loading hasn't been attempted yet.
    if (sourceLocalizations[localizationId] === undefined) {
      // Diff source isn't already loaded/loading so we need to request it.

      // Enter the loading state.
      loadingState.value = LoadingState.Loading

      // Mark the localization as `loading` in the list of localizations.
      sourceLocalizations[localizationId] = null

      try {
        // Make the actual request to the game server for tis localization data.
        const response = await gameServerApi.get(`/localization/${localizationId}`)
        const data = response.data as LocalizationContent
        if (data.info.bestEffortStatus !== 'Success') {
          // Load failed.
          loadingError.value = new DisplayError(
            `Failed to load diff source ${localizationId}`,
            `The status of the localization was ${data.info.bestEffortStatus}, expected status was Success. You can't compare against this localization because it failed to build.`
          )
          loadingState.value = LoadingState.Error
        } else {
          // Load succeeded.
          sourceLocalizations[localizationId] = data
          // Have all localizations loaded now? ie: are there no longer any localizations with the loading `null` state?
          if (
            (loadingState.value as LoadingState) !== LoadingState.Error &&
            !Object.values(sourceLocalizations).some((x) => x == null)
          ) {
            baselineLocalization.value = fetchLoadedLocalization(baselineLocalizationId.value)
            newLocalization.value = fetchLoadedLocalization(newLocalizationId.value)
            loadingState.value = LoadingState.Loaded
          }
        }
      } catch (e: any) {
        // Some unknown/unexpected error ocurred during loading.
        loadingError.value = new DisplayError(
          'Oh no, something went wrong while trying to compare two localizations:',
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          e.message
        )
        loadingState.value = LoadingState.Error
      }
    }
  }
}

/**
 * Are we currently loading any localizations?
 */
const loadingState = ref<LoadingState>(LoadingState.Loading)

/**
 * Error to be displayed if loading fails.
 */
const loadingError = ref<Error | DisplayError>()

// Baseline and new localizations --------------------------------------------------------------------------------------

/**
 * Complete baseline localization, or `undefined` if the localization isn't available.
 */
const baselineLocalization = ref<Record<string, any> | undefined>()

/**
 * Complete new localization, or `undefined` if the localization isn't available.
 */
const newLocalization = ref<Record<string, any> | undefined>()

/**
 * Explains the reason behind an item's difference.
 */
enum DiffItemReason {
  'Added' = 'Added',
  'Removed' = 'Removed',
  'Modified' = 'Modified',
}

/**
 * Fully describes a difference between items.
 */
interface Item {
  anchorName: string
  displayName: string
  languageName: string
  itemName: string
  reason: DiffItemReason
}

/**
 * The differences between the baseline and new localizations. Each difference is an `Item`.
 */
const items = computed((): Item[] => {
  const baselineLoc = baselineLocalization.value
  const newLoc = newLocalization.value

  // Early-out if one or more of the localizations is not yet loaded.
  if (!baselineLoc || !newLoc) {
    return []
  }

  const items: Item[] = []

  // Added items.
  const newLocalizationKeys = Object.keys(newLoc)
  const baselineLocalizationKeys = Object.keys(baselineLoc)
  newLocalizationKeys.forEach((language) => {
    const newLocalizationLibrary = newLoc[language] ?? null
    const baseLocalizationLibrary = baselineLoc[language] ?? null
    if (newLocalizationLibrary === null) {
      // Localization library cannot be parsed. Check if the whole library is new.
      if (baseLocalizationLibrary) {
        items.push({
          anchorName: `${language}-whole-library`,
          displayName: language,
          languageName: language,
          itemName: '',
          reason: DiffItemReason.Added,
        })
      }
      return
    }
    if (baseLocalizationLibrary !== null) {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      Object.keys(newLocalizationLibrary).forEach((itemName) => {
        if (!Object.prototype.hasOwnProperty.call(baseLocalizationLibrary, itemName)) {
          items.push({
            anchorName: `${language}-${itemName}`,
            displayName: `${language}.${itemName}`,
            languageName: language,
            itemName,
            reason: DiffItemReason.Added,
          })
        }
      })
    }
  })

  // Removed items.
  baselineLocalizationKeys.forEach((language) => {
    const baseLocalizationLibrary = baselineLoc[language] ?? null
    const newLocalizationLibrary = newLoc[language] ?? null
    if (baseLocalizationLibrary === null) {
      // Localization library cannot be parsed. Check if the whole library is removed.
      if (newLocalizationLibrary) {
        items.push({
          anchorName: `${language}-whole-library`,
          displayName: language,
          languageName: language,
          itemName: '',
          reason: DiffItemReason.Removed,
        })
      }
      return
    }
    if (newLocalizationLibrary !== null) {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      Object.keys(baseLocalizationLibrary).forEach((itemName) => {
        if (!Object.prototype.hasOwnProperty.call(newLocalizationLibrary, itemName)) {
          items.push({
            anchorName: `${language}-${itemName}`,
            displayName: `${language}.${itemName}`,
            languageName: language,
            itemName,
            reason: DiffItemReason.Removed,
          })
        }
      })
    }
  })

  // Modified items.
  baselineLocalizationKeys.forEach((language) => {
    const baseLocalizationLibrary = baselineLoc[language] ?? null
    const newLocalizationLibrary = newLoc[language] ?? null
    if (!baseLocalizationLibrary || !newLocalizationLibrary) {
      // Skip missing libraries. They are handled already.
      return
    }
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Object.keys(baseLocalizationLibrary).forEach((itemName) => {
      if (Object.prototype.hasOwnProperty.call(newLocalizationLibrary, itemName)) {
        const baselineItem = JSON.stringify(baseLocalizationLibrary[itemName])
        const newItem = JSON.stringify(newLocalizationLibrary[itemName])
        if (baselineItem !== newItem && !baselineItem.startsWith('"#missing#') && !newItem.startsWith('"#missing#')) {
          items.push({
            anchorName: `${language}-${itemName}`,
            displayName: `${language}.${itemName}`,
            languageName: language,
            itemName,
            reason: DiffItemReason.Modified,
          })
        }
      }
    })
  })

  // Sort items by name.
  items.sort((a, b) => {
    const nameA = a.anchorName.toLowerCase()
    const nameB = b.anchorName.toLowerCase()
    if (nameA < nameB) return -1
    else if (nameA > nameB) return 1
    else return 0
  })
  return items
})

/**
 * Clean up source localization and create one that is suitable for viewing.
 * @param sourceLocalizationId Id of the localization that we want to fetch.
 */
function fetchLoadedLocalization(
  sourceLocalizationId: string | undefined
): Record<string, Record<string, string>> | undefined {
  const sourceLocalization = sourceLocalizations[sourceLocalizationId ?? '']
  if (sourceLocalization) {
    const newLocs: Record<string, Record<string, string>> = {}
    Object.entries(sourceLocalization.locs).forEach(([key, value]) => {
      newLocs[key] = value.translations
    })

    // Done.
    return newLocs
  } else {
    // No localization found, or localization still loading.
    return undefined
  }
}

// Localization Diff Logic --------------------------------------------------------------------------------------------------

/**
 * Explains how a line differs between two localizations.
 */
enum DiffTextReason {
  'Unchanged' = 'Unchanged',
  'Added' = 'Added',
  'Removed' = 'Removed',
}

/**
 * Fully describes the difference between the lines of two localizations.
 */
interface DiffText {
  reason: DiffTextReason
  text: string
}

/**
 * Compare items from two localizations libraries and return a list of changes on each line.
 * @param libraryName Name of the library, ie: the root item.
 * @param itemName Name of the item inside the library.
 */
function itemDiffText(libraryName: string, itemName: string): DiffText[] {
  let baselineItem = {}
  if (Object.prototype.hasOwnProperty.call(baselineLocalization.value?.[libraryName], itemName)) {
    baselineItem = baselineLocalization.value?.[libraryName][itemName]
  }
  let newItem = {}
  if (Object.prototype.hasOwnProperty.call(newLocalization.value?.[libraryName], itemName)) {
    newItem = newLocalization.value?.[libraryName][itemName]
  }
  const baselineText = makeIntoText(baselineItem)
  const newText = makeIntoText(newItem)
  const diffItems = diffLines(baselineText, newText)
  const diffText: DiffText[] = []
  diffItems.forEach((diff) => {
    let reason: DiffTextReason = DiffTextReason.Unchanged
    if (diff.added) reason = DiffTextReason.Added
    else if (diff.removed) reason = DiffTextReason.Removed

    const lines = diff.value.split(/\r?\n/).slice(0, diff.count)
    diffText.push(
      ...lines.map((text: string) => {
        // Remove some double commas to make diffs easier to read.
        const key = text.split(':')[0]
        const newKey = key.replace(/"/g, '')
        const prettyText = text.replace(key, newKey)
        return { reason, text: prettyText }
      })
    )
  })
  return diffText
}

/**
 * Converts the given data into human readable text format.
 * @param data The data that needs to be converted into text.
 */
function makeIntoText(data: any): string {
  if (typeof data === 'string') return `  "${data}"`
  if (typeof data === 'number') return `  ${data}`
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  if (Object.keys(data).length === 0) return ' '
  return JSON.stringify(data, (k, v) => v, 2).slice(2, -2)
}

/**
 * Used to track how we want to view the differences.
 */
enum DiffViewType {
  'Diff' = 'Diff',
  'Additions' = 'Additions',
  'Baseline' = 'Baseline',
  'Destination' = 'Destination',
}

const diffViewTypeOptions = [
  { label: 'baseline', value: DiffViewType.Baseline },
  { label: 'Full Diff', value: DiffViewType.Diff },
  { label: 'Only Additions', value: DiffViewType.Additions },
  { label: 'New', value: DiffViewType.Destination },
]
/**
 * How are we currently viewing the differences between the two localizations?
 */
const diffViewType = ref<DiffViewType>(DiffViewType.Diff)

/**
 * Helper function for deciding whether to show the body of a diff item.
 * @param reason Modification reason for the item.
 */
function shouldShowDiffItemBody(reason: DiffItemReason): boolean {
  if (diffViewType.value === DiffViewType.Diff) return true
  else if (diffViewType.value === DiffViewType.Additions && reason !== DiffItemReason.Removed) {
    return true
  } else if (diffViewType.value === DiffViewType.Baseline && reason !== DiffItemReason.Added) {
    return true
  } else if (diffViewType.value === DiffViewType.Destination && reason !== DiffItemReason.Removed) {
    return true
  } else return false
}

// Opening/closing of items in the Detailed Changes list --------------------------------------------------------------

/**
 * A list of diff items that are open (ie: expanded) so that their contents are visible.
 */
const openDiffItems = ref<string[]>([])

/**
 * Toggle the visual open/collapsed state of a diff item based on its name.
 * @param anchorName Key of item to toggle.
 */
function toggleItemCollapsed(anchorName: string): void {
  const index = openDiffItems.value.indexOf(anchorName)
  if (index === -1) {
    openDiffItems.value.push(anchorName)
  } else {
    openDiffItems.value.splice(index, 1)
  }
}

/**
 * Should an item be visible as open or collapsed?
 * @param anchorName Key of item to check.
 */
function isItemCollapsed(anchorName: string): boolean {
  return !openDiffItems.value.includes(anchorName)
}

// MetaListCard localizationuration -----------------------------------------------------------------------------------------

/**
 * Filter set for the change overview card. Constructed dynamically from library names and statically from the
 * possible types of modifications.
 */
const overviewFilterSets = computed(() => {
  return [
    MetaListFilterSet.asDynamicFilterSet(items.value, 'library', (x) => (x as Item).languageName),
    new MetaListFilterSet('reason', [
      new MetaListFilterOption('Modified', (x) => (x as Item).reason === DiffItemReason.Modified),
      new MetaListFilterOption('Added', (x) => (x as Item).reason === DiffItemReason.Added),
      new MetaListFilterOption('Removed', (x) => (x as Item).reason === DiffItemReason.Removed),
    ]),
  ]
})

/**
 * Sort options for the change overview card.
 */
const overviewSortOptions = [
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Descending),
  new MetaListSortOption('Reason', 'reason', MetaListSortDirection.Ascending),
  new MetaListSortOption('Reason', 'reason', MetaListSortDirection.Descending),
]

/**
 * A message to be shown when there are no differences between the two localization versions.
 */
const emptyChangeOverviewMessage = computed(() => {
  if (baselineLocalizationId.value === newLocalizationId.value) {
    return 'You are comparing a localization version against itself. Unsurprisingly, this means that there are no differences to be found 🤔'
  } else {
    return 'No differences found between the two localization versions 🤔'
  }
})

// Localization Id searching -------------------------------------------------------------------------------------------

/**
 * List of localization options for selector.
 */
const localizationOptions = computed((): Array<MetaInputSelectOption<string>> => {
  return localizationIds.value.map((id) => {
    // There's a hack here! The `MetaInputSelect` only searches ID and value strings. We want to search the name and
    // description of the localizations as well. So we're going to concatenate the ID, name, and description into the
    // ID string to make them searchable.
    const localization = allLocalizationData.value?.find((localization) => localization.id === id)
    return {
      id: `${id} - ${localization?.name ?? 'No name available'} - ${localization?.description ?? 'No description available'}`,
      value: id,
    }
  })
})

/**
 * List of Ids of all valid, non-archived localizations.
 */
const localizationIds = computed((): string[] => {
  return (allLocalizationData.value ?? [])
    .filter((localization) => localization.bestEffortStatus === 'Success' && !localization.isArchived)
    .map((localization) => localization.id)
})
</script>

<style>
.diff {
  white-space: pre-wrap;
  font-size: 0.7rem;
  line-height: 1.2;
  margin-bottom: 1px;
}
.added {
  background: #bfb;
}
.removed {
  background: #fbb;
}

/** Further hack the dropdown to have more offset than usual. This is terrible. Don't do this again. Ever. */
.hacked-height > .multiselect > .multiselect-dropdown {
  margin-top: 4.9rem !important;
}
</style>
