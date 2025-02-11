<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MetaListCard(
  title="Database Shards"
  icon="database"
  :itemList="databaseShards"
  permission="api.database.status"
  data-testid="database-shards"
  )
  template(#description) The database type for this environment is #[MBadge(variant="primary") {{ databaseOptions.backend }}].
  template(#item-card="{ item: shard, index }")
    MListItem
      | \#{{ index }}
      template(#top-right) {{ shard.userId }}
      template(#bottom-left)
        div(v-if="databaseOptions.backend === 'MySql'")
          div Database: {{ shard.databaseName }}
          div RW host: {{ shard.readWriteHost }}
          div RO host: {{ shard.readOnlyHost }}
        div(v-if="databaseOptions.backend === 'Sqlite'")
          div File path: {{ shard.filePath }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListCard } from '@metaplay/meta-ui'
import { MBadge, MListItem } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getDatabaseStatusSubscriptionOptions } from '../../subscription_options/general'

const {
  data: databaseStatusData,
  hasPermission: databaseStatusPermission,
  error: databaseStatusError,
} = useSubscription(getDatabaseStatusSubscriptionOptions())

const databaseOptions = computed(() => {
  return databaseStatusData.value?.options.values
})

const databaseShards = computed((): any[] | undefined => {
  return databaseOptions.value?.shards
})
</script>
