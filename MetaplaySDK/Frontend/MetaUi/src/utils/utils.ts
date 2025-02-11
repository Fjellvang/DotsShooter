// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import countryEmoji from 'country-emoji'

import { camelCaseToTitleCase } from '@metaplay/meta-utilities'

/**
 * Transforms an objects key-value pairs into printable fields.
 * @param object Object to transform
 * @returns Array of string values
 * @example
 * getObjectPrintableFields({serverHost : 'localhost'}) => [{key: "serverHost", name: "Server Host", value: "localhost"}]
 */
export function getObjectPrintableFields(
  object: Record<string, unknown>
): Array<{ key: string; name: string; value: unknown }> {
  const fields = []
  for (const key in object) {
    if (key === '$type') {
      continue
    }
    const name = camelCaseToTitleCase(String(key))
    const value = object[key]
    fields.push({ key, name, value })
  }
  return fields
}

/**
 * Returns the display name of a given country based on it's ISO country code.
 * @param isoCode The source ISO country code.
 * @returns Display name of the country.
 */
export function isoCodeToCountryName(isoCode: string): string {
  return countryEmoji.name(isoCode) ?? isoCode
}

/**
 * Returns the flag emoji of a given country based on it's ISO country code.
 * @param isoCode The source ISO country code.
 * @returns Flag emoji of the country.
 */
export function isoCodeToCountryFlag(isoCode: string): string {
  return countryEmoji.flag(isoCode) ?? ''
}

/**
 * Humanize a dashboard username. For example: no_id -> No User ID.
 * @param username The username to humanize.
 * @returns Humanized result.
 */
export function humanizeUsername(username: string): string {
  if (username === 'auth_not_enabled') return 'Anonymous'
  else if (username === 'no_id') return 'No User ID'
  else return username
}

/**
 * Internal utility function for gathering device history entries that have a specific login method
 * associated to them.
 * @param authMethodId The authentication key of a login method.
 * @param devices Device history from player model.
 * @returns Array of (deviceId, deviceModel) pairs that match the query.
 */
function findDevicesForAuthMethod(
  authMethodId: string,
  devices: any
): Array<{
  id: string
  deviceModel: string
}> {
  return (
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Object.entries(devices ?? {})
      // eslint-disable-next-line @typescript-eslint/no-unused-vars
      .filter(([k, v]: [string, any]) => v.loginMethods.some((login: any) => login?.id === authMethodId))
      .map(([k, v]: [string, any]) => {
        return { id: k, deviceModel: v.deviceModel as string }
      })
  )
}

/**
 * Get display string for a ClientToken login entry based on the devices that is has been used with.
 * @param devices List of devices associated to the login.
 * @returns Display string.
 */
function getClientTokenDisplayString(devices: any[]): string {
  if (devices.length === 0) return 'ClientToken'
  else if (devices.length === 1) return devices[0].deviceModel
  else return 'ClientToken (multiple devices)'
}

/**
 * Transforms a list of raw auth records into more ergonomic objects for easier UI work.
 * @param auths An array of authentication objects.
 * @returns An array of objects in our dashboard preferred format.
 */
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type -- TODO: Make this return type explicit
export function parseAuthMethods(auths: Array<{ name: string; id: string; attachedAt: string }>, devices: unknown) {
  const res = []
  const authStrings = Object.keys(auths)
  for (const a of authStrings) {
    const properties = a.split('/')
    const associatedDevices = findDevicesForAuthMethod(properties[1], devices)
    const isClientToken = properties[0] === 'DeviceId'
    res.push(
      Object.assign(
        {
          name: properties[0],
          id: properties[1],
          type: isClientToken ? 'device' : 'social',
          displayString: isClientToken ? getClientTokenDisplayString(associatedDevices) : properties[0],
          devices: associatedDevices,
        },
        auths[a as any]
      )
    )
  }
  return res
}

/**
 * Returns a language name when given an ISO language code
 * @param languageId ISO language code
 * @param gameData Reference to gameData
 * @returns Language name or languageId if language not found in the gameData or gameData isn't loaded
 */
export function getLanguageName(languageId: string, gameData: any): string {
  const languageInfo = gameData?.gameConfig.Languages[languageId]
  return languageInfo?.displayName || languageId
}

/**
 * Returns an approximate size of an arbitrary JS object. Not super scientific, but good enough for a ballpark estimate.
 * @param object The object to evaluate.
 * @returns An integer with the estimated size in bytes.
 */
export function roughSizeOfObject(object: any): number {
  const objectList: any[] = []
  const stack = [object]
  let bytes = 0

  while (stack.length) {
    const value: any = stack.pop()

    if (typeof value === 'boolean') {
      bytes += 4
    } else if (typeof value === 'string') {
      bytes += value.length * 2
    } else if (typeof value === 'number') {
      bytes += 8
    } else if (typeof value === 'object' && !objectList.includes(value)) {
      objectList.push(value)

      for (const i in value) {
        stack.push(value[i])
      }
    }
  }
  return bytes
}

export function roundToDigits(value: number, numDigits: number): string {
  return value.toFixed(numDigits || 0)
}

/** Returns brief explanation of an experiment phase.
 * @param phaseName The id of the experiment phase.
 * @returns Human readable explanation of the phase.
 */
export function experimentPhaseDetails(phaseName: string): string {
  const experimentPhaseDetails: Record<string, string> = {
    Testing: 'The experiment is not yet active, it is only active to testers.',
    Active: 'The experiment is active for all players.',
    Paused: 'The experiment is temporarily suspended; it is only active for testers.',
    Concluded: 'The experiment is no longer active for all players.',
  }

  return experimentPhaseDetails[phaseName]
}

/**
 * Resolve using function or named path in object, eg:
 * resolve({item:{body:123}}), (elem) => elem.item.body) returns 123
 * resolve({item:{body:123}}), 'item.body') returns 123
 * @param obj Object to resolve from.
 * @param path The function or named path to resolve.
 * @returns The resolved function or path from the original object.
 */
export function resolve(obj: any, path: string | ((arg: any) => string)): any {
  if (typeof path === 'function') {
    return path(obj)
  } else {
    return path.split('.').reduce((prev, curr) => {
      return prev ? prev[curr] : undefined
    }, obj || self)
  }
}
