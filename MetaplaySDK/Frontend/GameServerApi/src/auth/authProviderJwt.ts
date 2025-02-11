// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { AxiosError, AxiosInstance } from 'axios'

import { useGameServerApi } from '../gameServerApi'
import type { AuthConfig } from './auth'
import { AuthProvider, type AuthInitResult, type UserDetails } from './authProvider'

/**
 * Config options that come from the game server.
 */
export interface AuthConfigJwt extends AuthConfig {
  rolePrefix: string
  logoutUri: string
}

/**
 * Server responses.
 */
interface UserInfoResponse {
  name?: string
  email?: string
  sub?: string
  picture?: string
}

/**
 * Jwt authentication provider.
 */

export class AuthProviderJwt extends AuthProvider {
  private readonly logoutUri: string
  private userDetails?: UserDetails | null
  private readonly gameServerApi: AxiosInstance

  /**
   * @param authConfig Auth config from the game server.
   */
  public constructor(authConfig: AuthConfigJwt) {
    super(authConfig.rolePrefix)

    this.logoutUri = authConfig.logoutUri
    this.gameServerApi = useGameServerApi()
  }

  /**
   * Initialize the provider.
   * @returns Result of initialization.
   */
  public async initialize(): Promise<AuthInitResult> {
    // Auth has already succeeded if we got this far.
    return {
      state: 'success',
      details: '',
    }
  }

  /**
   * Call to initiate the auth providers login flow.
   */
  public login(): void {
    // There is no login flow for this provider.
    throw new Error('No login possible')
  }

  /**
   * Call to initiate the auth providers logout flow.
   */
  public logout(): void {
    window.location.replace(this.logoutUri)
  }

  /**
   * @returns True if the auth provider supports logging out.
   */
  public getCanLogout(): boolean {
    return !!this.logoutUri
  }

  /**
   * @returns True if the auth provider supports assuming rules.
   */
  public getCanAssumeRoles(): boolean {
    return false
  }

  /**
   * @returns Details of the current user.
   */
  public async getUserDetails(): Promise<UserDetails | null> {
    if (this.userDetails === undefined) {
      this.userDetails = await this.fetchUserDetails()
    }
    return this.userDetails
  }

  /**
   * Fetch user details and cache them.
   */
  private async fetchUserDetails(): Promise<UserDetails | null> {
    let fetchedUserDetails: UserDetails | null = null
    await this.gameServerApi
      .get('/userInfo')
      .then((response) => {
        const userInfoResponse = response.data as UserInfoResponse
        fetchedUserDetails = {
          name: userInfoResponse.name ?? 'No name',
          email: userInfoResponse.email ?? 'No email',
          id: userInfoResponse.sub ?? 'No ID',
          picture: userInfoResponse.picture ?? 'No picture',
        }
      })
      .catch((error: Error | AxiosError) => {
        console.warn(`Failed to get userinfo from gameserver: ${error.message}`)
      })
    return fetchedUserDetails
  }
}
