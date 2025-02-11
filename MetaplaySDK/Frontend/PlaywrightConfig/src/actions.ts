// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { expect, type Locator, type Page, test } from '@playwright/test'

/**
 * A helper function for MButtons that first disables the safety lock if it is on, then clicks the button.
 */
export async function clickMButton(locator: Locator): Promise<void> {
  await test.step('Clicking MButton', async () => {
    // Locator should be visible.
    await expect(locator).toBeVisible()

    // If the given locator has a safety-lock-active attribute...
    const safetyLockActive = await locator.getAttribute('safety-lock-active')
    if (safetyLockActive === 'yes') {
      // ...find the safety lock button (it's a sibling element) and click it.
      const safetyLockButton = locator.locator('..').locator('[data-testid="safety-lock-button"]')
      await safetyLockButton.click()
    }

    // Click the button.
    await locator.click()
  })
}

/**
 * A helper function to check if the sidebar link is rendered, enabled and active in the current page.
 * @param page - The Playwright page object.
 * @param baseUrl - The base URL of the application, used to strip the URL from the full URL.
 */
export async function activeLinkExistsForCurrentPage(page: Page, baseUrl: string | undefined): Promise<void> {
  // Get the full URL.
  const fullUrl = page.url()

  // Ensure baseUrl is defined
  if (!baseUrl) {
    throw new Error('baseUrl is undefined, baseUrl is required to strip the actual URL from the full URL')
  }

  // Remove the base URL from the full URL.
  const url = fullUrl.replace(baseUrl, '')

  // Get the sidebar.
  const sideBar = page.locator('[data-testid="sidebar"]')

  // Get the sidebar link to the current page.
  const sidebarLink = sideBar.locator(`a[href="${url}"]`)

  // Check that the sidebar link is visible.
  await expect(sidebarLink).toBeVisible()

  // Check that the sidebar link is not disabled.
  await expect(sidebarLink).not.toBeDisabled()

  // Check that sidebar link has `aria-current="page"` attribute.
  await expect(sidebarLink).toHaveAttribute('aria-current', 'page')
}
