// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { useCoreStore } from '../../coreStore'
import type { IGeneratedUiFieldInfo } from './generatedUiTypes'

/**
 * This camelCase is a JavaScript version of Newtonsoft.Json.Utilities.StringUtils.ToCamelCase method, different from
 * lodash.camelCase which behaves differently in some cases.
 *
 * This is a largely mechanical translation based on public code at:
 * https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Utilities/StringUtils.cs#L182
 *
 * @param str The string to transform.
 * @returns The camelCased result string.
 */
export function newtonsoftCamelCase(str: string | undefined): string {
  const isUpper = (c: string): boolean => c.toUpperCase() === c && c.toLowerCase() !== c

  if (str === undefined || str.length === 0) {
    return ''
  }
  if (!isUpper(str.charAt(0))) {
    return str
  }

  const chars = Array.from(str)

  for (let i = 0; i < chars.length; ++i) {
    if (i === 1 && !isUpper(chars[i])) {
      break
    }

    const hasNext = i + 1 < chars.length
    if (i > 0 && hasNext && !isUpper(chars[i + 1])) {
      if (chars[i + 1] === ' ') {
        chars[i] = chars[i].toLowerCase()
      }
      break
    }

    chars[i] = chars[i].toLowerCase()
  }

  return chars.join('')
}

/**
 * Finds all languages inside an object from localized fields.
 * @param obj Object to find languages from.
 */
export function findLanguages(obj: any, gameData: any): string[] {
  if (typeof obj !== 'object' || obj === null || !gameData) {
    return []
  }
  if (obj.localizations) {
    return [
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      ...Object.keys(obj.localizations).filter((x) => x in gameData.gameConfig.Languages),
    ]
  }
  const returnSet = new Set<string>()
  for (const key in obj) {
    for (const lang of findLanguages(obj[key], gameData)) {
      returnSet.add(lang)
    }
  }
  return [...returnSet]
}

/**
 * Join fieldPath with fieldName from fieldInfo
 */
export function concatFieldPath(path: string, fieldInfo: IGeneratedUiFieldInfo): string {
  return (path.length === 0 ? fieldInfo.fieldName : path + '/' + fieldInfo.fieldName) ?? ''
}

/**
 * Whether the logicVersion is within the versionRange.
 * If addedInVersion is undefined, we use 0 instead
 * If removedInVersion is undefined, we use Number.MAX_SAFE_INTEGER instead
 * If logicVersion is undefined, we use the server's max supported logic version instead
 */
export function isVersionInRange(
  versionRange: { addedInVersion: number | undefined; removedInVersion?: number | undefined },
  logicVersion: number | undefined
): boolean {
  const coreStore = useCoreStore()

  logicVersion ??= Math.max(...coreStore.supportedLogicVersionOptions)

  if (logicVersion === undefined || logicVersion == null) {
    return true
  }

  const minVersion = versionRange.addedInVersion ?? 0
  const maxVersion = versionRange.removedInVersion ?? Number.MAX_SAFE_INTEGER

  return logicVersion >= minVersion && logicVersion < maxVersion
}

/**
 * Whether the versionRange only has a partial overlap with the server's supported logic version range, this means that not all players support specific features.
 */
export function hasPotentialLogicVersionMismatch(versionRange: {
  addedInVersion: number | undefined
  removedInVersion?: number | undefined
}): boolean {
  const coreStore = useCoreStore()

  if (versionRange.addedInVersion === undefined && versionRange.removedInVersion === undefined) return false

  if (versionRange.addedInVersion !== undefined) {
    const minSupportedLogicVersion = Math.min(...coreStore.supportedLogicVersionOptions)
    if (minSupportedLogicVersion >= versionRange.addedInVersion) return false
  }
  if (versionRange.removedInVersion !== undefined) {
    const maxSupportedLogicVersion = Math.max(...coreStore.supportedLogicVersionOptions)
    if (maxSupportedLogicVersion < versionRange.removedInVersion) return false
  }

  return true
}
