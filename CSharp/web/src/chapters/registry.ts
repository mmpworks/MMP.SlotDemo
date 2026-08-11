import type { Component } from 'vue'
import HarnessHome from './HarnessHome.vue'
import Chapter02 from './Chapter02.vue'
import Chapter03 from './Chapter03.vue'
import Chapter04 from './Chapter04.vue'
import Chapter05 from './Chapter05.vue'
import Chapter06 from './Chapter06.vue'
import Chapter07 from './Chapter07.vue'
import Finale from './Finale.vue'
import Library from './Library.vue'
import ParSheet from './ParSheet.vue'
import ChapterPlaceholder from './ChapterPlaceholder.vue'

export interface ChapterEntry {
  id: string
  label: string
  title: string
  blurb: string
  /** Built pages get a component; the rest render the placeholder with their blurb. */
  component: Component
  ready: boolean
}

/**
 * One row per episode. Adding a chapter page means writing the component and flipping
 * `ready` — the nav, the routing, and the page frame all read from here.
 */
export const chapters: ChapterEntry[] = [
  {
    id: 'home',
    label: 'Start',
    title: 'Slot Machine RTP Simulator — Companion Labs',
    blurb: 'One interactive page per episode. Every control runs the episode\'s own code on the server and narrates itself in the log stream below.',
    component: HarnessHome,
    ready: true,
  },
  {
    id: 'ch01',
    label: '01',
    title: 'Episode 1 — System Design',
    blurb: 'Requirements, the two-lane split between exact math and lossy telemetry, and the back-of-envelope that says one process is enough. The interactive blueprint lands here.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch02',
    label: '02',
    title: 'Episode 2 — Money You Can Trust',
    blurb: 'Millicents and SpinRng: integer money down to the bit, seeded per-worker streams, and modulo bias you can see.',
    component: Chapter02,
    ready: true,
  },
  {
    id: 'ch03',
    label: '03',
    title: 'Episode 3 — Reels and Paylines',
    blurb: 'A reel is a strip, a window is a slice of it, and the strip layout is the only source of odds in the whole engine.',
    component: Chapter03,
    ready: true,
  },
  {
    id: 'ch04',
    label: '04',
    title: 'Episode 4 — Paytable Math',
    blurb: 'One scale factor solves a paytable to a target RTP, and closed-form sigma prices the confidence band before a single spin.',
    component: Chapter04,
    ready: true,
  },
  {
    id: 'ch05',
    label: '05',
    title: 'Episode 5 — The Simulation Engine',
    blurb: 'Fixed quotas, batched atomic totals, and a lossy telemetry lane you can starve without moving the truth.',
    component: Chapter05,
    ready: true,
  },
  {
    id: 'ch06',
    label: '06',
    title: 'Episode 6 — Games as Data',
    blurb: 'A slot game as a JSON document: the loader compiles it or returns every problem at once, and declared facts are verified against the strips.',
    component: Chapter06,
    ready: true,
  },
  {
    id: 'ch07',
    label: '07',
    title: 'Episode 7 — Proving the Machine',
    blurb: 'Exhaustive enumeration referees the simulation: three implementations sharing only the game data, agreeing.',
    component: Chapter07,
    ready: true,
  },
  {
    id: 'par',
    label: 'PAR',
    title: 'The PAR Sheet — Orca Dive',
    blurb: 'The complete Probability and Accounting Report for Orca Dive, computed live by walking every stop combination. Click any underlined label for its explanation.',
    component: ParSheet,
    ready: true,
  },
  {
    id: 'library',
    label: 'Books',
    title: 'The Library',
    blurb: 'The books, papers, standards, and articles behind the series — slot math, PAR sheets, PRNGs, regulation, and the human cost. Every claim in these episodes has a shelf it came from.',
    component: Library,
    ready: true,
  },
  {
    id: 'finale',
    label: 'Run',
    title: 'The Proving Ground',
    blurb: 'Ten million spins live: the measured RTP walking into the analytic band as the funnel narrows. The whole series on one chart.',
    component: Finale,
    ready: true,
  },
]

export function chapterById(id: string): ChapterEntry {
  return chapters.find((c) => c.id === id) ?? chapters[0]
}
