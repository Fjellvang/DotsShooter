// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Player Matchmaking', () => {
  test.beforeAll('Skip test if asyncMatchmaker feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.asyncMatchmaker, 'Async matchmaker feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  // Skipped since R29 due to race condition in joining matchmaker.
  // Note: Failed by server returning 409 when trying to add player because player was already in matchmaker.
  test.skip('Add, remove and add player back to matchmaker', { tag: '@dynamic-button-text' }, async ({ page }) => {
    // Get the first matchmaker list entry.
    const matchmakerListEntry = page.getByTestId('player-matchmaking-list-entry').first()

    // Open the collapse by clicking the matchmaker list entry.
    await matchmakerListEntry.click()

    // Get the add/remove player button.
    const addRemovePlayerButton = page.getByTestId('add-remove-player-matchmaker-button')

    // Check the button label to determine the current state.
    const buttonLabel = await addRemovePlayerButton.textContent()

    // If the button label is 'Add Player', it means the player has not joined any matchmakers yet.
    // In this state, proceed to join a matchmaker.
    if (buttonLabel === 'Add Player') {
      // Open the add player to matchmaker modal.
      await addRemovePlayerButton.click()

      // Ok the modal.
      await clickMButton(page.getByTestId('add-remove-player-matchmaker-modal-ok-button'))

      // TODO: Check that we got a success toast.
    }

    // Check that the button name is `Remove Player` after joining the matchmaker (or already in the matchmaker in the first place).
    await expect(addRemovePlayerButton).toHaveText('Remove Player')

    // Open the remove player from matchmaker modal.
    await addRemovePlayerButton.click()

    // Ok the modal.
    await clickMButton(page.getByTestId('add-remove-player-matchmaker-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Check that the button name is `Add Player` after removing the player from the matchmaker.
    await expect(addRemovePlayerButton).toHaveText('Add Player')

    // Open the add player to matchmaker modal.
    await addRemovePlayerButton.click()

    // Ok the modal.
    await clickMButton(page.getByTestId('add-remove-player-matchmaker-modal-ok-button'))

    // TODO: Check that we got a success toast.
  })

  // Skipped since R29 due to race condition in joining matchmaker.
  // Note: Failed by server returning 409 when trying to add player because player was already in matchmaker.
  test.skip(
    'Check that player is in matchmaker detail page top players list',
    { tag: '@non-deterministic' },
    async ({ page, freshTestPlayer }) => {
      // The `searchOutcome` step will take the full `expect` time to resolve. That makes this test take a long time so
      // we mark it as slow.
      test.slow()

      // Join a matchmaker if not already in one, then prepare to navigate to matchmaker detail view --------------------

      // Get the first matchmaker list entry.
      const matchmakerListEntry = page.getByTestId('player-matchmaking-list-entry').first()

      // Check that the matchmaker list entry is rendered.
      await expect(matchmakerListEntry).toBeVisible()

      // Get the all the inner texts matchmaker list entry.
      const matchmakerListEntryText = await matchmakerListEntry.allInnerTexts()

      // If 'Not a participant' text is present, it means the player has not joined any matchmakers yet.
      // In this state, proceed to join a matchmaker.
      if (matchmakerListEntryText[0].includes('Not a participant')) {
        // Open the collapse by clicking the matchmaker list entry.
        await matchmakerListEntry.click()

        // Open the add player to matchmaker modal.
        await page.getByTestId('add-remove-player-matchmaker-button').click()

        // Ok the modal.
        await clickMButton(page.getByTestId('add-remove-player-matchmaker-modal-ok-button'))

        // TODO: Check that we got a success toast.
      }

      // Check that player is in the matchmaker.
      await expect(matchmakerListEntry).toContainText('Participant', {
        useInnerText: true,
      })

      // Get navigation link button to specific matchmaker.
      const matchmakerNavigationLink = matchmakerListEntry.getByRole('link', {
        name: 'View matchmaker',
        exact: true,
      })

      // Check that the matchmaker navigation link is rendered.
      await expect(matchmakerNavigationLink).toBeVisible()

      // Click the matchmaker navigation to go to the matchmaker detail view.
      await matchmakerNavigationLink.click()

      // Locate the top players list card and search for player ---------------------------------------------------------

      // Get the top players list card.
      const matchmakerTopPlayersListCard = page.getByTestId('matchmaker-top-players-list-card')

      // Check that the matchmaker top players list card is rendered.
      await expect(matchmakerTopPlayersListCard).toBeVisible()

      // Click top part of `MetaListCard` to expose utilities menu which includes the searchbox.
      await matchmakerTopPlayersListCard.locator('h4.card-title').click()

      // Get the searchbox in the utilities menu.
      const topPlayersSearchbox = matchmakerTopPlayersListCard.getByPlaceholder('Type your search here...', {
        exact: true,
      })

      // Fill the searchbox with the shared test player.
      await topPlayersSearchbox.fill(freshTestPlayer)

      // Get the no results message list item.
      // This should happen if the searched player is not in the top 100 players.
      const noResultsMessage = matchmakerTopPlayersListCard.getByTestId('meta-list-card-no-results-message')

      // Get the top players first list item.
      // This should happen if the searched player is in the top 100 players.
      const firstListItem = matchmakerTopPlayersListCard
        .locator('[data-testid="meta-list-card-list-container"] li')
        .first()

      // Check for the search outcome by counting the number of fulfilled promises.
      // Expect no results message text to match or the first list item to contain the shared test player name.
      // Only 1 promise should be fulfilled, if not, throw an error.
      // The error represents an edge case scenario where neither or both are rendered.
      // Note: @non-deterministic part below.
      const searchOutcome = await Promise.allSettled([
        expect(noResultsMessage).toHaveText('No items found. Try a different search string or filters? 🤔'),
        expect(firstListItem).toContainText(freshTestPlayer),
      ])
      const fulfilledPromisesCount: number = searchOutcome.reduce(
        (fulfilledCount, promiseSettledResult) =>
          (promiseSettledResult.status === 'fulfilled' ? 1 : 0) + fulfilledCount,
        0
      )
      if (fulfilledPromisesCount !== 1) {
        throw new Error(`Expected exactly 1 promise to be fulfilled, but got ${fulfilledPromisesCount}`)
      }
    }
  )

  test('Simulate matchmaker', async () => {
    // Note: Simulate player matchmaking feature development frozen.
    // `PlayerMatchmakingCard` has commented out code for further explanation.
    // This also serves as a reminder of the skipped Cypress tests.
    test.skip()
  })
})
