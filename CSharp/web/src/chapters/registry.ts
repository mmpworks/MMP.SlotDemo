import type { Component } from 'vue'
import HarnessHome from './HarnessHome.vue'
import Chapter02 from './Chapter02.vue'
import Chapter03 from './Chapter03.vue'
import Chapter04 from './Chapter04.vue'
import Chapter05 from './Chapter05.vue'
import Chapter06 from './Chapter06.vue'
import Chapter07 from './Chapter07.vue'
import Chapter08 from './Chapter08.vue'
import Chapter09 from './Chapter09.vue'
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
    blurb: 'One interactive page per episode. Every control runs the episode\'s own code on the server, and each step logs to the stream below.',
    component: HarnessHome,
    ready: true,
  },
  {
    id: 'ch01',
    label: '01',
    title: 'Episode 1 — System Design',
    blurb: 'Maps the whole machine (money, RNG, reels, paytable, engine, telemetry, verdict) and names which episode builds each part. Covers the requirements, the split between exact math and lossy telemetry, and the estimate that says one process is enough.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch02',
    label: '02',
    title: 'Episode 2 — Money You Can Trust',
    blurb: 'Millicents counts money in whole units a hundred-thousandth of a credit wide, so totals stay exact. SpinRng gives each worker its own seeded stream. A third lab shows modulo bias.',
    component: Chapter02,
    ready: true,
  },
  {
    id: 'ch03',
    label: '03',
    title: 'Episode 3 — Reels and Paylines',
    blurb: 'A reel is a strip of symbols and a window shows a few of them. Nothing else in the engine sets the odds.',
    component: Chapter03,
    ready: true,
  },
  {
    id: 'ch04',
    label: '04',
    title: 'Episode 4 — Paytable Math',
    blurb: 'One scale factor solves a paytable to a target RTP, and closed-form sigma gives the confidence band without running the game.',
    component: Chapter04,
    ready: true,
  },
  {
    id: 'ch05',
    label: '05',
    title: 'Episode 5 — Weighted Enumeration',
    blurb: 'Group repeated reel symbols, carry their counts as weights, and get the same exact answer with less work.',
    component: Chapter05,
    ready: true,
  },
  {
    id: 'ch06',
    label: '06',
    title: 'Episode 6 — The Simulation Engine',
    blurb: 'Fixed worker quotas make a parallel run repeatable. Progress updates may be dropped without changing the totals.',
    component: Chapter06,
    ready: true,
  },
  {
    id: 'ch07',
    label: '07',
    title: 'Episode 7 — Games as Data',
    blurb: 'A slot game becomes a JSON document. The loader either compiles it or returns a useful list of problems.',
    component: Chapter07,
    ready: true,
  },
  {
    id: 'ch08',
    label: '08',
    title: 'Episode 8 — Proving the Machine',
    blurb: 'Exhaustive counting gives an exact answer. Simulation should move toward that answer as more spins are played.',
    component: Chapter08,
    ready: true,
  },
  {
    id: 'ch09',
    label: '09',
    title: 'Episode 9 — Optimize the Machine You Proved',
    blurb: 'Keep the clear first version, measure the hot path, and accept only optimizations that preserve identical output and improve repeated benchmarks.',
    component: Chapter09,
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
    blurb: 'The books, papers, standards, and articles behind the series — slot math, PAR sheets, PRNGs, regulation, and the human cost. Each episode\'s claims are sourced from these.',
    component: Library,
    ready: true,
  },
  {
    id: 'finale',
    label: 'Run',
    title: 'The Proving Ground',
    blurb: 'Ten million spins live, with the measured RTP settling into the analytic band as the funnel narrows.',
    component: Finale,
    ready: true,
  },
]

export function chapterById(id: string): ChapterEntry {
  return chapters.find((c) => c.id === id) ?? chapters[0]
}
