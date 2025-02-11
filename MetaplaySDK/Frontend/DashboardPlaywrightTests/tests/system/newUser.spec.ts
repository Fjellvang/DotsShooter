// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Shows New User page if no roles assigned', () => {
  test.beforeEach('Skip test if assume roles not enabled', async ({ authDetails }) => {
    test.skip(!authDetails.canAssumeRoles, 'Assume roles not enabled in this deployment')
  })

  test.beforeEach('Set assumed roles to empty', async ({ context }) => {
    // Set assumed roles to empty.
    await context.setExtraHTTPHeaders({ 'Metaplay-Assumeduserroles': '' })
  })

  test('New user and log out card rendered in new user page', async ({ page }) => {
    // Navigate to any Dashboard page and we should be redirected to the New User page.
    await page.goto('/')

    // Check that the New User card is visible.
    await expect(page.getByTestId('new-user-card')).toBeVisible()

    // Check that the Log Out card is visible.
    await expect(page.getByTestId('log-out-card')).toBeVisible()
  })

  test.afterEach('Clear assumed roles', async ({ context }) => {
    // Clear the extra HTTP headers to set assumed roles to default state.
    await context.setExtraHTTPHeaders({})
  })
})
