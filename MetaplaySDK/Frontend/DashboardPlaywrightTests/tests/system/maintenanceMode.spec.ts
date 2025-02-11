// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Maintenance mode', () => {
  // Note: We can either manually check if in the UI if the toggle is on and
  // turn it off before starting the "actual" test part or send the request below.

  // Make the maintenance mode is off by default.
  // This is to make sure the test starts from known state.
  // test.beforeAll(async ({ request, apiURL }) => {
  //   await request.delete(`${apiURL}/maintenanceMode`)
  // })

  test.beforeEach('Navigate to system', async ({ page }) => {
    // Navigate to system page.
    await page.goto('/system')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Maintenance mode card renders', async ({ page }) => {
    // Check that the maintenance mode card is rendered.
    await expect(page.getByTestId('system-maintenance-mode-card')).toBeVisible()
  })

  test('Toggles maintenance mode (off) on and off', { tag: '@dynamic-button-text' }, async ({ page }) => {
    // Get the edit settings button.
    const editSettingsButton = page.getByTestId('maintenance-mode-button')

    // Open the `Update Maintenance Mode Settings` modal.
    await editSettingsButton.click()

    // Get the `Maintenance Mode Enabled` toggle.
    const maintenanceModeToggle = page.getByTestId('maintenance-enabled-switch-control')

    // When running tests locally, maintenance mode could be on.
    // This is the alternative to sending server requests commented out at the top of the file.
    // We need to toggle it off to ensure tests run from a known state.
    if (await maintenanceModeToggle.isChecked()) {
      // Toggles the `Maintenance Mode Enabled` toggle off.
      await maintenanceModeToggle.click()
    }

    // Toggle the `Maintenance Mode Enabled` toggle on.
    await maintenanceModeToggle.click()

    // Get the ok modal button. @dynamic-button-text
    const scheduleSaveSettingsButton = page.getByTestId('maintenance-mode-modal-ok-button')

    // Check that the ok modal button name is `Schedule`.
    await expect(scheduleSaveSettingsButton).toHaveText('Schedule')

    // Schedule the maintenance mode clicking the `Schedule` button, which triggers an API call. Then we wait for the API response of the maintenance mode update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse((response) => response.url().includes('/api/maintenanceMode') && response.status() === 200),
      clickMButton(scheduleSaveSettingsButton),
    ])

    // TODO: Check for success toast.

    // Get the maintenance mode alert.
    const maintenanceModeAlert = page.getByTestId('maintenance-mode-header-notification')

    // Check that the maintenance mode alert is rendered.
    await expect(maintenanceModeAlert).toBeVisible()

    // Open the `Update Maintenance Mode Settings` modal again.
    await editSettingsButton.click()

    // Toggle the `Maintenance Mode Enabled` toggle off.
    await maintenanceModeToggle.click()

    // Check that ok modal button name is `Save Settings`.
    await expect(scheduleSaveSettingsButton).toHaveText('Save Settings')

    // Unschedule the maintenance mode clicking the `Save Settings` button, which triggers an API call. Then we wait for the API response of the maintenance mode update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse((response) => response.url().includes('/api/maintenanceMode') && response.status() === 200),
      clickMButton(page.getByTestId('maintenance-mode-modal-ok-button')),
    ])

    // TODO: Check for success toast.

    // Check that the maintenance mode alert is no longer rendered.
    await expect(maintenanceModeAlert).not.toBeVisible()
  })
})
