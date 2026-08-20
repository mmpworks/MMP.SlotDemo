/**
 * Which article goes with which lab.
 *
 * The two halves of this series are written and built separately: nine articles in
 * docs/articles, and the chapter pages in this SPA. They line up by episode number, but
 * not perfectly — episode 1 has an article and no lab, the PAR sheet and the proving
 * ground are pages with no article of their own. Rather than leave the reader to guess,
 * the pairing is stated here once and both directions read from it.
 */

/** Article id (its file name without the extension) to chapter route id. */
const ARTICLE_TO_CHAPTER: Readonly<Record<string, string>> = {
  // Episode 1 is the system-design overview. Its lab page was never built, so a reader
  // sent to ch01 would land on a placeholder; the proving ground is the closest thing
  // the article actually describes.
  '01-system-design': 'finale',
  '02-money-and-randomness': 'ch02',
  '03-reels-and-paylines': 'ch03',
  '04-paytable-math': 'ch04',
  '05-weighted-enumeration': 'ch05',
  '06-simulation-engine': 'ch06',
  '07-games-as-data': 'ch07',
  '08-proving-the-machine': 'ch08',
  '09-optimization': 'ch09',
}

const CHAPTER_TO_ARTICLE: Readonly<Record<string, string>> = Object.fromEntries(
  Object.entries(ARTICLE_TO_CHAPTER)
    // Episode 1 points at the finale, but the finale's own reading is not article 1, so
    // the reverse direction skips it rather than sending a reader somewhere surprising.
    .filter(([article]) => article !== '01-system-design')
    .map(([article, chapter]) => [chapter, article]),
)

/** The lab that runs what this article describes, or null when there is none. */
export function chapterForArticle(articleId: string): string | null {
  return ARTICLE_TO_CHAPTER[articleId] ?? null
}

/** The article behind this lab, or null when the page has no written counterpart. */
export function articleForChapter(chapterId: string): string | null {
  return CHAPTER_TO_ARTICLE[chapterId] ?? null
}
