// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * @fileoverview This file works as a proxy to Playwright's built-in `test`, extending it with custom fixtures.
 * Instead of importing directly from Playwright, import from this file.
 */
import { test as baseTest } from '@playwright/test'

import type { TestFixtures } from './fixtureTypes.js'
import { gameServerFixtures } from './gameServerFixtures.js'

// Re-export everything from Playwright's test.
// eslint-disable-next-line import/export -- This is valid, linter is confused.
export * from '@playwright/test'

// Re-export the default configuration.
export { defaultConfig } from './defaultConfig.js'

// Re-export custom actions.
export { activeLinkExistsForCurrentPage, clickMButton } from './actions.js'

// Extend the test fixture with custom fixtures and export it to override the default test fixture.
export const test = baseTest.extend<TestFixtures>({
  ...gameServerFixtures,

  // Base URL for the API endpoints used in tests, such as `await request.post(apiURL)`.
  apiURL: process.env.API_URL ?? 'http://localhost:5550/api',

  // Unique test token for filling input fields in tests.
  testToken: Date.now().toString().slice(-5),

  // Extend the page fixture to include the console listener.
  page: async ({ page }, use) => {
    // Allow list for expected/acceptable messages.
    // TODO: test these by removing them one by one and see if test fails.
    const allowList = [
      '[HMR]',
      '[Vue warn]',
      '[BootstrapVue warn]',
      '(deprecation ',
      "^ The above deprecation's compat behavior is disabled and will likely lead to runtime errors.",
      'Lit is in dev mode. Not recommended for production!',
      'Blocked aria-hidden on a <button> element because the element that just received focus must not be hidden from assistive technology users.',
    ]

    // Listen for all console events and handle warnings and errors.
    page.on('console', (msg) => {
      if (msg.type() === 'error' || msg.type() === 'warning') {
        const message = msg.text()
        // Check if the message is in the allow list.
        if (!allowList.some((x) => message.startsWith(x))) {
          throw new Error(`Failing test due to unexpected console message: "${message}"`)
        }
      }
    })

    await use(page)
  },
})
