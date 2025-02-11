<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Note: Error will not trigger toast since that requires this specific data to be fetched at an unique endpoint.
//- This is a common problem shared with SegmentDetailView, OfferGroupDetailView, ActivableDetailView
MViewContainer(
  :is-loading="!singleOfferData"
  :error="singleOfferError"
  permission="api.activables.view"
  )
  template(#overview)
    MPageOverviewCard(
      :id="singleOfferData.config.offerId"
      :title="singleOfferData.config.displayName"
      :subtitle="singleOfferData.config.description"
      data-testid="offer-detail-overview-card"
      )
      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td In App Product
            b-td(
              v-if="singleOfferData.config.inAppProduct !== null"
              class="text-right"
              ) {{ singleOfferData.config.inAppProduct }}
            b-td(
              v-else
              class="text-right text-muted tw-italic"
              ) None
          b-tr(v-if="singleOfferData.referencePrice !== null")
            b-td Reference Price
            b-td(class="text-right") {{ singleOfferData.referencePrice }}

      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="times")] Limits
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Max Activations
            b-td(
              v-if="singleOfferData.config.maxActivationsPerPlayer === null"
              class="text-right text-muted tw-italic"
              ) Unlimited
            b-td(
              v-else
              class="text-right"
              ) {{ singleOfferData.config.maxActivationsPerPlayer }}
          b-tr
            b-td Max Total Purchases
            b-td(
              v-if="singleOfferData.config.maxPurchasesPerPlayer === null"
              class="text-right text-muted tw-italic"
              ) Unlimited
            b-td(
              v-else
              class="text-right"
              ) {{ singleOfferData.config.maxPurchasesPerPlayer }}
          b-tr
            b-td Max Total Purchases Per Offer Group
            b-td(
              v-if="singleOfferData.config.maxPurchasesPerOfferGroup === null"
              class="text-right text-muted tw-italic"
              ) Unlimited
            b-td(
              v-else
              class="text-right"
              ) {{ singleOfferData.config.maxPurchasesPerOfferGroup }}
          b-tr
            b-td Max Purchases Per Activation
            b-td(
              v-if="singleOfferData.config.maxPurchasesPerActivation === null"
              class="text-right text-muted tw-italic"
              ) Unlimited
            b-td(
              v-else
              class="text-right"
              ) {{ singleOfferData.config.maxPurchasesPerActivation }}

      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-line")] Statistics
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td #[MTooltip(content="Total value across all offer groups.") Global] Seen by
            b-td(class="text-right") #[meta-plural-label(:value="singleOfferData.statistics.numActivatedForFirstTime" label="player")]
          b-tr
            b-td #[MTooltip(content="Total value across all offer groups.") Global] Purchased by
            b-td(class="text-right") #[meta-plural-label(:value="singleOfferData.statistics.numPurchasedForFirstTime" label="player")]
          b-tr
            b-td #[MTooltip(content="Total value across all offer groups.") Global] Conversion
            b-td(class="text-right")
              MTooltip(:content="conversionTooltip") {{ conversionRate.toFixed(0) }}%
          b-tr
            b-td Total #[MTooltip(content="Total value across all offer groups.") Global] Seen Count
            b-td(class="text-right") #[meta-plural-label(:value="singleOfferData.statistics.numActivated" label="time")]
          b-tr
            b-td Total #[MTooltip(content="Total value across all offer groups.") Global] Purchased Count
            b-td(class="text-right") #[meta-plural-label(:value="singleOfferData.statistics.numPurchased" label="time")]
          b-tr
            b-td Total #[MTooltip(content="Total value across all offer groups.") Global] Revenue
            b-td(class="text-right") ${{ singleOfferData.statistics.revenue.toFixed(2) }}

  template(#default)
    b-row(
      no-gutters
      align-v="center"
      class="mb-2 tw-mt-4"
      )
      h3 Configuration

    b-row(class="mb-2 tw-mt-4")
      b-col(
        md="6"
        class="tw-mb-4"
        )
        meta-list-card(
          title="Contents"
          :itemList="rewards"
          listLayout="flex"
          data-testid="offer-detail-contents-card"
          )
          template(v-slot:item-card="slot")
            meta-reward-badge(:reward="slot.item")

      b-col(
        md="6"
        class="tw-mb-4"
        )
        meta-list-card(
          title="Referenced by"
          icon="exchange-alt"
          :itemList="referenceList"
          :filterSets="filterSets"
          :sortOptions="referencesSortOptions"
          :searchFields="['displayName', 'type']"
          emptyMessage="No offer groups or other offers reference this offer."
          data-testid="offer-detail-references-card"
          )
          template(#item-card="{ item: reference }")
            MListItem
              MBadge
                template(#icon)
                  fa-icon(:icon="reference.icon")
                | {{ reference.type }}
              span(class="tw-ml-1") {{ reference.displayName }}

              template(
                v-if="reference.linkUrl"
                #top-right
                )
                MTextButton(:to="reference.linkUrl") View {{ `${reference.linkText.toLocaleLowerCase()}` }}

    b-row(
      no-gutters
      align-v="center"
      class="mb-2 tw-mt-4"
      )
      h3 Targeting

    b-row(align-h="center")
      b-col(
        md="6"
        class="tw-mb-4"
        )
        segments-card(
          :segments="singleOfferData.config.segments"
          ownerTitle="This event"
          )
      b-col(
        md="6"
        class="tw-mb-4"
        )
        player-conditions-card(:playerConditions="singleOfferData.config.additionalConditions")

    meta-raw-data(
      :kvPair="singleOfferData"
      name="offer"
      )
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import {
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortDirection,
  MetaListSortOption,
  rewardsWithMetaData,
} from '@metaplay/meta-ui'
import { MBadge, MListItem, MPageOverviewCard, MTextButton, MTooltip, MViewContainer } from '@metaplay/meta-ui-next'
import { abbreviateNumber } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import PlayerConditionsCard from '../components/global/PlayerConditionsCard.vue'
import SegmentsCard from '../components/global/SegmentsCard.vue'
import { routeParamToSingleValue } from '../coreUtils'
import { getSingleOfferSubscriptionOptions } from '../subscription_options/offers'

const route = useRoute()
const offerId = routeParamToSingleValue(route.params.id)

// Offer data ----------------------------------------------------------------------------------------------------

const { data: singleOfferData, error: singleOfferError } = useSubscription(getSingleOfferSubscriptionOptions(offerId))

/**
 * Conversion rate for the offer.
 */
const conversionRate = computed(() => {
  const activated = singleOfferData.value.statistics.numActivatedForFirstTime
  const purchased = singleOfferData.value.statistics.numPurchasedForFirstTime
  if (activated === 0) {
    return 0
  } else {
    return (purchased / activated) * 100
  }
})

/**
 * Tooltip for the conversion rate.
 */
const conversionTooltip = computed(() => {
  const activated = singleOfferData.value.statistics.numActivatedForFirstTime
  const purchased = singleOfferData.value.statistics.numPurchasedForFirstTime
  if (activated === 0) {
    return 'Not activated by any players yet.'
  } else {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return `Activated by ${abbreviateNumber(activated)} players and purchased by ${abbreviateNumber(purchased)}.`
  }
})

/**
 * Rewards with metadata.
 */
const rewards = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  return rewardsWithMetaData(singleOfferData.value.config.rewards || [])
})

/**
 * Information for a single reference.
 */
interface ReferenceDetails {
  type: string
  displayName: string
  icon: string
  linkUrl: string
  linkText: string
}

/**
 * List of places where this offer is referenced.
 */
const referenceList = computed((): ReferenceDetails[] => {
  return singleOfferData.value.usedBy.map((referenceSource: any) => {
    switch (referenceSource.type) {
      case 'OfferGroup':
        return {
          type: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'tags',
          linkUrl: `/offerGroups/offerGroup/${referenceSource.id}`,
          linkText: 'Offer Group',
        }
      case 'Offer':
        return {
          type: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'tags',
          linkUrl: `/offerGroups/offer/${referenceSource.id}`,
          linkText: 'Offer',
        }
      default:
        return {
          type: referenceSource.type,
          displayName: referenceSource.displayName,
          icon: 'question-circle',
          linkUrl: '',
          linkText: '',
        }
    }
  })
})

// Search, sort, filter ------------------------------------------------------------------------------------------------

const referencesSortOptions = [
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Ascending),
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Descending),
]

const filterSets = computed(() => {
  return [
    new MetaListFilterSet(
      'type',
      [
        new MetaListFilterOption('Offer Groups', (x: any) => x.type === 'OfferGroup'),
        new MetaListFilterOption('Offers', (x: any) => x.type === 'Offer'),
      ].sort((a, b) => {
        const nameA = a.displayName.toUpperCase()
        const nameB = b.displayName.toUpperCase()
        if (nameA < nameB) {
          return -1
        } else if (nameA > nameB) {
          return 1
        } else {
          return 0
        }
      })
    ),
  ]
})
</script>
