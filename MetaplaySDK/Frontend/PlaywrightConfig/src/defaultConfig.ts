// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { devices, type PlaywrightTestConfig } from '@playwright/test'

/**
 * This is the default configuration for Playwright tests targeting the Metaplay SDK LiveOps Dashboard.
 * You can safely override these settings to your preferences.
 * See https://playwright.dev/docs/test-configuration.
 */
export const defaultConfig: PlaywrightTestConfig = {
  testDir: 'tests/e2e',
  /* Output directory for artifacts like screenshots and traces. */
  outputDir: process.env.OUTPUT_DIRECTORY ?? 'test-results',
  /* Run tests in files in parallel */
  fullyParallel: true,
  /* Fail the build on CI if you accidentally left test.only in the source code. */
  forbidOnly: !!process.env.CI,
  /* Shared settings for all the projects below. See https://playwright.dev/docs/api/class-testoptions. */
  use: {
    /* Base URL for the dashboard internal routes used in tests, such as `await page.goto('/')`. */
    baseURL: process.env.DASHBOARD_BASE_URL ?? 'http://localhost:5551', // Note: Use http://localhost:5550 to test against the pre-built dashboard that is a prod build.

    /* Collect trace when the test fails. See https://playwright.dev/docs/trace-viewer */
    trace: 'retain-on-failure',
  },
  /** Maximum time for expect(). Extended from 5s to 10s because some of our test steps are close enough to 5s that
   * they occasionally fail. This change increases reliability of the tests. */
  expect: {
    timeout: 10_000,
  },
  /* Configure projects for major browsers */
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },

    // {
    //   name: 'firefox',
    //   use: { ...devices['Desktop Firefox'] },
    // },

    // {
    //   name: 'webkit',
    //   use: { ...devices['Desktop Safari'] },
    // },
  ],
}
