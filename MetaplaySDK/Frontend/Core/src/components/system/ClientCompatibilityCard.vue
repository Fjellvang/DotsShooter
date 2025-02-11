<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  :isLoading="!backendStatusData || !staticConfig"
  title="Client Compatibility Settings"
  :error="backendStatusError"
  no-body-padding
  data-testid="system-client-compatibility-card"
  )
  template(#subtitle)
    p Prevent incompatible clients from connecting based on their logic version. For more details, see the #[MTextButton(to="https://docs.metaplay.io/game-logic/utilities/logic-versions.html") logic versioning documentation].

  //- Visualization of the client compatibility settings.
  div(class="tw-divide-y tw-divide-neutral-300")
    //- Incoming connection.
    div(class="tw-flex tw-px-4 tw-py-2")
      div(class="tw-w-12")
        FontAwesomeIcon(
          icon="arrow-down-long"
          size="xl"
          transform="down-4"
          )
      div
        p(class="tw-font-semibold") Incoming connection
        p(class="tw-text-sm tw-text-neutral-400") Known logic versions: {{ staticConfig?.supportedLogicVersionOptions.join(', ') }}

    //- Old versions that are refused.
    div(class="tw-flex tw-px-4 tw-py-2")
      div(class="tw-w-12")
        FontAwesomeIcon(
          icon="arrow-turn-up"
          class="tw-pt-2"
          size="xl"
          transform="right-7 rotate-90"
          )
      div(class="tw-flex tw-grow tw-items-center tw-gap-2 tw-py-2")
        p(class="tw-font-semibold") Logic versions older than {{ backendStatusData?.clientCompatibilitySettings.activeLogicVersionRange.minVersion }}
        div(class="tw-text-right")
          FontAwesomeIcon(icon="arrow-right-long")
        p(class="tw-font-semibold tw-text-red-500") Refused

    //- Dynamic list of active logic versions.
    div(
      v-for="version in activeLogicVersions"
      class="tw-flex tw-px-4 tw-py-2"
      )
      div(class="tw-w-12")
        FontAwesomeIcon(
          icon="arrow-turn-up"
          class="tw-pt-2"
          size="xl"
          transform="right-7 rotate-90"
          )
      div(class="tw-grow tw-py-2")
        p(class="tw-font-semibold") Logic version {{ version }}
        //- List of patch versions and their status.
        ul(class="tw-pl-2")
          template(v-if="getLogicVersionPatchesForVersion(version).length === 0")
            li(class="tw-flex tw-items-center tw-gap-2")
              FontAwesomeIcon(
                icon="arrow-turn-up"
                transform="rotate-90"
                )
              p(class="tw-text-sm") All patch versions
              div(class="tw-text-right")
                FontAwesomeIcon(icon="arrow-right-long")
              p(class="tw-font-semibold tw-text-green-500") Allowed

          template(v-else)
            li(
              v-for="patch in getLogicVersionPatchesForVersion(version)"
              class="tw-flex tw-items-center tw-gap-2"
              )
              FontAwesomeIcon(
                icon="arrow-turn-up"
                transform="rotate-90"
                )
              p(class="tw-text-sm") Patch {{ maybePluralString(patch.minPatchVersion, 'version', false) }} {{ patch.minPatchVersion - 1 <= 0 ? '0' : `0-${patch.minPatchVersion - 1}` }} on {{ patch.forPlatform === 'Unknown' ? 'unknown' : (patch.forPlatform ?? 'all platforms') }}
              div(class="tw-text-right")
                FontAwesomeIcon(icon="arrow-right-long")
              p(class="tw-font-semibold tw-text-red-500") Refused

            li(class="tw-flex tw-items-center tw-gap-2")
              FontAwesomeIcon(
                icon="arrow-turn-up"
                transform="rotate-90"
                )
              p(class="tw-text-sm") All other patch versions
              div(class="tw-text-right")
                FontAwesomeIcon(icon="arrow-right-long")
              p(class="tw-font-semibold tw-text-green-500") Allowed

    //- New versions that are redirected or refused.
    div(class="tw-flex tw-px-4 tw-py-2")
      div(class="tw-w-12")
        FontAwesomeIcon(
          icon="arrow-turn-up"
          class="tw-pt-2"
          size="xl"
          transform="right-7 rotate-90"
          )
      div
        div(class="tw-flex tw-grow tw-items-center tw-gap-2 tw-pb-0.5 tw-pt-2")
          p(class="tw-font-semibold") Logic versions newer than {{ backendStatusData?.clientCompatibilitySettings.activeLogicVersionRange.maxVersion }}
          div(class="tw-text-right")
            FontAwesomeIcon(icon="arrow-right-long")
          p(
            class="tw-font-semibold"
            :class="{ 'tw-text-red-500': !backendStatusData?.clientCompatibilitySettings.redirectEnabled, 'tw-text-blue-500': backendStatusData?.clientCompatibilitySettings.redirectEnabled }"
            data-testid="client-redirect-status"
            ) {{ backendStatusData?.clientCompatibilitySettings.redirectEnabled ? 'Redirected' : 'Refused' }}
        p(
          v-if="backendStatusData?.clientCompatibilitySettings.redirectEnabled"
          class="tw-text-sm tw-text-neutral-400"
          ) Redirecting to {{ backendStatusData?.clientCompatibilitySettings.redirectServerEndpoint?.serverHost }}:{{ backendStatusData?.clientCompatibilitySettings.redirectServerEndpoint?.serverPort }}

  //- Modal for editing the client compatibility settings.
  template(#buttons)
    MActionModalButton(
      modal-title="Update Client Compatibility Settings"
      :action="setClientCompatibilitySettings"
      trigger-button-label="Edit Settings"
      :trigger-button-disabled-tooltip="!staticConfig?.supportedLogicVersions ? 'Logic versions are not yet loaded.' : undefined"
      ok-button-label="Save Settings"
      :ok-button-disabled-tooltip="!isLogicVersionFormValid ? 'Invalid settings. Please fix the errors.' : undefined"
      permission="api.system.edit_logicversioning"
      @show="resetModal"
      data-testid="client-redirect-settings"
      )
      p(class="tw-mb-4") Changing the active logic version range will not kick connected clients as only new connections will be refused. Maintenance mode can be used to disconnect clients.

      div(class="tw-divide-y tw-divide-neutral-200 tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-50")
        //- Incoming connection.
        div(class="tw-flex tw-px-4 tw-py-3")
          div(class="tw-w-12")
            FontAwesomeIcon(
              icon="arrow-down-long"
              size="xl"
              transform="down-4"
              )
          div
            p(class="tw-font-semibold") Incoming connection
            p(class="tw-text-sm tw-text-neutral-400") Known logic versions: {{ staticConfig?.supportedLogicVersionOptions.join(', ') }}

        //- Min version.
        div(class="tw-flex tw-px-4 tw-py-2")
          div(class="tw-w-12")
            FontAwesomeIcon(
              icon="arrow-turn-up"
              class="tw-pt-2"
              size="xl"
              transform="right-7 rotate-90"
              )
          div(class="tw-grow tw-py-2")
            MInputSingleSelectRadio(
              label="Refuse logic versions older than..."
              :model-value="selectedActiveLogicVersionMin"
              :options="activeLogicVersionMinOptions"
              size="small"
              :variant="minVersionSelectVariant"
              :hintMessage="minVersionSelectHintMessage"
              @update:model-value="(event) => (selectedActiveLogicVersionMin = event)"
              )

        //- Dynamic list of active logic versions.
        div(
          v-for="version in selectedActiveLogicVersions"
          class="tw-flex tw-px-4 tw-py-2"
          )
          div(class="tw-w-12")
            FontAwesomeIcon(
              icon="arrow-turn-up"
              class="tw-pt-2"
              size="xl"
              transform="right-7 rotate-90"
              )
          div(class="tw-grow tw-space-y-2 tw-py-2")
            p(class="tw-font-semibold") Logic version {{ version }}
            //- List of patch versions and button for new ones.
            div(v-for="patch in getLogicVersionPatchesForSelectedVersion(version)")
              div(class="tw-flex tw-gap-2 tw-text-sm")
                MInputNumber(
                  label="Refuse patches under..."
                  :min="1"
                  :model-value="patch.minPatchVersion"
                  :variant="evaluateRule(patch).variant"
                  class="tw-basis-44"
                  @update:model-value="(event) => replacePatchVersionRequirement(version, patch, { ...patch, minPatchVersion: event ?? 1 })"
                  )
                MInputSingleSelectDropdown(
                  label="...on this platform"
                  :options="platformOptions"
                  :model-value="patch.forPlatform"
                  class="tw-grow"
                  @update:model-value="(event) => replacePatchVersionRequirement(version, patch, { ...patch, forPlatform: event })"
                  )
                MIconButton(
                  variant="danger"
                  @click="selectedClientPatchRequirements = selectedClientPatchRequirements.filter((r) => r !== patch)"
                  )
                  FontAwesomeIcon(
                    icon="trash-alt"
                    size="sm"
                    )
              MInputHintMessage(:variant="evaluateRule(patch).variant") {{ evaluateRule(patch).hintMessage }}

            div(v-if="getLogicVersionPatchesForSelectedVersion(version).length === 0")
              p(class="tw-text-sm") Allow all patch versions.
            div(v-else)
              p(class="tw-text-sm") ...and allow all other patch versions.

            //- Add new patch version.
            MButtonGroupLayout
              MButton(
                size="small"
                @click="addNewSelectedClientPatch(version)"
                ) Add New Rule

        //- Max version.
        div(class="tw-flex tw-px-4 tw-py-2")
          div(class="tw-w-12")
            FontAwesomeIcon(
              icon="arrow-turn-up"
              class="tw-pt-2"
              size="xl"
              transform="right-7 rotate-90"
              )
          div(class="tw-grow tw-py-2")
            MInputSingleSelectRadio(
              :label="`${selectedRedirectEnabled ? 'Redirect' : 'Refuse'} logic versions newer than...`"
              :model-value="selectedActiveLogicVersionMax"
              :options="activeLogicVersionMaxOptions"
              size="small"
              :variant="maxVersionSelectVariant"
              :hintMessage="maxVersionSelectHintMessage"
              @update:model-value="(event) => (selectedActiveLogicVersionMax = event)"
              )

      template(#bottom-panel)
        MCallout(
          v-if="isRollingBackLogicVersion"
          title="Tread Carefully, Brave Knight"
          ) Rolling back the active #[MBadge LogicVersion] can have very bad unintended consequences and should ideally never be done. Please make sure you know what you are doing before saving this action!

      template(#right-panel)
        div(class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-50 tw-p-3")
          div(class="tw-flex tw-justify-between")
            span(class="tw-text-sm tw-font-bold tw-leading-6") Redirect New Clients
            MInputSwitch(
              :model-value="selectedRedirectEnabled"
              size="small"
              class="tw-relative tw-top-0.5"
              @update:model-value="(event) => (selectedRedirectEnabled = event)"
              data-testid="client-redirect-enabled"
              )
          //- TODO: Support hintMessage on MInputSwitch, and use that instead of this ad hoc.
          MInputHintMessage(v-if="!selectedRedirectEnabled") Optionally redirect clients with logic versions newer than {{ selectedActiveLogicVersionMax }} to connect to a different game server.

          div(
            v-if="selectedRedirectEnabled"
            class="tw-mt-2 tw-space-y-4 tw-border-t tw-border-neutral-300 tw-pt-1"
            )
            MInputText(
              label="Host"
              :model-value="selectedRedirectHost"
              :disabled="!selectedRedirectEnabled"
              :variant="getFormVariant(selectedRedirectHost)"
              :hintMessage="!selectedRedirectHost ? 'This field is required.' : undefined"
              @update:model-value="(event) => (selectedRedirectHost = event)"
              data-testid="input-text-host"
              )

            MInputNumber(
              label="Port"
              :model-value="selectedRedirectPort"
              :disabled="!selectedRedirectEnabled"
              :variant="getFormVariant(selectedRedirectPort)"
              :hintMessage="!selectedRedirectPort ? 'This field is required.' : undefined"
              @update:model-value="(event) => (selectedRedirectPort = event ?? 9339)"
              data-testid="input-text-port"
              )

            MInputText(
              label="CDN URL"
              :model-value="selectedRedirectCdnUrl"
              :disabled="!selectedRedirectEnabled"
              :variant="getFormVariant(selectedRedirectCdnUrl)"
              :hintMessage="!selectedRedirectCdnUrl ? 'This field is required.' : undefined"
              @update:model-value="(event) => (selectedRedirectCdnUrl = event)"
              data-testid="input-text-cdn-url"
              )

            div(class="tw-flex tw-justify-between")
              span(class="tw-font-semibold") TLS Enabled
              MInputSwitch(
                :model-value="selectedRedirectTls"
                :disabled="!selectedRedirectEnabled"
                class="tw-relative tw-top-1 tw-mr-1"
                name="redirectTls"
                size="small"
                @update:model-value="(event) => (selectedRedirectTls = event)"
                )
</template>

<script lang="ts" setup>
import { replace } from 'lodash-es'
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MBadge,
  MCallout,
  MCard,
  MInputSingleSelectRadio,
  MInputSwitch,
  MInputText,
  MTextButton,
  MInputHintMessage,
  useNotifications,
  MInputNumber,
  MButton,
  MButtonGroupLayout,
  MInputSingleSelectDropdown,
  type MInputSingleSelectDropdownOption,
  MIconButton,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

import {
  getBackendStatusSubscriptionOptions,
  getStaticConfigSubscriptionOptions,
} from '../../subscription_options/general'
import type {
  ClientCompatibilitySettings,
  ClientPatchVersionRequirement,
  StatusResponse,
} from '../../subscription_options/generalTypes'

const gameServerApi = useGameServerApi()

const {
  data: backendStatusData,
  refresh: backendStatusTriggerRefresh,
  error: backendStatusError,
} = useSubscription<StatusResponse>(getBackendStatusSubscriptionOptions())
const { data: staticConfig } = useSubscription(getStaticConfigSubscriptionOptions())

/**
 * List of active logic versions that are within the supported range.
 */
const activeLogicVersions = computed(() => {
  if (!backendStatusData.value) return []
  const minVersion = backendStatusData.value.clientCompatibilitySettings.activeLogicVersionRange.minVersion
  const maxVersion = backendStatusData.value.clientCompatibilitySettings.activeLogicVersionRange.maxVersion
  const versions = staticConfig.value?.supportedLogicVersionOptions ?? []
  const activeVersions = versions.filter((version) => version >= minVersion && version <= maxVersion)
  return activeVersions
})

function getLogicVersionPatchesForVersion(version: number): ClientPatchVersionRequirement[] {
  if (!backendStatusData.value) return []
  return backendStatusData.value.clientCompatibilitySettings.clientPatchVersionRequirements.filter(
    (req) => req.forLogicVersion === version
  )
}

// Logic versions -----------------------------------------------------------------------------------------------------

const selectedActiveLogicVersionMin = ref<number>(0)
const selectedActiveLogicVersionMax = ref<number>(0)

const activeLogicVersionMinOptions = computed((): Array<MInputSingleSelectDropdownOption<number>> => {
  if (!staticConfig.value?.supportedLogicVersionOptions) return []

  return staticConfig.value.supportedLogicVersionOptions.map((version) => ({
    label:
      version === backendStatusData.value?.clientCompatibilitySettings.activeLogicVersionRange.minVersion
        ? `${version} (current)`
        : String(version),
    value: version,
  }))
})

const minVersionSelectVariant = computed((): 'primary' | 'warning' | 'danger' => {
  if (isRollingBackMinLogicVersion.value) return 'warning'
  else return 'primary'
})

const minVersionSelectHintMessage = computed((): string | undefined => {
  if (isRollingBackMinLogicVersion.value) return 'Rolling back the current selection! Are you sure?'
  else return undefined
})

const activeLogicVersionMaxOptions = computed((): Array<MInputSingleSelectDropdownOption<number>> => {
  if (!staticConfig.value?.supportedLogicVersionOptions) return []

  return staticConfig.value.supportedLogicVersionOptions.map((version) => ({
    label:
      version === backendStatusData.value?.clientCompatibilitySettings.activeLogicVersionRange.maxVersion
        ? `${version} (current)`
        : String(version),
    value: version,
    disabled: version < selectedActiveLogicVersionMin.value,
  }))
})

const maxVersionSelectVariant = computed((): 'primary' | 'warning' | 'danger' => {
  if (isInvalidLogicVersionRange.value) return 'danger'
  else if (isRollingBackMaxLogicVersion.value) return 'warning'
  else return 'primary'
})

const maxVersionSelectHintMessage = computed((): string | undefined => {
  if (isInvalidLogicVersionRange.value) return 'This must not be smaller than the min version.'
  else if (isRollingBackMaxLogicVersion.value) return 'Rolling back the current selection! Are you sure?'
  else return undefined
})

/**
 * List of logic versions that match the selected active logic version min and max.
 */
const selectedActiveLogicVersions = computed((): number[] => {
  if (!staticConfig.value?.supportedLogicVersionOptions) return []

  const selectedVersions = staticConfig.value.supportedLogicVersionOptions.filter(
    (version) => version >= selectedActiveLogicVersionMin.value && version <= selectedActiveLogicVersionMax.value
  )
  return selectedVersions
})

const isInvalidLogicVersionRange = computed((): boolean => {
  return selectedActiveLogicVersionMin.value > selectedActiveLogicVersionMax.value
})

const isRollingBackMinLogicVersion = computed((): boolean => {
  if (isInvalidLogicVersionRange.value || !backendStatusData.value) {
    return false
  } else {
    return (
      selectedActiveLogicVersionMin.value !== undefined &&
      selectedActiveLogicVersionMin.value <
        backendStatusData.value.clientCompatibilitySettings.activeLogicVersionRange.minVersion
    )
  }
})

const isRollingBackMaxLogicVersion = computed((): boolean => {
  if (isInvalidLogicVersionRange.value || !backendStatusData.value) {
    return false
  } else {
    return (
      selectedActiveLogicVersionMax.value !== undefined &&
      selectedActiveLogicVersionMax.value <
        backendStatusData.value.clientCompatibilitySettings.activeLogicVersionRange.maxVersion
    )
  }
})

const isRollingBackLogicVersion = computed((): boolean => {
  if (isInvalidLogicVersionRange.value || !backendStatusData.value) {
    return false
  } else {
    return isRollingBackMinLogicVersion.value || isRollingBackMaxLogicVersion.value
  }
})

// Client patches -----------------------------------------------------------------------------------------------------

const selectedClientPatchRequirements = ref<ClientPatchVersionRequirement[]>([])

function getLogicVersionPatchesForSelectedVersion(version: number): ClientPatchVersionRequirement[] {
  return selectedClientPatchRequirements.value.filter((revision) => revision.forLogicVersion === version)
}

function replacePatchVersionRequirement(
  version: number,
  patch: ClientPatchVersionRequirement,
  newPatch: ClientPatchVersionRequirement
): void {
  selectedClientPatchRequirements.value = selectedClientPatchRequirements.value.map((r) => (r === patch ? newPatch : r))
}

function addNewSelectedClientPatch(version: number): void {
  const newRevision: ClientPatchVersionRequirement = {
    forLogicVersion: version,
    forPlatform: null,
    minPatchVersion: 1,
  }
  selectedClientPatchRequirements.value = [...selectedClientPatchRequirements.value, newRevision]
}

// TODO: These options should come in from the backend as they can only be known during runtime.
const platformOptions: Array<MInputSingleSelectDropdownOption<string | null>> = [
  { label: 'all platforms', value: null },
  { label: 'iOS', value: 'iOS' },
  { label: 'Android', value: 'Android' },
  { label: 'WebGL', value: 'WebGL' },
  { label: 'unknown', value: 'Unknown' },
]

function evaluateRule(patchRequirement: ClientPatchVersionRequirement): {
  variant: 'default' | 'warning'
  hintMessage: string
} {
  // If another rule with this same platform OR 'all platforms' exists AND that revision has the same or larger patch version, then this rule is unnecessary.
  const isUnnecessary = selectedClientPatchRequirements.value.some(
    (r) =>
      r !== patchRequirement &&
      r.forLogicVersion === patchRequirement.forLogicVersion &&
      (r.forPlatform === patchRequirement.forPlatform || r.forPlatform === null) &&
      r.minPatchVersion >= patchRequirement.minPatchVersion
  )

  return {
    variant: isUnnecessary ? 'warning' : 'default',
    hintMessage: isUnnecessary
      ? 'This rule is unnecessary as it is covered by another rule.'
      : `Clients with patch ${maybePluralString(patchRequirement.minPatchVersion, 'version', false)} ${patchRequirement.minPatchVersion - 1 <= 0 ? '0' : `0-${patchRequirement.minPatchVersion - 1}`} on ${patchRequirement.forPlatform === 'Unknown' ? 'unknown' : patchRequirement.forPlatform} will be refused.`,
  }
}

// Redirect -----------------------------------------------------------------------------------------------------------

const selectedRedirectHost = ref('')
const selectedRedirectPort = ref(9339)
const selectedRedirectTls = ref(true)
const selectedRedirectCdnUrl = ref('')
const selectedRedirectEnabled = ref(false)

function getFormVariant(value: string | number): 'default' | 'success' | 'danger' {
  if (!selectedRedirectEnabled.value) return 'default'
  return value ? 'success' : 'danger'
}

const isLogicVersionFormValid = computed((): boolean => {
  return (
    (!selectedRedirectEnabled.value ||
      (!!selectedRedirectHost.value && !!selectedRedirectPort.value && !!selectedRedirectCdnUrl.value)) &&
    !!selectedActiveLogicVersionMin.value &&
    !!selectedActiveLogicVersionMax.value &&
    !isInvalidLogicVersionRange.value
  )
})

const { showSuccessNotification } = useNotifications()

async function setClientCompatibilitySettings(): Promise<void> {
  if (!selectedActiveLogicVersionMin.value || !selectedActiveLogicVersionMax.value) return

  const payload: ClientCompatibilitySettings = {
    activeLogicVersionRange: {
      minVersion: selectedActiveLogicVersionMin.value,
      maxVersion: selectedActiveLogicVersionMax.value,
    },
    redirectEnabled: selectedRedirectEnabled.value,
    redirectServerEndpoint: {
      serverHost: selectedRedirectHost.value,
      serverPort: selectedRedirectPort.value,
      enableTls: selectedRedirectTls.value,
      cdnBaseUrl: selectedRedirectCdnUrl.value,
    },
    clientPatchVersionRequirements: selectedClientPatchRequirements.value,
  }
  await gameServerApi.post('/clientCompatibilitySettings', payload)

  showSuccessNotification('Client compatibility settings updated.')

  backendStatusTriggerRefresh()
}

function resetModal(): void {
  if (!backendStatusData.value) return
  const settings = backendStatusData.value.clientCompatibilitySettings
  const redirectEndpoint = settings.redirectServerEndpoint
  selectedActiveLogicVersionMin.value = settings.activeLogicVersionRange.minVersion
  selectedActiveLogicVersionMax.value = settings.activeLogicVersionRange.maxVersion
  selectedClientPatchRequirements.value = settings.clientPatchVersionRequirements

  selectedRedirectEnabled.value = settings.redirectEnabled
  selectedRedirectHost.value = redirectEndpoint ? redirectEndpoint.serverHost : ''
  selectedRedirectPort.value = redirectEndpoint ? redirectEndpoint.serverPort : 9339
  selectedRedirectTls.value = redirectEndpoint ? redirectEndpoint.enableTls : true
  selectedRedirectCdnUrl.value = redirectEndpoint ? redirectEndpoint.cdnBaseUrl : ''
}
</script>
