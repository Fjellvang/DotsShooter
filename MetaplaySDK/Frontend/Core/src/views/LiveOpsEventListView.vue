<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(permission="api.liveops_events.view")
  template(#overview)
    MPageOverviewCard(
      title="LiveOps Events"
      data-testid="live-ops-event-list-overview-card"
      )
      p(class="tw-mb-1") LiveOps Events are dynamic in-game events that you can create and manage on this page.
      p(class="tw-text-xs tw-text-neutral-500") LiveOps Events are under active development and not yet feature-complete. They are safe to use in production but the UI and underlying API will change over the next few releases as we continue to work on them.

      template(#buttons)
        //- TODO: trigger refresh for lists on create
        LiveOpsEventFormModalButton(form-mode="create")
        LiveOpsEventActionImport
        LiveOpsEventActionExport

  template(#default)
    core-ui-placement(
      class="tw-mb-3"
      placementId="LiveOpsEvents/List"
      )

    MetaRawData(
      :kvPair="liveOpsEventsData"
      name="liveOpsEventsData"
      )
    MetaRawData(
      :kvPair="liveOpsEventTypesData"
      name="liveOpsEventTypesData"
      )
</template>

<script lang="ts" setup>
import { MetaRawData } from '@metaplay/meta-ui'
import { MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import LiveOpsEventActionExport from '../components/liveopsevents/LiveOpsEventActionExport.vue'
import LiveOpsEventActionImport from '../components/liveopsevents/LiveOpsEventActionImport.vue'
import LiveOpsEventFormModalButton from '../components/liveopsevents/LiveOpsEventFormModalButton.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import {
  getAllLiveOpsEventsSubscriptionOptions,
  getLiveOpsEventTypesSubscriptionOptions,
} from '../subscription_options/liveOpsEvents'

const { data: liveOpsEventsData } = useSubscription(getAllLiveOpsEventsSubscriptionOptions())

const { data: liveOpsEventTypesData } = useSubscription(getLiveOpsEventTypesSubscriptionOptions())
</script>
