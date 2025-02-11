// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

export { type ActionDebouncer, type ActionHandler, makeActionDebouncer } from './actionDebouncer'

export { makeAxiosActionHandler } from './axiosActionHandler'

export { assumeRoles, initialize as initializeAuth, login, logout } from './auth/auth'

export { initialize as initializeGameServerApi } from './initialization'

export type { PermissionDetails, UserDetails } from './auth/authProvider'

export { useGameServerApi, registerErrorVisualizationHandler } from './gameServerApi'

export { ApiPoller } from './apiPoller'
export { useGameServerApiStore, useStaticInfos } from './gameServerApiStore'
export { SseHandler } from './sseHandler'

export { type AxiosInstance } from 'axios'
