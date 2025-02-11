// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Audit Log List', () => {
  test.beforeEach('Navigate to audit logs', async ({ page }) => {
    // Navigate to the audit logs page.
    await page.goto('/auditLogs')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and latest log events cards render on list page', async ({ page }) => {
    // Get the overview search card and check that it is rendered.
    await expect(page.getByTestId('audit-log-list-overview-search-card')).toBeVisible()

    // Get the latest log events card and check that it is rendered.
    await expect(page.getByTestId('audit-log-list-latest-events-card')).toBeVisible()
  })
})
