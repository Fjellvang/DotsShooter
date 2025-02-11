// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { App } from 'vue'
import { defineAsyncComponent } from 'vue'

import MetaAbbreviateNumber from './components/MetaAbbreviateNumber.vue'
import MetaAlert from './components/MetaAlert.vue'
import MetaBarChart from './components/MetaBarChart.vue'
import MetaCountryCode from './components/MetaCountryCode.vue'
import MetaDuration from './components/MetaDuration.vue'
import MetaInputGuildSelect from './components/MetaInputGuildSelect.vue'
import MetaInputPlayerSelect from './components/MetaInputPlayerSelect.vue'
import MetaInputSelect from './components/MetaInputSelect.vue'
import MetaIpAddress from './components/MetaIpAddress.vue'
import MetaLazyLoader from './components/MetaLazyLoader.vue'
import MetaListCard from './components/MetaListCard.vue'
import MetaNoSeatbelts from './components/MetaNoSeatbelts.vue'
import MetaOrdinalNumber from './components/MetaOrdinalNumber.vue'
import MetaPluralLabel from './components/MetaPluralLabel.vue'
import MetaRawData from './components/MetaRawData.vue'
import MetaTime from './components/MetaTime.vue'
import MetaToast from './components/MetaToast.vue'
import MetaUsername from './components/MetaUsername.vue'
import { useToastsVuePlugin } from './toasts'

// Vue plugin to register all the global Metaplay components
export default function (app: App): void {
  app.component(
    'MetaAbbreviateNumber',
    defineAsyncComponent(async () => await import('./components/MetaAbbreviateNumber.vue'))
  )
  app.component(
    'MetaCountryCode',
    defineAsyncComponent(async () => await import('./components/MetaCountryCode.vue'))
  )
  app.component(
    'MetaDuration',
    defineAsyncComponent(async () => await import('./components/MetaDuration.vue'))
  )
  app.component(
    'MetaInputSelect',
    defineAsyncComponent(async () => await import('./components/MetaInputSelect.vue'))
  )
  app.component(
    'MetaInputPlayerSelect',
    defineAsyncComponent(async () => await import('./components/MetaInputPlayerSelect.vue'))
  )
  app.component(
    'MetaInputGuildSelect',
    defineAsyncComponent(async () => await import('./components/MetaInputGuildSelect.vue'))
  )
  app.component(
    'MetaIpAddress',
    defineAsyncComponent(async () => await import('./components/MetaIpAddress.vue'))
  )
  app.component(
    'MetaLazyLoader',
    defineAsyncComponent(async () => await import('./components/MetaLazyLoader.vue'))
  )
  app.component(
    'MetaListCard',
    defineAsyncComponent(async () => await import('./components/MetaListCard.vue'))
  )
  app.component(
    'MetaNoSeatbelts',
    defineAsyncComponent(async () => await import('./components/MetaNoSeatbelts.vue'))
  )
  app.component(
    'MetaOrdinalNumber',
    defineAsyncComponent(async () => await import('./components/MetaOrdinalNumber.vue'))
  )
  app.component(
    'MetaPluralLabel',
    defineAsyncComponent(async () => await import('./components/MetaPluralLabel.vue'))
  )
  app.component(
    'MetaRawData',
    defineAsyncComponent(async () => await import('./components/MetaRawData.vue'))
  )
  app.component(
    'MetaToast',
    defineAsyncComponent(async () => await import('./components/MetaToast.vue'))
  )
  app.component(
    'MetaTime',
    defineAsyncComponent(async () => await import('./components/MetaTime.vue'))
  )
  app.component(
    'MetaUsername',
    defineAsyncComponent(async () => await import('./components/MetaUsername.vue'))
  )
  app.component(
    'MetaRewardBadge',
    defineAsyncComponent(async () => await import('./components/MetaRewardBadge.vue'))
  )
  app.component(
    'MetaAlert',
    defineAsyncComponent(async () => await import('./components/MetaAlert.vue'))
  )
  app.component(
    'MetaBarChart',
    defineAsyncComponent(async () => await import('./components/MetaBarChart.vue'))
  )

  // eslint-disable-next-line @typescript-eslint/no-deprecated -- Known issue. Refactor.
  app.use(useToastsVuePlugin)
}

export type {
  MetaInputSelectOption,
  PlayerDeletionStatus,
  ActivePlayerInfo,
  PlayerListItem,
  GuildSearchResult,
  BulkListInfo,
  PlayerRawInfo,
  PlayerRawInfoResult,
} from './additionalTypes'

export {
  getLanguageName,
  humanizeUsername,
  isoCodeToCountryFlag,
  isoCodeToCountryName,
  parseAuthMethods, // Should this be refactored away?
  getObjectPrintableFields,
  roughSizeOfObject,
  experimentPhaseDetails,
} from './utils/utils'

export {
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortDirection,
  MetaListSortOption,
} from './utils/metaListUtils'

export { useUiStore } from './uiStore'

export {
  MetaToast,
  MetaAbbreviateNumber,
  MetaCountryCode,
  MetaDuration,
  MetaInputSelect,
  MetaInputPlayerSelect,
  MetaInputGuildSelect,
  MetaIpAddress,
  MetaLazyLoader,
  MetaListCard,
  MetaNoSeatbelts,
  MetaOrdinalNumber,
  MetaPluralLabel,
  MetaRawData,
  MetaTime,
  MetaUsername,
  MetaAlert,
  MetaBarChart,
}

export type { GameSpecificReward } from './utils/rewardUtils'

export { rewardWithMetaData, rewardsWithMetaData } from './utils/rewardUtils'
