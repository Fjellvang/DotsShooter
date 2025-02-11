// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

// Commands -----------------------------------------------------------------------------------------------------------

export interface ToServerCommand {
  commandType: string
}

// Command: Create New Item -------------------------------------------------------------------------------------------

export interface ToServerCommandCreateNewItem extends ToServerCommand {
  commandType: 'createNewItem'
  itemType: string
  itemConfig: unknown
  parentId: string
  parentVersion: number
}

// Command: Change Metadata -------------------------------------------------------------------------------------------

export interface ToServerCommandChangeMetaData extends ToServerCommand {
  commandType: 'changeMetadata'
  items: Array<{
    targetId: string
    currentVersion: number
    changes: Array<{
      property: string
      newValue: unknown
    }>
  }>
}

// Command: Move Items ------------------------------------------------------------------------------------------------

export interface ToServerCommandMoveItems extends ToServerCommand {
  commandType: 'moveItems'
  items: Array<{
    targetId: string
    currentVersion: number
    // parentId: string // is this needed?
    parentVersion: number
  }>
  newParent: {
    targetId: string
    currentVersion: number
    insertIndex: number
  }
}

// Command: Delete Items ----------------------------------------------------------------------------------------------

export interface ToServerCommandDeleteItems extends ToServerCommand {
  commandType: 'deleteItems'
  items: Array<{
    targetId: string
    currentVersion: number
    parentId: string // is this needed?
    parentVersion: number
  }>
}
