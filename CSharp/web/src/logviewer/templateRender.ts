// Rebuilds a log line from `message_template` + `properties`: literal text
// stays as-is, `{Name}` holes become distinct "value" segments so the
// component can render them with the interpolated-value accent color.
// Pure and framework-free so the parsing math is unit-testable.

import type { LogPropertyValue } from '../api/types'

export type TemplateSegmentKind = 'text' | 'value'

export interface TemplateSegment {
  kind: TemplateSegmentKind
  text: string
}

function formatPropertyValue(value: unknown): string {
  if (value === undefined) {
    return ''
  }
  if (value === null) {
    return 'null'
  }
  if (typeof value === 'object') {
    try {
      return JSON.stringify(value)
    } catch {
      return String(value)
    }
  }
  return String(value)
}

const HOLE_PATTERN = /\{(?<name>[^{}]+)\}/g

/**
 * Splits a message template into text/value segments. A hole whose name
 * isn't present in `properties` is treated as literal text (the `{Name}`
 * survives unchanged) rather than dropped, so malformed templates degrade
 * gracefully instead of losing content.
 */
export function renderTemplateSegments(
  template: string,
  properties: Record<string, LogPropertyValue> | undefined,
): TemplateSegment[] {
  if (!template) {
    return []
  }

  const segments: TemplateSegment[] = []
  let lastIndex = 0
  HOLE_PATTERN.lastIndex = 0

  let match: RegExpExecArray | null
  while ((match = HOLE_PATTERN.exec(template)) !== null) {
    if (match.index > lastIndex) {
      segments.push({ kind: 'text', text: template.slice(lastIndex, match.index) })
    }

    const name = match.groups?.name ?? ''
    const property = properties?.[name]
    if (property !== undefined) {
      segments.push({ kind: 'value', text: formatPropertyValue(property.value) })
    } else {
      segments.push({ kind: 'text', text: match[0] })
    }

    lastIndex = HOLE_PATTERN.lastIndex
  }

  if (lastIndex < template.length) {
    segments.push({ kind: 'text', text: template.slice(lastIndex) })
  }

  return segments
}

/**
 * The component's fallback rule: use the rebuilt template when there is one
 * to rebuild, otherwise fall back to the flat `message` string.
 */
export function messageSegments(
  template: string,
  message: string,
  properties: Record<string, LogPropertyValue> | undefined,
): TemplateSegment[] {
  const rebuilt = renderTemplateSegments(template, properties)
  return rebuilt.length > 0 ? rebuilt : [{ kind: 'text', text: message }]
}
