// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { Duration } from 'luxon'
import { computed, ref } from 'vue'

/**
 * The current game time offset to apply to the game time. 0 means no offset has been applied.
 */
const gameTimeOffset = ref<Duration>(Duration.fromMillis(0))

/**
 * Whether a game time offset has been applied or not.
 */
const hasGameTimeOffset = computed(() => gameTimeOffset.value.toMillis() !== 0)

/**
 * Set the game time offset to be used by the `useGameTimeOffset`'s `gameTimeOffset` property.
 * @param offset Game time offset to apply.
 */
export function setGameTimeOffset(offset: Duration): void {
  gameTimeOffset.value = offset
}

/**
 * A composable to manage game time.
 */
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export function useGameTimeOffset() {
  return {
    gameTimeOffset,
    hasGameTimeOffset,
  }
}
