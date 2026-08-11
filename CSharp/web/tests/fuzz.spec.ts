// Seeded fuzz over the frontend's external-data boundary: format helpers,
// the sanitize layer, and full App mounts fed malformed /api responses.
// Invariant everywhere: no exception escapes; the page always renders.

import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import App from '../src/App.vue'
import SystemCard from '../src/components/SystemCard.vue'
import MachineStrip from '../src/components/MachineStrip.vue'
import { formatMemoryMb, formatUptimeHours, formatClockTime } from '../src/format'
import { sanitizeStats } from '../src/api/sanitize'

// mulberry32 — tiny deterministic PRNG; fixed seed makes failures reproducible.
function prng(seed: number): () => number {
  let a = seed >>> 0
  return () => {
    a = (a + 0x6d2b79f5) >>> 0
    let t = a
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
}

function garbageValue(rand: () => number, depth = 0): unknown {
  const pick = Math.floor(rand() * (depth > 2 ? 8 : 10))
  switch (pick) {
    case 0: return null
    case 1: return undefined
    case 2: return rand() < 0.5 ? NaN : Infinity
    case 3: return (rand() - 0.5) * 1e12
    case 4: return String.fromCharCode(...Array.from({ length: Math.floor(rand() * 50) }, () => Math.floor(rand() * 0xffff)))
    case 5: return rand() < 0.5
    case 6: return ''
    case 7: return 'x'.repeat(Math.floor(rand() * 5000))
    case 8: return Array.from({ length: Math.floor(rand() * 5) }, () => garbageValue(rand, depth + 1))
    default: return Object.fromEntries(Array.from({ length: Math.floor(rand() * 5) },
      (_, i) => [`k${i}`, garbageValue(rand, depth + 1)]))
  }
}

function validStats(): Record<string, unknown> {
  return {
    generatedAtUtc: '2026-08-10T22:00:00Z',
    machine: { host: 'BEAST', os: 'Windows', processorCount: 24, totalMemoryMb: 130788, uptimeHours: 306.9 },
    systems: [{
      id: 'claude', name: 'Claude Code', installed: true, cliVersion: '2.1.226',
      running: true, processCount: 2, totalMemoryMb: 512.5,
      processes: [{ pid: 1, name: 'claude', memoryMb: 256.2, startedUtc: '2026-08-08T02:57:47Z' }],
    }],
  }
}

/** Mutate a valid payload in place at a random path — metamorphic fuzz. */
function mutatedStats(rand: () => number): unknown {
  const doc = validStats()
  if (rand() < 0.15) return garbageValue(rand) // whole-document garbage
  const targets: Array<[Record<string, unknown>, string]> = []
  const walk = (node: Record<string, unknown>): void => {
    for (const key of Object.keys(node)) {
      targets.push([node, key])
      const child = node[key]
      if (Array.isArray(child)) child.forEach(c => { if (c && typeof c === 'object') walk(c as Record<string, unknown>) })
      else if (child && typeof child === 'object') walk(child as Record<string, unknown>)
    }
  }
  walk(doc)
  const mutations = 1 + Math.floor(rand() * 4)
  for (let i = 0; i < mutations; i++) {
    const [node, key] = targets[Math.floor(rand() * targets.length)]
    if (rand() < 0.3) delete node[key]
    else node[key] = garbageValue(rand)
  }
  return doc
}

afterEach(() => vi.unstubAllGlobals())

describe('format helpers survive arbitrary input', () => {
  it('never throw and always return a string', () => {
    const rand = prng(0xf00d)
    for (let i = 0; i < 2000; i++) {
      const v = garbageValue(rand)
      // Intentional type-breaking casts: malformed input is the point of this test.
      expect(typeof formatMemoryMb(v as number)).toBe('string')
      expect(typeof formatUptimeHours(v as number)).toBe('string')
      expect(typeof formatClockTime(v as string)).toBe('string')
    }
  })
})

describe('sanitizeStats normalizes any payload', () => {
  it('always yields the exact contract shape', () => {
    const rand = prng(0xcafe)
    for (let i = 0; i < 2000; i++) {
      const result = sanitizeStats(mutatedStats(rand))
      expect(typeof result.generatedAtUtc).toBe('string')
      expect(Number.isFinite(result.machine.totalMemoryMb)).toBe(true)
      expect(Array.isArray(result.systems)).toBe(true)
      for (const system of result.systems) {
        expect(typeof system.id).toBe('string')
        expect(Array.isArray(system.processes)).toBe(true)
        expect(Number.isFinite(system.totalMemoryMb)).toBe(true)
        for (const proc of system.processes) expect(Number.isFinite(proc.memoryMb)).toBe(true)
      }
    }
  })
})

describe('components survive sanitized-from-garbage props', () => {
  it('SystemCard and MachineStrip render every mutated payload', () => {
    const rand = prng(0xdead)
    for (let i = 0; i < 300; i++) {
      const stats = sanitizeStats(mutatedStats(rand))
      const machineWrapper = mount(MachineStrip, { props: { machine: stats.machine } })
      machineWrapper.unmount()
      stats.systems.forEach((system, index) => {
        const wrapper = mount(SystemCard, { props: { system, staggerIndex: index } })
        wrapper.unmount()
      })
    }
  })
})

describe('App end-to-end with malformed /api responses', () => {
  async function clickStatWith(payload: () => Promise<Response> | Response): Promise<string[]> {
    const captured: string[] = []
    vi.stubGlobal('fetch', vi.fn((url: string) => {
      if (String(url).includes('/api/hello')) {
        return Promise.resolve(new Response(JSON.stringify({ message: 'hi', harness: 'CSharp', serverTimeUtc: '' })))
      }
      return Promise.resolve(payload())
    }))
    const wrapper = mount(App, {
      global: { config: { errorHandler: (err) => captured.push(String(err)) } },
    })
    await wrapper.find('button.stat-button').trigger('click')
    await new Promise(r => setTimeout(r, 0))
    await wrapper.vm.$nextTick()
    expect(wrapper.find('button.stat-button').exists()).toBe(true) // page still alive
    wrapper.unmount()
    return captured
  }

  it('renders mutated stats payloads without any component error', async () => {
    const rand = prng(0xbead)
    for (let i = 0; i < 120; i++) {
      const body = JSON.stringify(mutatedStats(rand)) ?? 'null'
      const errors = await clickStatWith(() => new Response(body, { status: 200 }))
      expect(errors).toEqual([])
    }
  })

  it('shows the error panel on non-JSON, non-2xx, and network failure', async () => {
    expect(await clickStatWith(() => new Response('<html>not json</html>', { status: 200 }))).toEqual([])
    expect(await clickStatWith(() => new Response('{}', { status: 503 }))).toEqual([])
    expect(await clickStatWith(() => Promise.reject(new TypeError('network down')) as never)).toEqual([])
  })
})
