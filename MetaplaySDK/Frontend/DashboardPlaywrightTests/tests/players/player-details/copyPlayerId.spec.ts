// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Copy player ID', () => {
  test.beforeEach('Grant clipboard permissions', async ({ context }) => {
    // Grant clipboard permissions to browser context.
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  })

  test('Copies a player ID to clipboard', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)

    // Copy to clipboard.
    await page.getByTestId('copy-to-clipboard').click()

    // Check that the clipboard contains the player ID.
    const copiedClipboardText = await page.evaluate(async () => {
      return await navigator.clipboard.readText()
    })
    expect(copiedClipboardText).toBe(freshTestPlayer)
  })
})
