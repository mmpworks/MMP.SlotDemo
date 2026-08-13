// Response types for SlotDemo.Server's /api/hello and /api/stats endpoints.
// Field names follow the server's JSON; keep them in lockstep with the C#
// response models. These describe values after sanitize.ts normalizes the payload,
// so a field's type here is what a component receives: level_rank is a string
// because Herald serializes the rank as a quoted JSON value.

export interface HelloResponse {
  message: string
  harness: string
  serverTimeUtc: string
}

export interface MachineStats {
  host: string
  os: string
  processorCount: number
  totalMemoryMb: number
  uptimeHours: number
}

export interface ProcessStats {
  pid: number
  name: string
  memoryMb: number
  startedUtc: string | null
}

export interface AiSystemStats {
  id: string
  name: string
  installed: boolean
  cliVersion: string | null
  running: boolean
  processCount: number
  totalMemoryMb: number
  processes: ProcessStats[]
}

export interface StatsResponse {
  generatedAtUtc: string
  machine: MachineStats
  systems: AiSystemStats[]
}

// Event type for SlotDemo.Server's /api/logs/stream SSE endpoint.
// Each `data:` frame contains one JSON object with these fields.

export interface LogPropertyValue {
  value: unknown
  capture_mode?: string
}

export interface LogEvent {
  time: string
  level: string
  level_key: string
  level_rank: string
  category: string
  message_template: string
  message: string
  properties: Record<string, LogPropertyValue>
  context: Record<string, unknown>
}
