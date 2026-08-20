import { describe, expect, it } from 'vitest'

/**
 * The route id is the first hash segment. Pulled out as a pure function here because the
 * behaviour that matters is the parsing, and a deep link that silently fell back to the
 * home page was a real bug: #/teach/04-paytable-math rendered Start.
 */
function routeIdFrom(hash: string, fallback = 'home'): string {
  return hash.replace(/^#\/?/, '').split('/')[0] || fallback
}

describe('hash route parsing', () => {
  it('reads a plain page id', () => {
    expect(routeIdFrom('#/ch03')).toBe('ch03')
    expect(routeIdFrom('#/finale')).toBe('finale')
  })

  it('keeps only the first segment, so a page can own the rest', () => {
    expect(routeIdFrom('#/teach/04-paytable-math')).toBe('teach')
    expect(routeIdFrom('#/teach/09-optimization')).toBe('teach')
  })

  it('falls back when there is no hash', () => {
    expect(routeIdFrom('')).toBe('home')
    expect(routeIdFrom('#/')).toBe('home')
    expect(routeIdFrom('#')).toBe('home')
  })
})
