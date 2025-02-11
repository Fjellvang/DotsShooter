// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { useCoreStore } from '../coreStore'
import { uiPlacementList } from './uiPlacementList'

// Generate a union type for the placements.
export type UiPlacement = (typeof uiPlacementList)[number]

/**
 * Information about a component that can be injected into the dashboard.
 */
export interface UiPlacementInfo {
  /**
   * A unique ID for the component. This is used to identify the component when adding or removing it.
   */
  uniqueId: string
  /**
   * The Vue component to render.
   */
  vueComponent: any
  /**
   * Optional: The width of the component. Defaults to 'half'.
   */
  width?: 'half' | 'full'
  /**
   * Optional: The props to pass to the component.
   */
  props?: Record<string, any>
  /**
   * Optional: Set permission requirement for viewing this component. The component will hidden if the user does not have the required permission.
   * @example 'api.players.set_wallet'
   */
  displayPermission?: string
}

/**
 * A rule for how to position a UI component in relation to its peers in a particular dashboard placement.
 */
export interface LayoutRule {
  position: 'before' | 'after'
  targetId?: string
}

/**
 * Add a new UI component into a pre-defined placement within dashboard.
 * @param placement The placement to add the UI component to.
 * @param payload The component to add.
 * @param layoutRule Optional: how to position the new component in relation to other possible components in the same placement.
 * @example
 * setGameSpecificInitialization(async (initializationApi) => {
    initializationApi.addUiComponent(
      'Players/Details/Tab0',
      {
        uniqueId: 'ProducerListCard',
        vueComponent: async () => await import('./ProducersCard.vue'),
        // Set the 'displayPermission' property to hide this component,
        // if a user does not have the required permission.
        // displayPermission: 'api.players.set_wallet'
      })
  })
 */
export function addUiComponent(placement: UiPlacement, payload: UiPlacementInfo, layoutRule?: LayoutRule): void {
  const coreStore = useCoreStore()
  layoutRule = layoutRule ?? { position: 'after' }

  // Check the the placements exists
  if (!uiPlacementList.includes(placement)) {
    throw new Error(`Attempting to add a UI component to placement '${placement}' which does not exist`)
  }

  // Check that the placement doesn't have a component with the same uniqueId
  if (coreStore.uiComponents[placement]?.find((x) => x.uniqueId === payload.uniqueId)) {
    throw new Error(
      `Attempting to add a UI component with duplicate uniqueId '${payload.uniqueId}' to placement '${placement}'`
    )
  }

  // Add the placement to the store if needed.
  if (!coreStore.uiComponents[placement]) {
    coreStore.uiComponents[placement] = []
  }

  // Make a local reference to the placement so we can guarantee for TypeScript that it exists.
  const uiPlacement = coreStore.uiComponents[placement]

  if (!uiPlacement) {
    throw new Error(`Could not find placement '${placement}' in the store. This should never happen.`)
  }

  if (layoutRule.position === 'after') {
    if (layoutRule.targetId) {
      // Look for the id -> error out if not found
      const target = layoutRule.targetId || ''
      const targetIndex = uiPlacement.findIndex((x) => x.uniqueId === target)
      if (targetIndex === -1) {
        throw new Error(`Could not find target id '${target}' for placement '${placement}'.`)
      }
      // Insert to after the position of the found element
      uiPlacement.splice(targetIndex + 1, 0, payload)
    } else {
      uiPlacement.push(payload)
    }
  } else if (layoutRule.position === 'before') {
    if (layoutRule.targetId) {
      // Look for the id -> error out if not found
      const target = layoutRule.targetId || ''
      const targetIndex = uiPlacement.findIndex((x) => x.uniqueId === target)
      if (targetIndex === -1) {
        throw new Error(`Could not find target id '${target}' for placement '${placement}'.`)
      }
      // Insert to before the position of the found element
      uiPlacement.splice(targetIndex, 0, payload)
    } else {
      uiPlacement.unshift(payload)
    }
  }
}

/**
 * Remove a previously registered UI component from a pre-defined placement within the dashboard.
 * @param placement The placement to remove the UI component from.
 * @param targetId The ID of the component to remove.
 * @example
 * setGameSpecificInitialization(async (initializationApi) => {
    initializationApi.removeUiComponent('Players/Details/Tab0', 'ProducerListCard')
  })
 */
export function removeUiComponent(placement: UiPlacement, targetId: string): void {
  const coreStore = useCoreStore()
  // Check that the element exists and error out if it doesn't
  const targetIndex = coreStore.uiComponents[placement]?.findIndex((x) => x.uniqueId === targetId)
  if (targetIndex === undefined || targetIndex === -1) {
    throw new Error(
      `Could not remove a UI component with ID '${targetId}' from placement '${placement}' because the component was not found.`
    )
  }
  // Remove the element
  coreStore.uiComponents[placement]?.splice(targetIndex, 1)
}

/**
 * Remove all previously registered UI components from a pre-defined placement within the dashboard.
 * @param placement The placement to remove the UI components from.
 * @example
 * setGameSpecificInitialization(async (initializationApi) => {
    initializationApi.removeAllUiComponents('Players/Details/Tab4')
  })
 */
export function removeAllUiComponents(placement: UiPlacement): void {
  const coreStore = useCoreStore()
  coreStore.uiComponents[placement] = []
}
