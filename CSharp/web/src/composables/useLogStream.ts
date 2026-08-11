// SSE-backed ring buffer for the log viewer. Connects on mount, reconnects
// with backoff on drop, and disconnects on unmount. Incoming events are
// batched per animation frame before touching the reactive array, so a
// burst of events costs one Vue re-render per frame instead of one per event.

import { onMounted, onUnmounted, ref, shallowRef, type Ref, type ShallowRef } from 'vue'
import type { LogEvent } from '../api/types'
import { sanitizeLogEvent } from '../api/sanitize'
import { appendWithCap } from '../logviewer/ringBuffer'

export type LogStreamConnectionState = 'connecting' | 'live' | 'reconnecting'

export interface UseLogStreamOptions {
  cap?: number
  reconnectBaseDelayMs?: number
  reconnectMaxDelayMs?: number
}

export interface UseLogStream {
  events: ShallowRef<LogEvent[]>
  connectionState: Ref<LogStreamConnectionState>
  clear: () => void
}

const DEFAULT_CAP = 25_000
const DEFAULT_BASE_DELAY_MS = 500
const DEFAULT_MAX_DELAY_MS = 8_000

export function useLogStream(url: string, options: UseLogStreamOptions = {}): UseLogStream {
  const cap = options.cap ?? DEFAULT_CAP
  const baseDelayMs = options.reconnectBaseDelayMs ?? DEFAULT_BASE_DELAY_MS
  const maxDelayMs = options.reconnectMaxDelayMs ?? DEFAULT_MAX_DELAY_MS

  const events = shallowRef<LogEvent[]>([])
  const connectionState = ref<LogStreamConnectionState>('connecting')

  let source: EventSource | null = null
  let pending: LogEvent[] = []
  let flushHandle = 0
  let reconnectTimer = 0
  let reconnectDelay = baseDelayMs
  let stopped = false

  function scheduleFlush(): void {
    if (flushHandle !== 0) {
      return
    }
    flushHandle = requestAnimationFrame(flush)
  }

  function flush(): void {
    flushHandle = 0
    if (pending.length === 0) {
      return
    }
    const batch = pending
    pending = []
    events.value = appendWithCap(events.value, batch, cap)
  }

  function clear(): void {
    events.value = []
  }

  function teardownSource(): void {
    if (source) {
      source.close()
      source = null
    }
  }

  function connect(): void {
    if (stopped) {
      return
    }
    if (typeof EventSource === 'undefined') {
      // No SSE support in this environment (e.g. a test runner without a
      // browser EventSource polyfill) — stay in 'connecting' rather than
      // crashing the host component.
      return
    }
    teardownSource()
    source = new EventSource(url)

    source.onopen = () => {
      connectionState.value = 'live'
      reconnectDelay = baseDelayMs
    }

    source.onmessage = (ev: MessageEvent<string>) => {
      let parsed: unknown
      try {
        parsed = JSON.parse(ev.data)
      } catch (err) {
        console.warn('LogViewer: dropped malformed SSE frame', err)
        return
      }
      const sanitized = sanitizeLogEvent(parsed)
      if (sanitized) {
        pending.push(sanitized)
        scheduleFlush()
      }
    }

    source.onerror = () => {
      teardownSource()
      if (stopped) {
        return
      }
      connectionState.value = 'reconnecting'
      reconnectTimer = window.setTimeout(() => {
        reconnectDelay = Math.min(reconnectDelay * 2, maxDelayMs)
        connect()
      }, reconnectDelay)
    }
  }

  onMounted(connect)

  onUnmounted(() => {
    stopped = true
    window.clearTimeout(reconnectTimer)
    if (flushHandle !== 0) {
      cancelAnimationFrame(flushHandle)
    }
    teardownSource()
  })

  return { events, connectionState, clear }
}
