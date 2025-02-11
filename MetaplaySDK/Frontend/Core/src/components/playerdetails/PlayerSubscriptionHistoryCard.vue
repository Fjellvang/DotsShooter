<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  meta-list-card(
    title="Subscription History"
    icon="money-check-alt"
    :itemList="subscriptionList"
    :sortOptions="sortOptions"
    emptyMessage="No subscriptions... yet!"
    class="tw-mb-4"
    data-testid="player-subscriptions-history-card"
    )
    //- Mapping slot.item to subscription for convenience
    template(#item-card="{ item: subscription }")
      MCollapse(extraMListItemMargin)
        //- Row header
        template(#header)
          MListItem(noLeftPadding) {{ subscription.productId }}
            span(class="tw-ml-1 tw-text-neutral-500") ${{ subscription.spend.toFixed(2) }}

            template(#badge)
              MBadge(
                v-if="isActive(subscription.expirationTime)"
                variant="success"
                ) Active
              MBadge(v-else) Expired

            template(#top-right)
              meta-time(
                :date="subscription.startTime"
                showAs="date"
                )

            template(#bottom-left)
              //- Expired due to re-use
              div(
                v-if="subscription.latestInstanceIsDisabledDueToReuse"
                class="tw-text-red-500"
                ) The subscription was disabled because it was used on another player account.

            template(#bottom-right)
              //- Active
              div(v-if="isActive(subscription.expirationTime)")
                span(v-if="subscription.renewalStatus === 'ExpectedToRenew'") Renewing #[meta-time(:date="subscription.expirationTime")]
                span(v-else-if="subscription.renewalStatus === 'NotExpectedToRenew'") Set to expire in #[meta-time(:date="subscription.expirationTime")]
                span(
                  v-else
                  class="tw-text-orange-400"
                  ) Unhandled renewal status: {{ subscription.renewalStatus }}
              //- Expired naturally
              div(v-else) Expired #[meta-time(:date="subscription.expirationTime")]

        //- Collapse content
        div(class="font-weight-bold") Overview
        MList(
          showBorder
          striped
          class="tw-my-2"
          )
          MListItem(condensed) Start time
            template(#top-right) #[meta-time(:date="subscription.startTime" showAs="datetime")]
          MListItem(condensed) Expiration time
            template(#top-right) #[meta-time(:date="subscription.expirationTime" showAs="datetime")]
          MListItem(condensed) Auto-renewal status
            template(#top-right)
              MBadge(
                v-if="subscription.renewalStatus === 'ExpectedToRenew'"
                variant="success"
                ) On
              MBadge(v-else-if="subscription.renewalStatus === 'NotExpectedToRenew'") Off
              span(v-else) {{ subscription.renewalStatus }}

        div(
          v-if="subscription.subscriptionInstances?.length > 0"
          class="font-weight-bold tw-mb-1"
          ) Individual Subscriptions
          MList(
            showBorder
            striped
            class="tw-my-2"
            )
            MListItem(
              v-for="(instance, instanceId) in subscription.subscriptionInstances"
              :key="instanceId"
              class="tw-px-3"
              striped
              ) {{ instance.platform }}
              MTooltip(
                :content="instance.spendExplanation"
                class="tw-ml-1"
                ) ${{ instance.spend.toFixed(2) }}
              template(#top-right)
                MBadge(
                  v-if="isActive(instance.lastKnownState.expirationTime)"
                  variant="success"
                  ) Active
                MBadge(v-else) Expired
              template(#bottom-left)
                span {{ instanceId }}
                div(
                  v-if="instance.disabledDueToReuse"
                  class="text-danger"
                  )
                  div The subscription was disabled because it was used on {{ maybePluralString(instance.otherPlayers.length, 'other player account') }}:
                  div(v-for="otherPlayerId in instance.otherPlayers") #[MTextButton(:to="`/players/${otherPlayerId}`") {{ otherPlayerId }}]

              template(#bottom-right)
                div(
                  v-if="instance.lastKnownState"
                  class="tw-text-right"
                  ) Start: #[meta-time(:date="instance.lastKnownState.startTime")]
                div(
                  v-if="instance.lastKnownState"
                  class="tw-text-right"
                  ) Expiry: #[meta-time(:date="instance.lastKnownState.expirationTime")]
                div(
                  v-if="instance.lastKnownState"
                  class="tw-text-right"
                  ) Periods: {{ instance.lastKnownState.numPeriods }}

        div(class="tw-mt-1 tw-inline-flex tw-w-full tw-justify-end")
          MActionModalButton(
            v-if="enableSubscriptionRemoving"
            modal-title="Detach Subscription"
            :action="() => removeSubscription(subscription.productId)"
            trigger-button-label="Detach Subscription"
            variant="danger"
            ok-button-label="Detach Subscription"
            permission="api.players.remove_iap_subscription"
            @show="selectedSubscription = subscription"
            )
            p You are about to detach the #[MBadge {{ selectedSubscription.productId }}] subscription from #[MBadge {{ playerData.model.playerName }}]. This will also remove the related subscription IAP from the player's purchase history.
            div(class="tw-text-xs+ tw-text-neutral-500") Players can re-activate their subscriptions by reclaiming IAPs from within the game. Detaching a subscription does not refund the purchase and instead is primarily a development-only tool.
            meta-no-seatbelts(
              :name="playerData.model.playerName"
              class="tw-mt-4"
              )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import { useGameServerApi, useStaticInfos } from '@metaplay/game-server-api'
import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import {
  MActionModalButton,
  MBadge,
  MCollapse,
  MList,
  MListItem,
  MTextButton,
  MTooltip,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../coreStore'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player whose subscription history we want to show.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const coreStore = useCoreStore()
const staticInfos = useStaticInfos()
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))
const sortOptions = [
  new MetaListSortOption('Start time', 'startTime', MetaListSortDirection.Ascending),
  new MetaListSortOption('Start time', 'startTime', MetaListSortDirection.Descending),
  new MetaListSortOption('Expiry time', 'expirationTime', MetaListSortDirection.Ascending),
  new MetaListSortOption('Expiry time', 'expirationTime', MetaListSortDirection.Descending),
]
const selectedSubscription = ref<any>({})

const enableSubscriptionRemoving = computed((): boolean => {
  return staticInfos.featureFlags.removeIapSubscriptions || playerData.value.model.isDeveloper
})

const subscriptionList = computed(() => {
  // Create a lookup for past subscription purchases, to
  // enrich the subscription data with some purchase info.
  // Key is the originalTransactionId of the purchase.
  const subscriptionInstancePurchases: any = {}
  for (const purchaseEvent of playerData.value.model.inAppPurchaseHistory) {
    // Ignore purchases of non-subscription products.
    if (purchaseEvent.subscriptionQueryResult === null) {
      continue
    }

    const originalTransactionId = purchaseEvent.originalTransactionId
    subscriptionInstancePurchases[originalTransactionId] = purchaseEvent
  }

  // Create the subscriptions list.
  const subscriptions = []
  for (const productId in playerData.value.model.iapSubscriptions.subscriptions) {
    const subscription = playerData.value.model.iapSubscriptions.subscriptions[productId]

    let subscriptionSpend = 0

    // Enrich subscriptionInstances entries, and calculate total spend on subscription.
    const instances: any = {}
    for (const instanceId in subscription.subscriptionInstances) {
      const instance = subscription.subscriptionInstances[instanceId]
      const purchase = subscriptionInstancePurchases[instanceId]
      const platform = purchase?.platform // \note purchase should never be missing - the `?.` is just being defensive
      const referencePrice = purchase?.referencePrice ?? 0 // \note ditto

      let instanceSpend
      let instanceSpendExplanation
      if (instance.disabledDueToReuse) {
        instanceSpend = 0
        instanceSpendExplanation =
          'Spend is omitted from this subscription, because it is being used on another player account.'
      } else if (instance.lastKnownState?.isAcquiredViaFamilySharing ?? false) {
        instanceSpend = 0
        instanceSpendExplanation =
          'Spend is omitted from this subscription, because this player acquired it via Family Sharing instead of purchasing it themselves.'
      } else {
        const numPeriods = instance.lastKnownState?.numPeriods ?? 1
        instanceSpend = numPeriods * referencePrice
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        instanceSpendExplanation = `${maybePluralString(numPeriods, 'period')}, reference price ${referencePrice}`
      }

      subscriptionSpend += instanceSpend

      instances[instanceId] = {
        platform,
        spend: instanceSpend,
        spendExplanation: instanceSpendExplanation,
        referencePrice,
        otherPlayers: playerData.value.iapSubscriptionsExtra.instances[instanceId].otherPlayers,
        ...instance,
      }
    }

    subscriptions.unshift({
      productId,
      spend: subscriptionSpend,
      ...subscription,

      // \note This must be after the `...subscription` part, because it
      //       overwrites the subscriptionInstances property of `subscription`.
      subscriptionInstances: instances,
    })
  }
  return subscriptions
})

const { showSuccessNotification } = useNotifications()

async function removeSubscription(productId: string): Promise<void> {
  await gameServerApi.post(`/players/${playerData.value.model.playerId}/removeIAPSubscription/${productId}`)
  showSuccessNotification(`Subscription '${productId}' detached from this player.`)
  playerRefresh()
}

function resetModal(): void {
  selectedSubscription.value = {}
}

function isActive(expiryDateIsoString: string): boolean {
  return (
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    DateTime.fromISO(playerData.value.model.currentTime) < DateTime.fromISO(expiryDateIsoString)
  )
}
</script>
