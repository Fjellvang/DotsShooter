// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Audit Log Details', () => {
  test.beforeEach('Generate log event and navigate to it', async ({ page, freshTestPlayer }) => {
    // Navigate to player page to generate an audit log event.
    await page.goto(`/players/${freshTestPlayer}`)

    // Navigate to the Audit Logs page.
    await page.goto('/auditLogs')

    // Get the latest log events card
    const latestLogEventsCard = page.getByTestId('audit-log-list-latest-events-card')

    // Get the first table row link in the latest log events card.
    const latestLogEventRow = latestLogEventsCard.locator('.table-row-link').first()

    // Navigate to the specific audit log event detail page.
    await latestLogEventRow.click()
  })

  test('Overview and event payload cards render on detail page', async ({ page }) => {
    // Get the overview card and check it renders.
    await expect(page.getByTestId('audit-log-detail-overview-card')).toBeVisible()

    // Get the event payload card and check it renders.
    await expect(page.getByTestId('audit-log-detail-event-payload-card')).toBeVisible()
  })
})
