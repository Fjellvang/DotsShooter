// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Guild List', () => {
  test.beforeAll('Skip if guilds feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.guilds, 'Guilds feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to guilds list', async ({ page }) => {
    // Navigate to guilds list page.
    await page.goto('/guilds')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Types `Guest` into the search box', async ({ page }) => {
    // Get the `MetaInputGuildSelect`.
    const inputGuildSelectWrapper = page.getByTestId('input-guild-select')

    // Find the input element within the wrapper.
    const inputGuildSelect = inputGuildSelectWrapper.locator('input.multiselect-search')

    // Fill the input element with `Guest`.
    await inputGuildSelect.fill('Guest')

    // Check that the input element has the value `Guest`.
    await expect(inputGuildSelect).toHaveValue('Guest')
  })
})
