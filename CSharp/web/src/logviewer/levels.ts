// Level metadata for the log viewer: canonical rank order, display labels,
// and the sys.*/plain pairing used by the filter control and row coloring.
// Framework-free, so the filter UI and row coloring share one source of truth
// without importing Vue.

export type LevelKey =
  | 'sys.verbose'
  | 'verbose'
  | 'sys.debug'
  | 'debug'
  | 'sys.information'
  | 'information'
  | 'sys.warning'
  | 'warning'
  | 'error'
  | 'fatal'

export type LevelOrigin = 'framework' | 'app'

export interface LevelMeta {
  key: LevelKey
  label: string
  /** The sys/plain counterpart in the same family, or null for error/fatal (no pair). */
  pairKey: LevelKey | null
  origin: LevelOrigin
}

// Rank order as specified by the server contract — lowest severity first.
export const LEVEL_ORDER: readonly LevelKey[] = [
  'sys.verbose',
  'verbose',
  'sys.debug',
  'debug',
  'sys.information',
  'information',
  'sys.warning',
  'warning',
  'error',
  'fatal',
]

export const LEVEL_META: Readonly<Record<LevelKey, LevelMeta>> = {
  'sys.verbose': { key: 'sys.verbose', label: 'Verbose', pairKey: 'verbose', origin: 'framework' },
  verbose: { key: 'verbose', label: 'Verbose', pairKey: 'sys.verbose', origin: 'app' },
  'sys.debug': { key: 'sys.debug', label: 'Debug', pairKey: 'debug', origin: 'framework' },
  debug: { key: 'debug', label: 'Debug', pairKey: 'sys.debug', origin: 'app' },
  'sys.information': { key: 'sys.information', label: 'Information', pairKey: 'information', origin: 'framework' },
  information: { key: 'information', label: 'Information', pairKey: 'sys.information', origin: 'app' },
  'sys.warning': { key: 'sys.warning', label: 'Warning', pairKey: 'warning', origin: 'framework' },
  warning: { key: 'warning', label: 'Warning', pairKey: 'sys.warning', origin: 'app' },
  error: { key: 'error', label: 'Error', pairKey: null, origin: 'app' },
  fatal: { key: 'fatal', label: 'Fatal', pairKey: null, origin: 'app' },
}

// Families group the sys/plain pairs for the compact paired-toggle filter UI.
// Singles (error, fatal) render as one toggle each.
export interface LevelFamily {
  label: string
  sysKey: LevelKey | null
  appKey: LevelKey
}

export const LEVEL_FAMILIES: readonly LevelFamily[] = [
  { label: 'Verbose', sysKey: 'sys.verbose', appKey: 'verbose' },
  { label: 'Debug', sysKey: 'sys.debug', appKey: 'debug' },
  { label: 'Information', sysKey: 'sys.information', appKey: 'information' },
  { label: 'Warning', sysKey: 'sys.warning', appKey: 'warning' },
  { label: 'Error', sysKey: null, appKey: 'error' },
  { label: 'Fatal', sysKey: null, appKey: 'fatal' },
]

// Single source for level -> CSS custom-property name, so LogRow (message
// text) and the filter chips (toolbar) color themselves identically without
// duplicating a 10-entry color switch in two components.
export const LEVEL_COLOR_VAR: Readonly<Record<LevelKey, string>> = {
  'sys.verbose': '--color-log-sys-verbose',
  verbose: '--color-log-verbose',
  'sys.debug': '--color-log-sys-debug',
  debug: '--color-log-debug',
  'sys.information': '--color-log-sys-information',
  information: '--color-log-information',
  'sys.warning': '--color-log-sys-warning',
  warning: '--color-log-warning',
  error: '--color-log-error',
  fatal: '--color-log-fatal',
}

export function levelColor(key: LevelKey): string {
  return `var(${LEVEL_COLOR_VAR[key]})`
}

export function isKnownLevelKey(value: string): value is LevelKey {
  return (LEVEL_ORDER as readonly string[]).includes(value)
}

/** Falls back to 'information' for any level_key the server contract doesn't yet cover. */
export function normalizeLevelKey(value: string): LevelKey {
  return isKnownLevelKey(value) ? value : 'information'
}
