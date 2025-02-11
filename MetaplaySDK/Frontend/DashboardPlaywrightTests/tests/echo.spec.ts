// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test('succeeds', async ({ request }) => {
  // Call the echo endpoint.
  const res = await request.get('/api/echo')

  // Expect success.
  expect(res.status()).toBe(200)

  // Response should contain the expected headers.
  const json = (await res.json()) as object
  expect(json).toHaveProperty('headers')
  expect(json).toHaveProperty('metaplay')
})
