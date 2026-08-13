import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ComprehensionCheck from './ComprehensionCheck.vue'

const props = {
  question: 'Why keep weighted counts?',
  choices: ['To estimate', 'To preserve repeated outcomes', 'To choose a seed'],
  answer: 1,
  explanation: 'The weight records how many physical outcomes share one result.',
}

describe('ComprehensionCheck', () => {
  it('requires a choice before checking', () => {
    const wrapper = mount(ComprehensionCheck, { props })
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.find('.check__result').exists()).toBe(false)
  })

  it('explains an incorrect choice without revealing it before submission', async () => {
    const wrapper = mount(ComprehensionCheck, { props })
    await wrapper.findAll('input')[0].setValue()
    await wrapper.get('button').trigger('click')
    expect(wrapper.get('.check__result').text()).toContain('Try again.')
    expect(wrapper.get('.check__result').text()).toContain(props.explanation)
  })

  it('confirms the correct choice and shows the reason', async () => {
    const wrapper = mount(ComprehensionCheck, { props })
    await wrapper.findAll('input')[1].setValue()
    await wrapper.get('button').trigger('click')
    expect(wrapper.get('.check__result').text()).toContain('Correct.')
    expect(wrapper.get('.check__result').text()).toContain(props.explanation)
  })
})
