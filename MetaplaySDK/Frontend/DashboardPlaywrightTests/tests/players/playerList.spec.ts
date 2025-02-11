// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, type Page, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Player List', () => {
  test.beforeEach('Navigate to players', async ({ page }) => {
    // Navigate to players page.
    await page.goto('/players')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Types `Guest` into the search box', async ({ page }) => {
    // Get the `MetaInputPlayerSelect`.
    const inputPlayerSelectWrapper = page.getByTestId('input-player-select')

    // Find the input element within the wrapper.
    const inputPlayerSelect = inputPlayerSelectWrapper.locator('input.multiselect-search')

    // Fill the input element with `Guest`.
    await inputPlayerSelect.fill('Guest')

    // Check that the input element has the value `Guest`.
    await expect(inputPlayerSelect).toHaveValue('Guest')
  })

  test('Searches for test player by id', async ({ page, freshTestPlayer }) => {
    // Search without any case changes.
    await searchForPlayerById(page, freshTestPlayer)
  })

  test('Searches for test player by id prefix', async ({ page, freshTestPlayer }) => {
    // Search with the last two characters removed.
    await searchForPlayerById(page, freshTestPlayer.slice(0, -2))
  })

  test('Searches for test player by uppercase id', async ({ page, freshTestPlayer }) => {
    const freshTestPlayerParts: string[] = freshTestPlayer.split(':')

    // Search with a concatenated string with latter part in uppercase.
    await searchForPlayerById(page, `${freshTestPlayerParts[0]}:${freshTestPlayerParts[1].toUpperCase()}`)
  })

  test('Searches for test player by lowercase id', async ({ page, freshTestPlayer }) => {
    const freshTestPlayerParts: string[] = freshTestPlayer.split(':')

    // Search with a concatenated string with latter part in lowercase.
    await searchForPlayerById(page, `${freshTestPlayerParts[0]}:${freshTestPlayerParts[1].toLowerCase()}`)
  })
})

// Helper Functions ---------------------------------------------------------------------------------------------------

/**
 * Searches for a player by id.
 * @param page - The Playwright page object.
 * @param playerId - The id of the player to search for.
 */
async function searchForPlayerById(page: Page, playerId: string): Promise<void> {
  // Get the `MetaInputPlayerSelect` component.
  const playerMultiSelectInputWrapper = page.getByTestId('input-player-select')

  // Find the input element within the wrapper.
  const playerMultiSelectInput = playerMultiSelectInputWrapper.locator('input.multiselect-search')

  // Fill the input element with the player's id.
  await playerMultiSelectInput.fill(playerId)

  // Get the multiselect options of the `MetaInputPlayerSelect`.
  const playerMultiSelectOptions = playerMultiSelectInputWrapper.locator('.multiselect-options')

  // Check that the multi-select options contain the player's id.
  await expect(playerMultiSelectOptions).toContainText(playerId, { useInnerText: true, ignoreCase: true })
}
