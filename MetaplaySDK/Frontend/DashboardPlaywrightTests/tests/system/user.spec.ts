// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('User settings and role assumption', () => {
  test.beforeEach('Navigate to user', async ({ page }) => {
    // Navigate to the user page.
    await page.goto('/user')
  })

  test('User overview, permissions and assume role card render', async ({ page }) => {
    // Get the user overview card and check that it renders.
    await expect(page.getByTestId('user-overview-card')).toBeVisible()

    // Get the user permissions card and check that it renders.
    await expect(page.getByTestId('user-permissions-card')).toBeVisible()

    // Get the assume roles card and check that it renders.
    await expect(page.getByTestId('assume-role-card')).toBeVisible()
  })

  test('Checks that assume role card contains all user roles', async ({ page, authDetails }) => {
    test.skip(!authDetails.canAssumeRoles, 'Assume roles not enabled in this deployment')

    // Get the assume role card.
    const assumeRoleCard = page.getByTestId('assume-role-card')

    // Check that the assume role card contains all roles.
    for (const role of authDetails.allUserRoles) {
      await expect(assumeRoleCard).toContainText(role)
    }
  })

  test('Assumes game viewer role', async ({ page, authDetails }) => {
    test.skip(!authDetails.canAssumeRoles, 'Assume roles not enabled in this deployment')
    test.skip(!authDetails.allUserRoles.includes('game-viewer'), 'No game-viewer role available in this deployment')

    // Get sidebar list.
    const sidebarList = page.getByTestId('sidebar')

    // Get runtime options list item from the sidebar.
    // Note: this contains both the outer anchor that wraps the inner list item that contains the title `Runtime Options`.
    const scanJobsWrapper = sidebarList.getByRole('link', {
      name: 'Scan Jobs',
    })

    // Get the inner list item to prepare disabled test.
    const scanJobsListItem = scanJobsWrapper.nth(1)

    // Get game viewer checkbox
    const gameViewerCheckbox = page.getByTestId(`checkbox-game-viewer`)

    // Check the game viewer checkbox.
    await gameViewerCheckbox.check()

    // Check that the inner list item is disabled.
    await expect(scanJobsListItem).toHaveAttribute('disabled', 'true')

    // Uncheck the game viewer checkbox.
    await gameViewerCheckbox.uncheck()

    // Check that the inner list item is no longer disabled.
    await expect(scanJobsListItem).not.toHaveAttribute('disabled')

    // Navigate to the runtime options page.
    await scanJobsWrapper.first().click()

    // Check that the header title is `View Runtime Options`.
    await expect(page.getByTestId('header-bar-title')).toHaveText('Manage Database Scan Jobs')
  })
})
