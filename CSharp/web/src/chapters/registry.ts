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
import Teach from './Teach.vue'

export interface ChapterEntry {
  id: string
  label: string
  title: string
  blurb: string
  /** Built pages get a component; the rest render the placeholder with their blurb. */
  component: Component
  ready: boolean
  /**
   * Which band of the site this belongs to. The labs are things you run; TEACH ME is
   * things you read. They are navigated separately so a reader looking for the written
   * series is not hunting through nine lab tabs for it.
   */
  section: 'labs' | 'teach'
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
  section: 'labs',
  },
  {
    id: 'ch01',
    label: '01',
    title: 'Episode 1 — System Design',
    blurb: 'Maps the whole machine (money, RNG, reels, paytable, engine, telemetry, verdict) and names which episode builds each part. Covers the requirements, the split between exact math and lossy telemetry, and the estimate that says one process is enough.',
    component: ChapterPlaceholder,
    ready: false,
  section: 'labs',
  },
  {
    id: 'ch02',
    label: '02',
    title: 'Episode 2 — Money You Can Trust',
    blurb: 'Store one-credit wagers as integer millicents, give each worker a repeatable random stream, and map those draws fairly onto Orca Dive reel stops.',
    component: Chapter02,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch03',
    label: '03',
    title: 'Episode 3 — Reels and Paylines',
    blurb: 'Slide Orca Dive\'s visible window over its ordered reel strips, read its payline, and compare observed symbols with the PAR counts.',
    component: Chapter03,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch04',
    label: '04',
    title: 'Episode 4 — Paytable Math',
    blurb: 'Calculate how each Orca Dive award contributes to RTP, then use sigma to estimate the range a finite simulation may cover.',
    component: Chapter04,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch05',
    label: '05',
    title: 'Episode 5 — Weighted Enumeration',
    blurb: 'Use a 24-outcome teaching game to group repeated symbols without losing any physical reel outcomes.',
    component: Chapter05,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch06',
    label: '06',
    title: 'Episode 6 — The Simulation Engine',
    blurb: 'Run Orca Dive on several workers, reproduce a run from its seed, and show that dropped chart updates do not change the totals.',
    component: Chapter06,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch07',
    label: '07',
    title: 'Episode 7 — Games as Data',
    blurb: 'Load Orca Dive from JSON, check its PAR facts, and compile its strips, paytable, and Penguin Bonus into runtime objects.',
    component: Chapter07,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch08',
    label: '08',
    title: 'Episode 8 — Proving the Machine',
    blurb: 'Count all 14,781,416 Orca Dive stop combinations, then compare that reference with an independent random simulation.',
    component: Chapter08,
    ready: true,
  section: 'labs',
  },
  {
    id: 'ch09',
    label: '09',
    title: 'Episode 9 — Optimize the Machine You Proved',
    blurb: 'Run Orca Dive through the original and optimized window code, reject unequal output, and compare repeated Release measurements.',
    component: Chapter09,
    ready: true,
  section: 'labs',
  },
  {
    id: 'par',
    label: 'PAR',
    title: 'The PAR Sheet — Orca Dive',
    blurb: 'The complete Probability and Accounting Report for Orca Dive, computed live by walking every stop combination. Click any underlined label for its explanation.',
    component: ParSheet,
    ready: true,
  section: 'labs',
  },
  {
    id: 'library',
    label: 'Books',
    title: 'The Library',
    blurb: 'The books, papers, standards, and articles behind the series — slot math, PAR sheets, PRNGs, regulation, and the human cost. Each episode\'s claims are sourced from these.',
    component: Library,
    ready: true,
  section: 'labs',
  },
  {
    id: 'finale',
    label: 'Run',
    title: 'The Proving Ground',
    blurb: 'Ten million spins live, with the measured RTP settling into the analytic band as the funnel narrows.',
    component: Finale,
    ready: true,
  section: 'labs',
  },
  {
    id: 'teach',
    label: 'Teach me!',
    title: 'Teach Me — the written series',
    blurb: 'Nine articles that build the simulator from an empty repository to a machine that proves its own return.',
    component: Teach,
    ready: true,
    section: 'teach',
  },
]

/** The lab pages, in order. */
export const labChapters = chapters.filter((c) => c.section === 'labs')

/** The reading section. Separate from the labs on purpose. */
export const teachChapters = chapters.filter((c) => c.section === 'teach')

export function chapterById(id: string): ChapterEntry {
  return chapters.find((c) => c.id === id) ?? chapters[0]
}
