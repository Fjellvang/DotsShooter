// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Convert a camelCase string to sentence case. Handles both lower and upper camel case.
 * @param input String to convert.
 * @returns Result of conversion.
 * @throws Error if input contains spaces.
 * @example camelCaseToSentenceCase('camelCaseString') -> 'Camel case string'
 */
export function camelCaseToSentenceCase(input: string): string {
  return stringToSentenceCase(camelCaseToString(input))
}

/**
 * Convert a camelCase string to title case. Handles both lower and upper camel case.
 * @param input String to convert.
 * @returns Result of conversion.
 * @throws Error if input contains spaces.
 * @example camelCaseToTitleCase('camelCaseString') -> 'Camel Case String'
 */
export function camelCaseToTitleCase(input: string): string {
  return stringToTitleCase(camelCaseToString(input))
}

/**
 * Convert a string to sentence case.
 * @param input String to convert.
 * @returns Result of conversion.
 * @example stringToSentenceCase('this is a sentence') -> 'This is a sentence'
 */
export function stringToSentenceCase(input: string): string {
  const result = input.charAt(0).toUpperCase() + input.slice(1).toLowerCase()
  return upperCaseAcronyms(result)
}

/**
 * Convert a string to title case.
 * @param input String to convert.
 * @returns Result of conversion.
 * @example stringToTitleCase('this is a sentence') -> 'This is a Sentence'
 */
export function stringToTitleCase(input: string): string {
  // Upper-case the first letter of each word.
  let result = input.replace(/([^\W_]+[^\s-]*) */g, (txt) => {
    return txt.charAt(0).toUpperCase() + txt.substring(1).toLowerCase()
  })

  // Certain minor words should be left lowercase unless they are the first or last words in the string.
  const lowers = [
    'a',
    'an',
    'the',
    'and',
    'but',
    'or',
    'for',
    'nor',
    'as',
    'at',
    'by',
    'for',
    'from',
    'in',
    'into',
    'near',
    'of',
    'on',
    'onto',
    'to',
    'with',
  ]
  lowers.forEach((lower) => {
    result = result.replace(new RegExp(`\\s${lower}\\s`, 'gi'), ` ${lower} `)
  })

  // Uppercase specific acronyms.
  result = upperCaseAcronyms(result)

  return result
}

/**
 * Convert a camelCase string to a lower-cased string with spaces between words. Handles both lower and upper camel case.
 * @param input String to convert.
 * @returns Result of conversion.
 * @throws Error if input contains spaces.
 * @example camelCaseToString('camelCaseString') -> 'camel case string'
 */
function camelCaseToString(input: string): string {
  if (input.includes(' ')) {
    throw new Error('Input string cannot contain spaces')
  }
  return input
    .split(/(?=[A-Z])/)
    .join(' ')
    .toLowerCase()
}

/**
 * Convert all known acronyms to uppercase.
 * @param input String to convert.
 * @returns Result of conversion.
 * @example upperCaseAcronyms('this is a string with id and http') -> 'this is a string with ID and HTTP'
 */
function upperCaseAcronyms(input: string): string {
  const uppers = ['ID', 'TV', 'HTTP', 'MMR']
  uppers.forEach((upper) => {
    input = input.replace(new RegExp(`\\b${upper}\\b`, 'gi'), upper)
  })
  return input
}

/**
 * Generates a humanized string that pluralizes a word if needed.
 * @param count The number of units.
 * @param unit The unit to be pluralized if needed.
 * @param showCount Optional: Whether or not the count should be included in the beginning of the string. Defaults to true
 * @returns The pluralized string.
 * @example maybePluralString(3, 'test') -> '1 tests'
 */
export function maybePluralString(count: number, unit: string, showCount = true): string {
  return `${showCount ? count + ' ' : ''}${unit}${count !== 1 ? 's' : ''}`
}

/**
 * Generates prefix text for potentially pluralized text. For example: 'there was' or 'there were'.
 * @param count The number of units.
 * @param singularText Text to use when count is 1.
 * @param pluralText Text to use when count is not 1.
 * @returns The generated string.
 * @example maybePluralPrefixString(3, 'was', 'were') -> 'were'
 */
export function maybePluralPrefixString(count: number, singularText: string, pluralText: string): string {
  return count === 1 ? singularText : pluralText
}

/**
 * Returns a string with an ordinal appended to the given number.
 * Note that numbers less than 1 will return the number without an ordinal.
 * @param number The number to append an ordinal to.
 * @returns A string with the ordinal suffix appended to the number
 * @example toOrdinalString(1) -> '1st'
 */
export function toOrdinalString(number: number): string {
  if (number >= 1) {
    const suffixes = ['th', 'st', 'nd', 'rd']
    const lastTwoDigits = number % 100
    return `${number}${suffixes[(lastTwoDigits - 20) % 10] || suffixes[lastTwoDigits] || suffixes[0]}`
  } else {
    return number.toString()
  }
}

/**
 * Convert a sentence case string to a lower-cased string with hyphens between words.
 * @param input The string to convert.
 * @returns The converted string.
 * @example sentenceCaseToKebabCase('This is a test') -> 'this-is-a-test'
 */
export function sentenceCaseToKebabCase(input: string): string {
  const result = input
    // Replace spaces with hyphens.
    .replace(/\s+/g, '-')
    // Convert to lower case.
    .toLowerCase()

  return result
}

/**
 * Convert a camel case string to a lower-cased string with hyphens between words. Handles both lower and upper
 * camel case.
 * @param input The string to convert.
 * @returns The converted string.
 * @throws Error if input contains spaces.
 * @example camelCaseToKebabCase('someComponentName') => 'some-component-name'
 */
export function camelCaseToKebabCase(input: string): string {
  return sentenceCaseToKebabCase(camelCaseToSentenceCase(input))
}
