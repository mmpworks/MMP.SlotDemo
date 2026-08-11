// Boundary validation: the wire payload is external data, so it gets normalized
// into the StatsResponse shape before any component sees it. Malformed or hostile
// JSON degrades to placeholder values, so components downstream always receive a
// well-shaped object.

import type { AiSystemStats, LogEvent, LogPropertyValue, MachineStats, ProcessStats, StatsResponse } from './types'

function num(value: unknown, fallback = 0): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function str(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : fallback
}

function strOrNull(value: unknown): string | null {
  return typeof value === 'string' ? value : null
}

function bool(value: unknown): boolean {
  return value === true
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
}

function arr(value: unknown): unknown[] {
  return Array.isArray(value) ? value : []
}

function sanitizeProcess(raw: unknown): ProcessStats {
  const p = record(raw)
  return {
    pid: num(p.pid),
    name: str(p.name, '?'),
    memoryMb: num(p.memoryMb),
    startedUtc: strOrNull(p.startedUtc),
  }
}

function sanitizeSystem(raw: unknown, index: number): AiSystemStats {
  const s = record(raw)
  return {
    id: str(s.id, `system-${index}`),
    name: str(s.name, 'Unknown system'),
    installed: bool(s.installed),
    cliVersion: strOrNull(s.cliVersion),
    running: bool(s.running),
    processCount: num(s.processCount),
    totalMemoryMb: num(s.totalMemoryMb),
    processes: arr(s.processes).map(sanitizeProcess),
  }
}

function sanitizeMachine(raw: unknown): MachineStats {
  const m = record(raw)
  return {
    host: str(m.host, '?'),
    os: str(m.os, '?'),
    processorCount: num(m.processorCount),
    totalMemoryMb: num(m.totalMemoryMb),
    uptimeHours: num(m.uptimeHours),
  }
}

export function sanitizeStats(raw: unknown): StatsResponse {
  const r = record(raw)
  return {
    generatedAtUtc: str(r.generatedAtUtc),
    machine: sanitizeMachine(r.machine),
    systems: arr(r.systems).map(sanitizeSystem),
  }
}

function sanitizePropertyValue(raw: unknown): LogPropertyValue {
  const p = record(raw)
  return {
    value: 'value' in p ? p.value : undefined,
    capture_mode: typeof p.capture_mode === 'string' ? p.capture_mode : undefined,
  }
}

function sanitizeProperties(raw: unknown): Record<string, LogPropertyValue> {
  const p = record(raw)
  const out: Record<string, LogPropertyValue> = {}
  for (const key of Object.keys(p)) {
    out[key] = sanitizePropertyValue(p[key])
  }
  return out
}

// A log frame with no message is not useful to render — the SSE parser
// treats a null return as "skip this frame" rather than showing a blank row.
export function sanitizeLogEvent(raw: unknown): LogEvent | null {
  const e = record(raw)
  if (typeof e.message !== 'string') {
    return null
  }
  return {
    time: str(e.time),
    level: str(e.level, 'Information'),
    level_key: str(e.level_key, 'information'),
    level_rank: str(e.level_rank, '0'),
    category: str(e.category),
    message_template: str(e.message_template),
    message: e.message,
    properties: sanitizeProperties(e.properties),
    context: record(e.context),
  }
}
