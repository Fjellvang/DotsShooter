<template lang="pug">
MCard(
  title="Game Config Contents"
  :isLoading="libraries.length === 0"
  :error="error"
  data-testid="game-config-contents-card"
  )
  //- Header.
  template(#icon)
    fa-icon(icon="table")

  template(#header-right)
    //- Hide the selected if the page has fatal errors, otherwise it looks odd.
    div(v-if="!error")
      meta-input-select(
        v-if="showExperimentSelector"
        :value="selectedExperimentIdInput"
        :options="experimentSelectionOptions"
        :searchFields="['displayName', 'experimentId']"
        placeholder="Select an experiment..."
        @input="onSelectedExperimentIdChanged($event)"
        )
        template(#option="{ option }")
          div {{ option === undefined ? 'error' : `${option.displayName} / ${option.experimentId}` }}
      div(
        v-else
        class="text-sm"
        ) Showing variants from #[MBadge(variant="primary") {{ selectedExperimentId }}]

  //- Labels for when there is nothing to show.
  div(
    v-if="hideNoDiffs && librariesToShow.length === 0"
    class="tw-my-auto tw-px-3 tw-py-10 tw-text-center tw-text-neutral-400"
    )
    span(v-if="selectedExperimentId === 'Baseline'") You have not selected an experiment, so there are no differences to the baseline configuration to display.
    span(v-else) The selected experiment contains no differences to the baseline configuration.

  div(
    v-else
    class="tw-overflow-x-auto"
    )
    table(class="tw-w-full")
      //- For each library...
      tbody(
        v-for="(library, libraryIndex) in librariesToShow"
        :key="library.id"
        )
        //- Empty space between libraries.
        tr(
          v-if="libraryIndex > 0"
          class="tw-h-4"
          )
        //- Library title row.
        tr(
          class="tw-cursor-pointer tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-font-normal hover:tw-bg-neutral-200 active:tw-bg-neutral-300"
          @click="toggleLibraryExpanded(library.id)"
          data-testid="library-title-row"
          )
          td(class="tw-inline-flex tw-items-center tw-p-2")
            //- Title.
            fa-icon(
              icon="angle-right"
              :class="{ 'tw-rotate-90': isLibraryExpanded(library.id) }"
              )
            span(class="tw-ml-2 tw-font-semibold") {{ library.id }}
            MBadge(
              v-if="!library.error"
              class="tw-ml-2 tw-text-xs"
              :variant="isLibraryExpanded(library.id) ? 'primary' : 'neutral'"
              shape="pill"
              ) {{ library.numTopLevelItems }}
            MBadge(
              v-else
              class="tw-ml-2 tw-text-xs"
              variant="danger"
              shape="pill"
              ) Error

          //- Nasty hack to create cells to fill the header. Colspan did not work for some reason? Long titles look fugly atm.
          td(
            v-for="col in selectedExperimentVariantIds.length + 1"
            class="tw-px-2 tw-text-right"
            )
            span(v-if="col === selectedExperimentVariantIds.length + 1")
              MBadge(
                v-if="allExperiments[selectedExperimentId].patchedLibraries.includes(library.id)"
                size="small"
                variant="primary"
                ) Variants
              //- Modification summary.
              //- MBadge(
              //-   v-if="library.summary === ItemDiffSummary.Added"
              //-   variant="success"
              //-   size="small"
              //-   ) Added
              //- MBadge(
              //-   v-else-if="library.summary === ItemDiffSummary.Modified"
              //-   variant="warning"
              //-   size="small"
              //-   ) Modified
              //- MBadge(
              //-   v-else-if="library.summary === ItemDiffSummary.Removed"
              //-   variant="danger"
              //-   size="small"
              //-   ) Removed

        //- If the library is open...
        template(v-if="isLibraryExpanded(library.id)")
          //- Library has an error.
          template(v-if="library.error")
            td(
              colspan="999"
              class="tw-p-0"
              )
              MErrorCallout(
                :error="library.error"
                class="tw-rounded-none"
                )

          //- Library contents.
          template(v-else)
            tr(class="tw-border-x tw-border-neutral-200 tw-text-xs")
              //- Library column header cells.
              th(
                class="tw-pl-2"
                :style="selectedExperimentId !== 'Baseline' ? { 'min-width': '14rem' } : { 'min-width': '' }"
                ) Key
              th(
                class="tw-border-l tw-border-neutral-200 tw-pl-2"
                :style="selectedExperimentId !== 'Baseline' ? { 'max-width': '9rem' } : { 'max-width': '' }"
                ) {{ selectedExperimentVariantIds.length === 0 ? 'Value' : 'Control' }}
              th(
                v-for="selectedExperimentVariantId in selectedExperimentVariantIds"
                :key="selectedExperimentVariantId"
                class="tw-border-l tw-border-neutral-200 tw-pl-2"
                )
                div(class="tw-font-light tw-text-neutral-600") {{ allExperiments[selectedExperimentId].displayName }}
                div {{ selectedExperimentVariantId }}

            //- Library loading state.
            template(
              v-if="!libraryContents[library.id] || libraryContents[library.id].experimentConfigItems[selectedExperimentId] === null"
              )
              tr(class="tw-border-x tw-border-b tw-border-neutral-200")
                td(
                  v-for="col in selectedExperimentVariantIds.length + 2"
                  :class="['tw-px-2 tw-text-xs tw-italic tw-text-neutral-400 tw-border-neutral-200 tw-border-t', { 'tw-border-l': col > 1 }]"
                  ) Loading...

            //- Library loaded, but it's empty.
            template(v-else-if="libraryContents[library.id].flattenedConfigItems.length === 0")
              tr(class="tw-border-x tw-border-b tw-border-neutral-200")
                td(
                  v-for="col in selectedExperimentVariantIds.length + 2"
                  :class="['tw-px-2 tw-text-xs tw-italic tw-text-neutral-400 tw-border-neutral-200 tw-border-t', { 'tw-border-l': col > 1 }]"
                  ) Library is empty.

            //- Library loaded.
            template(v-else)
              //- Library item rows.
              // TODO: diffs
                :class="['tw-border-t tw-font-mono', { 'tw-bg-red-500': item.differences, 'tw-bg-green-400': item.summary === ItemDiffSummary.Added, 'tw-bg-red-400': item.summary === ItemDiffSummary.Removed, 'tw-bg-orange-400': item.summary === ItemDiffSummary.Modified }]"
              tr(
                v-for="(item, index) in libraryContents[library.id].flattenedConfigItems"
                :key="item.path"
                :class="['tw-text-xs tw-font-mono hover:tw-bg-neutral-50 tw-border-x tw-border-neutral-200', { 'tw-border-t': item.indentation === 0, 'tw-border-b': index === libraryContents[library.id].flattenedConfigItems.length - 1 }]"
                )
                //- Item key. May be a clickable list or a single value.
                td(
                  :class="['tw-pl-2 tw-pt-0.5', { 'tw-cursor-pointer hover:tw-bg-neutral-200 active:tw-bg-neutral-300': item.clickAction }]"
                  @click="item.clickAction ? togglePathExpanded(library.id, item.path) : null"
                  )
                  span(
                    v-if="item.indentation >= 1"
                    :style="`padding-left: ${0.8 * (item.indentation - 1)}rem`"
                    class="tw-mr-1 tw-text-neutral-700"
                    ) ┗━

                  //- Note: Rotation animation is currently defined globally. Inline it here if we want to change it.
                  fa-icon(
                    v-if="item.clickAction"
                    icon="angle-right"
                    :class="['tw-mr-1', { 'tw-rotate-90': expandedPaths[library.id]?.includes(item.path) }]"
                    )
                  span(class="tw-mr-1 tw-font-mono") {{ item.title }}
                  span(
                    v-if="item.subtitle"
                    class="tw-mr-1 tw-text-neutral-400"
                    ) {{ item.subtitle }}
                  MBadge(
                    v-if="item.differences"
                    size="small"
                    class="tw-relative"
                    style="bottom: 0.1rem"
                    variant="primary"
                    ) V
                  //- MBadge(
                  //-   v-if="item.summary === ItemDiffSummary.Added"
                  //-   variant="success"
                  //-   size="small"
                  //-   ) A
                  //- MBadge(
                  //-   v-else-if="item.summary === ItemDiffSummary.Modified"
                  //-   variant="warning"
                  //-   size="small"
                  //-   ) M
                  //- MBadge(
                  //-   v-else-if="item.summary === ItemDiffSummary.Removed"
                  //-   variant="danger"
                  //-   size="small"
                  //-   ) R

                //- TODO: can we pretty print some hard-to-read values like durations?
                //- Baseline value.
                td(class="tw-break-words tw-border-l tw-border-neutral-200 tw-pl-2 tw-pt-0.5")
                  span(v-if="item.values")
                    span(v-if="item.values.Baseline !== undefined") {{ item.values.Baseline }}
                    span(
                      v-else
                      class="tw-italic tw-text-orange-400"
                      ) missing

                //- Variant values.
                td(
                  v-for="(selectedExperimentVariantId, index) in selectedExperimentVariantIds"
                  :key="item.path"
                  class="tw-break-words tw-border-l tw-border-neutral-200 tw-pl-2 tw-pt-0.5"
                  )
                  span(v-if="item.values") {{ item.values[`${selectedExperimentId}.${selectedExperimentVariantId}`] }}
</template>

<script lang="ts" setup>
import { computed, type ComputedRef, ref, onMounted } from 'vue'

import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MBadge, MCard, MErrorCallout, type DisplayError } from '@metaplay/meta-ui-next'
import { fetchSubscriptionDataOnceOnly } from '@metaplay/subscriptions'

import type { LibraryConfigItem, GameConfigLibraryContent } from '../../gameConfigServerTypes'
import { flattenLibraryConfigItems, gameConfigErrorToDisplayError } from '../../gameConfigUtils'
import type { LibraryFlattenedItem } from '../../gameConfigUtils'
import {
  getSingleGameConfigCountsSubscriptionOptions,
  getSingleGameConfigLibraryContentSubscriptionOptions,
} from '../../subscription_options/gameConfigs'

const props = withDefaults(
  defineProps<{
    /**
     * Optional: ID of the game config to show. Defaults to currently active.
     */
    gameConfigId?: string
    /**
     * Optional: Which experiment to show in the game config.
     */
    experimentId?: string
    /**
     * Optional: Show the experiment selector.
     */
    showExperimentSelector?: boolean
    /**
     * Optional: Hide libraries that don't contain any differences.
     */
    hideNoDiffs?: boolean
    /**
     * Optional: Ignore server libraries and only show shared libraries.
     */
    excludeServerLibraries?: boolean
  }>(),
  {
    gameConfigId: '$active',
    experimentId: 'Baseline',
  }
)

// Libraries. -------------------------------------------------------------------------------------

/**
 * Describes overview information about a library.
 */
interface Library {
  id: string
  error?: Error | DisplayError
  numTopLevelItems?: number
}

/**
 * Contains overview information about all libraries.
 */
const libraries = ref<Library[]>([])

/**
 * Error object, set if a data fetch errors out.
 */
const error = ref<Error>()

/**
 * Contains overview information about all libraries that we want to display in the card. It's possible that we want to
 * hide libraries that don't contain any differences.
 */
const librariesToShow = computed((): Library[] => {
  if (props.hideNoDiffs) {
    return libraries.value.filter((library) => {
      return allExperiments.value[selectedExperimentId.value].patchedLibraries.includes(library.id)
    })
  } else {
    return libraries.value
  }
})

/**
 * When the page mounts, kick off a request to get the game config overview.
 */
onMounted(async () => {
  await fetchSubscriptionDataOnceOnly(getSingleGameConfigCountsSubscriptionOptions(props.gameConfigId))
    .then((data) => {
      // Do we want to filter out the server libraries?
      let filteredLibraries = { ...data.contents.sharedLibraries }
      if (!props.excludeServerLibraries) {
        filteredLibraries = {
          ...filteredLibraries,
          ...data.contents.serverLibraries,
        }
      }

      // Build information about all experiments. First add `Baseline` as an experiment..
      allExperiments.value = {
        Baseline: {
          displayName: 'Baseline',
          variants: [],
          patchedLibraries: [],
        },
      }

      // ..then add all "real" experiments.
      Object.values(data.experiments).forEach((experiment: any) => {
        const newExperimentDataId = experiment.id
        const newExperimentData = {
          displayName: experiment.displayName,
          variants: experiment.variants,
          patchedLibraries: experiment.patchedLibraries,
        }
        allExperiments.value[newExperimentDataId] = newExperimentData
      })

      // Build a list of overview information for all interesting libraries.
      // Note: Libraries that contain errors might be in this list *and* the `libraryImportErrors`. We filter those
      // out here so that we don't see duplicates.
      // Further Note: This should only be the case for the `PlayerExperiments` library, because it's loading in multiple stages.
      //   First we load the raw experiment data, then we try to create a config item that has the patched values in it, the second step is the most likely to fail due to it deserializing to a user type (which might no longer be compatible with old data)/
      libraries.value = Object.keys(filteredLibraries)
        .sort((a, b) => a.localeCompare(b))
        .filter((a) => data.libraryImportErrors && data.libraryImportErrors[a] === undefined)
        .map((id) => {
          return {
            id,
            numTopLevelItems: filteredLibraries[id],
          }
        })

      // Add entries for all libraries that have errors in them.
      Object.entries(data.libraryImportErrors ?? []).forEach(([id, error]) => {
        libraries.value.push({
          id,
          error: gameConfigErrorToDisplayError(error),
        })
      })

      // Finally, sort into order.
      libraries.value = libraries.value.sort((a: Library, b: Library) => a.id.localeCompare(b.id))
    })
    .catch((e) => {
      // Error fetching initial game config data.
      error.value = e
    })
})

/**
 * Describes overview information about an experiment.
 */
interface ExperimentDetails {
  displayName: string
  variants: string[]
  patchedLibraries: string[]
}

/**
 * Contains information about all experiments.
 */
const allExperiments = ref<Record<string, ExperimentDetails>>({})

interface LibraryContent {
  /**
   * These are the raw config items that the server gives us. For each library we cache the config items for
   * every experiment. These lazily loaded as required. A value of `null` here indicates that the data is loading.
   */
  experimentConfigItems: Record<string, LibraryConfigItem[] | null>
  /**
   * These are essentially display lists for the library, ie: they are a linear list of items that the
   * render code can iterate over to generate the UI. They are generated from `experimentConfigItems`.
   */
  flattenedConfigItems: LibraryFlattenedItem[]
}

/**
 * Details for all libraries.
 */
const libraryContents = ref<Record<string, LibraryContent>>({})

/**
 * Asynchronously load a library. This is idempotent - it's safe to call even if the library is already loaded.
 * @param libraryId ID of the library to load.
 * @param experimentId ID of the experiment that we want to view, or `Baseline` for none.
 */
async function tryLoadLibrary(libraryId: string, experimentId: string): Promise<void> {
  // Don't try to load libraries that have errors.
  const library = libraries.value.find((x) => x.id === libraryId)
  if (library === undefined || library.error) {
    return
  }

  // Get or create an entry for this library.
  libraryContents.value[libraryId] = libraryContents.value[libraryId] || {
    experimentConfigItems: {},
    flattenedConfigItems: [],
  }
  const libraryDetail = libraryContents.value[libraryId]

  if (libraryDetail.experimentConfigItems[experimentId]) {
    // Library already loaded.
    if (isLibraryExpanded(libraryId)) {
      // If the library is expanded, make sure that it is flattened correctly.
      const libraryConfigItems = libraryDetail.experimentConfigItems[selectedExperimentId.value]
      libraryDetail.flattenedConfigItems = flattenLibraryConfigItems(libraryConfigItems, expandedPaths[libraryId] || [])
    }
  } else if (libraryDetail.experimentConfigItems[experimentId] === undefined) {
    // Start loading the library. `null` indicates that the data is loading.
    libraryDetail.experimentConfigItems[experimentId] = null
    await fetchSubscriptionDataOnceOnly<GameConfigLibraryContent>(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      getSingleGameConfigLibraryContentSubscriptionOptions(
        props.gameConfigId,
        libraryId,
        experimentId === 'Baseline' ? undefined : experimentId
      )
    )
      .then((data) => {
        // Library has loaded.
        const libraryConfigItems: LibraryConfigItem[] = data.config[libraryId].children ?? []
        libraryDetail.experimentConfigItems[experimentId] = libraryConfigItems

        // Once the library has loaded we make a display list for it if it is expanded (ie: visible)
        if (isLibraryExpanded(libraryId)) {
          libraryDetail.flattenedConfigItems = flattenLibraryConfigItems(
            libraryConfigItems,
            expandedPaths[libraryId] || []
          )
        }
      })
      .catch((error: Error) => {
        // Error fetching library data.
        library.error = error
      })
  } else {
    // Library is still loading.
  }
}

/**
 * The currently selected experiment ID, or undefined for none.
 */
const selectedExperimentIdInput = ref<string | undefined>(props.experimentId)

/**
 * The currently selected experiment ID, or 'Baseline' for none.
 */
const selectedExperimentId = computed(() => {
  return selectedExperimentIdInput.value ?? 'Baseline'
})

/**
 * Describes an experiment for the 'MetaInputSelect'.
 */
interface ExperimentSelectionOption {
  experimentId: string
  displayName: string
}

/**
 * Options for the 'MetaInputSelect'.
 */
const experimentSelectionOptions: ComputedRef<Array<MetaInputSelectOption<ExperimentSelectionOption>>> = computed(
  () => {
    // Note that 'Baseline' is not selectable as an option, so we filter it out here. To view the baseline config
    // we use the clear button on the input select.
    return Object.keys(allExperiments.value)
      .filter((id) => id !== 'Baseline')
      .map((id) => {
        return {
          id,
          value: {
            experimentId: id,
            displayName: allExperiments.value[id].displayName,
          },
        }
      })
  }
)

/**
 * Callback fired when a new experiment ID is selected.
 * @param newId Newly selected ID.
 */
function onSelectedExperimentIdChanged(newId: ExperimentSelectionOption | undefined): void {
  // Store the new value.
  selectedExperimentIdInput.value = newId?.experimentId

  // Whenever we change experiment ID we need to (potentially) load all visible libraries or just regenerate their
  // display lists.
  expandedLibraries.value.forEach((libraryId) => {
    const library = libraries.value.find((library) => library.id === libraryId)
    // Don't try to expand libraries that have errors.
    if (!library?.error) {
      const libraryDetail = libraryContents.value[libraryId]
      libraryDetail.flattenedConfigItems = []
      if (libraryDetail.experimentConfigItems[selectedExperimentId.value]) {
        // We already have the library data for this experiment, so we just need to create the display list.
        const libraryConfigItems = libraryDetail.experimentConfigItems[selectedExperimentId.value]
        libraryDetail.flattenedConfigItems = flattenLibraryConfigItems(
          libraryConfigItems,
          expandedPaths[libraryId] || []
        )
      } else {
        // We don't have the library data for the particular experiment yet so let's start loading it.
        void tryLoadLibrary(libraryId, selectedExperimentId.value)
      }
    }
  })
}

/**
 * All IDs for the currently selected experiment.
 */
const selectedExperimentVariantIds = computed((): string[] => {
  return allExperiments.value[selectedExperimentId.value].variants
})

// Expansion of libraries in the UI. --------------------------------------------------------------

/**
 * List of libraries that are expanded, ie: open and visible.
 */
const expandedLibraries = ref<string[]>([])

/**
 * Helper function to find out if a library is expanded or not.
 * @param libraryId Library ID.
 */
function isLibraryExpanded(libraryId: string): boolean {
  return expandedLibraries.value.includes(libraryId)
}

/**
 * Helper function to toggle a library open/closed.
 */
function toggleLibraryExpanded(libraryId: string): void {
  if (isLibraryExpanded(libraryId)) {
    expandedLibraries.value = expandedLibraries.value.filter((expandedLibraryId) => expandedLibraryId !== libraryId)
  } else {
    expandedLibraries.value.push(libraryId)

    // When we open a library we also need to make sure that the data is loaded.
    void tryLoadLibrary(libraryId, selectedExperimentId.value)
  }
}

// Expansion of paths within libraries in the UI. -------------------------------------------------

/**
 * Nested list of expanded paths of each library
 */
const expandedPaths: Record<string, string[]> = {}

/**
 * Helper function to toggle a path expanded/collapsed.
 * @param libraryId Library ID.
 * @param path Path Id.
 */
function togglePathExpanded(libraryId: string, path: string): void {
  const libraryPaths = expandedPaths[libraryId]
  if (libraryPaths) {
    if (libraryPaths.includes(path)) {
      expandedPaths[libraryId] = expandedPaths[libraryId].filter((extendedPath) => extendedPath !== path)
    } else {
      expandedPaths[libraryId].push(path)
    }
  } else {
    // First time expanding this path.
    expandedPaths[libraryId] = [path]
  }

  // Whenever we expand/collapse a library we need to regenerate the display list.
  const libraryDetail = libraryContents.value[libraryId]
  libraryDetail.flattenedConfigItems = flattenLibraryConfigItems(
    libraryDetail.experimentConfigItems[selectedExperimentId.value],
    expandedPaths[libraryId]
  )
}
</script>
