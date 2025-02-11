// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Client Compatibility Settings', () => {
  // test.beforeAll(async () => {
  // Note: We can either manually check if in the UI if the toggle is on and
  // turn it off before starting the "actual" test part or send the request below.
  // Make sure client compatibility settings are off by default.
  // These are default settings from idler when you run after database has been reset.
  // The key is that `redirectEnabled` is set to false to ensure test starts from a known state.

  test.beforeEach('Navigate to system', async ({ page }) => {
    // Navigate to system page.
    await page.goto('/system')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Client compatibility settings card renders', async ({ page }) => {
    // Check that the client compatibility settings card is rendered.
    await expect(page.getByTestId('system-client-compatibility-card')).toBeVisible()
  })

  test('Toggles client redirect (off), on and off', async ({ page, testToken }) => {
    // Open modal, input settings and ok the modal --------------------------------------------------------------------

    // Get the edit settings button.
    const editSettingsButton = page.getByTestId('client-redirect-settings-button')

    // Open the update client compatibility settings modal.
    await editSettingsButton.click()

    // Get the `New Client Redirect Enabled` toggle.
    const clientRedirectToggle = page.getByTestId('client-redirect-enabled-switch-control')

    // When running tests locally, client redirect could be on.
    // This is the alternative to sending server requests commented out at the top of the file.
    // We need to toggle it off to ensure tests run from a known state.
    if (await clientRedirectToggle.isChecked()) {
      // Toggles the `New Client Redirect Enabled` toggle off.
      await clientRedirectToggle.click()
    }

    // Toggles the `New Client Redirect Enabled` toggle on.
    await clientRedirectToggle.click()

    // Type testToken into host text input.
    await page.getByTestId('input-text-host').fill(`host${testToken}`)

    // Type testToken into cdn url text input.
    await page.getByTestId('input-text-cdn-url').fill(`cdn${testToken}`)

    // Save the settings by clicking the `Save Settings` button, which triggers an API call. Then we wait for the API response of the client compatibility settings update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().includes('/api/clientCompatibilitySettings') && response.status() === 200
      ),
      clickMButton(page.getByTestId('client-redirect-settings-modal-ok-button')),
    ])

    // TODO: Check for success toast.

    // Check that the redirect is on -----------------------------------------------------------------------------------

    // Check that `New Version Redirect` is on.
    await expect(page.getByTestId('client-redirect-status')).toContainText('Redirected')

    // Open modal again to toggle `New Version Redirect` off and check it is off --------------------------------------

    // Open the update client compatibility settings modal again.
    await editSettingsButton.click()

    // Toggles the `New Version Redirect Enabled` toggle off.
    await clientRedirectToggle.click()

    // Save the settings by clicking the `Ok` button, which triggers an API call. Then we wait for the API response of the client compatibility settings update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().includes('/api/clientCompatibilitySettings') && response.status() === 200
      ),
      clickMButton(page.getByTestId('client-redirect-settings-modal-ok-button')),
    ])

    // TODO: Check for success toast.

    // Check that the redirect is off ----------------------------------------------------------------------------------
    await expect(page.getByTestId('client-redirect-status')).toContainText('Refused')
  })
})
