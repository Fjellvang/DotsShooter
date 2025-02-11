export type Color =
  | 'Crimson'
  | 'Coral'
  | 'Sunset'
  | 'Goldenrod'
  | 'Metaplay'
  | 'Sky'
  | 'Royal'
  | 'Lavender'
  | 'Violet'
  | 'Slate'

/**
 * Color palette for events, groups, etc..
 */
export const ColorPickerPalette: Record<Color, string> = {
  Crimson: '#c4001d',
  Coral: '#d97775',
  Sunset: '#e34a2f',
  Goldenrod: '#ebbf34',
  Metaplay: '#3f6730',
  Sky: '#4b99e3',
  Royal: '#4b4cb3',
  Lavender: '#7d83c9',
  Violet: '#8702a8',
  Slate: '#616161',
}

/**
 * Default colors (per item type) for items that don't have a color explicitly set in their data.
 */
export const defaultItemColors: Record<string, string> = {
  liveopsEventItem: ColorPickerPalette.Metaplay,
  instantItem: ColorPickerPalette.Crimson,
}

/**
 * Given a color as a hex code, find the closest matching color from the `ColorPickerPalette`.
 * If no color is given, returns the Metaplay green as a sensible default.
 * @param hexCode Color to find the closest matched color for.
 * @returns Closest matched color from the palette, or `undefined` if no color given.
 */
export function findClosestColorFromPicketPalette(hexCode?: string): string {
  if (!hexCode) return ColorPickerPalette.Metaplay

  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const [sourceR, sourceG, sourceB] = hexCode.match(/\w\w/g)!.map((x) => parseInt(x, 16))

  let closestDistance = 0
  let closestColor: string | undefined
  for (const hexCode of Object.values(ColorPickerPalette)) {
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    const [targetR, targetG, targetB] = hexCode.match(/\w\w/g)!.map((x) => parseInt(x, 16))
    const distance = Math.abs(sourceR - targetR) + Math.abs(sourceG - targetG) + Math.abs(sourceB - targetB)
    if (distance < closestDistance || closestColor === undefined) {
      if (distance === 0) return hexCode
      closestDistance = distance
      closestColor = hexCode
    }
  }
  return closestColor ?? ColorPickerPalette.Metaplay
}
