// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { shallowRef } from 'vue'

/**
 * A reactive reference to the permissions the user is aware of and wether they have it.
 */
const allPermissions = shallowRef<string[]>()

/**
 * A callback that is called when `doesHavePermission` fails.
 */
let missingPermissionCallback: ((permission: string) => void) | undefined

/**
 * Set the permissions for this user to be used by the `doesHavePermission` function.
 * @param permissions A dictionary of permissions the user should be aware of and if they currently have it.
 */
export function setPermissions(permissions: string[]): void {
  allPermissions.value = permissions
}

/**
 * Register an optional callback to be called when `doesHavePermission` fails. Note that this could happen because
 * the user does not have the permission or because the permissions does not exist.
 * @param callback Function to call.
 */
export function setMissingPermissionCallback(callback: (permission: string) => void): void {
  missingPermissionCallback = callback
}

/**
 * Checks if the user has a given permission.
 * @param permission The permission to check.
 */
export function doesHavePermission(permission: string | undefined): boolean {
  if (!permission) return true
  else if (allPermissions.value === undefined) {
    throw new Error(
      `Trying to check permission ${permission} before permissions have been set. Call the setPermissions function before using doesHavePermission.`
    )
  } else if (allPermissions.value.includes(permission)) {
    return true
  } else if (missingPermissionCallback) {
    missingPermissionCallback(permission)
  }
  return false
}

/**
 * A composable to manage permissions.
 */
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
export function usePermissions() {
  return {
    allPermissions,
    setPermissions,
    setMissingPermissionCallback,
    doesHavePermission,
  }
}
