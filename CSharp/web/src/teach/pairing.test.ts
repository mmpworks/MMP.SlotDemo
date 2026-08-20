import { describe, expect, it } from 'vitest'
import { articleForChapter, chapterForArticle } from './pairing'

describe('article and lab pairing', () => {
  it('sends every article to a lab', () => {
    const articles = [
      '01-system-design',
      '02-money-and-randomness',
      '03-reels-and-paylines',
      '04-paytable-math',
      '05-weighted-enumeration',
      '06-simulation-engine',
      '07-games-as-data',
      '08-proving-the-machine',
      '09-optimization',
    ]
    for (const id of articles) {
      expect(chapterForArticle(id), `${id} has no lab`).not.toBeNull()
    }
  })

  it('round-trips every chapter that has an article', () => {
    for (const chapter of ['ch02', 'ch03', 'ch04', 'ch05', 'ch06', 'ch07', 'ch08', 'ch09']) {
      const article = articleForChapter(chapter)
      expect(article, `${chapter} has no article`).not.toBeNull()
      expect(chapterForArticle(article!)).toBe(chapter)
    }
  })

  it('does not claim article 1 as the finale reading', () => {
    // Article 1 links forward to the proving ground because its own lab was never built.
    // The reverse would tell a reader on the finale that article 1 is its counterpart,
    // which it is not.
    expect(chapterForArticle('01-system-design')).toBe('finale')
    expect(articleForChapter('finale')).toBeNull()
  })

  it('reports nothing for a page with no written counterpart', () => {
    expect(articleForChapter('par')).toBeNull()
    expect(articleForChapter('library')).toBeNull()
    expect(articleForChapter('home')).toBeNull()
    expect(chapterForArticle('no-such-article')).toBeNull()
  })
})
