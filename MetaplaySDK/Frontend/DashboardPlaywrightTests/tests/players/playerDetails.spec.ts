// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Player Details Rendering', () => {
  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Overview and admin tools cards', async ({ page, freshTestPlayer }) => {
    // Get the overview card.
    const overviewCard = page.getByTestId('player-overview-card')

    // Check that the overview card contains the player ID.
    await expect(overviewCard).toContainText(freshTestPlayer)

    // Get the admin actions card.
    const adminActionsCard = page.getByTestId('player-admin-actions-card')

    // Check that admin actions card is rendered.
    await expect(adminActionsCard).toBeVisible()
  })

  test('Tab 0 cards', async ({ page, dashboardOptions, featureFlags }) => {
    // Get tab 0 button.
    const tab0Button = page.getByTestId('tab-0')

    // Check that the tab 0 button contains the `dashboardOptions.playerDetailsTab0DisplayName`.
    await expect(tab0Button).toHaveText(dashboardOptions.playerDetailsTab0DisplayName)

    // Get the inbox card and check that the inbox card is rendered.
    await expect(page.getByTestId('player-inbox-list-card')).toBeVisible()

    // Get the broadcasts card and check that the broadcasts card is rendered.
    await expect(page.getByTestId('player-broadcasts-card')).toBeVisible()

    // Check that the matchmaking feature flag is enabled.
    if (featureFlags.asyncMatchmaker) {
      // Get the matchmaking card and check that the matchmaking card is rendered.
      await expect(page.getByTestId('player-matchmaking-card')).toBeVisible()
    }

    // Check that the player leagues feature flag is enabled.
    if (featureFlags.playerLeagues) {
      // Get the player leagues card and check that the player leagues card is rendered.
      await expect(page.getByTestId('player-leagues-card')).toBeVisible()
    }
  })

  test('Tab 1 cards', async ({ page, dashboardOptions }) => {
    // Get tab 1 button.
    const tab1Button = page.getByTestId('tab-1')

    // Check that the tab 1 button contains the `dashboardOptions.playerDetailsTab1DisplayName`.
    await expect(tab1Button).toHaveText(dashboardOptions.playerDetailsTab1DisplayName)

    // Navigate to tab 1 and check that the page query has updated.
    await tab1Button.click()
    expect(page.url()).toContain('?tab=1')

    // Get the login methods card and check that the login methods card is rendered.
    await expect(page.getByTestId('player-login-methods-card')).toBeVisible()

    // Get the device history card and check that the device history card is rendered.
    await expect(page.getByTestId('player-device-history-card')).toBeVisible()

    // Get the login history card and check that the login history card is rendered.
    await expect(page.getByTestId('player-login-history-card')).toBeVisible()

    // Get player event log card and check that the player event log card is rendered.
    await expect(page.getByTestId('entity-event-log-card')).toBeVisible()

    // Get the audit log card and check that the audit log card is rendered.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })

  test('Tab 2 cards', async ({ page, dashboardOptions, featureFlags }) => {
    // Get tab 2 button.
    const tab2Button = page.getByTestId('tab-2')

    // Check that the tab 2 button contains the `dashboardOptions.playerDetailsTab2DisplayName`.
    await expect(tab2Button).toHaveText(dashboardOptions.playerDetailsTab2DisplayName)

    // Navigate to tab 2 and check that the page query has updated.
    await tab2Button.click()
    expect(page.url()).toContain('?tab=2')

    // Get purchase history card and check that the purchase history card is rendered.
    await expect(page.getByTestId('player-purchase-history-card')).toBeVisible()

    // Get subscriptions card and check that the subscriptions card is rendered.
    await expect(page.getByTestId('player-subscriptions-history-card')).toBeVisible()

    // Check that the web3 feature flag is enabled.
    if (featureFlags.web3) {
      // Get the player NFTs card and check that the player NFTs card is rendered.
      await expect(page.getByTestId('player-nfts-card')).toBeVisible()

      // Triggers NFT ownership refresh by clicking the `refresh now` text button in the description of the `player-nfts-card`.
      await page.getByTestId('nft-refresh-button').click()

      // TODO: Check for success toast.
    }
  })

  test('Tab 3 cards', async ({ page, dashboardOptions, featureFlags }) => {
    // Get tab 3 button.
    const tab3Button = page.getByTestId('tab-3')

    // Check that the tab 3 button contains the `dashboardOptions.playerDetailsTab3DisplayName`.
    await expect(tab3Button).toHaveText(dashboardOptions.playerDetailsTab3DisplayName)

    // Navigate to tab 3 and check that the page query has updated.
    await tab3Button.click()
    expect(page.url()).toContain('?tab=3')

    // Get the segments card and check that the segments card is rendered.
    await expect(page.getByTestId('segments-card')).toBeVisible()

    // Get the experiments card and check that the experiments card is rendered.
    await expect(page.getByTestId('player-experiments-card')).toBeVisible()

    // Check that the liveOps events feature flag is enabled.
    if (featureFlags.liveOpsEvents) {
      // Get the liveOps events card and check that the liveOps events card is rendered.
      await expect(page.getByTestId('player-liveops-events-card')).toBeVisible()
    }

    // Check that the ingame events feature flag is enabled.
    if (featureFlags.ingameEvents) {
      // Get the in-game events and offer groups card and check that the in-game events card is rendered.
      await expect(page.getByTestId('event-list-card')).toBeVisible()
    }

    // Get the offer groups card and check that the offer groups card is rendered.
    await expect(page.getByTestId('offer-group-list-card')).toBeVisible()
  })

  test('Tab 4 cards', async ({ page, dashboardOptions }) => {
    // Get tab 4 button.
    const tab4Button = page.getByTestId('tab-4')

    // Check that the tab 4 button contains the `dashboardOptions.playerDetailsTab4DisplayName`.
    await expect(tab4Button).toHaveText(dashboardOptions.playerDetailsTab4DisplayName)

    // Navigate to tab 4 and check that the page query has updated.
    await tab4Button.click()
    expect(page.url()).toContain('?tab=4')

    // Get the incident history card and check that the incident history card is rendered.
    await expect(page.getByTestId('player-incident-history-card')).toBeVisible()
  })
})
