// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { AxiosHeaders } from 'axios'

import { useGameServerApi } from '../gameServerApi'
import { useGameServerApiStore } from '../gameServerApiStore'
import type { AuthInitResult, AuthProvider, PermissionDetails } from './authProvider'
import { type AuthConfigJwt, AuthProviderJwt } from './authProviderJwt'
import { type AuthConfigNoAuth, AuthProviderNoAuth } from './authProviderNoAuth'

/**
 * Authentication is initialized during startup by calling initialize() and passing in auth config.
 * This allows the auth system to select an AuthProvider object based on the auth type that has been
 * configured in the game server. The actual hard work (and authentication platform specific work)
 * of authentication is delegated to this object.
 */

let authProvider: AuthProvider | null = null

/**
 * The contents of the AuthConfig object is specific to each authentication type, but they must all
 * include 'type' as a minimum.
 */
export interface AuthConfig {
  type: string
}

/**
 * Server responses.
 */
interface UserResponse {
  roles: string[]
  permissions: string[]
}

interface AllPermissionsAndRolesResponse {
  roles: string[]
  permissionGroups: Array<{
    title: string
    permissions: Array<{
      name: string
      description: string
    }>
  }>
}

/**
 * Initialize the authentication component.
 */
export async function initialize(): Promise<AuthInitResult> {
  const gameServerApiStore = useGameServerApiStore()
  const gameServerApi = useGameServerApi()

  // Error if calling this function before connecting to the game server.
  if (!gameServerApiStore.hasConnected || !gameServerApiStore.staticInfos) {
    return {
      state: 'error',
      details: 'Game server API needs to be initialized before authentication',
    }
  }

  const authConfig = gameServerApiStore.staticInfos.liveOpsDashboardInfo.authConfig

  // Select the correct authentication provider.
  switch (authConfig.type) {
    case 'None':
      authProvider = new AuthProviderNoAuth(authConfig as AuthConfigNoAuth)
      break
    case 'JWT':
      authProvider = new AuthProviderJwt(authConfig as AuthConfigJwt)
      break
  }
  if (authProvider === null) {
    return {
      state: 'error',
      details: `Could not create auth provider for "${authConfig.type}" type`,
    }
  }

  // Initialize the auth provider.
  const authInitResult: AuthInitResult = await authProvider.initialize()

  // Auth flow has completed at this point, and we have the result.
  if (authInitResult.state === 'success') {
    // Auth succeeded
    try {
      // Re-assume previous roles if needed.
      const canAssumeRoles = authProvider.getCanAssumeRoles()
      if (canAssumeRoles) {
        const previousAssumedRoles = sessionStorage.getItem('assumedRoles')
        if (previousAssumedRoles !== null) {
          const assumedRoles = JSON.parse(previousAssumedRoles) as string[]
          if (assumedRoles.length !== 0) {
            await assumeRoles(assumedRoles)
          }
        }
      }

      // Fetch initial user roles and permissions.
      await gameServerApi.get('/authDetails/user').then((result) => {
        const userResponse = result.data as UserResponse
        gameServerApiStore.auth.userRoles = userResponse.roles
        gameServerApiStore.auth.userPermissions = userResponse.permissions
      })
      let cachedUserRoles: string = gameServerApiStore.auth.userRoles.sort().join(',')

      // When the user's roles change, update the list of roles and permissions that they have.
      const onActiveRolesReceived = async (activeRolesRaw: string): Promise<void> => {
        const activeRoles = [...new Set(activeRolesRaw.split(',').map((role) => role.trim()))].sort().join(',')
        if (activeRoles !== cachedUserRoles) {
          // Roles have changed since the last time we checked.
          cachedUserRoles = activeRoles
          await gameServerApi.get('/authDetails/user').then((result) => {
            const userResponse = result.data as UserResponse
            gameServerApiStore.auth.userRoles = userResponse.roles
            gameServerApiStore.auth.userPermissions = userResponse.permissions
          })
        }
      }

      // Look for changes in the users roles.
      gameServerApi.interceptors.response.use(async (response) => {
        // Server returns active roles in the response of every request, but in some cases this header might be missing.
        const headers = response.headers
        if (headers instanceof AxiosHeaders && headers.has('metaplay-activeroles')) {
          const activeRoles = headers['metaplay-activeroles'] as string | null
          if (activeRoles !== null) {
            await onActiveRolesReceived(activeRoles)
          }
        }
        return response
      })

      // Fetch list of all available roles and permissions.
      const allPermissionsAndRoles = (await gameServerApi.get('/authDetails/allPermissionsAndRoles'))
        .data as AllPermissionsAndRolesResponse
      const allRoles = allPermissionsAndRoles.roles
      const allPermissions: PermissionDetails[] = []
      allPermissionsAndRoles.permissionGroups.forEach((group) => {
        group.permissions.forEach((permission) => {
          allPermissions.push({
            name: permission.name,
            description: permission.description,
            group: group.title.split(' permissions')[0],
            type: permission.name.split('.')[0],
          })
        })
      })

      // Store state.
      gameServerApiStore.auth.serverRoles = allRoles
      gameServerApiStore.auth.serverPermissions = allPermissions
      gameServerApiStore.auth.canLogout = authProvider.getCanLogout()
      gameServerApiStore.auth.canAssumeRoles = authProvider.getCanAssumeRoles()
      gameServerApiStore.auth.rolePrefix = authProvider.getRolePrefix()

      // Get user details.
      gameServerApiStore.auth.userDetails = (await authProvider.getUserDetails()) ?? {
        name: 'Failed to retrieve user name',
        email: 'Failed to retrieve user email',
        id: 'Failed to retrieve user id',
        picture: 'Failed to retrieve user picture',
      }

      // Check that the user has the most basic permissions required to do anything useful.
      // This if also catches users who don't have any roles and therefore no permissions, i.e.,
      // new users that haven't been granted any roles yet.
      if (
        !gameServerApiStore.auth.userPermissions.includes('dashboard.view') ||
        !gameServerApiStore.auth.userPermissions.includes('api.general.view')
      ) {
        return {
          state: 'not_enough_permissions',
          details: 'User does not have enough permissions to continue',
        }
      }

      // And we're done..
      return {
        state: 'success',
        details: 'Authentication succeeded',
      }
    } catch (error) {
      const errMessage = error instanceof Error ? error.message : String(error)
      // Error thrown during initialization.
      return {
        state: 'error',
        details: 'Failed to read user and permission data: ' + errMessage,
      }
    }
  } else if (authInitResult.state === 'require_login' || authInitResult.state === 'error') {
    // Auth returned either an error or that login is required.
    return authInitResult
  } else {
    // Auth provider didn't return a proper response.
    return {
      state: 'error',
      details: 'Auth provider failed to initialize properly',
    }
  }
}

/**
 * Cause the user to assume the given roles.
 * @param rolesToAssume
 */
export async function assumeRoles(rolesToAssume: string[] | null): Promise<void> {
  const gameServerApiStore = useGameServerApiStore()
  const gameServerApi = useGameServerApi()

  if (authProvider?.getCanAssumeRoles() === true) {
    // Set or clear gameServerApi header.
    const customHeaderName = 'Metaplay-AssumedUserRoles'
    if (rolesToAssume && rolesToAssume.length > 0) {
      gameServerApi.defaults.headers.common[customHeaderName] = rolesToAssume.toString()
      sessionStorage.setItem('assumedRoles', JSON.stringify(rolesToAssume))
      gameServerApiStore.auth.userAssumedRoles = rolesToAssume
    } else {
      // Note: using delete here instead of setting to undefined, because the latter will still potentially send the header.
      // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
      delete gameServerApi.defaults.headers.common[customHeaderName]
      sessionStorage.removeItem('assumedRoles')
      gameServerApiStore.auth.userAssumedRoles = []
    }

    await gameServerApi.get('/authDetails/user').then((result) => {
      const userResponse = result.data as UserResponse
      gameServerApiStore.auth.userRoles = userResponse.roles
      gameServerApiStore.auth.userPermissions = userResponse.permissions
    })
  } else {
    throw new Error('Assuming roles is disabled')
  }
}

/**
 * Call to initiate the auth providers login flow.
 */
export function login(): void {
  authProvider?.login()
}

/**
 * Call to initiate the auth providers logout flow.
 */
export function logout(): void {
  authProvider?.logout()
}
