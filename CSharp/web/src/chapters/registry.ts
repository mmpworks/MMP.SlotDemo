import type { Component } from 'vue'
import HarnessHome from './HarnessHome.vue'
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
    blurb: 'Requirements, the two-lane split between exact math and lossy telemetry, and the back-of-envelope that says one process is enough.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch02',
    label: '02',
    title: 'Episode 2 — Money You Can Trust',
    blurb: 'Millicents and SpinRng: integer money down to the bit, seeded per-worker streams, and modulo bias you can see.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch03',
    label: '03',
    title: 'Episode 3 — Reels and Paylines',
    blurb: 'Why a reel is a strip and not a weighted die, and what a payline actually walks across.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch04',
    label: '04',
    title: 'Episode 4 — Paytable Math',
    blurb: 'Solving a paytable to a target RTP, and the closed-form sigma that draws the confidence band.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch05',
    label: '05',
    title: 'Episode 5 — The Simulation Engine',
    blurb: 'Fixed quotas over work stealing, batched atomic totals, and the bounded telemetry channel.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch06',
    label: '06',
    title: 'Episode 6 — Games as Data',
    blurb: 'A game definition as a document: schema, loader, and the validation boundary it passes through.',
    component: ChapterPlaceholder,
    ready: false,
  },
  {
    id: 'ch07',
    label: '07',
    title: 'Episode 7 — Proving the Machine',
    blurb: 'Exhaustive enumeration as a referee, and the bit-for-bit parallel-equals-sequential test.',
    component: ChapterPlaceholder,
    ready: false,
  },
]

export function chapterById(id: string): ChapterEntry {
  return chapters.find((c) => c.id === id) ?? chapters[0]
}
