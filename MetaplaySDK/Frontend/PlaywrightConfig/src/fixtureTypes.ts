// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * These fixtures will be evaluated for each test.
 */
export interface TestFixtures {
  // General fixture types --------------------------------------------------------------------------------------------

  /**
   * Base URL for the API endpoints.
   */
  apiURL: string

  /**
   * A unique test token for filling input fields.
   */
  testToken: string

  // Game server fixture types ----------------------------------------------------------------------------------------

  /**
   * The features that are currently enabled.
   */
  featureFlags: {
    pushNotifications: boolean
    guilds: boolean
    asyncMatchmaker: boolean
    web3: boolean
    playerLeagues: boolean
    localization: boolean
    liveOpsEvents: boolean
    ingameEvents: boolean
  }

  /**
   * A newly created player account. Not shared between tests.
   */
  freshTestPlayer: string

  /**
   * Raw player detail entry titles fetched from the API.
   */
  rawPlayerDetailEntryTitles: string[]

  /**
   * Authentication details fetched from the API.
   */
  authDetails: {
    allUserRoles: string[]
    canAssumeRoles: boolean
  }

  /**
   * Dashboard options fetched from the API.
   */
  dashboardOptions: {
    dashboardHeaderColorInHex: string
    playerDetailsTab0DisplayName: string
    playerDetailsTab1DisplayName: string
    playerDetailsTab2DisplayName: string
    playerDetailsTab3DisplayName: string
    playerDetailsTab4DisplayName: string
  }
}
