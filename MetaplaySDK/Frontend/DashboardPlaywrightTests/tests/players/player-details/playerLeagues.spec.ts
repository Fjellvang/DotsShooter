import { test } from '@metaplay/playwright-config'

test.describe('Player Leagues', () => {
  // Note: original test in old cypress equivalent is already tested in the `playerDetails.spec.ts` file.
  // We will do a similar type of test as player matchmaking where we add and remove test player from a league.
  // Potentially other tests like change players rank and go to division detail page to check player is in it as well.
  test.beforeAll('Skip test if leagues feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.playerLeagues, 'Player leagues feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Add, remove and add player back to league', async ({ page }) => {
    // Get the add player to league button and click it.
    await page.getByTestId('player-leagues-list-entry').first().click()

    // TODO: finish the test after migrating remaining cypress ones.
  })
})
