<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Past Scan Jobs"
  icon="clipboard-list"
  :itemList="databaseScanJobsData.jobHistory"
  :getItemKey="getItemKey"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="1"
  emptyMessage="No scan jobs yet."
  data-testid="past-scan-jobs-card"
  )
  template(#item-card="slot")
    MCollapse(
      extraMListItemMargin
      data-testid="scan-jobs-entry"
      )
      template(#header)
        MListItem(noLeftPadding) {{ slot.item.jobTitle }}
          template(#top-right): MBadge {{ notificationPhaseDisplayString(slot.item.phase) }}
          template(#bottom-left)
            span Started #[meta-time(:date="slot.item.startTime")] and lasted for #[meta-duration(:duration="DateTime.fromISO(slot.item.endTime).diff(DateTime.fromISO(slot.item.startTime))")].
          template(#bottom-right)
            span Items scanned: #[meta-abbreviate-number(:value="slot.item.scanStatistics.numItemsScanned")]

      //- TODO: visualising the top level entry doesn't work... but maybe this is good enough?
      meta-generated-content(
        class="tw-text-xs"
        :value="slot.item.scanStatistics"
        )

      //- Old code for future layout reference
        b-row.pt-2
          b-col
            blistgroup(style="font-size: .8rem")
              blistgroupitem.p-2.d-flex.justify-content-between
                span.font-weight-bold Job Title
                span {{ slot.item.jobTitle }}

              blistgroupitem.p-2.d-flex.justify-content-between
                span.font-weight-bold Metrics Tag
                div: MBadge {{ slot.item.metricsTag }}

              blistgroupitem.p-2.d-flex.justify-content-between
                span.font-weight-bold Priority
                span {{ slot.item.priority }}

              blistgroupitem.p-2.d-flex.justify-content-between
                span.font-weight-bold Start Time
                meta-time(:date="slot.item.startTime")

              blistgroupitem.p-2.d-flex.justify-content-between
                span.font-weight-bold End Time
                meta-time(v-if="slot.item.endTime" :date="slot.item.endTime")
                span(v-else).text-muted.tw-italic Still running!

              blistgroupitem.p-2.d-flex.justify-content-between(:variant="failedWorkersVariant")
                span.font-weight-bold Failed workers
                span {{ slot.item.scanStatistics.numSurrendered }} / {{ slot.item.numWorkers }}

              blistgroupitem.p-2.d-flex.justify-content-between
                span.font-weight-bold Duration
                span(v-if="slot.item.endTime"): meta-duration(:duration="duration")
                span(v-else).text-muted.tw-italic Still running!

              blistgroupitem.p-2.d-sm-flex.justify-content-between
                span.font-weight-bold Summary
                blistgroup.mt-2(v-if="slot.item.summary && Object.keys(slot.item.summary).length > 0")
                  blistgroupitem.pb-0(v-for="(value, key) in slot.item.summary" :key="key").py-2.px-3
                    b-row(no-gutters align-h="between")
                      small {{ key }}
                      span.small {{ value }}
                div(v-else)
                  span.text-muted.tw-italic No summary
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MCollapse, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { notificationPhaseDisplayString } from '../../coreUtils'
import { getAllScanJobsSubscriptionOptions } from '../../subscription_options/scanJobs'
import MetaGeneratedContent from '../generatedui/components/MetaGeneratedContent.vue'

const { data: databaseScanJobsData } = useSubscription(getAllScanJobsSubscriptionOptions())

const filterSets = computed(() => {
  return [
    MetaListFilterSet.asDynamicFilterSet(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      databaseScanJobsData.value?.jobHistory,
      'title',
      (x: any) => x.jobTitle
    ),
    new MetaListFilterSet('phase', [
      new MetaListFilterOption('Stopped', (x: any) => x.phase === 'Stopped'),
      new MetaListFilterOption('Cancelled', (x: any) => x.phase === 'Cancelled'),
      new MetaListFilterOption('Other', (x: any) => x.phase !== 'Stopped' && x.phase !== 'Cancelled'),
    ]),
    new MetaListFilterSet('running-time', [
      new MetaListFilterOption(
        'Fast',
        (job: any) =>
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          DateTime.fromISO(job.endTime).diff(DateTime.fromISO(job.startTime)).hours <= 1
      ),
      new MetaListFilterOption(
        'Slow',
        (job: any) =>
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          DateTime.fromISO(job.endTime).diff(DateTime.fromISO(job.startTime)).hours > 1
      ),
    ]),
  ]
})

const sortOptions = [
  new MetaListSortOption('Start time', 'startTime', MetaListSortDirection.Ascending),
  new MetaListSortOption('Start time', 'startTime', MetaListSortDirection.Descending),
  new MetaListSortOption('End time', 'endTime', MetaListSortDirection.Ascending),
  new MetaListSortOption('End time', 'endTime', MetaListSortDirection.Descending),
  new MetaListSortOption('Title', 'jobTitle', MetaListSortDirection.Ascending),
  new MetaListSortOption('Title', 'jobTitle', MetaListSortDirection.Descending),
]

function getItemKey(job: any): string {
  return job.id
}
</script>
