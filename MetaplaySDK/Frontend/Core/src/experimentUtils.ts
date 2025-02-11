// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { estimateAudienceSize } from './coreUtils'

/**
 * Estimate the audience size based (optionally) on the experiment's targeting.
 * @param totalPlayerCount Total number of players in the game.
 * @param singleExperimentData Details of the experiment.
 * @param playerSegmentsData Details of play segments.
 * @returns Returns estimated audience size or null if it cannot be calculated.
 */
export function totalEstimatedAudienceSize(
  totalPlayerCount: number,
  singleExperimentData: any,
  playerSegmentsData: any
): number | null {
  if (singleExperimentData.state.targetCondition) {
    if (playerSegmentsData) {
      return estimateAudienceSize(playerSegmentsData.segments, {
        targetCondition: singleExperimentData.state.targetCondition,
      })
    } else {
      return null
    }
  } else {
    return totalPlayerCount
  }
}

/**
 * Data format returned by `calculatedAudienceSize`.
 */
export interface CalculatedAudienceSize {
  size: number
  tooltip: string
}

/**
 * Calculate the audience size based on rollout and audience size.
 * @param totalPlayerCount Total number of players in the game.
 * @param singleExperimentData Details of the experiment.
 * @param playerSegmentsData Details of play segments.
 * @returns Returns the calculated audience size and a tooltip explaining how it was calculated.
 */
export function calculatedAudienceSize(
  totalPlayerCount: number,
  singleExperimentData: any,
  playerSegmentsData: any
): CalculatedAudienceSize {
  const totalAudienceSize = totalEstimatedAudienceSize(totalPlayerCount, singleExperimentData, playerSegmentsData) ?? 0
  const rolloutEstimatedAudienceSize = totalAudienceSize * (singleExperimentData.state.rolloutRatioPermille / 1000)

  if (
    singleExperimentData.state.hasCapacityLimit &&
    singleExperimentData.state.maxCapacity < rolloutEstimatedAudienceSize
  ) {
    return {
      size: singleExperimentData.state.maxCapacity,
      tooltip: 'The calculated audience size is capped by max capacity.',
    }
  } else {
    return {
      size: Math.floor(rolloutEstimatedAudienceSize),
      tooltip: 'The target audience size is derived by multiplying total audience size with rollout percentage.',
    }
  }
}
