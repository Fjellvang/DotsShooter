// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import axios from 'axios'
import { readonlyProxyOf } from 'readonly-proxy'
import { z } from 'zod'

import { useGameServerApi } from './gameServerApi'
import { useGameServerApiStore } from './gameServerApiStore'

const HelloResponseSchema = z.object({
  projectInfo: z.object({
    projectName: z.string(),
  }),
  environmentInfo: z.object({
    environmentName: z.string(),
    environmentFamily: z.union([
      z.literal('Local'),
      z.literal('Development'),
      z.literal('Staging'),
      z.literal('Production'),
    ]),
    isProductionEnvironment: z.boolean(),
    grafanaUri: z.string().nullable(),
    kubernetesNamespace: z.string().nullable(),
  }),
  gameServerInfo: z.object({
    buildNumber: z.string(),
    commitId: z.string(),
  }),
  liveOpsDashboardInfo: z.object({
    playerDeletionDefaultDelay: z.string(),
    authConfig: z.object({
      type: z.string(),
      allowAssumeRoles: z.boolean(),
      rolePrefix: z.string().nullable(),
      logoutUri: z.string().nullable(),
    }),
  }),
  featureFlags: z.object({
    pushNotifications: z.boolean(),
    guilds: z.boolean(),
    asyncMatchmaker: z.boolean(),
    web3: z.boolean(),
    playerLeagues: z.boolean(),
    localization: z.boolean(),
    liveOpsEvents: z.boolean(),
    gameTimeSkip: z.boolean(),
    googlePlayInAppPurchaseRefunds: z.boolean(),
    removeIapSubscriptions: z.boolean(),
  }),
})

/**
 * Static information about the game server and its environment that gets fetched as the first step of the initialization process.
 */
export type StaticInfos = z.infer<typeof HelloResponseSchema>

export async function initialize(): Promise<void> {
  const gameServerApiStore = useGameServerApiStore()
  const gameServerApi = useGameServerApi()

  // Error if calling this function twice.
  if (gameServerApiStore.hasConnected) {
    throw new Error('Game server API has already been initialized.')
  }

  // Read static infos from the game server. This route is open and always reachable.
  let helloResponse: unknown
  let retries = 5
  while (!helloResponse) {
    try {
      helloResponse = (await gameServerApi.get<StaticInfos>('/hello')).data
    } catch (error) {
      // Tolerate network errors by retrying a few times.
      if (axios.isAxiosError(error)) {
        if (retries-- <= 0) {
          throw new Error('Failed to connect to the game server after 5 retries.')
        } else {
          // Wait a second before retrying.
          await new Promise((resolve) => setTimeout(resolve, 1000))
        }
      } else {
        // eslint-disable-next-line @typescript-eslint/only-throw-error
        throw error
      }
    }
  }

  // Validate the response. We want this type to never get out of sync with the server implementation.
  HelloResponseSchema.parse(helloResponse)

  gameServerApiStore.staticInfos = readonlyProxyOf(helloResponse) as StaticInfos
  gameServerApiStore.hasConnected = true
  gameServerApiStore.isConnected = true

  await Promise.resolve()
}
