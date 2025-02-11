<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  :title="title"
  :icon="categoryIcon"
  :itemList="decoratedActivables"
  :getItemKey="getItemKey"
  :pageSize="longList ? 50 : undefined"
  :searchFields="searchFields"
  :filterSets="filterSets || defaultFilterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  :emptyMessage="emptyMessage"
  :data-testid="`${camelCaseToKebabCase(props.category)}-list-card`"
  )
  template(#item-card="{ item: activableInfo }")
    MCollapse(
      :id="hideCollapse ? '' : activableInfo.activable.config.activableId"
      extra-m-list-item-margin
      :hide-collapse="hideCollapse"
      )
      //- Row header
      template(#header)
        MListItem(no-left-padding) {{ activableInfo.activable.config.displayName }}
          template(#badge)
            span(
              v-if="activableInfo.activable.config.displayShortInfo"
              class="tw-text-neutral-500"
              ) ({{ activableInfo.activable.config.displayShortInfo }})

          template(#top-right)
            MBadge(
              v-if="playerId && activableInfo.activable.debugState"
              :tooltip="activableInfo.debugPhaseTooltip"
              variant="warning"
              ) {{ getPhaseDisplayString(activableInfo.activable) }}
            meta-activable-phase-badge(
              v-else
              :activable="activableInfo.activable"
              :playerId="props.playerId"
              :typeName="`${categoryDisplayName?.toLocaleLowerCase()}`"
              )

          template(#bottom-left) {{ activableInfo.activable.config.description }}
          template(#bottom-right)
            div(
              v-if="activableInfo.activable.debugState"
              class="text-warning"
              ) Debug override!
            div(v-else-if="activableInfo.nextPhase") {{ getPhaseDisplayString(activableInfo.activable, activableInfo.nextPhase) }} #[meta-time(:date="String(activableInfo.nextPhaseStartTime)")]
            slot(
              name="additionalTexts"
              v-bind:item="activableInfo"
              v-bind:index="`${activableInfo.activable.activableId}`"
              )
            div(v-if="!props.hideConversion")
              MTooltip(:content="String(activableInfo.conversionTooltip)") {{ activableInfo.conversion.toFixed() }}% conversion
            MTextButton(
              v-if="activableInfo.linkUrl"
              :to="activableInfo.linkUrl"
              :data-testid="`view-${camelCaseToKebabCase(props.category)}`"
              ) View {{ `${activableInfo.linkText.toLocaleLowerCase()}` }}

      MList(
        v-if="!hideCollapse"
        showBorder
        class="tw-mx-5"
        )
        slot(
          name="collapseContents"
          v-bind:item="activableInfo"
          v-bind:index="`${activableInfo.activable.activableId}`"
          )

      //- Debug controls
      div(
        v-if="playerId && enableDevelopmentFeatures"
        class="border rounded-sm bg-light tw-mx-5 tw-my-2 tw-px-5 tw-py-3"
        )
        div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="wrench")] Debug Controls
        div(class="small text-muted mb-2") You can force this player into different activation states to test the feature easier. Just remember to clear the debug state afterwards!
        div(class="d-sm-flex justify-content-between align-content-center")
          label(
            for="inline-form-debug-controls"
            class="sr-only"
            ) Debug Phase
          meta-input-select(
            id="inline-form-debug-controls"
            :value="selectedDebugPhase[activableKey(activableInfo)] ?? 'undefined'"
            :options="debugPhaseOptions"
            no-clear
            @input="selectedDebugPhase[activableKey(activableInfo)] = $event"
            )
          div(
            :style="activableInfo.activable.debugState ? 'min-width: 16rem' : 'min-width: 7rem'"
            class="tw-relative tw-top-1 tw-mr-2 tw-text-right"
            )
            MButton(
              variant="warning"
              size="small"
              :disabled-tooltip="!hasValidSelectedDebugPhase(activableInfo) ? 'Select a phase.' : undefined"
              permission="api.players.force_activable_phase"
              @click="forceSelectedPhase(activableInfo)"
              ) Set Phase
            MButton(
              v-if="activableInfo.activable.debugState"
              size="small"
              permission="api.players.force_activable_phase"
              @click="clearDebugPhase(activableInfo)"
              ) Clear Debug Phase
      div(
        v-else
        class="tw-my-3 tw-text-center tw-italic tw-text-neutral-400"
        ) Debug controls disabled in current environment.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MetaListFilterOption,
  MetaListFilterSet,
  type MetaListSortOption,
  type MetaInputSelectOption,
} from '@metaplay/meta-ui'
import {
  MButton,
  MBadge,
  MCollapse,
  MList,
  MListItem,
  MTextButton,
  MTooltip,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { abbreviateNumber, camelCaseToKebabCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import * as Activables from '../../activables'
import { useCoreStore } from '../../coreStore'
import { getIsActivableForcePhaseEnabledSubscriptionOptions } from '../../subscription_options/activables'
import { getStaticConfigSubscriptionOptions } from '../../subscription_options/general'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'
import MetaActivablePhaseBadge from './MetaActivablePhaseBadge.vue'

const props = withDefaults(
  defineProps<{
    activables?: any
    /**
     * Optional: If true hide disabled activable data.
     */
    hideDisabled?: boolean
    /**
     * Optional: If true hide activable conversion statistics.
     */
    hideConversion?: boolean
    /**
     * Optional: Id of the player whose data we are interested in.
     */
    playerId?: string
    /**
     * Group name assigned to similar activables.
     * @example 'In-game events'
     */
    category: string
    /**
     * Optional: If true show the 50 list items on one page.
     * Defaults 8.
     */
    longList?: boolean
    /**
     * Custom message to be shown when there are no activables available to display.
     */
    emptyMessage: string
    /**
     * Title to be shown on the card.
     */
    title: string
    /**
     * Optional: If true renders a non-collapsible list card.
     */
    hideCollapse?: boolean
    /**
     * Optional: String to be added at the beginning of the URL.
     * @example "/offerGroups"
     */
    linkUrlPrefix?: string
    /**
     * Optional: Array of list item property names that can be searched.
     */
    searchFields?: string[]
    /**
     * Optional: List of filter sets that can be applied.
     */
    filterSets?: MetaListFilterSet[]
    /**
     * Optional: List of sort types that can be applied.
     */
    sortOptions?: MetaListSortOption[]
    /**
     * Optional: Index of sort option to choose by default.
     */
    defaultSortOption?: number
  }>(),
  {
    playerId: undefined,
    linkUrlPrefix: undefined,
    activables: undefined,
    searchFields: undefined,
    filterSets: undefined,
    sortOptions: undefined,
    defaultSortOption: 1,
  }
)

const gameServerApi = useGameServerApi()
const coreStore = useCoreStore()
const emits = defineEmits(['refreshActivables'])

/**
 * Subscribe to the static config data.
 */
const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

/**
 * Subscription check if 'force phase debug controls' is enabled for the activable.
 */
const { data: activablesForcePhaseEnabled, refresh: refreshActivablesForcePhaseEnabled } = useSubscription(
  getIsActivableForcePhaseEnabledSubscriptionOptions()
)

/**
 * Subscribe to player data when a playerId is provided. We watch the id and resubscribe as it changes.
 */
const { data: playerData } = useSubscription(() =>
  props.playerId ? getSinglePlayerSubscriptionOptions(props.playerId) : undefined
)

// Activable data -----------------------------------------------------------------------------------------------------

interface ActivableInfo {
  activable: any
  kindId: string
  linkUrl: string
  linkText: string
  nextPhase: string | undefined
  nextPhaseStartTime: string | undefined
  phaseSortOrder: number
  phaseDisplayString: string
  conversion: number
  conversionTooltip: string | number
  debugPhaseTooltip: string | undefined
  scheduleStatus?: any
}
/**
 * Activable data to be displayed in this card.
 */
const decoratedActivables = computed(() => {
  if (props.activables && activablesMetadata.value) {
    const categoryData = activablesMetadata.value.categories[props.category]
    const activableInfos: ActivableInfo[] = []
    for (const activableKind of categoryData.kinds) {
      const kindInfo = activablesMetadata.value.kinds[activableKind]
      activableInfos.push(
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        ...Object.values(props.activables?.[activableKind].activables).map((activable: any) => {
          const scheduleStatus = activable.scheduleStatus
          const statistics = activable.statistics
          return {
            activable,
            kindId: activableKind,
            // FIXME: Category does not exist?
            linkUrl:
              (props.linkUrlPrefix ?? `/activables/${kindInfo.category}`) +
              `/${activableKind}/${activable.config.activableId}`,
            linkText: categoryData.shortSingularDisplayName,
            nextPhase: scheduleStatus?.nextPhase ? scheduleStatus.nextPhase.phase : undefined,
            nextPhaseStartTime: scheduleStatus?.nextPhase ? scheduleStatus.nextPhase.startTime : undefined,
            phaseSortOrder: Activables.phaseSortOrder(
              // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
              activable,
              null,
              playerData.value
            ),
            phaseDisplayString: Activables.phaseDisplayString(
              // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
              activable,
              null,
              playerData.value
            ),
            conversion: props.hideConversion
              ? 0
              : conversion(statistics.numConsumedForFirstTime, statistics.numActivatedForFirstTime),
            conversionTooltip: props.hideConversion
              ? 0
              : conversionTooltip(statistics.numConsumedForFirstTime, statistics.numActivatedForFirstTime),
            debugPhaseTooltip: debugPhaseTooltip(activable),
          }
        })
      )
    }
    return activableInfos.filter(
      (x: any) => !props.hideDisabled || x.activable.config.activableParams.isEnabled === true
    )
  } else return undefined
})

/**
 * Additional data about the activable.
 */
const activablesMetadata = computed(() => {
  return staticConfigData.value?.activablesMetadata
})

// Debug controls --------------------------------------------------------------------------------------------------

/**
 * When true the debug controls are visible on the dashboard.
 */
const enableDevelopmentFeatures = computed(() => {
  return activablesForcePhaseEnabled.value?.forcePlayerActivablePhaseEnabled || false
})

/**
 * The phase to forcibly activate.
 */
const selectedDebugPhase: any = ref({})

/**
 * List of all debug phases displayed as dropdown options.
 */
const debugPhaseOptions = computed((): Array<MetaInputSelectOption<string>> => {
  const phases = ['Preview', 'Active', 'EndingSoon', 'Review', 'Inactive']

  const options = phases.map((phase) => {
    return { value: phase, id: Activables.phaseToDisplayString(phase) }
  })
  // \todo Figure out a better way to have a default option than having the string 'undefined' here.
  //       Can we populate the initial selectedDebugPhase for all the activables?
  options.unshift({ value: 'undefined', id: 'Select a phase' })
  return options
})

/**
 * Text to show the phase badge when an activiable is in a forced debug state.
 */
function getPhaseDisplayString(activable: any, phase?: string): string {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  return Activables.phaseDisplayString(activable, phase, playerData.value)
}

/**
 * Tooltip text to show the phase when an activiable is in a forced debug state.
 */
function debugPhaseTooltip(activable: any): string | undefined {
  if (!activable.debugState) {
    return undefined
  } else {
    return `${activable.config.activableId} is in debug-forced phase '${activable.debugState.phase}'`
  }
}

/**
 * Helper function that retrieves an activible unique key.
 */
function activableKey(decoratedActivable: any): string {
  return decoratedActivable.kindId + '/' + decoratedActivable.activable.config.activableId
}

/**
 * Helper function that checks that the activable has a valid debug phase.
 */
function hasValidSelectedDebugPhase(decoratedActivable: any): boolean {
  const selected = selectedDebugPhase.value[activableKey(decoratedActivable)]
  return selected && selected !== 'undefined' // \todo Funky 'undefined'. See debugPhaseOptions.
}

const { showSuccessNotification } = useNotifications()

/**
 * Async function that forcibly activates an activable phase and ignores the configured schedules and/or other conditions.
 */
async function forceSelectedPhase(decoratedActivable: any): Promise<void> {
  const phase = selectedDebugPhase.value[activableKey(decoratedActivable)]
  if (!phase || phase === 'undefined') {
    // \todo Funky 'undefined'. See debugPhaseOptions.
    return
  }
  const kindId = decoratedActivable.kindId
  const activableId = decoratedActivable.activable.config.activableId
  const displayName = decoratedActivable.activable.config.displayName

  const phaseStr = phase === null ? 'none' : phase
  await gameServerApi.post(`/activables/${props.playerId}/forcePhase/${kindId}/${activableId}/${phaseStr}`)

  refreshActivablesForcePhaseEnabled()
  showSuccessNotification(`'${displayName}' debug-forced to phase '${phase}'.`)
}

/**
 * Async function that removes a forced phase to return the Activable to its normal configured behavior.
 */
async function clearDebugPhase(decoratedActivable: any): Promise<void> {
  const kindId = decoratedActivable.kindId
  const activableId = decoratedActivable.activable.config.activableId
  const displayName = decoratedActivable.activable.config.displayName

  await gameServerApi.post(`/activables/${props.playerId}/forcePhase/${kindId}/${activableId}/none`)

  refreshActivablesForcePhaseEnabled()
  showSuccessNotification(`'${displayName}' reset to its original phase.`)
}

// Filtering and Sorting ----------------------------------------------------------------------------------------------

/**
 * Filtering options passed to the MetaListCard component.
 */
const defaultFilterSets = computed(() => {
  let allPhaseDisplayStrings = Activables.allPhaseDisplayStrings(playerData.value !== null)
  if (props.hideDisabled) {
    allPhaseDisplayStrings = allPhaseDisplayStrings.filter((x) => x !== 'Disabled')
  }
  return [
    new MetaListFilterSet(
      'status',
      allPhaseDisplayStrings.map((phaseDisplayString) => {
        return new MetaListFilterOption(phaseDisplayString, (x: any) => {
          return x.phaseDisplayString === phaseDisplayString
        })
      })
    ),
  ]
})

// Misc ui ---------------------------------------------------------------------------------------------------------------

/**
 * The activable category icon to be displayed.
 */
const categoryIcon = computed(() => {
  return coreStore.gameSpecific.activableCustomization[props.category]?.icon || 'calendar-alt'
})

/**
 * The activable category name to be displayed.
 */
const categoryDisplayName = computed(() => {
  return activablesMetadata.value?.categories[props.category].shortSingularDisplayName
})

/**
 * Function that returns the conversion rate of the displayed activable.
 */
function conversion(consumed: any, activated: any): number {
  if (activated === 0) {
    return 0
  } else {
    return (consumed / activated) * 100
  }
}

/**
 * Returns a tooltip for the conversion rate.
 */
function conversionTooltip(consumed: any, activated: any): string {
  if (activated === 0) {
    return 'Not activated by any players yet.'
  } else {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return `Activated by ${abbreviateNumber(activated)} players and consumed by ${abbreviateNumber(consumed)}.`
  }
}

/**
 * Retrieves the activable Id.
 */
function getItemKey(item: any): string {
  return item.activable.config.activableId
}
</script>
