// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * @fileoverview The subscription system allows clients to subscribe to receive updates to source of data. If many
 * subscribers subscribe to the same data then it is still only fetched once. Data is cached so that it is
 * available instantly to new subscribers.
 */
import isEqual from 'lodash-es/isEqual'
import {
  type Ref,
  type ShallowRef,
  getCurrentInstance,
  onUnmounted,
  readonly,
  ref,
  shallowRef,
  watch,
  type WatchStopHandle,
} from 'vue'

import { type CacheRetentionPolicy, getCacheRetentionPolicyKeepForever } from './cacheRetentionPolicies'
import { type FetcherPolicy, getFetcherPolicyFixed } from './fetcherPolicies'
import { hasPermission, log, delayedSubscriptions, isPaused } from './initialization'
import { type PollingPolicy, getPollingPolicyOnceOnly } from './pollingPolicies'

/**
 * Data mutator callback interface.
 */
type DataMutatorCallback<OutputDataType, PreMutatedDataType> = (data: PreMutatedDataType) => OutputDataType

/**
 * Defines the configuration required to set up a subscription.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface SubscriptionOptions<T = any, PreMutatedDataType = any> {
  /**
   * Defines when to poll for new data from the server.
   * @example new PollingPolicyTimer(10000)
   */
  pollingPolicy: PollingPolicy

  /**
   * Defines how to fetch data for this subscription form the server.
   * @example getFetcherPolicyGet('/players')
   */
  fetcherPolicy: FetcherPolicy

  /**
   * Optional: Data mutation function for this subscription.
   * @example (data) => data.map((player) => ({ ...player, uppercaseName: player.name.toUpperCase() }))
   */
  dataMutator?: DataMutatorCallback<T, PreMutatedDataType>

  /**
   * Optional: The permission required for the data fetch to succeed. If user does not have this permission then we can fail early and skip the fetch.
   * @example 'api.players.view'
   */
  permission?: string

  /**
   * Cache retention policy (when to clear old data) for when there are no more subscribers.
   * @example getCacheRetentionPolicyTimed(10000)
   */
  cacheRetentionPolicy: CacheRetentionPolicy
}

/**
 * Defines the details about a subscription returned from the `use*` functions.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface SubscriptionDetails<T = any> {
  /**
   * Reactive data object containing the latest data for this subscription.
   */
  data: ShallowRef<T | undefined>
  /**
   * Reactive data object for any errors preventing data fetch for this subscription.
   */
  error: ShallowRef
  /**
   * Reactive data object for whether the user has the required permissions to fetch data for this subscription.
   */
  hasPermission: Ref<boolean>
  /**
   * A function to refresh the subscription. This will force a new fetch of data from the server.
   */
  refresh: () => void
}

/**
 * Defines the extended information about a subscription returned from the `useManuallyManagedStaticSubscription` function.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface ManualSubscriptionDetails<T = any> extends SubscriptionDetails<T> {
  /**
   * A function to cancel the current subscription.
   */
  unsubscribe: () => void
}

/**
 * Internally stored instance of an individual subscription. Each subscription may have multiple subscribers.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface Subscription<T = any, PreMutatedDataType = any> {
  /**
   * Unique id for this subscription type. Used for data de-duplication.
   */
  key: string

  /**
   * Data that was fetched from the game server.
   */
  data: ShallowRef<T | undefined>

  /**
   * Error object if data could not be fetched.
   */
  error: ShallowRef<Error | undefined>

  /**
   * Counts the number of fetches that have completed.
   */
  fetchesCompleted: number

  /**
   * True if the user has the required permissions to fetch the data.
   */
  hasPermission: Ref<boolean>

  /**
   * Total number of subscribers to this subscription. When this goes to zero, the subscription is automatically cancelled.
   */
  subscriberCount: number

  /**
   * Polling policy (how often is data fetched) for this subscription.
   */
  pollingPolicy: PollingPolicy

  /**
   * Fetcher policy (how is data fetched) for this subscription.
   */
  fetcherPolicy: FetcherPolicy

  /**
   * Optional data mutation function for this subscription.
   */
  dataMutator?: DataMutatorCallback<T, PreMutatedDataType>

  /**
   * Cache retention policy (when to clear old data) for this subscription.
   */
  cacheRetentionPolicy: CacheRetentionPolicy
}

/**
 * Local object that holds (and thus, caches) all subscriptions. Abandoned subscriptions are removed from this object
 * based on their cache retention policy.
 */
const subscriptions: Record<string, Subscription | undefined> = {}

/**
 * Create a new subscription. Subscriptions can be either static (always fetching the same data) or dynamic (the source
 * of the fetched data can change). When you pass a function as the `options` argument, you are explicitly asking for a
 * dynamic subscription. If the 'options' function returns undefined then the subscription will be unsubscribed.
 * @param options Either SubscriptionOptions or a function that returns SubscriptionOptions.
 * @returns Reactive objects for the data, error, and permission status of the subscription as well as a function to refresh the subscription.
 * @example
 * // Static subscription:
 * const { data } = useSubscription(getSinglePlayerSubscriptionOptions(playerId))
 * @example
 * // Dynamic subscription:
 * const { data } = useSubscription(getSinglePlayerSubscriptionOptions(() => props.playerId)
 */
export function useSubscription<T>(
  options: SubscriptionOptions<T> | (() => SubscriptionOptions<T> | undefined)
): SubscriptionDetails<T> {
  if (typeof options === 'function') {
    // eslint-disable-next-line @typescript-eslint/no-deprecated -- Internal function used on purpose.
    return useDynamicSubscription(options)
  } else {
    // eslint-disable-next-line @typescript-eslint/no-deprecated -- Internal function used on purpose.
    return useStaticSubscription(options)
  }
}

/**
 * Create a new static subscription. Static subscriptions are ideal for fetching data with a query that doesn't change.
 * @param options The configuration for what kind of subscription to create.
 * @returns Reactive objects for the data, error, and permission status of the subscription as well as a function to refresh the subscription.
 * @deprecated Use `useSubscription` instead. Since R25.
 * @example
 * const { data } = useStaticSubscription(getBackendStatusSubscriptionOptions())
 */
export function useStaticSubscription<T>(options: SubscriptionOptions<T>): SubscriptionDetails<T> {
  if (!getCurrentInstance()) {
    throw new Error(
      'Failed to set up subscription expiration. You must call subscriptions from within the `setup()` function of a component or use the `useManuallyManagedStaticSubscription()` to acknowledge that the subscription will never stop or be cleared.'
    )
  }

  // Subscribe to the subscription.
  const subscription = addSubscriberToSubscription(options)

  // Use `onUnmounted` to automatically unsubscribe when the component is destroyed.
  onUnmounted(() => {
    removeSubscriberFromSubscription(subscription.key)
  })

  return {
    data: subscription.data,
    error: subscription.error,
    hasPermission: subscription.hasPermission,
    refresh: (): void => {
      refreshSubscription(subscription.key)
    },
  }
}

/**
 * An empty subscription that always returns `null` as data. Used as a default value for dynamic subscriptions.
 */
const placeholderSubscriptionOptions = {
  key: 'placeholder',
  pollingPolicy: getPollingPolicyOnceOnly(),
  fetcherPolicy: getFetcherPolicyFixed(null),
  cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
}

/**
 * Create a new dynamic subscription. Dynamic subscriptions are ideal for fetching data with a query that changes, such as a search query, and where you still want to cache the data.
 * @param sourceSubscriptionOptions A function that returns the subscription options to use. Return `undefined` to unsubscribe.
 * @returns Reactive objects for the data, error, and permission status of the subscription as well as a function to refresh the subscription.
 * @deprecated Use `useSubscription` instead. Since R25.
 * @example
 * const { data } = useDynamicSubscription(playerId : getSinglePlayerSubscriptionOptions(): undefined)
 */
export function useDynamicSubscription<T>(
  sourceSubscriptionOptions: () => SubscriptionOptions<T> | undefined
): SubscriptionDetails<T> {
  if (!getCurrentInstance()) {
    throw new Error(
      'Failed to set up subscription expiration. Dynamic subscriptions can only be used from within the `setup()` function of a component.'
    )
  }

  // Subscribe to an empty placeholder.
  const subscription = addSubscriberToSubscription(placeholderSubscriptionOptions)
  const currentSubscriptionKey = ref(subscription.key)

  // Create fixed refs to our data.
  const data = shallowRef<T>()
  const error = shallowRef<Error>()
  const hasPermission = ref(true)

  // Somewhere to store the unwatchers.
  let unwatchData: WatchStopHandle | undefined
  let unwatchError: WatchStopHandle | undefined

  // Use `onUnmounted` to automatically unsubscribe when the component is destroyed.
  onUnmounted(() => {
    // Unwatch the old data and error refs to make sure there are no dangling watchers.
    if (unwatchData) {
      unwatchData()
    }
    if (unwatchError) {
      unwatchError()
    }

    // Unsubscribe from the current subscription.
    removeSubscriberFromSubscription(currentSubscriptionKey.value)
  })

  // Local function to resubscribe to a new subscription.
  function resubscribe(options: SubscriptionOptions<T>): void {
    removeSubscriberFromSubscription(currentSubscriptionKey.value)
    const subscription = addSubscriberToSubscription(options)
    currentSubscriptionKey.value = subscription.key

    // Unwatch the old data and error refs.
    if (unwatchData) {
      unwatchData()
    }
    if (unwatchError) {
      unwatchError()
    }

    // Watch data and error refs from the subscription, and copy them to our local refs when they change. We also need
    // to set up unwatchers so that we can stop watching the old refs when we resubscribe.
    unwatchData = watch(
      subscription.data,
      () => {
        data.value = subscription.data.value as T
      },
      { immediate: true }
    )
    unwatchError = watch(subscription.error, () => {
      error.value = subscription.error.value
    })
    hasPermission.value = subscription.hasPermission.value
  }

  // Watch the source subscription options and resubscribe when they change.
  watch(
    sourceSubscriptionOptions,
    () => {
      const newOptions = sourceSubscriptionOptions()
      if (newOptions) resubscribe(newOptions)
      else resubscribe(placeholderSubscriptionOptions)
    },
    { immediate: true }
  )

  return {
    data,
    error,
    hasPermission,
    refresh: (): void => {
      refreshSubscription(currentSubscriptionKey.value)
    },
  }
}

/**
 * Create a new static subscription that does not automatically stop. Manually managed static subscriptions are useful for setting up background pollers that do not depend on the lifecycle of any given Vue component.
 * @param options The configuration for what kind of subscription to create.
 * @returns Same properties as for a static subscription, and an extra function to manually unsubscribe.
 * @example
 * // Subscribe to data.
 * const { data, unsubscribe } = useManuallyManagedStaticSubscription(getRuntimeOptionsSubscriptionOptions())
 *
 * // If you need to unsubscribe (eg: on page unmount).
 * unsubscribe()
 */
export function useManuallyManagedStaticSubscription<T>(options: SubscriptionOptions<T>): ManualSubscriptionDetails<T> {
  // Subscribe to the subscription.
  const subscription = addSubscriberToSubscription(options)

  return {
    data: subscription.data,
    error: subscription.error,
    hasPermission: subscription.hasPermission,
    refresh: (): void => {
      refreshSubscription(subscription.key)
    },
    unsubscribe: (): void => {
      removeSubscriberFromSubscription(subscription.key)
    },
  }
}

/**
 * Fetches the data from a subscription once and then unsubscribes.
 * @returns A promise that resolves to the fetched data.
 * @example const allGuilds = await fetchSubscriptionDataOnceOnly(getAllGuildsSubscriptionOptions())
 */
export async function fetchSubscriptionDataOnceOnly<T>(options: SubscriptionOptions<T>): Promise<T> {
  // Subscribe to the subscription.
  const subscription = addSubscriberToSubscription(options)

  // Check for permissions.
  if (!subscription.hasPermission.value) {
    throw new Error(
      `Trying to fetch subscription data when we don't have the required permission '${options.permission}'. Subscription Key was '${subscription.key}'`
    )
  }

  // Wait until data fetched.
  while (subscription.fetchesCompleted === 0) {
    if (subscription.error.value) {
      throw subscription.error.value
    }
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  const data = subscription.data.value as T // TBD is this 'reference correct/safe'?

  // Unsubscribe.
  removeSubscriberFromSubscription(subscription.key)

  // Return the fetched data.
  return data
}

/**
 * Internal function to add a subscriber to a subscription. If the subscription does not exist, it will be created.
 */
function addSubscriberToSubscription(options: SubscriptionOptions): Subscription {
  // Get an existing subscription or create a new one.
  const subscription = subscriptions[options.fetcherPolicy.getUniqueKey()] ?? createSubscription(options)

  // Cancel any potentially ongoing cache retention policies.
  subscription.cacheRetentionPolicy.cancel()

  // Increment the subscriber count.
  subscription.subscriberCount++

  // Check for any required permission.
  subscription.hasPermission.value = !options.permission || hasPermission(options.permission)

  // If we are the first subscriber and we have permission..
  if (subscription.hasPermission.value && subscription.subscriberCount === 1) {
    // ...then start the fetcher policy immediately.
    // Note: Fetch will automatically trigger the polling policy to start after it finishes.
    subscription.fetcherPolicy.fetch()
  }

  // Store the subscription in the global list of subscriptions if not already there.
  if (!subscriptions[subscription.key]) {
    subscriptions[subscription.key] = subscription
  }

  return subscription
}

/**
 * Manually refresh the data for this subscription. Also clears any errors that may have been set.
 * @param subscriptionKey The key of the subscription to refresh.
 */
function refreshSubscription(subscriptionKey: string): void {
  const subscription = subscriptions[subscriptionKey]
  if (subscription) {
    if (subscription.hasPermission.value) {
      log('Manual refresh for', subscriptionKey)

      // Clear any errors when we forcibly refresh. Use case: fetching from a subscription that threw an error, but that
      // error has now cleared and the client code wants to start fetching the data again.
      subscription.error.value = undefined

      // Cancel any polling and fetch the data now. When the fetch completes, the polling cycle will be restarted.
      subscription.pollingPolicy.cancel()
      subscription.fetcherPolicy.fetch()
    }
  } else {
    log('Tried to refresh a subscription that does not exist:', subscriptionKey)
  }
}

/**
 * Create a new subscription from subscription options.
 */
function createSubscription<T>(options: SubscriptionOptions<T>): Subscription<T> {
  log('Creating a new subscription', options.fetcherPolicy.getUniqueKey())

  // Initialize an empty subscription.
  const subscription: Subscription<T> = {
    key: options.fetcherPolicy.getUniqueKey(),
    data: shallowRef<T>(),
    error: shallowRef(),
    fetchesCompleted: 0,
    hasPermission: ref(true),
    pollingPolicy: options.pollingPolicy,
    fetcherPolicy: options.fetcherPolicy,
    dataMutator: options.dataMutator,
    cacheRetentionPolicy: options.cacheRetentionPolicy,
    subscriberCount: 0,
  }

  // Create the data poller.
  subscription.pollingPolicy.register(() => {
    // When polling policy tells us that it wants to fetch the data...

    // If the page is not visible, then queue the fetch for when the page becomes visible again.
    if (isPaused) {
      log('Page is not visible, so delaying fetch for', subscription.key)
      delayedSubscriptions.push(subscription)
    } else {
      // Fetch the data.
      log('Fetching', subscription.key)
      subscription.fetcherPolicy.fetch()
    }
  })

  // Create the data fetcher.
  subscription.fetcherPolicy.register(
    // When the fetcher receives data...
    (data: T) => {
      // Optionally massage and/or fixup the data.
      if (subscription.dataMutator) {
        try {
          data = subscription.dataMutator(data)
        } catch (err) {
          console.error(
            `Data mutation for subscription '${subscription.key}' threw an error: ${(err as Error).message}`
          )
        }
      }

      // Compare to data that's already in the store. If it's the same, don't bother updating the store.
      let dataChanged: boolean
      if (!isEqual(data, subscription.data.value)) {
        // Store the data.
        if (data) {
          // Note that we make the data read only to prevent accidental mutation.
          subscription.data.value = readonly(data) as T
        } else {
          // Null and undefined responses cannot be made readonly.
          subscription.data.value = data
        }
        dataChanged = true
      } else {
        dataChanged = false
      }

      // Data was fetched.
      subscription.fetchesCompleted++

      // Start the polling policy.
      subscription.pollingPolicy.start()

      if (dataChanged) {
        log('Fetch completed for', subscription.key)
      } else {
        log('Fetch completed for', subscription.key, 'but data is the same as what we already have')
      }
    },
    (error, isFatal) => {
      // When the fetcher receives an error, store it. Note that we do not kick off another request to the polling
      // policy if the error `isFatal`. This effectively means that a fatal error stops data from being polled.
      if (isFatal) {
        log('Fetch fatally failed for', subscription.key, error)
        subscription.error.value = readonly(error)
      } else {
        log('Fetch non-fatally failed', subscription.key, error)
        subscription.pollingPolicy.onFetchFailed()
      }
    }
  )

  // Create the data cache retention policy.
  subscription.cacheRetentionPolicy.register(() => {
    // When the invalidation policy tells us to expire the subscription, then do so.
    log('Deleting subscription cache', subscription.key)
    // Note: Using dynamic delete here because we know that the subscription exists and trust it to not collide with any other properties.
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
    delete subscriptions[subscription.key]
  })

  return subscription
}

/**
 * This function should be called when a subscriber is no longer interested in a subscription. If this was the last
 * subscriber then the subscription's data fetching will cancelled and its invalidation policy will be triggered.
 * @param subscriptionKey The key of the subscription to remove a subscriber from.
 */
function removeSubscriberFromSubscription(subscriptionKey: string): void {
  const subscription = subscriptions[subscriptionKey]
  if (subscription) {
    log('Removing a subscriber', subscriptionKey)

    // Reduce subscriber count.
    subscription.subscriberCount--
    if (subscription.subscriberCount < 0) {
      throw new Error(`Subscriber count for '${subscriptionKey}' went negative!`)
    }

    // If there are no more subscribers...
    if (subscription.subscriberCount === 0) {
      // Stop polling and fetching.
      log('Cancelling fetching and polling after subscribers dropped to 0 for', subscriptionKey)
      subscription.pollingPolicy.cancel()
      subscription.fetcherPolicy.cancel()

      // Remove the fetcher from the list of paused fetchers in case it was there.
      const index = delayedSubscriptions.indexOf(subscription)
      if (index !== -1) {
        delayedSubscriptions.splice(index, 1)
      }

      // Start the cache retention policy.
      log('Starting cache retention policy', subscriptionKey)
      subscription.cacheRetentionPolicy.start()
    }
  } else {
    log('Tried to remove a subscriber from a subscription that does not exist:', subscriptionKey)
  }
}
