// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import App from './App.vue'
import MetaAudienceSizeEstimate from './components/MetaAudienceSizeEstimate.vue'
import MetaLanguageLabel from './components/MetaLanguageLabel.vue'
import AuditLogCard from './components/auditlogs/AuditLogCard.vue'
import MetaGeneratedCard from './components/generatedui/components/MetaGeneratedCard.vue'
import MetaGeneratedContent from './components/generatedui/components/MetaGeneratedContent.vue'
import MetaGeneratedForm from './components/generatedui/components/MetaGeneratedForm.vue'
import MetaGeneratedSection from './components/generatedui/components/MetaGeneratedSection.vue'
import PlayerListCard from './components/global/PlayerListCard.vue'
import BroadcastDeleteFormButton from './components/mails/BroadcastDeleteFormButton.vue'
import MessageAudienceForm from './components/mails/MessageAudienceForm.vue'
import TargetingCard from './components/mails/TargetingCard.vue'

export {
  App,
  AuditLogCard,
  MetaGeneratedCard,
  MetaGeneratedContent,
  MetaGeneratedForm,
  MetaGeneratedSection,
  PlayerListCard,
  BroadcastDeleteFormButton,
  MessageAudienceForm,
  TargetingCard,
  MetaAudienceSizeEstimate,
  MetaLanguageLabel,
}

export { router } from './router'

export { CorePlugin } from './corePlugin'

export {
  extractMultipleValuesFromQueryString,
  extractSingleValueFromQueryStringOrUndefined,
  extractSingleValueFromQueryStringOrDefault,
  routeParamToSingleValue,
} from './coreUtils'

export { setGameSpecificInitialization } from './integration_api/integrationApi'

export type { UiPlacement } from './integration_api/uiPlacementApis'

export { OverviewListItem } from './integration_api/overviewListsApis'

export { estimateAudienceSize, isValidPlayerId, parseDotnetTimeSpanToLuxon } from './coreUtils'

export * from './subscription_options/activables'
export * from './subscription_options/analyticsEvents'
export * from './subscription_options/auditLogs'
export * from './subscription_options/broadcasts'
export * from './subscription_options/experiments'
export * from './subscription_options/gameConfigs'
export * from './subscription_options/general'
export * from './subscription_options/guilds'
export * from './subscription_options/incidents'
export * from './subscription_options/leagues'
export * from './subscription_options/liveOpsEvents'
export * from './subscription_options/localization'
export * from './subscription_options/matchmaking'
export * from './subscription_options/notifications'
export * from './subscription_options/offers'
export * from './subscription_options/players'
export * from './subscription_options/scanJobs'
export * from './subscription_options/web3'

export type {
  IGeneratedUiComponentRule,
  IGeneratedUiFieldSchemaDerivedTypeInfo,
} from './components/generatedui/generatedUiTypes'

export {
  generatedUiFieldBaseProps,
  generatedUiFieldFormProps,
  useGeneratedUiFieldBase,
  useGeneratedUiFieldForm,
  generatedUiFieldFormEmits,
} from './components/generatedui/generatedFieldBase'

export { GetTypeSchemaForTypeName } from './components/generatedui/getGeneratedUiTypeSchema'
