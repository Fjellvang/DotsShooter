// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { describe, expect, test } from 'vitest'

import { makeHash, makeIntoUniqueKey } from '../../src/generalUtils'

describe('makeUniqueKey', () => {
  test('Basic functionality test', () => {
    expect(makeIntoUniqueKey('test')).toEqual('test_0000')
    expect(makeIntoUniqueKey('anotherTest')).toEqual('anotherTest_0001')
  })
})

describe('makeHash', () => {
  test('Produces a result', () => {
    expect(makeHash(undefined)).toBeTypeOf('string')
    expect(makeHash(null)).toBeTypeOf('string')
    expect(makeHash(0)).toBeTypeOf('string')
    expect(makeHash(123)).toBeTypeOf('string')
    expect(makeHash('')).toBeTypeOf('string')
    expect(makeHash('test')).toBeTypeOf('string')
    expect(makeHash({})).toBeTypeOf('string')
    expect(makeHash({ test: 'test' })).toBeTypeOf('string')
    expect(makeHash([])).toBeTypeOf('string')
    expect(makeHash([1, 2, 3])).toBeTypeOf('string')
  })

  test('Produces a different result for different values', () => {
    const test: unknown[] = [
      undefined,
      null,
      0,
      123,
      -123,
      456,
      '',
      'test',
      'TEST',
      'TESTA',
      'TESTB',
      {},
      { test: 'test' },
      [],
      [1, 2, 3],
      [3, 2, 1],
    ]
    const hashes = test.map(makeHash)
    const uniqueIds = new Set(hashes)
    expect(uniqueIds.size).toEqual(test.length)
  })
})
