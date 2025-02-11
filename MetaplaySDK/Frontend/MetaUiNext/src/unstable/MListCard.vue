<template lang="pug">
MCard(:title="title")
</template>

<script lang="ts" setup generic="T">
import MCard from '../primitives/MCard.vue'

const props = withDefaults(
  defineProps<{
    /**
     * Title of the card.
     */
    title: string
    /**
     * Array of items to be shown.
     */
    items: T[] | undefined
    // TODO
    // icon?: string
    // TODO variants
    //dangerous?: boolean
    /**
     * Optional: Amount of items to load at once. Defaults to 8.
     */
    pageSize?: number
    /**
     * Optional: Array of item property names that can be searched.
     * @example: ['id', 'contents.title']
     */
    propertiesToIncludeInSearch?: string[]
    /**
     * Optional: Placeholder text for the search box.
     */
    searchPlaceholder?: string
    /**
     * Optional: List of filter sets that can be applied.
     */
    //filterOptions?: MListCardFilterOptionGroup[]
    /**
     * Optional: List of sort types that can be applied.
     */
    //sortOptions?: MListCardSortOption[]
    /**
     * Optional: Index of sort option to choose by default.
     */
    defaultSortOption?: number
    /**
     * Optional: Message to be shown when data has loaded but has no entries.
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
    subtitle?: string
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
    propertiesToIncludeInSearch: undefined,
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
    subtitle: undefined,
    allowPausing: false,
  }
)
</script>
