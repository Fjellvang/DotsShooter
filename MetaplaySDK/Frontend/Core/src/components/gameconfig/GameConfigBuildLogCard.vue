<template lang="pug">
meta-list-card(
  title="Build Log"
  description="Messages generated during the config build process. Errors will cause the build to fail."
  icon="clipboard-list"
  :itemList="buildMessages"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :filterSets="filterSets"
  :emptyMessage="buildMessagesMissing ? 'Logs not available.' : 'No log entries.'"
  data-testid="game-config-build-log-card"
  )
  template(#item-card="{ item: buildReport }")
    MCollapse(extraMListItemMargin)
      //- Row header
      template(#header)
        MListItem(noLeftPadding)
          span(
            :class="['tw-font-normal', { 'tw-text-red-500': messageLevelBadgeVariant(buildReport.level) === 'danger' }]"
            ) {{ buildReport.message }}
          template(#top-right)
            MBadge(:variant="messageLevelBadgeVariant(buildReport.level)") {{ buildReport.level }}
          template(
            v-if="buildReport.itemId"
            #bottom-left
            ) {{ buildReport.itemId }} in {{ buildReport.shortSource }} #[span(v-if="buildReport.variantId") ({{ buildReport.variantId }})]
          template(#bottom-right)
            MTextButton(
              v-if="isUrl(buildReport.locationUrl)"
              :to="buildReport.locationUrl || ''"
              ) View source #[fa-icon(icon="external-link-alt" class="tw-ml-1")]

      //- Collapse content
      MList(
        show-border
        class="tw-bg-neutral-100"
        )
        div(
          v-if="buildReport.exception"
          class="tw-pt-3"
          )
          div(class="tw-mx-3 tw-flex tw-items-center tw-justify-between")
            div(class="tw-font-mono tw-text-xs tw-text-neutral-500") // raw_error
            MClipboardCopy(
              v-if="buildReport.exception"
              :contents="`${buildReport.exception}`"
              )
          pre(class="tw-px-3 tw-text-xs") {{ buildReport.exception }}

        div(
          v-if="buildReport.sourceInfo || buildReport.sourceLocation || buildReport.locationUrl"
          class="tw-pt-3"
          )
          div(class="tw-ml-3 tw-font-mono tw-text-xs tw-text-neutral-500") // message_source
          pre(class="tw-px-3 tw-text-xs")
            div {
            div(class="tw-ml-4") "source": "{{ buildReport.sourceInfo }}"
            div(class="tw-ml-4") "location": "{{ buildReport.sourceLocation }}"
            div(class="tw-ml-4") "item": "{{ buildReport.itemId }}"
            div(class="tw-ml-4") "variant": {{ buildReport.variantId ? `"${buildReport.variantId}"` : 'null' }}
            div(
              v-if="buildReport.locationUrl"
              class="tw-ml-4"
              ) "url":
              MTextButton(:to="buildReport.locationUrl") "{{ buildReport.locationUrl }}"
            div }

        div(
          v-if="buildReport.callerFileName"
          class="tw-pt-3"
          )
          div(class="tw-ml-3 tw-font-mono tw-text-xs tw-text-neutral-500") // callsite
          pre(class="tw-px-3 tw-text-xs")
            div {
            div(class="tw-ml-4") "function": "{{ buildReport.callerMemberName }}"
            div(class="tw-ml-4") "file": "{{ buildReport.callerFileName }}"
            div(class="tw-ml-4") "line": "{{ buildReport.callerLineNumber }}"
            div }

        // pre {{ buildReport }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import {
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortOption,
  MetaListSortDirection,
  MetaListCard,
} from '@metaplay/meta-ui'
import { MBadge, MClipboardCopy, MCollapse, type Variant, MListItem, MList, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type {
  GameConfigBuildMessage,
  GameConfigLogLevel,
  LibraryCountGameConfigInfo,
} from '../../gameConfigServerTypes'
import { getSingleGameConfigCountsSubscriptionOptions } from '../../subscription_options/gameConfigs'

const props = defineProps<{
  /**
   * Id of game config whose Build report we want to show.
   */
  gameConfigId: string
}>()

/**
 * Fetch data for the specific game config that is to be displayed.
 */
const { data: gameConfigData } = useSubscription(getSingleGameConfigCountsSubscriptionOptions(props.gameConfigId))

/**
 * Are the build messages completely missing?
 */
const buildMessagesMissing = computed(() => {
  return gameConfigData.value?.contents === null
})

/**
 * Extract build messages from the build report.
 */
const buildMessages = computed((): GameConfigBuildMessage[] | undefined => {
  if (buildMessagesMissing.value) {
    return []
  } else {
    return gameConfigData.value?.contents.metaData.buildReport?.buildMessages ?? undefined
  }
})

/**
 * Card search fields.
 */
const searchFields = ['message', 'sheetName', 'configKey', 'columnHint']

/**
 * Card sort options.
 * */
const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Message', 'message', MetaListSortDirection.Ascending),
  new MetaListSortOption('Message', 'message', MetaListSortDirection.Descending),
  new MetaListSortOption('Warning Level', 'level', MetaListSortDirection.Ascending),
  new MetaListSortOption('Warning Level', 'level', MetaListSortDirection.Descending),
  new MetaListSortOption('Library', 'sheetName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Library', 'sheetName', MetaListSortDirection.Descending),
  new MetaListSortOption('Config Key', 'configKey', MetaListSortDirection.Ascending),
  new MetaListSortOption('Config Key', 'configKey', MetaListSortDirection.Descending),
  new MetaListSortOption('Column', 'columnHint', MetaListSortDirection.Ascending),
  new MetaListSortOption('Column', 'columnHint', MetaListSortDirection.Descending),
]

/**
 * Card filters.
 */
const filterSets = computed(() => {
  return [
    new MetaListFilterSet('level', [
      new MetaListFilterOption('Verbose', (x: any) => x.level === 'Verbose'),
      new MetaListFilterOption('Debug', (x: any) => x.level === 'Debug'),
      new MetaListFilterOption('Information', (x: any) => x.level === 'Information'),
      new MetaListFilterOption('Warning', (x: any) => x.level === 'Warning'),
      new MetaListFilterOption('Error', (x: any) => x.level === 'Error'),
    ]),
  ]
})

/**
 * Calculate variant (ie: color) for badges based on message level.
 * @param level Message level of warning.
 */
function messageLevelBadgeVariant(level: GameConfigLogLevel): Variant {
  const mappings: Record<string, Variant> = {
    Verbose: 'neutral',
    Debug: 'neutral',
    Information: 'primary',
    Warning: 'warning',
    Error: 'danger',
  }
  return mappings[level] ?? 'danger'
}

/**
 * Crudely determine if the given string is a valid URL or not. Used to decide if a warning's URL should be a clickable
 * link or not.
 * @param url URL to check.
 */
function isUrl(url: string | undefined | null): boolean {
  return !!url && (url.startsWith('http://') || url.startsWith('https://'))
}
</script>
