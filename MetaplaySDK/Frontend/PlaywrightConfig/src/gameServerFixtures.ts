// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * @file Contains Playwright fixtures for authenticating and managing machine users in the Metaplay Developer Portal.
 */
import { type test, request } from '@playwright/test'

import type { TestFixtures } from './fixtureTypes.ts'

// We define minimal types for some server types here to help type checking.
// Ideally we'd import these types from some shared location, such as `CoreTypes`.

interface AuthDetailsResponse {
  roles: string[]
}

interface CreatePlayerResponse {
  id: string
}

interface StaticInfos {
  projectInfo: {
    projectName: string
  }
  environmentInfo: {
    environmentName: string
    environmentFamily: 'Local' | 'Development' | 'Staging' | 'Production'
    isProductionEnvironment: boolean
    grafanaUri: string | null
    kubernetesNamespace: string | null
  }
  gameServerInfo: {
    buildNumber: string
    commitId: string
  }
  liveOpsDashboardInfo: {
    playerDeletionDefaultDelay: string
    authConfig: {
      type: string
      allowAssumeRoles: boolean
      rolePrefix: string | null
      logoutUri: string | null
    }
  }
  featureFlags: {
    pushNotifications: boolean
    guilds: boolean
    asyncMatchmaker: boolean
    web3: boolean
    playerLeagues: boolean
    localization: boolean
    liveOpsEvents: boolean
    gameTimeSkip: boolean
    googlePlayInAppPurchaseRefunds: boolean
    removeIapSubscriptions: boolean
  }
}

interface StaticConfigResponse {
  activablesMetadata: {
    categories: {
      Event: Record<string, unknown>
    }
  }
}

/**
 * Playwright fixtures that make it easier to write tests for the Metaplay LiveOps Dashboard.
 */
export const gameServerFixtures: Parameters<typeof test.extend<TestFixtures>>[0] = {
  /**
   * Fixture that fetches feature flags from the game server.
   */
  // eslint-disable-next-line no-empty-pattern -- We don't need any fixtures, but need to specify {} to access to `use`.
  featureFlags: async ({}, use) => {
    const context = await request.newContext()

    // Perform requests in parallel to save time.
    // Note: static configs can be really large and slow. Any alternative?
    const [helloResponse, staticConfigResponse] = await Promise.all([
      context.get('/api/hello'),
      context.get('/api/staticConfig'),
    ])
    const helloData = (await helloResponse.json()) as StaticInfos
    const staticConfigData = (await staticConfigResponse.json()) as StaticConfigResponse

    // Extract feature flags from the helloData and staticConfigData.
    const featureFlags: TestFixtures['featureFlags'] = {
      ...helloData.featureFlags,
      ingameEvents: !!staticConfigData.activablesMetadata.categories.Event,
    }

    await use(featureFlags)
  },

  /**
   * Fixture that creates a fresh player and provides it to the test.
   */
  // eslint-disable-next-line no-empty-pattern -- We don't need any fixtures, but need to specify {} to access to `use`.
  freshTestPlayer: async ({}, use) => {
    const playerId = await createTestPlayer()

    await use(playerId)

    // await deleteTestPlayer(playerId)
  },

  /**
   * Fixture that fetches raw player detail entries from the API.
   * TODO: what even is this? Titles? Why a fresh player without documenting it?
   */
  rawPlayerDetailEntryTitles: async ({ freshTestPlayer }, use) => {
    const context = await request.newContext()
    const res = await context.get(`api/players/${freshTestPlayer}/raw`)
    const data = (await res.json()) as Record<string, { title: string }>
    const titles: TestFixtures['rawPlayerDetailEntryTitles'] = Object.values(data).map((entry) => entry.title)
    await use(titles)
  },

  /**
   * Authentication details fetched from the API.
   */
  // eslint-disable-next-line no-empty-pattern -- We don't need any fixtures, but need to specify {} to access to `use`.
  authDetails: async ({}, use) => {
    const context = await request.newContext()
    const [helloResponse, authDetailsResponse] = await Promise.all([
      context.get('/api/hello'),
      context.get('/api/authDetails/allPermissionsAndRoles'),
    ])
    const helloData = (await helloResponse.json()) as StaticInfos
    const authDetailsData = (await authDetailsResponse.json()) as AuthDetailsResponse

    const authDetails: TestFixtures['authDetails'] = {
      allUserRoles: authDetailsData.roles,
      canAssumeRoles: helloData.liveOpsDashboardInfo.authConfig.allowAssumeRoles,
    }

    await use(authDetails)
  },

  /**
   * Fixture that fetches dashboard runtime options from the API.
   */
  // eslint-disable-next-line no-empty-pattern -- We don't need any fixtures, but need to specify {} to access to `use`.
  dashboardOptions: async ({}, use) => {
    const context = await request.newContext()
    const res = await context.get('/api/dashboardOptions')
    const data = (await res.json()) as TestFixtures['dashboardOptions']

    await use(data)
  },
}

/**
 * Helper function to create a new player account via the debug HTTP API.
 * @returns The ID of the newly created player.
 */
async function createTestPlayer(): Promise<TestFixtures['freshTestPlayer']> {
  const context = await request.newContext()
  const res = await context.post('/api/testing/createPlayer')
  const data = (await res.json()) as CreatePlayerResponse
  return data.id
}

// async function deleteTestPlayer (playerId: string) {
//   // TODO: implement an `/api/testing/deletePlayer` endpoint.
// }
