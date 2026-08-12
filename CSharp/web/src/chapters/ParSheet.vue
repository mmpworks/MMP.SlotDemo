<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import PaylinePattern from '../components/PaylinePattern.vue'

defineProps<{ title: string; blurb: string }>()

interface ParSheet {
  supported: boolean
  reason?: string
  game: {
    name: string
    reels: number
    rows: number
    stopsPerReel: number[]
    cycle: number
    paylines: { name: string; rows: number[] }[]
    betCredits: number
  }
  census: {
    id: number
    name: string
    isWild: boolean
    isScatter: boolean
    perReel: number[]
    total: number
  }[]
  strips: number[][]
  paytable: {
    category: string
    kind: string
    count: number
    payMultiplier: number
    hits: number
    probability: number
    hitRate: number
    rtpSlice: number
  }[]
  totals: {
    lineRtp: number
    bonusRtp: number
    totalRtp: number
    hitCombinations: number
    lineHitFrequency: number
    lineHitRate: number
  }
  feature: {
    name: string
    scatter: string
    requiredReels: number[]
    triggerProbability: number
    triggerRate: number
    prizeTiers: { value: number; count: number }[]
    prizes: number
    blanks: number
    consolation: number
    giftCount: number
    prizeTotal: number
    maxAward: number
    meanAward: number
    variance: number
    rtpContribution: number
  } | null
  volatility: {
    sigma: number
    lineSigma: number
    bonusSigma: number
    volatilityIndex: { level: string; vi: number }[]
    bands: { spins: number; halfWidth95: number; halfWidth99: number }[]
  }
  verification: { scatterReels: { reel: number; positions: number[]; separated: boolean }[] }
}

interface SummaryRow {
  file: string
  name: string
  reels: number
  lines: number
  hasScatter: boolean
  wagerCredits: number
  symbolsPerReel: string
  cycle: number
  paybackPercent: number
  lineHitFrequencyPercent: number
  playsPerJackpot: number
  jackpotCredits: number
  playsPerBonus: number | null
  volatilityIndex90: number
}

const sheet = ref<ParSheet | null>(null)
const summary = ref<SummaryRow[]>([])
const error = ref('')
const explainKey = ref('sheet')

onMounted(async () => {
  try {
    sheet.value = await postJson<ParSheet>('/api/par/sheet', { gameFile: 'orca-dive.json' })
    summary.value = await getJson<SummaryRow[]>('/api/par/summary')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the PAR sheet.'
  }
})

/**
 * The teaching layer: every underlined element on the sheet selects one of these.
 * Written at a high-school level; the sheet shows the numbers, this panel says what
 * each one is for.
 */
const explanations: Record<string, { title: string; body: string }> = {
  sheet: {
    title: 'What a PAR sheet is',
    body: 'PAR stands for Probability and Accounting Report. It is the design document of a slot machine: the reel strips, the paytable, and every probability and return figure that follows from them. Casinos and regulators read it to know what a machine will do over millions of plays. Click any underlined label on this page to have that part explained here. Everything shown is computed live by walking all 14,781,416 stop combinations of the game.',
  },
  cycle: {
    title: 'Cycle',
    body: 'The cycle is the total number of distinct stop combinations: multiply the strip lengths together (26 × 29 × 26 × 29 × 26 = 14,781,416). Every probability on this sheet is a count of combinations divided by the cycle. A "full cycle" analysis visits each combination once, which is why these numbers are exact rather than estimated.',
  },
  geometry: {
    title: 'Geometry',
    body: 'Five reels, each its own strip with its own length, showing three rows in the window. Ragged strip lengths (26/29/26/29/26) are normal on real machines. This game reads one payline across the centre row; more paylines would change the feel but, for regular line pays, RTP stays the same because bet and wins scale together.',
  },
  summary: {
    title: 'The summary table',
    body: 'This is the column layout researchers found on real PAR sheets. In 2009, Kevin Harrigan and Mike Dixon obtained 23 PAR sheets for Ontario slot machines through freedom-of-information requests and published a summary in this column layout (their Table 1): game and geometry, wager, symbols per reel, payback percentage, hit frequency, plays per jackpot, jackpot size, plays per bonus, and volatility index. Our games appear here in the same columns, so you can read this row against their published rows for Double Diamond Deluxe (92.6% payback, VI 10.5) or Lobstermania (85 to 96.2% across versions).',
  },
  playsPerJackpot: {
    title: 'Plays per jackpot',
    body: 'The average number of spins before the top prize lands: cycle divided by the hit count of the jackpot rule. The 5000× Red 7 line has 4 hits in a 14.8-million cycle, so about one jackpot per 3.7 million spins. The paper reports 46,656 for Double Diamond and 8.1 million for Lobstermania. Rarity on this scale is normal, and it keeps the jackpot\'s RTP slice small.',
  },
  versions: {
    title: 'Approved versions',
    body: 'Casinos order the same cabinet in several approved payback versions. Lobstermania ran from 85% to 96.2%, and the versions look identical to the player because hit frequency barely changes between them. Payback is the knob the operator picks; the feel of the game stays put. Our preset picker on the proving-ground page works the same way: same reels, different solved paytable.',
  },
  census: {
    title: 'Reel census',
    body: 'How many times each symbol appears on each strip. This table sets the odds: a symbol\'s chance of landing on a payline cell is its count divided by that strip\'s length. The machine holds no separate probability table. The census differs per reel: Wild Orca is 2-in-26 on reel 1 but 1-in-29 on reel 2.',
  },
  wild: {
    title: 'Wild symbol (★)',
    body: 'Wild Orca substitutes for the four fish symbols, extending their runs. It substitutes for nothing else, because a wild that stood in for the sevens would inflate their rare, high pays. It also pays on its own, down to a single symbol on reel 1. When one line matches two rules, the highest pay wins, and ties go to the longer run.',
  },
  scatter: {
    title: 'Scatter symbol (◆)',
    body: 'The Penguin triggers the bonus when it is visible anywhere in the window on reels 1, 3, and 5 at once. Scatters count window area: with 2 Penguins on a 26-stop strip and a 3-row window, 6 of 26 stops show one. Penguin lives only on reels 1, 3, and 5 because those are the reels the bonus requires.',
  },
  paytable: {
    title: 'Paytable',
    body: 'Each row is one pay rule: this category, at this run length, pays this multiple of the total bet. The rest of the row is what the enumeration adds: how many of the cycle\'s combinations land that win, the probability, how rare it feels (one in N spins), and the slice of RTP the rule contributes. RTP contributions are additive, so a designer tunes one row at a time.',
  },
  hits: {
    title: 'Hits (combinations)',
    body: 'How many of the 14,781,416 stop combinations produce this exact win. Textbook formulas compute these as products of per-reel symbol counts, then subtract overlaps where a line matches two rules ("prioritisation discounts"). Example: naive counting gives exactly-four Red 7s 100 hits, but 20 of those carry another seven on reel 5 and pay as Mixed 7 five-of-a-kind instead, leaving the 80 shown here.',
  },
  probability: {
    title: 'Probability and hit rate',
    body: 'Probability is hits divided by cycle. Hit rate is its reciprocal, one such win every N spins, and it is how designers talk about pacing. Red 7 five-of-a-kind is p = 0.00000027, or once in 3.7 million spins.',
  },
  rtpSlice: {
    title: 'RTP slice',
    body: 'Pay times probability: the fraction of every wagered credit this rule gives back over the long run. Sum the column and you get the line RTP. Sort by this column and the 2× single Wild Orca contributes more than the 5000× Red 7 jackpot: it pays less but lands far more often.',
  },
  lineRtp: {
    title: 'Line RTP, bonus RTP, total',
    body: 'Line RTP is the paytable column summed. Bonus RTP is the trigger probability times the average bonus award. Total is their sum: 86.11% here, meaning the game keeps about 13.9 cents of every wagered dollar over the long run. The total is the number regulators certify and casinos order by.',
  },
  hitFrequency: {
    title: 'Hit frequency',
    body: 'The share of spins that win anything on the line: 10.26%, or one hit every 9.75 spins. This figure counts line wins only. A simulator that counts bonus triggers as hits reports a slightly higher number (~11.4%), so a PAR sheet states which definition it uses.',
  },
  feature: {
    title: 'The bonus feature',
    body: 'Three Penguins (reels 1, 3, 5) start a pick game: a bag of 30 gifts (24 prizes and 6 blanks) picked one at a time until a blank, keeping what was picked plus a 1× consolation. Because picking is without replacement, the average award has an exact closed form: 21.57 bets. Trigger rate times average award gives the bonus\'s 26.51% RTP slice.',
  },
  prizeTiers: {
    title: 'Prize tiers',
    body: 'The bag\'s contents by value. Many small prizes and few large ones shape the bonus the same way the paytable shapes the base game. The 5× tier carries more of the expected value than any other: nine prizes at 5× come to 45 of the bag\'s 144, ahead of the single 20× and the three 10× together. The max award is every prize plus the consolation, 145× bet.',
  },
  volatility: {
    title: 'Sigma and the volatility index',
    body: 'Two games with the same RTP can feel very different: steady dribbles at one extreme, rare jackpots at the other. Sigma (standard deviation per unit wagered) measures that spread, 6.13 here, moderate for a 5-reel game. The volatility index is sigma times a confidence factor z (1.64, 1.96, or 2.58 for 90/95/99%), which is the form regulators ask for.',
  },
  bands: {
    title: 'Confidence bands',
    body: 'After N spins, measured RTP should sit within ±z·σ/√N of the true figure. The table shows the band tightening with the square root of N: 100× the spins buys 10× the certainty. The proving-ground page draws this same band as a funnel.',
  },
  verification: {
    title: 'Scatter separation check',
    body: 'A classic PAR-sheet trap: window-area scatter math assumes scatters sit far enough apart that two never show in one window. If a strip edit put two Penguins within 3 stops, the shipped trigger rate would silently drift from the model. This check reads the actual strips: Penguins sit at stops 0 and 13 on each scatter reel, far enough apart that the exact enumeration equals the clean (6/26)³ form. A regression test pins this.',
  },
  paylines: {
    title: 'Payline patterns',
    body: 'A payline is a fixed path across the window: one row picked per reel. The diagram shows which cell each reel contributes. Orca Dive reads a single Centre line, the classic single-line shape. Multi-line games add paths (Top, Bottom, V, zigzags), and for ordinary line pays the extra lines leave RTP unchanged, because the bet scales with the line count. The chapter 3 lab draws every pattern for the multi-line presets.',
  },
  strips: {
    title: 'Strip order',
    body: 'The full ordered strips, exactly as the window slides over them. Order changes nothing for single-line pays (only counts matter) but everything for scatters and multi-row windows, because adjacent stops enter the window together. Strip order is the part of a real PAR sheet that stays confidential. Orca Dive\'s is in `games/orca-dive.json`, so you can read it against the counts above.',
  },
}

const explain = computed(() => explanations[explainKey.value] ?? explanations.sheet)

function pct(v: number, digits = 4): string {
  return `${(v * 100).toFixed(digits)}%`
}

function oneIn(v: number): string {
  if (v <= 0) return '—'
  return v >= 100 ? `1 in ${Math.round(v).toLocaleString()}` : `1 in ${v.toFixed(1)}`
}

const symbolName = computed(() => {
  const map = new Map(sheet.value?.census.map((s) => [s.id, s.name]) ?? [])
  return (id: number) => map.get(id) ?? String(id)
})
</script>

<template>
  <article>
    <header class="chapter-head">
      <h2>{{ title }}</h2>
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <p v-if="error" class="lab__error">{{ error }}</p>

    <div v-if="sheet?.supported" class="par-layout">
      <div class="par-doc">
        <!-- header block -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'sheet'">
            PAR — Probability &amp; Accounting Report
          </button>
          <div class="par-header">
            <div><span class="par-label">Game</span><span class="mono">{{ sheet.game.name }}</span></div>
            <div>
              <button class="par-term par-label" @click="explainKey = 'geometry'">Geometry</button>
              <span class="mono">{{ sheet.game.reels }} reels × {{ sheet.game.rows }} rows, stops {{ sheet.game.stopsPerReel.join('/') }}</span>
            </div>
            <div>
              <button class="par-term par-label" @click="explainKey = 'cycle'">Cycle</button>
              <span class="mono">{{ sheet.game.cycle.toLocaleString() }}</span>
            </div>
            <div><span class="par-label">Bet</span><span class="mono">{{ sheet.game.betCredits }} credit, {{ sheet.game.paylines.length }} payline</span></div>
          </div>
          <div class="par-lines">
            <button class="par-term par-label" @click="explainKey = 'paylines'">Payline patterns</button>
            <div class="par-lines__row">
              <div v-for="line in sheet.game.paylines" :key="line.name" class="par-lines__item">
                <PaylinePattern :rows="line.rows" :window-rows="sheet.game.rows" active />
                <span class="mono">{{ line.name }}</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Harrigan Table-1 summary -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'summary'">
            Summary — after Harrigan &amp; Dixon (2009), Table 1
          </button>
          <div class="par-scroll">
            <table class="lab-table">
              <thead>
                <tr>
                  <th>Game / reels / lines / scatter</th>
                  <th>Wager</th>
                  <th>Symbols per reel</th>
                  <th>Payback %</th>
                  <th>Hit freq % (line)</th>
                  <th><button class="par-term" @click="explainKey = 'playsPerJackpot'">Plays per jackpot</button></th>
                  <th>Jackpot (× bet)</th>
                  <th>Plays per bonus</th>
                  <th>VI (90%)</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in summary" :key="row.file" :class="{ 'par-row--current': row.file === 'orca-dive.json' }">
                  <td>{{ row.name }} / {{ row.reels }} / {{ row.lines }}{{ row.hasScatter ? ' / S' : '' }}</td>
                  <td>{{ row.wagerCredits }}</td>
                  <td>{{ row.symbolsPerReel }}</td>
                  <td>{{ row.paybackPercent.toFixed(2) }}</td>
                  <td>{{ row.lineHitFrequencyPercent.toFixed(1) }}</td>
                  <td>{{ Math.round(row.playsPerJackpot).toLocaleString() }}</td>
                  <td>{{ row.jackpotCredits.toLocaleString() }}</td>
                  <td>{{ row.playsPerBonus === null ? 'n/a' : Math.round(row.playsPerBonus).toLocaleString() }}</td>
                  <td>{{ row.volatilityIndex90.toFixed(1) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p class="lab-note">
            Column set from the published study of 23 real Ontario PAR sheets
            (<button class="par-term" @click="explainKey = 'versions'">multiple approved versions</button>
            of one game differ mainly in this table's payback column). Their mechanical
            games reported VI at the 90% confidence level; we compute ours the same way.
          </p>
        </section>

        <!-- reel census -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'census'">Reel census</button>
          <table class="lab-table">
            <thead>
              <tr>
                <th>Symbol</th>
                <th v-for="r in sheet.game.reels" :key="r">R{{ r }}</th>
                <th>Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in sheet.census" :key="s.id">
                <td>
                  <button
                    v-if="s.isWild || s.isScatter"
                    class="par-term"
                    @click="explainKey = s.isWild ? 'wild' : 'scatter'"
                  >{{ s.name }} {{ s.isWild ? '★' : '◆' }}</button>
                  <template v-else>{{ s.name }}</template>
                </td>
                <td v-for="(c, i) in s.perReel" :key="i" :class="{ 'par-zero': c === 0 }">{{ c }}</td>
                <td>{{ s.total }}</td>
              </tr>
              <tr class="par-sum">
                <td>Strip length</td>
                <td v-for="(len, i) in sheet.game.stopsPerReel" :key="i">{{ len }}</td>
                <td>{{ sheet.game.stopsPerReel.reduce((a, b) => a + b, 0) }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <!-- paytable -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'paytable'">Paytable &amp; line analysis</button>
          <table class="lab-table">
            <thead>
              <tr>
                <th>Category</th>
                <th>Count</th>
                <th>Pays</th>
                <th><button class="par-term" @click="explainKey = 'hits'">Hits</button></th>
                <th><button class="par-term" @click="explainKey = 'probability'">Probability</button></th>
                <th><button class="par-term" @click="explainKey = 'probability'">Hit rate</button></th>
                <th><button class="par-term" @click="explainKey = 'rtpSlice'">RTP slice</button></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in sheet.paytable" :key="`${row.category}-${row.count}`">
                <td>{{ row.category }}</td>
                <td>{{ row.count }}</td>
                <td>{{ row.payMultiplier }}×</td>
                <td>{{ row.hits.toLocaleString() }}</td>
                <td>{{ row.probability.toExponential(3) }}</td>
                <td>{{ oneIn(row.hitRate) }}</td>
                <td>{{ pct(row.rtpSlice, 3) }}</td>
              </tr>
            </tbody>
          </table>
          <div class="par-totals">
            <button class="par-term" @click="explainKey = 'lineRtp'">
              Line RTP <span class="mono">{{ pct(sheet.totals.lineRtp) }}</span>
            </button>
            <button class="par-term" @click="explainKey = 'lineRtp'">
              Bonus RTP <span class="mono">{{ pct(sheet.totals.bonusRtp) }}</span>
            </button>
            <button class="par-term par-totals__grand" @click="explainKey = 'lineRtp'">
              Total RTP <span class="mono">{{ pct(sheet.totals.totalRtp) }}</span>
            </button>
            <button class="par-term" @click="explainKey = 'hitFrequency'">
              Line hit frequency <span class="mono">{{ pct(sheet.totals.lineHitFrequency, 2) }} ({{ oneIn(sheet.totals.lineHitRate) }})</span>
            </button>
          </div>
        </section>

        <!-- feature -->
        <section v-if="sheet.feature" class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'feature'">
            Feature — {{ sheet.feature.name }}
          </button>
          <div class="par-header">
            <div><span class="par-label">Trigger</span><span class="mono">{{ sheet.feature.scatter }} ◆ visible on reels {{ sheet.feature.requiredReels.join(', ') }}</span></div>
            <div><span class="par-label">Trigger rate</span><span class="mono">{{ oneIn(sheet.feature.triggerRate) }} ({{ pct(sheet.feature.triggerProbability, 4) }})</span></div>
            <div><span class="par-label">Average award</span><span class="mono">{{ sheet.feature.meanAward.toFixed(3) }}× bet</span></div>
            <div><span class="par-label">RTP contribution</span><span class="mono">{{ pct(sheet.feature.rtpContribution) }}</span></div>
          </div>
          <button class="par-term" @click="explainKey = 'prizeTiers'">Prize bag</button>
          <table class="lab-table par-tiers">
            <thead>
              <tr><th>Prize</th><th v-for="t in sheet.feature.prizeTiers" :key="t.value">{{ t.value }}×</th><th>Blanks</th></tr>
            </thead>
            <tbody>
              <tr>
                <td>Count</td>
                <td v-for="t in sheet.feature.prizeTiers" :key="t.value">{{ t.count }}</td>
                <td>{{ sheet.feature.blanks }}</td>
              </tr>
            </tbody>
          </table>
          <p class="lab-note">
            Pick until a blank; keep what you picked plus {{ sheet.feature.consolation }}× consolation.
            {{ sheet.feature.giftCount }} gifts in the bag, prize total {{ sheet.feature.prizeTotal }}×,
            maximum award {{ sheet.feature.maxAward }}× bet.
          </p>
        </section>

        <!-- volatility -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'volatility'">Volatility</button>
          <div class="par-header">
            <div><span class="par-label">Sigma / unit wagered</span><span class="mono">{{ sheet.volatility.sigma.toFixed(4) }}</span></div>
            <div v-for="vi in sheet.volatility.volatilityIndex" :key="vi.level">
              <span class="par-label">VI {{ vi.level }}</span><span class="mono">{{ vi.vi.toFixed(3) }}</span>
            </div>
          </div>
          <button class="par-term" @click="explainKey = 'bands'">Confidence bands</button>
          <table class="lab-table">
            <thead>
              <tr><th>Spins</th><th>±95%</th><th>±99%</th></tr>
            </thead>
            <tbody>
              <tr v-for="b in sheet.volatility.bands" :key="b.spins">
                <td>{{ b.spins.toLocaleString() }}</td>
                <td>±{{ (b.halfWidth95 * 100).toFixed(4) }}pp</td>
                <td>±{{ (b.halfWidth99 * 100).toFixed(4) }}pp</td>
              </tr>
            </tbody>
          </table>
        </section>

        <!-- verification -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'verification'">Verification</button>
          <table class="lab-table">
            <thead>
              <tr><th>Scatter reel</th><th>Positions</th><th>Separated ≥ window</th></tr>
            </thead>
            <tbody>
              <tr v-for="s in sheet.verification.scatterReels" :key="s.reel">
                <td>R{{ s.reel }}</td>
                <td class="mono">{{ s.positions.join(', ') }}</td>
                <td :class="s.separated ? 'par-pass' : 'par-fail'">{{ s.separated ? 'yes' : 'NO' }}</td>
              </tr>
            </tbody>
          </table>
        </section>

        <!-- strips -->
        <section class="par-block">
          <button class="par-term par-block__title" @click="explainKey = 'strips'">Reel strips, in order</button>
          <div v-for="(strip, reel) in sheet.strips" :key="reel" class="par-strip">
            <span class="par-label">R{{ reel + 1 }}</span>
            <div class="par-strip__cells">
              <span
                v-for="(id, stop) in strip"
                :key="stop"
                class="par-strip__cell"
                :title="`stop ${stop}: ${symbolName(id)}`"
              >{{ symbolName(id).slice(0, 2) }}</span>
            </div>
          </div>
        </section>
      </div>

      <!-- the explainer panel -->
      <aside class="par-explain">
        <h3>{{ explain.title }}</h3>
        <p>{{ explain.body }}</p>
        <p class="par-explain__hint">Click any underlined label on the sheet.</p>
      </aside>
    </div>
    <p v-else-if="sheet" class="lab-note">{{ sheet.reason }}</p>
  </article>
</template>

<style scoped>
.par-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 20rem;
  gap: var(--space-lg);
  align-items: start;
}

@media (max-width: 1100px) {
  .par-layout {
    grid-template-columns: 1fr;
  }
  .par-explain {
    position: static;
  }
}

.par-doc {
  display: grid;
  gap: var(--space-lg);
}

.par-block {
  border: var(--rule-hairline);
  padding: var(--space-md);
  display: grid;
  gap: var(--space-sm);
}

.par-block__title {
  font-family: var(--font-display);
  font-size: 0.85rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--color-accent);
  justify-self: start;
}

.par-term {
  background: transparent;
  border: 0;
  padding: 0;
  font: inherit;
  color: inherit;
  cursor: pointer;
  text-decoration: underline dotted color-mix(in srgb, var(--color-accent) 55%, transparent);
  text-underline-offset: 3px;
}

.par-term:hover {
  color: var(--color-accent-bright);
}

.par-header {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: var(--space-sm) var(--space-lg);
}

.par-header > div {
  display: grid;
  gap: 0.15rem;
}

.par-label {
  font-family: var(--font-display);
  font-size: 0.66rem;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: var(--color-text-muted);
  justify-self: start;
}

.par-zero {
  color: var(--color-text-muted);
}

.par-sum td {
  border-top: var(--rule-brass);
  color: var(--color-text-secondary);
}

.par-totals {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-md) var(--space-lg);
  padding-top: var(--space-xs);
  border-top: var(--rule-hairline);
  font-size: 0.85rem;
}

.par-totals .par-term {
  display: grid;
  gap: 0.1rem;
  text-align: left;
  font-family: var(--font-display);
  font-size: 0.68rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.par-totals .mono {
  font-size: 0.95rem;
  color: var(--color-text-primary);
}

.par-totals__grand .mono {
  color: var(--color-accent-bright);
}

.par-tiers td,
.par-tiers th {
  text-align: center;
}

.par-pass {
  color: var(--color-status-installed);
}

.par-fail {
  color: var(--color-status-error);
}

.par-scroll {
  overflow-x: auto;
}

.par-row--current td {
  color: var(--color-accent-bright);
}

.par-lines {
  display: grid;
  gap: 0.4rem;
}

.par-lines__row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-md);
}

.par-lines__item {
  display: grid;
  gap: 0.25rem;
  justify-items: center;
  font-size: 0.7rem;
}

.par-strip {
  display: grid;
  grid-template-columns: 2.2rem 1fr;
  gap: var(--space-sm);
  align-items: start;
}

.par-strip__cells {
  display: flex;
  flex-wrap: wrap;
  gap: 2px;
}

.par-strip__cell {
  font-family: var(--font-mono);
  font-size: 0.62rem;
  padding: 0.18rem 0.28rem;
  background: var(--color-surface);
  border: var(--rule-hairline);
  color: var(--color-text-secondary);
  cursor: default;
}

.par-explain {
  position: sticky;
  top: var(--space-md);
  border: var(--rule-brass);
  padding: var(--space-md);
  background: var(--color-surface);
}

.par-explain h3 {
  margin: 0 0 var(--space-xs);
  font-size: 1rem;
  color: var(--color-accent);
}

.par-explain p {
  font-size: 0.86rem;
  line-height: 1.6;
  color: var(--color-text-secondary);
  margin: 0 0 var(--space-sm);
}

.par-explain__hint {
  font-family: var(--font-mono);
  font-size: 0.68rem;
  color: var(--color-text-muted);
}
</style>
