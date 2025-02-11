// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Generate guaranteed locally unique keys.
 * @param baseKey Optional: The key to transform.
 * @returns A unique string.
 * @example makeIntoUniqueKey() -> '0022'
 * @example makeIntoUniqueKey('key') -> 'key0023'
 */
export function makeIntoUniqueKey(baseKey?: string): string {
  const uniquePart = (uniqueKeyIndex++).toString(36).padStart(4, '0')
  if (baseKey) {
    return `${baseKey}_${uniquePart}`
  } else {
    return uniquePart
  }
}

// The next index for the `makeIntoUniqueKey`.
let uniqueKeyIndex = 0

/**
 * Generate a hash for the given value.
 * @param value The value to be hashed.
 * @returns A hash of the value as a string.
 * @example makeHash(1234) -> Hash of the value.
 * @example makeHash({abc: 'def'}) -> Hash of the value.
 */
export function makeHash(value: unknown): string {
  // Handle generic values.
  let valueString: string
  if (typeof value === 'object') {
    valueString = JSON.stringify(value)
  } else {
    valueString = String(value)
  }

  // Generate the hash based on the string value.
  let hash = 0
  for (let i = 0; i < valueString.length; i++) {
    hash = (hash << 5) - hash + valueString.charCodeAt(i)
  }

  // Return the hash as a string.
  return (hash & 0x7fffffff).toString(36)
}
