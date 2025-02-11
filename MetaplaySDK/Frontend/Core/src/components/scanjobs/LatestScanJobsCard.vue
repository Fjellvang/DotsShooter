<template lang="pug">
meta-list-card(
  icon="clipboard-list"
  title="Latest Scan Jobs by Type"
  :item-list="latestFinishedJobsOnePerKind"
  data-testid="past-scan-jobs-by-type-card"
  )
  template(#item-card="{ item: scanJob }")
    MCollapse(extraMListItemMargin)
      template(#header)
        MListItem(noLeftPadding) {{ scanJob.jobTitle }}
          template(#top-right) Finished #[meta-time(:date="scanJob.endTime")]
      //- Content
      MList(
        showBorder
        striped
        class="tw-text-sm"
        )
        MListItem(
          v-for="(value, key) in scanJob.summary"
          :key="scanJob.id + '_' + key"
          class="tw-px-4"
          condensed
          )
          span {{ key }}
          template(#top-right)
            span {{ value }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MCollapse, MList, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllScanJobsSubscriptionOptions } from '../../subscription_options/scanJobs'

const { data: allScanJobsData } = useSubscription(getAllScanJobsSubscriptionOptions())

const latestFinishedJobsOnePerKind = computed((): any[] | undefined => {
  return allScanJobsData.value?.latestFinishedJobsOnePerKind
})
</script>
