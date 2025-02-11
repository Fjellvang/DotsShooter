// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Quick Links', () => {
  test.beforeEach('Set up quick links mock', async ({ page }) => {
    await page.route('/api/quickLinks', async (route) => {
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify([
          {
            icon: '@game-icon',
            title: 'Mocked title 1',
            uri: '/players',
            color: 'rgb(134, 199, 51)',
          },
          {
            icon: '@game-icon',
            title: 'Mocked title 2',
            uri: '/runtimeOptions',
            color: 'rgb(134, 199, 51)',
          },
          {
            icon: '@game-icon',
            title: 'Mocked title 3',
            uri: '/guilds',
            color: 'rgb(134, 199, 51)',
          },
        ]),
      })
    })
  })

  test.beforeEach('Navigate to overview', async ({ page }) => {
    // Navigate to the overview page.
    await page.goto('/')
  })

  test('Check quick links modal is enabled', async ({ page }) => {
    // Get quick links popover button.
    const quickLinksButton = page.getByTestId('quick-links-button')

    // Check that quick links popover button is enabled when links are defined.
    await expect(quickLinksButton).not.toHaveAttribute('disabled')

    // Click the quick links popover button to open the popover.
    await quickLinksButton.click()

    // Get the quick links popover.
    const quickLinksPopover = page.getByTestId('quick-links-popover')

    // Check that the popover is opened.
    await expect(quickLinksPopover).toBeVisible()

    // Get all the quick link elements in the popover.
    const quickLinks = quickLinksPopover.locator('div[data-testid^="quick-link-"]')

    // Check that there are exactly 3 quick link elements.
    await expect(quickLinks).toHaveCount(3)

    // Check that the quick link elements have the correct text.
    await expect(quickLinks.nth(0)).toHaveText('Mocked title 1')
    await expect(quickLinks.nth(1)).toHaveText('Mocked title 2')
    await expect(quickLinks.nth(2)).toHaveText('Mocked title 3')

    // Navigate to the first quick link and get the page instance of the new tab.
    const [newPage] = await Promise.all([page.waitForEvent('popup'), quickLinks.nth(0).click()])

    // Check that the header title is `Manage Players` in the new tab.
    await expect(newPage.getByTestId('header-bar-title')).toHaveText('Manage Players')
  })
})
