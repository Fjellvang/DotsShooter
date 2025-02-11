<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.segmentation.view"
  :is-loading="!singlePlayerSegmentData"
  :error="singlePlayerSegmentError"
  )
  template(#overview)
    MPageOverviewCard(
      :id="singlePlayerSegmentData.details.info.segmentId"
      :title="singlePlayerSegmentData.details.info.displayName"
      :subtitle="singlePlayerSegmentData.details.info.description"
      data-testid="player-segment-detail-overview-card"
      )
      div(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Audience Size Estimate
            b-td(class="text-right") #[meta-audience-size-estimate(:sizeEstimate="singlePlayerSegmentData.details.sizeEstimate")]

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Configuration

  b-row(align-h="center")
    b-col(
      lg="6"
      class="tw-mb-4"
      )
      b-card(
        title="Conditions"
        class="h-100 shadow-sm"
        data-testid="player-segment-detail-conditions-card"
        )
        //- Property requirements
        span(v-if="singlePlayerSegmentData.details.info.playerCondition.propertyRequirements")
          div
            span(class="font-weight-bold") Must match #[span(class="font-weight-bold tw-italic") all] properties
          div(class="pl-2 pr-2 pt-1 pb-1 bg-light rounded border mb-3")
            b-table-simple(
              small
              responsive
              class="m-0"
              )
              b-thead
                b-tr
                  b-th(class="border-0 pl-0") Property
                  b-th(class="border-0 tw-text-center") Min
                  b-th(class="border-0 tw-text-center") Max
              b-tbody
                b-tr(
                  v-for="requirement in singlePlayerSegmentData.details.info.playerCondition.propertyRequirements"
                  :key="requirement.id.displayName"
                  )
                  b-td(
                    style="padding-left: 0.1rem"
                    class="small"
                    ) {{ requirement.id.displayName }}
                  b-td(class="small tw-text-center") {{ requirement.min?.constantValue }}
                  b-td(class="small tw-text-center") {{ requirement.max?.constantValue }}

        //- ANY segment requirements
        span(
          v-if="singlePlayerSegmentData.details.info.playerCondition.requireAnySegment && singlePlayerSegmentData.details.info.playerCondition.requireAnySegment.length > 0"
          )
          div(class="tw-mb-1")
            span(
              v-if="singlePlayerSegmentData.details.info.playerCondition.propertyRequirements"
              class="font-weight-bold"
              ) And must match #[span(class="font-weight-bold tw-italic") any] segments from
            span(
              v-else
              class="font-weight-bold"
              ) Must match #[span(class="font-weight-bold tw-italic") at least one] of these segments:
          MList(showBorder)
            MListItem(
              v-for="requiredSegment in singlePlayerSegmentData.details.info.playerCondition.requireAnySegment"
              :key="requiredSegment"
              class="tw-px-3"
              condensed
              )
              span #[fa-icon(icon="user-tag" class="tw-mr-1")] {{ getSegmentNameById(requiredSegment) }}
              template(#top-right)
                MTextButton(:to="`/segments/${requiredSegment}`") View segment

        //- ALL segment requirements
        span(
          v-if="singlePlayerSegmentData.details.info.playerCondition.requireAllSegments && singlePlayerSegmentData.details.info.playerCondition.requireAllSegments.length > 0"
          )
          div(class="tw-mb-1")
            span(
              v-if="singlePlayerSegmentData.details.info.playerCondition.propertyRequirements || singlePlayerSegmentData.details.info.playerCondition.requireAnySegment"
              class="font-weight-bold"
              ) And must match #[span(class="font-weight-bold tw-italic") all] segments from
            span(
              v-else
              class="font-weight-bold"
              ) Must match #[span(class="font-weight-bold tw-italic") all] of these segments:
          MList(showBorder)
            MListItem(
              v-for="requiredSegment in singlePlayerSegmentData.details.info.playerCondition.requireAllSegments"
              :key="requiredSegment"
              class="tw-px-3"
              condensed
              )
              span #[fa-icon(icon="user-tag" class="tw-mr-1")] {{ getSegmentNameById(requiredSegment) }}
              template(#top-right)
                MTextButton(:to="`/segments/${requiredSegment}`") View segment

    b-col(lg="6")
      meta-list-card(
        title="Referenced by"
        icon="exchange-alt"
        tooltip="Other game systems that reference this segment in their conditions or targeting."
        :itemList="referenceList"
        :searchFields="['displayName', 'type']"
        :filterSets="filterSets"
        :sortOptions="referencesSortOptions"
        emptyMessage="No game systems reference this segment."
        class="h-100"
        data-testid="player-segment-detail-references-card"
        )
        template(#item-card="{ item: segmentReference }")
          MListItem
            MBadge
              template(#icon)
                fa-icon(:icon="segmentReference.icon")
              | {{ segmentReference.displayType }}
            span(class="tw-ml-1") {{ segmentReference.displayName }}

            template(
              v-if="segmentReference.linkUrl"
              #top-right
              )
              MTextButton(:to="segmentReference.linkUrl") View {{ `${segmentReference.linkText.toLocaleLowerCase()}` }}

            template(
              v-if="segmentReference.type === 'Activable'"
              #bottom-right
              )
              //- meta-activable-phase-badge(:activable="slot.item.id") <- TODO figure this out

  meta-raw-data(
    :kvPair="singlePlayerSegmentData"
    name="segment"
    )
</template>

<script lang="ts" setup>
/* eslint-disable @typescript-eslint/no-unsafe-argument */
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MList, MListItem, MPageOverviewCard, MTextButton, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetaAudienceSizeEstimate from '../components/MetaAudienceSizeEstimate.vue'
import { useCoreStore } from '../coreStore'
import { routeParamToSingleValue } from '../coreUtils'
import {
  getPlayerSegmentsSubscriptionOptions,
  getSinglePlayerSegmentSubscriptionOptions,
  getStaticConfigSubscriptionOptions,
} from '../subscription_options/general'

const coreStore = useCoreStore()
const route = useRoute()
const segmentId = routeParamToSingleValue(route.params.id)
/**
 * Fetching all segments for getSegmentNameById function below. Edge case scenario that we want avoid in the future.
 */
const { data: allPlayerSegmentsData } = useSubscription(getPlayerSegmentsSubscriptionOptions())

const { data: singlePlayerSegmentData, error: singlePlayerSegmentError } = useSubscription(
  getSinglePlayerSegmentSubscriptionOptions(segmentId)
)

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

// Segment data ----------------------------------------------------------------------------------------------------

/**
 * Utility function to get the display name of a segment by its ID.
 * TODO: Single endpoint that has segment requirements should return its segment name in addition to segment id.
 * @param id Id of the selected segment
 */
function getSegmentNameById(id: string): string {
  if (allPlayerSegmentsData.value?.segments) {
    return (Object.values(allPlayerSegmentsData.value.segments).find((x: any) => x.info.configKey === id) as any).info
      .displayName
  } else return 'Loading...'
}

/**
 * Utility computed to get the "selected" segment's metadata.
 */
const activablesMetadata = computed(() => {
  return staticConfigData.value?.activablesMetadata
})

/**
 * Information for a single reference.
 */
interface ReferenceDetails {
  type: string
  displayType: string
  displayName: string
  icon: string
  linkUrl: string
  linkText: string
  activableCategory?: string
}

/**
 * Computed to get all segments that reference the "selected" segment.
 */
const referenceList = computed((): ReferenceDetails[] | undefined => {
  if (!singlePlayerSegmentData.value || !activablesMetadata.value) {
    return undefined
  }

  // Decorate references with links
  return singlePlayerSegmentData.value.details.usedBy.map((referenceSource: any) => {
    switch (referenceSource.type) {
      case 'Activable': {
        const kindId = referenceSource.subtype
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
        const categoryId = activablesMetadata.value!.kinds[kindId].category
        const categoryName = activablesMetadata.value?.categories[categoryId].shortSingularDisplayName
        const urlPathName =
          '/' + (coreStore.gameSpecific.activableCustomization[categoryId]?.pathName || `activables/${categoryId}`)
        return {
          type: referenceSource.type,
          displayType: categoryName,
          displayName: referenceSource.displayName,
          icon: coreStore.gameSpecific.activableCustomization[categoryId]?.icon || 'calendar-alt',
          linkUrl: `${urlPathName}/${kindId}/${referenceSource.id}`,
          linkText: categoryName,
          activableCategory: categoryId,
        }
      }
      case 'Offer':
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'tags',
          linkUrl: `/offerGroups/offer/${referenceSource.id}`,
          linkText: referenceSource.type,
        }
      case 'Broadcast':
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'broadcast-tower',
          linkUrl: `/broadcasts/${referenceSource.id}`,
          linkText: referenceSource.type,
        }
      case 'Experiment':
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'flask',
          linkUrl: `/experiments/${referenceSource.id}`,
          linkText: referenceSource.type,
        }
      case 'Notification':
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'comment-alt',
          linkUrl: `/notifications/${referenceSource.id}`,
          linkText: referenceSource.type,
        }
      case 'Segment':
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'user-tag',
          linkUrl: `/segments/${referenceSource.id}`,
          linkText: referenceSource.type,
        }
      case 'LiveOpsEvent':
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'calendar-alt',
          linkUrl: `/liveOpsEvents/${referenceSource.id}`,
          linkText: 'event',
        }
      default:
        return {
          type: referenceSource.type,
          displayType: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'question-circle',
          linkUrl: '',
          linkText: '',
        }
    }
  })
})

// Filtering ----------------------------------------------------------------------------------------------------------

function activableKindFilters(): MetaListFilterOption[] {
  const filters = []
  for (const categoryId in activablesMetadata.value?.categories) {
    const category = activablesMetadata.value.categories[categoryId]
    filters.push(
      new MetaListFilterOption(
        category.displayName,
        (x: any) => x.type === 'Activable' && x.activableCategory === categoryId
      )
    )
  }
  return filters
}

const filterSets = computed((): MetaListFilterSet[] => {
  return [
    new MetaListFilterSet(
      'type',
      [
        new MetaListFilterOption('Segments', (x: any) => x.type === 'Segment'),
        new MetaListFilterOption('Broadcasts', (x: any) => x.type === 'Broadcast'),
        new MetaListFilterOption('Experiments', (x: any) => x.type === 'Experiment'),
        new MetaListFilterOption('Notifications', (x: any) => x.type === 'Notification'),
        new MetaListFilterOption('Offers', (x: any) => x.type === 'Offer'),
        new MetaListFilterOption('LiveOps Events', (x: any) => x.type === 'LiveOpsEvent'),
        ...activableKindFilters(),
      ].sort((a, b) => a.displayName.localeCompare(b.displayName)) // Sort alphabetically
    ),
  ]
})

const referencesSortOptions = [
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Ascending),
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Descending),
]
</script>
