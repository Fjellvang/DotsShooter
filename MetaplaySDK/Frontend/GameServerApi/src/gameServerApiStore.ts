// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { defineStore } from 'pinia'

import type { PermissionDetails, UserDetails } from './auth/authProvider'
import type { StaticInfos } from './initialization'

export interface GameServerApiStoreState {
  isConnected: boolean
  hasConnected: boolean
  staticInfos?: StaticInfos
  auth: {
    requiresBasicPermissions: boolean
    requiresLogin: boolean
    userRoles: string[]
    userPermissions: string[]
    userDetails: UserDetails
    userAssumedRoles: string[]
    serverRoles: string[]
    serverPermissions: PermissionDetails[]
    canLogout: boolean
    canAssumeRoles: boolean
    rolePrefix: string
    hasTokenExpired: boolean
  }
}

const defaultState: GameServerApiStoreState = {
  isConnected: false,
  hasConnected: false,
  auth: {
    requiresBasicPermissions: false,
    requiresLogin: false,
    userRoles: [],
    userPermissions: [],
    userDetails: {
      name: '',
      email: '',
      id: '',
      picture: '',
    },
    userAssumedRoles: [],
    serverRoles: [],
    serverPermissions: [],
    canLogout: false,
    canAssumeRoles: false,
    rolePrefix: '',
    hasTokenExpired: false,
  },
}

/**
 * Use a Pinia store to remember connection states. This makes them easy to view in the Vue debugger.
 */
export const useGameServerApiStore = defineStore('game-server-api', {
  state: () => defaultState,
})

/**
 * Helper function to get the available static information about the dashboard, game server and environment.
 * These are used internally to enable and disable dashboard features and to show better error messages in case of trouble.
 */
export function useStaticInfos(): StaticInfos {
  const gameServerApiStore = useGameServerApiStore()

  if (!gameServerApiStore.hasConnected || !gameServerApiStore.staticInfos) {
    throw new Error('Game server API has not been initialized yet.')
  }

  return gameServerApiStore.staticInfos
}
