// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Audit Log', () => {
  test.beforeEach('Navigate to player details and open log tab', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)

    // Navigate to tab 1.
    await page.getByTestId('tab-1').click()
  })

  test('Finds audit log events list in player detail page', async ({ page }) => {
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })

  test('Creates an audit log event, identify it and view it in audit log detail page', async ({
    page,
    freshTestPlayer,
  }) => {
    // Note that simply viewing the player will have already created a log event.

    // Get the audit log events list card.
    const auditLogCard = page.getByTestId('audit-log-card')

    // Check that a player viewed log entry was created.
    const event = auditLogCard.getByTestId('event-PlayerDetailsController-PlayerEventViewed')
    await event.scrollIntoViewIfNeeded()

    // Check for title and description.
    await expect(event).toContainText('Viewed')
    await expect(event).toContainText('Player details viewed')

    // Get view event text button.
    const viewEventTextButton = event.getByTestId('view-more-link')

    // Navigate to the view event text page.
    await viewEventTextButton.click()

    // Get the detail event card.
    const auditLogDetailOverviewCard = page.getByTestId('audit-log-detail-overview-card')

    // Check that the detail event card contains the correct title.
    await expect(auditLogDetailOverviewCard).toContainText('Event: Viewed')

    // Check that the detail event card contains the correct description.
    await expect(auditLogDetailOverviewCard).toContainText('Player details viewed.')

    // Get the target id link containing the `freshTestPlayer` id.
    const targetIdLink = auditLogDetailOverviewCard.getByText(freshTestPlayer, {
      exact: true,
    })

    // Navigate to the audit log list page by clicking the target id link.
    await targetIdLink.click()

    // Get the audit log list search card.
    const auditLogListSearchCard = page.getByTestId('audit-log-list-overview-search-card')

    // TODO: This was the old cypress test check which we don't do anymore. Remove or move to auditLogList test group.
    // Check that the audit log list search card contains the title `Audit Log Events`.
    await expect(auditLogListSearchCard).toContainText('Audit Log Events')

    // Get the selected `Type` in the `MetaInputPlayerSelect`.
    const selectedType = auditLogListSearchCard.locator('.multiselect-option.is-selected')

    // Check that the selected type contains `id="multiselect-option-Player"`.
    await expect(selectedType).toHaveAttribute('id', 'multiselect-option-Player')

    // Check that the selected type contains `aria-selected="true"`.
    await expect(selectedType).toHaveAttribute('aria-selected', 'true')

    // Check that the preselected type contains `aria-label="Player"`.
    await expect(selectedType).toHaveAttribute('aria-label', 'Player')

    // Get the `ID` text input.
    const idInput = auditLogListSearchCard.getByLabel('ID', { exact: true })

    // Check that the `ID` input contains the value `sharedTestPlayer`.
    await expect(idInput).toHaveValue(freshTestPlayer.split(':')[1])

    // Get the search results card.
    const searchResultsCard = page.getByTestId('audit-log-list-search-results-card')

    // Check that the search results card does not have no entries.
    await expect(searchResultsCard).not.toContainText('No search results')

    // Check that it contains the event title.
    await expect(searchResultsCard).toContainText('Viewed')
  })
})
