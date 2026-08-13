import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import OptimizationPreview from './OptimizationPreview.vue'

const props = {
  question: 'How much does wraparound cost?',
  later: 'Episode 9 races the direct formula against wrapped drawing strips.',
}

describe('OptimizationPreview', () => {
  it('shows the question and what episode 9 does with it', () => {
    const wrapper = mount(OptimizationPreview, { props })
    expect(wrapper.get('h3').text()).toBe(props.question)
    expect(wrapper.text()).toContain(props.later)
  })

  it('navigates to the episode 9 lab through the hash the router reads', () => {
    const wrapper = mount(OptimizationPreview, { props })
    expect(wrapper.get('a').attributes('href')).toBe('#/ch09')
  })

  it('lets the browser follow the link instead of intercepting the click', () => {
    const wrapper = mount(OptimizationPreview, { props, attachTo: document.body })
    const click = new MouseEvent('click', { bubbles: true, cancelable: true })
    wrapper.get('a').element.dispatchEvent(click)
    expect(click.defaultPrevented).toBe(false)
    wrapper.unmount()
  })
})
