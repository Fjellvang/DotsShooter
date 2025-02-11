<template lang="pug">
MListItem {{ event.categoryName }} {{ event.displayName }}
  template(#top-right)
    span(class="tw-text-neutral-500") Code: #[span(class="tw-font-mono tw-text-xs") {{ event.typeCode }}]

  template(#bottom-left) {{ event.docString || 'No description provided.' }}
    //- span.text-monospace.small(v-if="event.parameters.length > 0") {{ event.parameters.join(', ') }}
    //- small(v-else).text-muted.tw-italic No parameters

  template(#bottom-right)
    MTextButton(
      :to="`/analyticsEvents/${event.typeCode}`"
      data-testid="analytics-details-link"
      ) View details
</template>

<script lang="ts" setup>
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'

defineProps({
  event: {
    type: Object,
    required: true,
  },
})
</script>
