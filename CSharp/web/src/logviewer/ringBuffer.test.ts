import { describe, expect, it } from 'vitest'
import { appendWithCap } from './ringBuffer'

describe('appendWithCap', () => {
  it('appends below the cap without dropping anything', () => {
    expect(appendWithCap([1, 2], [3, 4], 10)).toEqual([1, 2, 3, 4])
  })

  it('drops the oldest entries once the cap is exceeded', () => {
    const existing = [1, 2, 3, 4, 5]
    expect(appendWithCap(existing, [6, 7], 5)).toEqual([3, 4, 5, 6, 7])
  })

  it('returns the existing reference unchanged when nothing new arrives', () => {
    const existing = [1, 2, 3]
    expect(appendWithCap(existing, [], 5)).toBe(existing)
  })

  it('never grows past the cap even with a single oversized batch', () => {
    const incoming = Array.from({ length: 30 }, (_, i) => i)
    const result = appendWithCap([], incoming, 10)
    expect(result).toHaveLength(10)
    expect(result[0]).toBe(20)
    expect(result[9]).toBe(29)
  })
})
