<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!allActiveGuildsData"
  :error="allActiveGuildsError"
  )
  //- Search
  b-row(
    class="justify-content-center"
    style="margin-top: 8rem; margin-bottom: 7rem"
    )
    b-col(
      md="10"
      lg="7"
      xl="5"
      )
      h4(
        class="tw-mb-1 tw-ml-2"
        data-testid="guild-search"
        ) #[fa-icon(icon="search")] Find a guild
      meta-input-guild-select(
        :value="selectedGuild"
        @input="onGuildSelected"
        )

  //- Active guilds list. Shown only when developer flag is on.
  div(v-if="uiStore.showDeveloperUi")
    div(
      v-if="!allActiveGuildsData"
      class="pt-5 tw-w-full tw-text-center"
      )
      b-spinner(
        label="Loading..."
        class="mt-5"
        )/

    div(v-else)
      MSingleColumnLayout
        MCard(
          title="Recently Active Guilds"
          data-testid="active-guilds-list"
          )
          MCallout(
            v-if="allActiveGuildsData.length === 0"
            title="No recently active guilds"
            variant="neutral"
            ) No guilds have been active since last server reboot.

          b-table(
            v-else
            small
            striped
            hover
            responsive
            :items="allActiveGuildsData"
            :fields="tableFields"
            primary-key="entityId"
            sort-by="startAt"
            sort-desc
            :tbody-tr-class="rowClass"
            class="table-fixed-column"
            @row-clicked="rowClicked"
            )
            template(#cell(entityId)="data")
              span {{ data.item.entityId }}

            template(#cell(displayName)="data")
              span(v-if="data.item.phase === 'Closed'") ☠️
              span(v-else) {{ data.item.displayName }}

            template(#cell(numMembers)="data")
              span {{ data.item.numMembers }}

            template(#cell(lastLoginAt)="data")
              meta-time(
                v-if="!isEpochTime(data.item.lastLoginAt)"
                :date="data.item.lastLoginAt"
                disableTooltip
                showAs="timeagoSentenceCase"
                )
              span(v-else) Closed

  MetaRawData(
    :kvPair="allActiveGuildsData"
    name="allActiveGuilds"
    )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import type { GuildSearchResult } from '@metaplay/meta-ui'
import { useUiStore } from '@metaplay/meta-ui'
import { MCallout, MCard, MSingleColumnLayout, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { isEpochTime } from '../coreUtils'
import { getActiveGuildsSubscriptionOptions } from '../subscription_options/guilds'

const uiStore = useUiStore()

const { data: allActiveGuildsData, error: allActiveGuildsError } = useSubscription(getActiveGuildsSubscriptionOptions())

type BTableField = string | { key: string; label: string }

const tableFields = computed((): BTableField[] => {
  const allFields: BTableField[] = [
    {
      key: 'entityId',
      label: 'ID',
    },
    {
      key: 'displayName',
      label: 'Name',
    },
    {
      key: 'numMembers',
      label: 'Members',
    },
    {
      key: 'lastLoginAt',
      label: 'Last Active',
    },
  ]
  if (Math.max(document.documentElement.clientWidth || 0, window.innerWidth || 0) < 576) {
    return filterTableFields(allFields, ['entityId', 'displayName'])
  } else {
    return allFields
  }
})

function filterTableFields(allFields: BTableField[], desiredFields: string[]): BTableField[] {
  return allFields.filter((element) => {
    let key
    if (typeof element === 'string') {
      key = element
    } else {
      key = element.key
    }
    return desiredFields.includes(key)
  })
}

const router = useRouter()

const selectedGuild = ref<GuildSearchResult>()

async function onGuildSelected(guild: GuildSearchResult): Promise<void> {
  selectedGuild.value = guild
  await router.push(`/guilds/${guild.entityId}`)
}

async function rowClicked(item: any): Promise<void> {
  await router.push(`/guilds/${item.entityId}`)
}

function rowClass(item: any, type: string): string | undefined {
  if (!item || type !== 'row') {
    return
  }
  if (item.phase === 'Closed') {
    return 'text-danger table-row-link'
  } else {
    return 'table-row-link'
  }
}
</script>
