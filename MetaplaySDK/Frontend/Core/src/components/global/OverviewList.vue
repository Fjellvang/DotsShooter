<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A wrapper component for displaying customizable overview lists. -->

<template lang="pug">
div
  div(class="tw-border-b tw-border-neutral-300 tw-pb-1 tw-font-bold")
    fa-icon(:icon="icon")
    span {{ listTitle }}

  div(
    v-for="item in filteredItems"
    :key="item.displayName"
    class="py-1 px-0 tw-flex tw-justify-between tw-border-b tw-border-neutral-300"
    )
    template(v-if="item.displayType === 'country'")
      div {{ item.displayName }}
      meta-country-code(
        :isoCode="item.displayValue(sourceObject)"
        :class="item.displayValue(sourceObject) === 'Unknown' ? 'tw-text-neutral-500 tw-italic' : ''"
        show-name
        )

    template(v-else-if="item.displayType === 'currency'")
      div {{ item.displayName }}
      div ${{ item.displayValue(sourceObject).toFixed(2) }}

    template(v-else-if="item.displayType === 'datetime'")
      div {{ item.displayName }}
      meta-time(
        :date="item.displayValue(sourceObject)"
        :showAs="item.displayHint === 'date' ? item.displayHint : undefined"
        )

    template(v-else-if="item.displayType === 'language'")
      div {{ item.displayName }}
      meta-language-label(
        :language="item.displayValue(sourceObject)"
        variant="badge"
        class="tw-mt-0.5 tw-text-sm"
        )

    template(v-else-if="item.displayType === 'number'")
      div(
        :class="item.displayHint === 'highlightIfNonZero' && item.displayValue(sourceObject) !== 0 ? 'tw-text-red-500' : ''"
        ) {{ item.displayName }}
      div(
        :class="item.displayHint === 'highlightIfNonZero' && item.displayValue(sourceObject) !== 0 ? 'tw-text-red-500' : ''"
        ) {{ item.displayValue(sourceObject) }}

    template(v-else-if="item.displayType === 'text'")
      div {{ item.displayName }}
      div(
        :class="item.displayHint === 'monospacedText' ? 'tw-font-mono' : item.displayValue(sourceObject) === 'Unknown' ? 'tw-text-neutral-500 tw-italic' : ''"
        ) {{ item.displayValue(sourceObject) }}

    template(v-else-if="item.displayType === 'link'")
      div {{ item.displayName }}
      MTextButton(
        :to="item.linkUrl ? item.linkUrl(sourceObject) : undefined"
        :disabled-tooltip="item.disabledTooltip ? item.disabledTooltip(sourceObject) : undefined"
        :variant="item.displayValue(sourceObject) === undefined ? 'warning' : 'primary'"
        ) {{ item.displayValue(sourceObject) ?? 'Undefined' }}

    template(v-else)
      div(class="tw-text-red-500") Unknown displayType
      div(class="tw-text-red-500") {{ item.displayType }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaCountryCode, MetaTime } from '@metaplay/meta-ui'
import { MTextButton, usePermissions } from '@metaplay/meta-ui-next'

import type { OverviewListItem } from '../../integration_api/overviewListsApis'
import MetaLanguageLabel from '../MetaLanguageLabel.vue'

const props = withDefaults(
  defineProps<{
    /**
     * Title for this overview list.
     */
    listTitle: string
    /**
     * Optional font awesome icon.
     */
    icon?: string
    /**
     * The Overview list items to be rendered.
     */
    items: OverviewListItem[]
    /**
     * Base object that contains the data i.e the guild or player object.
     */
    sourceObject: object
  }>(),
  {
    icon: 'bar-chart',
  }
)

const permissions = usePermissions()

/**
 * List of items to be displayed on the overview card.
 * By default all items are visible to all users however,
 * items that have the 'displayPermission' property,
 * will only be visible to users with the required permission.
 */
const filteredItems = computed(() => {
  return props.items.filter((item) => permissions.doesHavePermission(item.displayPermission))
})
</script>
