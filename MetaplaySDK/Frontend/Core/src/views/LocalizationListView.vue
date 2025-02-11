<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!allLocalizationData"
  :error="allLocalizationError"
  :variant="hasTooManyUnpublishedLocalizations || hasTooManyArchivedLocalizations ? 'warning' : undefined"
  permission="api.localization.view"
  )
  template(#alerts)
    //- Alert for archiving older localizations.
    MCallout(
      v-if="hasTooManyUnpublishedLocalizations"
      title="Many Unpublished Localizations"
      variant="warning"
      class="tw-mb-3"
      )
      div You have {{ maybePluralString(archivableLocaliationIds.length, 'unpublished localization') }}. Consider archiving or publishing them to reduce clutter.
      div(class="tw-mt-4 tw-flex tw-justify-end")
        MActionModalButton(
          modal-title="Archive All Unpublished localizations"
          :action="archiveAllUnpublished"
          trigger-button-label="Archive All Unpublished"
          trigger-button-size="small"
          )
          template(#default)
            div You are about to archive {{ archivableLocaliationIds.length }} unpublished localizations.
              |
              | Archiving a localization will hide it from the list of available localizations. An archived localization can be unarchived at any time.
    //- Alert for pruning archived localizations.
    MCallout(
      v-if="hasTooManyArchivedLocalizations"
      title="Many Archived Localizations"
      variant="warning"
      )
      div(class="tw-mb-3") You have {{ archivedLocalizationCount }} archived localizations. These localizations are not being used and can be safely removed. Removing these localizations will improve your server performance.
      div(v-if="hasPermissionToDelete") Archived localizations can be removed from the #[MTextButton(to="/system") settings] page.

  template(#overview)
    MPageOverviewCard(
      title="View Localizations"
      data-testid="localization-list-overview-card"
      )
      p(class="tw-mb-1") Localizations are a way to deliver localized text content to your players.
      div(class="tw-text-sm tw-text-neutral-500") You can upload new localization builds and review them before publishing. Published localizations will be delivered over-the-air and do not require players to update their clients.

      template(#buttons)
        MActionModalButton(
          modal-title="Build New Localizations"
          :action="buildLocalizations"
          trigger-button-label="New Build"
          :trigger-button-disabled-tooltip="!staticConfigData?.localizationsBuildInfo.buildSupported ? 'Localization builds have not been enabled for this environment.' : undefined"
          ok-button-label="Build Localizations"
          :ok-button-disabled-tooltip="!formValidationState ? 'Please fill in all required fields to proceed.' : undefined"
          permission="api.localization.edit"
          @show="resetBuildNewConfigModal"
          data-testid="build-localization-form"
          )
          template(#default)
            p You can configure and trigger a new localizations build to happen directly on the game server. It may take a few minutes for large projects.
            MInputText(
              label="Localizations Build Name"
              :model-value="localizationsName"
              :variant="nameValidationState !== null ? (nameValidationState ? 'success' : 'danger') : 'default'"
              placeholder="For example: 1.3.2"
              class="tw-mb-1"
              @update:model-value="localizationsName = $event"
              )

            MInputTextArea(
              label="Localizations Build Description"
              :model-value="localizationsDescription"
              :variant="descriptionValidationState !== null ? (descriptionValidationState ? 'success' : 'danger') : 'default'"
              placeholder="For example: Reduced the difficulty of levels between 5 and 10."
              :rows="3"
              class="tw-mb-1"
              @update:model-value="localizationsDescription = $event"
              )

          template(#right-panel)
            //- Use a generated form for the rest of the build params.
            div(class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-100 tw-p-4")
              div(class="tw-mb-2 tw-font-semibold") Build Parameters
              div(class="small") Optional configuration for how the localizations should be built. You can, for example, pull data from a different sources.
              //- NOTE: This is a hack as the form has some invisible margin that would otherwise create a horizontal scrollbar. Investigate later.
              div
                meta-generated-form(
                  typeName="Metaplay.Core.Config.LocalizationsBuildParameters"
                  :value="buildParams"
                  :page="'LocalizationsBuildCard'"
                  :abstract-type-filter="buildParamsTypeFilter"
                  class="tw-mt-2"
                  @input="buildParams = $event"
                  @status="buildParamsValidationState = $event"
                  )

  template(#default)
    core-ui-placement(placementId="Localizations/List")

    MetaRawData(
      :kv-pair="allLocalizationData"
      name="allLocalizationData"
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
  MTextButton,
  MViewContainer,
  MPageOverviewCard,
  usePermissions,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import MetaGeneratedForm from '../components/generatedui/components/MetaGeneratedForm.vue'
import type { IGeneratedUiFieldSchemaDerivedTypeInfo } from '../components/generatedui/generatedUiTypes'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import type { MinimalLocalizationInfo } from '../localizationServerTypes'
import { getStaticConfigSubscriptionOptions } from '../subscription_options/general'
import { getAllLocalizationsSubscriptionOptions } from '../subscription_options/localization'

const gameServerApi = useGameServerApi()
const permissions = usePermissions()

// Subscribe to the data that we need ---------------------------------------------------------------------------------

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

const {
  data: allLocalizationData,
  error: allLocalizationError,
  refresh: allLocalizationsTriggerRefresh,
} = useSubscription(getAllLocalizationsSubscriptionOptions())

// Form data ----------------------------------------------------------------------------------------------------------

/**
 * Optional name for the new localizations.
 */
const localizationsName = ref<string>()

/**
 * Optional description for the new localization.
 */
const localizationsDescription = ref<string>()

const buildParams = ref()

/**
 * Reset state of the new build modal.
 */
function resetBuildNewConfigModal(): void {
  buildParams.value = null
  localizationsName.value = ''
  localizationsDescription.value = ''
}

// Form validation ----------------------------------------------------------------------------------------------------

/**
 *  Validation check for the name input field.
 */
const nameValidationState = computed((): true | false | null => {
  if (localizationsName.value && localizationsName.value.length > 0) {
    return true
  }
  // Optional validation here (eg: check for length, invalid chars, etc.) could return false if invalid.
  return false
})

/**
 *  Validation check for the description input field.
 */
const descriptionValidationState = computed((): true | false | null => {
  if (localizationsDescription.value && localizationsDescription.value.length > 0) {
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

function buildParamsTypeFilter(abstractType: string) {
  if (abstractType === 'Metaplay.Core.Config.LocalizationsBuildParameters') {
    const hasCustomBuildParams =
      staticConfigData.value != null &&
      staticConfigData.value.localizationsBuildInfo.buildParametersNamespaceQualifiedName !==
        'Metaplay.Core.Config.DefaultLocalizationsBuildParameters'
    if (hasCustomBuildParams) {
      return (concreteType: IGeneratedUiFieldSchemaDerivedTypeInfo): boolean =>
        concreteType.typeName !== 'Metaplay.Core.Config.DefaultLocalizationsBuildParameters'
    }
  }
  return (): boolean => true
}

// Sending build command to the game server ---------------------------------------------------------------------------

const { showSuccessNotification } = useNotifications()

/**
 * Build localization from source data.
 */
async function buildLocalizations(): Promise<void> {
  const params = {
    Properties: {
      Name: localizationsName.value,
      Description: localizationsDescription.value,
    },
    BuildParams: buildParams.value,
  }

  await gameServerApi.post('/localization/build', params)

  showSuccessNotification('Localizations build started.')
  allLocalizationsTriggerRefresh()
}

// Archive older localizations alert and modal ------------------------------------------------------------------------

/**
 * Get a list of "archivable" localization IDs. These are localizations that are older than have not been published.
 */
const archivableLocaliationIds = computed((): string[] => {
  return (
    (allLocalizationData.value ?? ([] as MinimalLocalizationInfo[]))
      // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
      // needed here for now. (18/12/23)
      .filter(
        (x: MinimalLocalizationInfo) =>
          !x.isArchived && !(x.publishedAt !== null || x.unpublishedAt !== null || x.isActive)
      )
      .map((x: MinimalLocalizationInfo) => x.id)
  )
})

/**
 * True if there are too many unpublished localizations.
 * Note: "too many" is defined as "more than 100" and this is currently hard-coded here.
 */
const hasTooManyUnpublishedLocalizations = computed(() => {
  return archivableLocaliationIds.value.length > 100
})

/**
 * Archive all unpublished localizations.
 */
async function archiveAllUnpublished(): Promise<void> {
  await gameServerApi.post('/localization/archive', archivableLocaliationIds.value)
  showSuccessNotification(`Archived ${archivableLocaliationIds.value.length} unpublished localizations.`)
}

// Delete archived localizations alert --------------------------------------------------------------------------------

/**
 * Get a count of archived localizations.
 */
const archivedLocalizationCount = computed((): number | undefined => {
  if (allLocalizationData.value) {
    return allLocalizationData.value.reduce((accumulator: number, localization) => {
      if (localization.isArchived) return accumulator + 1
      else return accumulator
    }, 0)
  } else {
    return undefined
  }
})

/**
 * True if there are too many archived localizations.
 * Note: "too many" is defined as "more than 200" and this is currently hard-coded here.
 */
const hasTooManyArchivedLocalizations = computed((): boolean => {
  return (archivedLocalizationCount.value ?? 0) > 200
})

/**
 * True if the user has permission to delete archived localizations.
 */
const hasPermissionToDelete = computed(() => {
  return permissions.doesHavePermission('api.localization.delete')
})
</script>
