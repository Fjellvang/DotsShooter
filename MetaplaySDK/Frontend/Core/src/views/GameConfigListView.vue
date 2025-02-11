<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!allGameConfigsData"
  :error="allGameConfigsError"
  :variant="hasTooManyUnpublishedGameConfigs || hasTooManyArchivedGameConfigs ? 'warning' : undefined"
  permission="api.game_config.view"
  )
  template(#alerts)
    //- Alert for archiving older game configs.
    MCallout(
      v-if="hasTooManyUnpublishedGameConfigs"
      title="Many Unpublished Game Configs"
      variant="warning"
      class="tw-mb-3"
      )
      div You have {{ maybePluralString(archivableGameConfigIds.length, 'unpublished game config') }}. Consider archiving or publishing them to reduce clutter.
      div(class="tw-mt-4 tw-flex tw-justify-end")
        MActionModalButton(
          modal-title="Archive All Unpublished Game Configs"
          :action="archiveAllUnpublished"
          trigger-button-label="Archive All Unpublished"
          trigger-button-size="small"
          )
          template(#default)
            div You are about to archive {{ archivableGameConfigIds.length }} unpublished game configs.
              |
              | Archiving a game config will hide it from the list of available game configs. An archived game config can be unarchived at any time.
    //- Alert for pruning archived game configs.
    MCallout(
      v-if="hasTooManyArchivedGameConfigs"
      title="Many Archived Game Configs"
      variant="warning"
      )
      div(class="tw-mb-3") You have {{ archivedGameConfigCount }} archived game configs. These configs are not being used and can be safely removed. Removing these configs will improve your server performance.
      div(v-if="hasPermissionToDelete") Archived game configs can be removed from the #[MTextButton(to="/system") settings] page.

  template(#overview)
    MPageOverviewCard(
      title="View Game Configs"
      data-testid="game-config-list-overview-card"
      )
      p(class="tw-mb-1") Game configs contain all your game data, such as the economy balancing.
      div(class="tw-text-sm tw-text-neutral-500") You can make new game configs builds and review them before publishing. Published game configs will be delivered over-the-air and do not require players to update their clients.

      template(#buttons)
        MActionModalButton(
          modal-title="Build New Game Config"
          :action="buildGameConfig"
          trigger-button-label="New Build"
          :trigger-button-disabled-tooltip="!staticConfigData?.gameConfigBuildInfo.buildSupported ? 'Game config builds have not been enabled for this environment.' : undefined"
          ok-button-label="Build Config"
          :ok-button-disabled-tooltip="!formValidationState ? 'Fill out all required fields to proceed.' : undefined"
          permission="api.game_config.edit"
          @show="resetBuildNewConfigModal"
          data-testid="build-game-config-form"
          )
          template(#default)
            p You can configure and trigger a new game configs build to happen directly on the game server. It may take a few minutes for large projects.
            MInputText(
              label="Game Config Name"
              :model-value="gameConfigName"
              :variant="nameValidationState !== null ? (nameValidationState ? 'success' : 'danger') : 'default'"
              placeholder="For example: 1.3.2"
              class="tw-mb-2"
              @update:model-value="gameConfigName = $event"
              )

            MInputTextArea(
              label="Game Config Description"
              :model-value="gameConfigDescription"
              :variant="descriptionValidationState !== null ? (descriptionValidationState ? 'success' : 'danger') : 'default'"
              placeholder="For example: Reduced the difficulty of levels between 5 and 10."
              :rows="3"
              class="tw-mb-1"
              @update:model-value="gameConfigDescription = $event"
              )

          template(#right-panel)
            //- Use a generated form for the rest of the build params.
            div(class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-p-4")
              div(class="tw-mb-2 tw-font-semibold") Build Parameters
              div(class="tw-text-sm") Optional configuration for how the game config should be built. You can, for example, pull data from a different source or only build a subset of the configs.
              //- NOTE: This is a hack as the form has some invisible margin that would otherwise create a horizontal scrollbar. Investigate later.
              div
                meta-generated-form(
                  typeName="Metaplay.Core.Config.GameConfigBuildParameters"
                  :abstractTypeFilter="buildParamsTypeFilter"
                  :value="buildParams"
                  :page="'GameConfigBuildCard'"
                  class="tw-mt-2"
                  @input="buildParams = $event"
                  @status="buildParamsValidationState = $event"
                  )

  template(#default)
    core-ui-placement(placementId="GameConfigs/List")

    MetaRawData(
      :kv-pair="allGameConfigsData"
      name="allGameConfigsData"
      )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaRawData } from '@metaplay/meta-ui'
import {
  MActionModalButton,
  MCallout,
  MInputText,
  MInputTextArea,
  MPageOverviewCard,
  MTextButton,
  MViewContainer,
  useNotifications,
  usePermissions,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import MetaGeneratedForm from '../components/generatedui/components/MetaGeneratedForm.vue'
import type { IGeneratedUiFieldSchemaDerivedTypeInfo } from '../components/generatedui/generatedUiTypes'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import type { MinimalGameConfigInfo } from '../gameConfigServerTypes'
import { getAllGameConfigsSubscriptionOptions } from '../subscription_options/gameConfigs'
import { getStaticConfigSubscriptionOptions } from '../subscription_options/general'

const gameServerApi = useGameServerApi()
const permissions = usePermissions()

// Subscribe to the data that we need ---------------------------------------------------------------------------------

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

const {
  data: allGameConfigsData,
  error: allGameConfigsError,
  refresh: allGameConfigsTriggerRefresh,
} = useSubscription(getAllGameConfigsSubscriptionOptions())

// Form data ----------------------------------------------------------------------------------------------------------

/**
 * Optional name for the new game config.
 */
const gameConfigName = ref<string>()

/**
 * Optional description for the new game config.
 */
const gameConfigDescription = ref<string>()

const buildParams = ref()

/**
 * Reset state of the new build modal.
 */
function resetBuildNewConfigModal(): void {
  buildParams.value = null
  gameConfigName.value = ''
  gameConfigDescription.value = ''
}

function buildParamsTypeFilter(abstractType: string) {
  if (abstractType === 'Metaplay.Core.Config.GameConfigBuildParameters') {
    const hasCustomBuildParams =
      staticConfigData.value != null &&
      staticConfigData.value.gameConfigBuildInfo.buildParametersNamespaceQualifiedName !==
        'Metaplay.Core.Config.DefaultGameConfigBuildParameters'
    if (hasCustomBuildParams) {
      return (concreteType: IGeneratedUiFieldSchemaDerivedTypeInfo): boolean =>
        concreteType.typeName !== 'Metaplay.Core.Config.DefaultGameConfigBuildParameters'
    }
  }
  return (): boolean => true
}

// Form validation ----------------------------------------------------------------------------------------------------

/**
 *  Validation check for the name input field.
 */
const nameValidationState = computed((): true | false | null => {
  if (gameConfigName.value && gameConfigName.value.length > 0) {
    return true
  }
  // Optional validation here (eg: check for length, invalid chars, etc.) could return false if invalid.
  return false
})

/**
 *  Validation check for the description input field.
 */
const descriptionValidationState = computed((): true | false | null => {
  if (gameConfigDescription.value && gameConfigDescription.value.length > 0) {
    return true
  }
  // Optional validation here (eg: check for length, invalid chars, etc.) could return false if invalid.
  return null
})

/**
 * Validation state of the generated form.
 */
const buildParamsValidationState = ref<boolean>()

/**
 * Overall validation state of the entire modal.
 */
const formValidationState = computed(() => {
  return (
    nameValidationState.value === true && descriptionValidationState.value !== false && buildParamsValidationState.value
  )
})

// Sending build command to the game server ---------------------------------------------------------------------------

const { showSuccessNotification } = useNotifications()

/**
 * Build game config from source data.
 */
async function buildGameConfig(): Promise<void> {
  const params = {
    SetAsActive: false,
    Properties: {
      Name: gameConfigName.value,
      Description: gameConfigDescription.value,
    },
    BuildParams: buildParams.value,
  }

  await gameServerApi.post('/gameConfig/build', params)

  showSuccessNotification('Game config build started.')
  allGameConfigsTriggerRefresh()
}

// Archive older configs alert and modal ------------------------------------------------------------------------------

/**
 * Get a list of "archivable" game config IDs. These are game configs that are older than have not been published.
 */
const archivableGameConfigIds = computed((): string[] => {
  return (
    (allGameConfigsData.value ?? ([] as MinimalGameConfigInfo[]))
      // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
      // needed here for now. (18/12/23)
      .filter(
        (x: MinimalGameConfigInfo) =>
          !x.isArchived && !(x.publishedAt !== null || x.unpublishedAt !== null || x.isActive)
      )
      .map((x: MinimalGameConfigInfo) => x.id)
  )
})

/**
 * True if there are too many unpublished game configs.
 * Note: "too many" is defined as "more than 100" and this is currently hard-coded here.
 */
const hasTooManyUnpublishedGameConfigs = computed(() => {
  return archivableGameConfigIds.value.length > 100
})

/**
 * Archive all unpublished game configs.
 */
async function archiveAllUnpublished(): Promise<void> {
  await gameServerApi.post('/gameConfig/archive', archivableGameConfigIds.value)
  showSuccessNotification(`Archived ${archivableGameConfigIds.value.length} unpublished game configs.`)
}

// Delete archived configs alert --------------------------------------------------------------------------------------

/**
 * Get a count of archived game configs.
 */
const archivedGameConfigCount = computed((): number | undefined => {
  if (allGameConfigsData.value) {
    return allGameConfigsData.value.reduce((accumulator: number, config) => {
      if (config.isArchived) return accumulator + 1
      else return accumulator
    }, 0)
  } else {
    return undefined
  }
})

/**
 * True if there are too many archived game configs.
 * Note: "too many" is defined as "more than 200" and this is currently hard-coded here.
 */
const hasTooManyArchivedGameConfigs = computed((): boolean => {
  return (archivedGameConfigCount.value ?? 0) > 200
})

/**
 * True if the user has permission to delete archived game configs.
 */
const hasPermissionToDelete = computed(() => {
  return permissions.doesHavePermission('api.game_config.delete')
})
</script>
