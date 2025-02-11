// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Stores whether the safety locks are globally enabled by default.
 * This defaults to true. Use `setSafetyLockEnabledByDefault` to change it.
 */
let isSafetyLockEnabledByDefault = true

/**
 * Set whether the safety locks are globally enabled by default.
 * @param enabled Should the safety locks be enabled by default?
 */
export function setSafetyLockEnabledByDefault(enabled: boolean): void {
  isSafetyLockEnabledByDefault = enabled
}

/**
 * Get whether the safety locks are globally enabled by default.
 */
export function getSafetyLockEnabledByDefault(): boolean {
  return isSafetyLockEnabledByDefault
}

/**
 * A composable to manage safety locks.
 */
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export function useSafetyLock() {
  return {
    setSafetyLockEnabledByDefault,
    getSafetyLockEnabledByDefault,
  }
}
