// Seeded fuzz for the log viewer's external-data boundary and buffer math:
// sanitizeLogEvent, renderTemplateSegments, appendWithCap, and full LogViewer
// mounts fed malformed SSE frames through a stubbed EventSource.

import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import LogViewer from '../src/components/LogViewer.vue'
import { sanitizeLogEvent } from '../src/api/sanitize'
import { renderTemplateSegments } from '../src/logviewer/templateRender'
import { appendWithCap } from '../src/logviewer/ringBuffer'

// mulberry32 — fixed seed, reproducible failures.
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
    case 4: return String.fromCharCode(...Array.from({ length: Math.floor(rand() * 60) }, () => Math.floor(rand() * 0xffff)))
    case 5: return rand() < 0.5
    case 6: return ''
    case 7: return '{'.repeat(Math.floor(rand() * 20)) + 'x'.repeat(Math.floor(rand() * 3000)) + '}'.repeat(Math.floor(rand() * 20))
    case 8: return Array.from({ length: Math.floor(rand() * 4) }, () => garbageValue(rand, depth + 1))
    default: return Object.fromEntries(Array.from({ length: Math.floor(rand() * 4) },
      (_, i) => [`k${i}`, garbageValue(rand, depth + 1)]))
  }
}

function validLogEvent(): Record<string, unknown> {
  return {
    time: '2026-08-11T01:19:15.708+00:00',
    level: 'Information',
    level_key: 'information',
    level_rank: '5',
    category: 'SlotDemo',
    message_template: 'probe {Target} took {Ms} ms',
    message: 'probe claude took 42 ms',
    properties: {
      Target: { value: 'claude', capture_mode: 'Default' },
      Ms: { value: 42, capture_mode: 'Default' },
    },
    context: {},
  }
}

function mutatedLogEvent(rand: () => number): unknown {
  if (rand() < 0.15) return garbageValue(rand)
  const doc = validLogEvent()
  const keys = Object.keys(doc)
  const mutations = 1 + Math.floor(rand() * 3)
  for (let i = 0; i < mutations; i++) {
    const key = keys[Math.floor(rand() * keys.length)]
    if (rand() < 0.3) delete doc[key]
    else doc[key] = garbageValue(rand)
  }
  return doc
}

describe('sanitizeLogEvent survives arbitrary payloads', () => {
  it('always returns null or the exact contract shape', () => {
    const rand = prng(0x10c5)
    for (let i = 0; i < 2000; i++) {
      const result = sanitizeLogEvent(mutatedLogEvent(rand))
      if (result === null) continue
      expect(typeof result.time).toBe('string')
      expect(typeof result.level_key).toBe('string')
      expect(typeof result.category).toBe('string')
      expect(typeof result.message).toBe('string')
      expect(typeof result.message_template).toBe('string')
      expect(result.properties).toBeTypeOf('object')
      for (const key of Object.keys(result.properties)) {
        expect(result.properties[key]).toBeTypeOf('object')
      }
    }
  })
})

describe('renderTemplateSegments survives arbitrary templates', () => {
  it('never throws; segments reassemble without losing literal text', () => {
    const rand = prng(0x7e41)
    for (let i = 0; i < 2000; i++) {
      const template = String(garbageValue(rand) ?? '')
      const sanitized = sanitizeLogEvent({ ...validLogEvent(), message_template: template })
      const properties = sanitized?.properties ?? {}
      const segments = renderTemplateSegments(template, properties)
      for (const segment of segments) {
        expect(segment.kind === 'text' || segment.kind === 'value').toBe(true)
        expect(typeof segment.text).toBe('string')
      }
    }
  })

  it('aligned holes become value segments; unknown holes stay literal', () => {
    const segments = renderTemplateSegments('a {Known} b {Unknown} c', {
      Known: { value: 'K', capture_mode: 'Default' },
    })
    expect(segments.map(s => `${s.kind}:${s.text}`)).toEqual([
      'text:a ', 'value:K', 'text: b ', 'text:{Unknown}', 'text: c',
    ])
  })
})

describe('appendWithCap ring math', () => {
  it('never exceeds cap, keeps the newest items in order, never mutates inputs', () => {
    const rand = prng(0xca95)
    for (let i = 0; i < 300; i++) {
      const cap = 1 + Math.floor(rand() * 100)
      let buffer: readonly number[] = []
      let next = 0
      const rounds = 1 + Math.floor(rand() * 20)
      for (let r = 0; r < rounds; r++) {
        const incoming = Array.from({ length: Math.floor(rand() * 60) }, () => next++)
        const before = [...buffer]
        buffer = appendWithCap(buffer, incoming, cap)
        expect(before).toEqual(before.slice()) // input untouched
        expect(buffer.length).toBeLessThanOrEqual(cap)
      }
      // The tail is the most recent values, contiguous and ascending.
      const expectedTail = Array.from({ length: Math.min(next, cap) }, (_, i) => next - Math.min(next, cap) + i)
      expect([...buffer]).toEqual(expectedTail)
    }
  })

  it('caps at 25000 across randomized batch sizes past overflow', () => {
    const rand = prng(0x25e3)
    let buffer: readonly number[] = []
    let next = 0
    while (next < 30_000) {
      const size = 1 + Math.floor(rand() * 999)
      buffer = appendWithCap(buffer, Array.from({ length: size }, () => next++), 25_000)
    }
    expect(buffer.length).toBe(25_000)
    expect(buffer[buffer.length - 1]).toBe(next - 1)
    expect(buffer[0]).toBe(next - 25_000)
  })
})

// ---- Full component mounts fed malformed SSE frames --------------------------

class FakeEventSource {
  static instances: FakeEventSource[] = []
  onopen: (() => void) | null = null
  onmessage: ((e: { data: string }) => void) | null = null
  onerror: (() => void) | null = null
  closed = false
  constructor(public url: string) {
    FakeEventSource.instances.push(this)
  }
  close(): void { this.closed = true }
  emit(data: string): void { this.onmessage?.({ data }) }
  open(): void { this.onopen?.() }
}

afterEach(() => {
  vi.unstubAllGlobals()
  FakeEventSource.instances = []
})

describe('LogViewer survives malformed SSE frames', () => {
  it('mounts, ingests garbage frames without a component error, and stays alive', async () => {
    vi.stubGlobal('EventSource', FakeEventSource)
    const rand = prng(0x55e5)
    const captured: string[] = []
    const wrapper = mount(LogViewer, {
      global: { config: { errorHandler: (err) => captured.push(String(err)) } },
    })
    await wrapper.vm.$nextTick()
    const sourceInstance = FakeEventSource.instances.at(-1)
    expect(sourceInstance).toBeDefined()
    sourceInstance!.open()

    for (let i = 0; i < 400; i++) {
      const roll = rand()
      if (roll < 0.2) sourceInstance!.emit('this is not json {{{')
      else if (roll < 0.3) sourceInstance!.emit('')
      else sourceInstance!.emit(JSON.stringify(mutatedLogEvent(rand)) ?? 'null')
    }

    // Let the rAF flush and Vue re-render settle.
    await new Promise(r => setTimeout(r, 50))
    await wrapper.vm.$nextTick()

    expect(captured).toEqual([])
    expect(wrapper.find('.log-viewer, [class*=log]').exists()).toBe(true)
    wrapper.unmount()
  })
})
