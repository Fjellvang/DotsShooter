// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { AxiosInstance } from 'axios'

import type { Subscription } from './subscriptions'

/**
 * The axios client to use for all subscription requests.
 */
let internalAxiosClient: AxiosInstance | undefined

/**
 * A function that returns whether the current user has permission to access subscription data. Defaults to a function that always returns `true` if no permission is passed to it and `false` otherwise.
 */
let internalHasPermissionFunction: (permission?: string) => boolean = (permission?: string) => !permission

/**
 * Initializes the subscriptions module.
 * @param axiosClient The axios client to use for all subscription requests. You can configure this client with your own interceptors and such before passing it in.
 * @param hasPermissionFunction Optional: A function that returns whether the current user has permission to access subscription data. This function will be called before every subscription request that needs a cache. If it returns false, the request will not be made. Defaults to a function that always returns true.
 * @param disableAutoPausing Optional: Whether to disable the automatic pausing of subscriptions when the page is not visible. Defaults to false. You can call pauseAllSubscriptions() and resumeAllSubscriptions() manually if you want to control this yourself.
 */
export function initializeSubscriptions(
  axiosClient: AxiosInstance,
  hasPermissionFunction?: (permission?: string) => boolean,
  disableAutoPausing?: boolean
): void {
  if (internalAxiosClient !== undefined) {
    console.warn(
      'Subscriptions module already initialized. Calling the initializeSubscriptions() more than once works, but it is likely unintended.'
    )
  }

  if (hasPermissionFunction) {
    internalHasPermissionFunction = hasPermissionFunction
  }
  internalAxiosClient = axiosClient

  if (!disableAutoPausing) {
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) pauseAllSubscriptions()
      else resumeAllSubscriptions()
    })
  }
}

/**
 * Gets the axios client to use for all subscription requests. Fails if the subscriptions module has not been initialized.
 */
export function getAxiosClient(): AxiosInstance {
  if (!internalAxiosClient) {
    throw new Error(
      'Subscriptions module not initialized. Call initializeSubscriptions() before calling getAxiosClient().'
    )
  }

  return internalAxiosClient
}

/**
 * Gets whether the current user has permission to access subscription data. This will always return true if the subscriptions module has not been initialized with a hasPermissionFunction.
 */
export function hasPermission(permission: string): boolean {
  return internalHasPermissionFunction(permission)
}

/**
 * Reactive status for whether the page is active in the browser.
 */
export let isPaused = false

/**
 * List of subscriptions that are waiting for the page to become visible before they start fetching.
 */
export let delayedSubscriptions: Subscription[] = []

/**
 * Pauses all subscriptions. This will prevent any subscription from fetching new data until resumeAllSubscriptions() is called. The subscription timers will still run, but they will not fetch new data.
 */
export function pauseAllSubscriptions(): void {
  isPaused = true
  log('Subscriptions paused.')
}

/**
 * Resumes all subscriptions. This will allow all subscriptions to start fetching new data again. If any subscriptions were delayed, they will start fetching immediately.
 */
export function resumeAllSubscriptions(): void {
  isPaused = false
  log('Subscriptions unpaused. Starting all delayed subscriptions.')
  for (const subscription of delayedSubscriptions) {
    log('Starting delayed subscription', subscription.key)
    subscription.fetcherPolicy.fetch()
  }

  delayedSubscriptions = []
}

/**
 * Uncomment this to enable detailed logging code.
 */
// eslint-disable-next-line @typescript-eslint/no-unused-vars
export function log(...message: unknown[]): void {
  // console.log('[Subscriptions]', ...message)
}
