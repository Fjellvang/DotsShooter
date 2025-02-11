<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(permission="api.incident_reports.view")
  template(#overview)
    MPageOverviewCard(title="View Player Incident Reports")
      p(class="tw-mb-1") Incident reports are enriched error messages collected from your game clients.
      p(class="tw-text-xs+ tw-text-neutral-500") Errors are collected by the Metaplay Unity SDK and sent on the next successful connection. They contain all the technical details a programmer typically needs to start to diagnose the issue at hand.

  core-ui-placement(placementId="PlayerIncidents/List")
</template>

<script lang="ts" setup>
import { MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
</script>
