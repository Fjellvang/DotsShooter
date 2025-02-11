<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="dashboard.system.view"
  fullWidth
  )
  template(#overview)
    MPageOverviewCard(title="Manage Deployment")
      p Settings and utilities to help manage this game server.

  MTabLayout(:tabs="tabs")
    template(#tab-0)
      core-ui-placement(placementId="System/Common")

    template(#tab-1)
      core-ui-placement(placementId="System/Advanced")
</template>

<script lang="ts" setup>
import { MPageOverviewCard, MTabLayout, MViewContainer, type TabOption } from '@metaplay/meta-ui-next'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'

const tabs: TabOption[] = [
  {
    label: 'Common Settings',
  },
  {
    label: 'Advanced',
  },
]
</script>
