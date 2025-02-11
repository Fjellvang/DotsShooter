// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Abbreviates an arbitrary number into a short display string. For example 123321 -> 123k.
 * @param x The number to abbreviate.
 * @param precision The amount of numbers to display after abbreviation.
 * @returns A string with the abbreviated result.
 * @example abbreviateNumber(123321) -> '123k'
 */
export function abbreviateNumber(x: number, precision = 3): string | undefined {
  if (x >= 1000000000.0) {
    return (x / 1000000000.0).toPrecision(precision) + 'B'
  } else if (x >= 1000000.0) {
    return (x / 1000000.0).toPrecision(precision) + 'M'
  } else if (x >= 1000.0) {
    return (x / 1000.0).toPrecision(precision) + 'k'
  } else {
    if (x || x === 0) return x.toString()
    else return undefined
  }
}
