import { describe, expect, it } from 'vitest'
import { messageSegments, renderTemplateSegments } from './templateRender'
import type { LogPropertyValue } from '../api/types'

describe('renderTemplateSegments', () => {
  it('splits literal text and holes with resolved property values', () => {
    const properties: Record<string, LogPropertyValue> = {
      Protocol: { value: 'HTTP/1.1' },
      Path: { value: '/api/hello' },
    }
    const segments = renderTemplateSegments('Request starting {Protocol} {Path}', properties)
    expect(segments).toEqual([
      { kind: 'text', text: 'Request starting ' },
      { kind: 'value', text: 'HTTP/1.1' },
      { kind: 'text', text: ' ' },
      { kind: 'value', text: '/api/hello' },
    ])
  })

  it('leaves an unresolved hole as literal text', () => {
    const segments = renderTemplateSegments('Missing {Unknown} here', {})
    expect(segments).toEqual([
      { kind: 'text', text: 'Missing ' },
      { kind: 'text', text: '{Unknown}' },
      { kind: 'text', text: ' here' },
    ])
  })

  it('returns an empty array for an empty template', () => {
    expect(renderTemplateSegments('', {})).toEqual([])
  })

  it('stringifies object property values', () => {
    const segments = renderTemplateSegments('{Payload}', { Payload: { value: { a: 1 } } })
    expect(segments).toEqual([{ kind: 'value', text: '{"a":1}' }])
  })
})

describe('messageSegments', () => {
  it('falls back to the flat message when the template is empty', () => {
    const segments = messageSegments('', 'plain fallback message', {})
    expect(segments).toEqual([{ kind: 'text', text: 'plain fallback message' }])
  })

  it('prefers the rebuilt template when one is available', () => {
    const segments = messageSegments('Hello {Name}', 'Hello World', { Name: { value: 'World' } })
    expect(segments).toEqual([
      { kind: 'text', text: 'Hello ' },
      { kind: 'value', text: 'World' },
    ])
  })
})
