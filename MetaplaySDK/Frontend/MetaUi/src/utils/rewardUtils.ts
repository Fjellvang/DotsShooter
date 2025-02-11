// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { useUiStore } from '../uiStore'

const rewardMetaProperties = ['displayName', 'getDiplayValue', '$type', 'matcher']

/**
 * Configuration needed to display a rich visualization for a game-specific reward type.
 */
export interface GameSpecificReward {
  /**
   * The C# type of the reward so it can be identified.
   * @example 'Game.Logic.RewardGems'
   */
  $type: string
  /**
   * A function that returns the amount of the reward to be shown in the UI.
   * @param reward The reward object.
   * @example (reward: any) => `💎 Gems x${reward.amount}`
   */
  getDisplayValue: (reward: any) => string
  /**
   * If there are multiple rewards that share the same C# type then this optional matched function should be used to
   * match a given reward against this particular type.
   * @example (reward) => reward.producerId === gameData.gameConfig.Producers[key].id
   */
  matcher?: (reward: any) => boolean
}

function matchRewardEntry(entry: any, candidate: any): boolean {
  if (entry.$type !== candidate.$type) return false
  if (entry.matcher && entry.matcher(candidate) !== true) return false
  return true
}

export function rewardWithMetaData(v: any): any {
  const uiStore = useUiStore()
  let rewardType = uiStore.gameSpecific.playerRewards.find((t: any) => matchRewardEntry(t, v))
  if (!rewardType) {
    // If the type doesn't exist then warn, but also return a placeholder GameSpecificReward so that the rest of the UI
    // doesn't fall over.
    console.error(`Unregistered reward type: ${v.$type}. Did you register it via the integration API?`)
    const unregisteredTypeMessage = `Unregistered Type: ${v.$type}`
    rewardType = {
      getDisplayValue: (): string => unregisteredTypeMessage,
      $type: v.$type,
    }
  }
  return { ...v, ...rewardType }
}

// TODO: Not used anymore? Consider removing.
export function rewardsWithMetaData(rewards: any[]): any[] {
  return rewards.map((v: any) => rewardWithMetaData(v))
}

// TODO: Not used anymore? Consider removing.
export function stripRewardsMetadata(rewards: any[]): Array<Record<string, unknown>> {
  return rewards.map((v: Record<string, unknown> | ArrayLike<unknown>) =>
    Object.fromEntries(
      Object.entries(v).filter(
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        ([key, _]) => !rewardMetaProperties.includes(key)
      )
    )
  )
}
