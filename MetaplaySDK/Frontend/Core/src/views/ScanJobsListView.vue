<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.scan_jobs.view"
  :is-loading="!allScanJobsData"
  :error="allScanJobsError"
  :alerts="alerts"
  )
  template(#overview)
    MPageOverviewCard(
      title="Database Scan Jobs"
      data-testid="scan-jobs-overview-card"
      )
      p Scan jobs are slow-running workers that crawl the database and perform operations on its data.
      div(class="tw-text-xs+ tw-text-neutral-500") You can create your own scan jobs and tune the performance of the job runners to perform complicated operations on live games for hundreds of millions of players.

      template(#buttons)
        MActionModalButton(
          modal-title="Pause All Database Scan Jobs"
          :action="onPauseAllJobsOk"
          :trigger-button-label="!allScanJobsData?.globalPauseIsEnabled ? 'Pause Jobs' : 'Resume Jobs'"
          ok-button-label="Apply"
          :ok-button-disabled-tooltip="willPauseAllJobs === allScanJobsData?.globalPauseIsEnabled ? `Toggle the switch to ${allScanJobsData?.globalPauseIsEnabled ? 'resume' : 'pause'} the scan jobs.` : undefined"
          variant="warning"
          permission="api.scan_jobs.manage"
          @show="willPauseAllJobs = allScanJobsData?.globalPauseIsEnabled"
          data-testid="pause-all-jobs"
          )
          div(class="tw-flex tw-justify-between")
            span(class="tw-font-semibold") Scan Jobs Paused
            MInputSwitch(
              :model-value="willPauseAllJobs"
              class="tw-relative tw-top-1 tw-mr-2"
              name="pauseAllScanJobs"
              size="small"
              @update:model-value="willPauseAllJobs = $event"
              data-testid="pause-jobs-toggle"
              )
          p(class="tw-my-2") You can pause the execution of all database scan jobs. This can be helpful to debug the performance of slow-running jobs.

        MActionModalButton(
          modal-title="Create a New Maintenance Scan Job"
          :action="onNewScanJobOk"
          trigger-button-label="Create Job"
          ok-button-label="Create Job"
          :ok-button-disabled-tootlip="!selectedJobKind ? 'Select a job type to proceed.' : undefined"
          variant="primary"
          permission="api.scan_jobs.manage"
          @show="selectedJobKind = null"
          data-testid="new-scan-job"
          )
          p Database maintenance jobs handle routine operations such as deleting players. They are safe to use in production, but it is still a good idea to try them once is staging to verify that the various jobs do what you expect them to to!

          div(class="tw-font-semibold") Scan Job Type
          meta-input-select(
            :value="selectedJobKind?.id ?? 'none'"
            :options="jobKindOptions"
            :variant="selectedJobKind ? 'success' : 'default'"
            @input="selectJobKind"
            data-testid="job-kind-select"
            )

          div(
            v-if="selectedJobKind"
            class="tw-mt-2 tw-text-xs tw-text-neutral-500"
            ) {{ selectedJobKind.spec.jobDescription }}

  core-ui-placement(:placement-id="'ScanJobs/List'")

  meta-raw-data(
    :kvPair="allScanJobsData"
    name="scanJobs"
    )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import {
  MActionModalButton,
  MInputSwitch,
  MPageOverviewCard,
  MViewContainer,
  useNotifications,
  type MViewContainerAlert,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import {
  getAllMaintenanceJobTypesSubscriptionOptions,
  getAllScanJobsSubscriptionOptions,
} from '../subscription_options/scanJobs'

const gameServerApi = useGameServerApi()
const { data: allMainentenanceJobTypesData } = useSubscription(getAllMaintenanceJobTypesSubscriptionOptions())
const {
  data: allScanJobsData,
  error: allScanJobsError,
  refresh: allScanJobsRefresh,
} = useSubscription(getAllScanJobsSubscriptionOptions())

const jobKindOptions = computed((): Array<MetaInputSelectOption<string | null>> => {
  const options: Array<MetaInputSelectOption<string | null>> = [
    {
      value: 'none',
      id: 'Select a job type...',
    },
  ]
  if (allMainentenanceJobTypesData.value?.supportedJobKinds) {
    for (const jobKindInfo of Object.values(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      allMainentenanceJobTypesData.value.supportedJobKinds
    ) as any) {
      options.push({
        value: jobKindInfo.id,
        id: jobKindInfo.spec.jobTitle,
      })
    }
  }
  return options
})

const selectedJobKind = ref<any>(null)
function selectJobKind(newSelectedJobKind: any): void {
  selectedJobKind.value = allMainentenanceJobTypesData.value.supportedJobKinds.find(
    (jobKind: any) => jobKind.id === newSelectedJobKind
  )
}

const { showSuccessNotification } = useNotifications()

async function onNewScanJobOk(): Promise<void> {
  const job = (
    await gameServerApi.post('/maintenanceJobs', {
      jobKindId: selectedJobKind.value.id,
    })
  ).data
  showSuccessNotification(`New job '${job.spec.jobTitle}' enqueued.`)
  allScanJobsRefresh()
}

const willPauseAllJobs = ref(false)
async function onPauseAllJobsOk(): Promise<void> {
  await gameServerApi.post('databaseScanJobs/setGlobalPause', {
    isPaused: willPauseAllJobs.value,
  })
  showSuccessNotification(`All scan jobs ${willPauseAllJobs.value ? 'paused' : 'resumed'}.`)
  allScanJobsRefresh()
}

const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []
  if (allScanJobsData.value?.globalPauseIsEnabled === true) {
    allAlerts.push({
      title: 'All Scan Jobs Paused',
      message: 'All database scan jobs are currently paused. You can resume them by clicking the Resume Jobs button.',
      variant: 'warning',
      dataTest: 'all-jobs-paused-alert',
    })
  }
  return allAlerts
})
</script>
