import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import BitStrip from './BitStrip.vue'

/**
 * The strip is the one piece of view logic in chapter 2 that carries a rule: 64 cells,
 * most significant bit first, with the sub-credit tail marked. An off-by-one here would
 * put the credit boundary in the wrong place on camera.
 */
describe('BitStrip', () => {
  const oneCredit = (100_000).toString(2) // 100,000 millicents

  it('always renders 64 cells, padding a short input on the left', () => {
    const strip = mount(BitStrip, { props: { bits: oneCredit } })
    expect(strip.findAll('.bit-strip__bit')).toHaveLength(64)
  })

  it('keeps the value right-aligned so bit weight reads correctly', () => {
    const strip = mount(BitStrip, { props: { bits: oneCredit } })
    const rendered = strip.findAll('.bit-strip__bit').map((cell) => cell.text()).join('')
    expect(rendered).toBe(oneCredit.padStart(64, '0'))
  })

  it('marks exactly the requested number of trailing bits as sub-credit', () => {
    const strip = mount(BitStrip, { props: { bits: oneCredit, fractionBits: 17 } })
    const cells = strip.findAll('.bit-strip__bit')
    const marked = cells.filter((cell) => cell.classes('bit-strip__bit--fraction'))

    expect(marked).toHaveLength(17)
    // The marked run has to be the tail, not a slice from the middle.
    expect(cells.slice(47).every((cell) => cell.classes('bit-strip__bit--fraction'))).toBe(true)
  })

  it('marks nothing when no fraction width is given', () => {
    const strip = mount(BitStrip, { props: { bits: oneCredit } })
    expect(strip.findAll('.bit-strip__bit--fraction')).toHaveLength(0)
  })

  it('highlights set bits and leaves clear bits plain', () => {
    const strip = mount(BitStrip, { props: { bits: '1'.repeat(4) } })
    const cells = strip.findAll('.bit-strip__bit')
    expect(cells.filter((cell) => cell.classes('bit-strip__bit--set'))).toHaveLength(4)
  })
})
