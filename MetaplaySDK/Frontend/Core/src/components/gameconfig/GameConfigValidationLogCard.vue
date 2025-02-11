<template lang="pug">
meta-list-card(
  title="Validation Log"
  description="Messages generated during validation of the game config. Validation issues may label the game config as failed."
  icon="clipboard-list"
  :itemList="validationMessages"
  :searchFields="searchFields"
  :defaultSortOption="defaultSortOption"
  :sortOptions="sortOptions"
  :filterSets="filterSets"
  :emptyMessage="validationMessagesMissing ? 'Logs not available.' : 'No log entries.'"
  data-testid="game-config-validation-log-card"
  )
  template(#item-card="{ item: validationMessage }")
    MCollapse(extraMListItemMargin)
      //- Row header
      template(#header)
        //- Title can be too long and truncate when width is too narrow.
        MListItem(noLeftPadding)
          span(
            :class="['tw-font-normal', { 'tw-text-red-500': messageLevelBadgeVariant(validationMessage.messageLevel) === 'danger' }]"
            ) {{ validationMessage.message }}
          template(#top-right)
            MBadge(:variant="messageLevelBadgeVariant(validationMessage.messageLevel)") {{ validationMessage.messageLevel }}
          template(#bottom-left)
            div {{ getSourceLabelForValidationMessage(validationMessage) }}
            div(v-if="validationMessage.variants.length > 1") #[MetaPluralLabel(:value="validationMessage.variants.length" label="variant")] affected.
            div(v-else-if="validationMessage.variants.length === 1") {{ validationMessage.variants[0] }} variant affected.
          template(#bottom-right)
            MTextButton(
              v-if="isUrl(validationMessage.url)"
              :to="validationMessage.url"
              ) View source #[fa-icon(icon="external-link-alt" class="tw-ml-1")]

      //- Collapse content
      MList(
        show-border
        class="tw-bg-neutral-100"
        )
        div(class="tw-pt-3")
          div(class="tw-ml-3 tw-font-mono tw-text-xs tw-text-neutral-500") // message_source
          pre(class="tw-px-3 tw-text-xs")
            div {
              div(class="tw-ml-4") "library:" "{{ validationMessage.sheetName }}"
              div(class="tw-ml-4") "configKey:" "{{ validationMessage.configKey }}"
              div(class="tw-ml-4") "columnHint:" "{{ validationMessage.columnHint }}"
            div }

        div(
          v-if="validationMessage.variants.length > 0"
          class="tw-pt-3"
          )
          div(class="tw-ml-3 tw-font-mono tw-text-xs tw-text-neutral-500") // affected_variants
          pre(class="tw-px-3 tw-text-xs") {{ validationMessage.variants }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import {
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortOption,
  MetaListSortDirection,
  MetaListCard,
  MetaPluralLabel,
} from '@metaplay/meta-ui'
import { MBadge, MTextButton, MClipboardCopy, MCollapse, type Variant, MListItem, MList } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type { GameConfigLogLevel, GameConfigValidationMessage } from '../../gameConfigServerTypes'
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
 * Are the validation messages completely missing?
 */
const validationMessagesMissing = computed(() => {
  return gameConfigData.value?.contents === null
})

/**
 * Extract validation messages from the build report.
 */
const validationMessages = computed(() => {
  if (validationMessagesMissing.value) {
    return []
  } else {
    return gameConfigData.value?.contents.metaData.buildReport?.validationMessages ?? undefined
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
  new MetaListSortOption('Message', 'message', MetaListSortDirection.Ascending),
  new MetaListSortOption('Message', 'message', MetaListSortDirection.Descending),
  new MetaListSortOption('Warning Level', 'messageLevel', MetaListSortDirection.Ascending),
  new MetaListSortOption('Warning Level', 'messageLevel', MetaListSortDirection.Descending),
  new MetaListSortOption('Library', 'sheetName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Library', 'sheetName', MetaListSortDirection.Descending),
  new MetaListSortOption('Config Key', 'configKey', MetaListSortDirection.Ascending),
  new MetaListSortOption('Config Key', 'configKey', MetaListSortDirection.Descending),
  new MetaListSortOption('Column', 'columnHint', MetaListSortDirection.Ascending),
  new MetaListSortOption('Column', 'columnHint', MetaListSortDirection.Descending),
  MetaListSortOption.asUnsorted(),
]
const defaultSortOption = 3

/**
 * Card filters.
 */
const filterSets = computed(() => {
  return [
    new MetaListFilterSet('messageLevel', [
      new MetaListFilterOption('Verbose', (x: any) => x.messageLevel === 'Verbose'),
      new MetaListFilterOption('Debug', (x: any) => x.messageLevel === 'Debug'),
      new MetaListFilterOption('Information', (x: any) => x.messageLevel === 'Information'),
      new MetaListFilterOption('Warning', (x: any) => x.messageLevel === 'Warning'),
      new MetaListFilterOption('Error', (x: any) => x.messageLevel === 'Error'),
    ]),
  ]
})

/**
 * Calculate variant (ie: color) for badges based on message level.
 * @param messageLevel Message level of warning.
 */
function messageLevelBadgeVariant(messageLevel: GameConfigLogLevel): Variant {
  const mappings: Record<string, Variant> = {
    Verbose: 'neutral',
    Debug: 'neutral',
    Information: 'primary',
    Warning: 'warning',
    Error: 'danger',
  }
  return mappings[messageLevel] ?? 'danger'
}

/**
 * Crudely determine if the given string is a valid URL or not. Used to decide if a warning's URL should be a clickable
 * link or not.
 * @param url URL to check.
 */
function isUrl(url: string | undefined | null): boolean {
  return !!url && (url.startsWith('http://') || url.startsWith('https://'))
}

/**
 * Label for the source of a validation message.
 */
function getSourceLabelForValidationMessage(message: GameConfigValidationMessage): string {
  if (message.columnHint && message.configKey && message.sheetName) {
    return `${message.columnHint} of ${message.configKey} in ${message.sheetName}.`
  } else if (message.columnHint && message.configKey) {
    return `${message.columnHint} of ${message.configKey}.`
  } else if (message.configKey && message.sheetName) {
    return `${message.configKey} in ${message.sheetName}.`
  } else if (message.columnHint && message.sheetName) {
    return `${message.columnHint} in ${message.sheetName}.`
  } else if (message.configKey) {
    return message.configKey
  } else if (message.columnHint) {
    return message.columnHint
  } else if (message.sheetName) {
    return message.sheetName
  } else {
    return ''
  }
}
</script>
