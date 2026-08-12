<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import type { CurvePoint, RunDescription, RunLimits } from '../api/labs'
import { buildGeometry } from '../chart/convergence'

defineProps<{ title: string; blurb: string }>()

const limits = ref<RunLimits | null>(null)

// The subject: a shipped game by default. 'preset:' ids switch to the configurable
// solved game.
const subject = ref('game:orca-dive.json')
const baseBp = ref(7500)
const freeSpinsBp = ref(1300)
const pickBonusBp = ref(1000)
const seed = ref(20260811)
const workers = ref(8)
const targetSpins = ref(10_000_000)
const stride = ref(50_000)

const run = ref<RunDescription | null>(null)
const curve = ref<CurvePoint[]>([])
const liveSpins = ref(0)
const liveRtp = ref(0)
const liveHitFrequency = ref(0)
const error = ref('')
const busy = ref(false)

let source: EventSource | null = null

onMounted(async () => {
  try {
    limits.value = await getJson<RunLimits>('/api/run/limits')
    // A run may already be live or finished; adopt it so a reload keeps the chart.
    const current = await fetch('/api/run/current')
    if (current.status === 200) adopt((await current.json()) as RunDescription)
  } catch {
    // The panel still renders; starting a run will surface any real problem.
  }
  subscribe()
})

onUnmounted(() => source?.close())

function subscribe(): void {
  source = new EventSource('/api/run/stream')
  source.onmessage = (message) => {
    const event = JSON.parse(message.data) as { type: string; data: never }
    if (event.type === 'started') adopt(event.data as RunDescription)
    else if (event.type === 'point') {
      const payload = event.data as { runId: string; point: CurvePoint }
      curve.value = [...curve.value, payload.point]
      // A fast run can finish between progress ticks; points keep the readout live.
      liveSpins.value = payload.point.spins
      liveRtp.value = payload.point.measuredRtp
      liveHitFrequency.value = payload.point.hitFrequency
    } else if (event.type === 'progress') {
      const p = event.data as { spins: number; measuredRtp: number; hitFrequency: number }
      liveSpins.value = p.spins
      liveRtp.value = p.measuredRtp
      liveHitFrequency.value = p.hitFrequency
    } else if (event.type === 'completed' || event.type === 'cancelled') {
      adopt(event.data as RunDescription)
    }
  }
}

function adopt(description: RunDescription): void {
  run.value = description
  curve.value = description.curve
  liveSpins.value = description.latest.spins
  liveRtp.value = description.latest.measuredRtp
  liveHitFrequency.value = description.latest.hitFrequency
}

async function start(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    curve.value = []
    await postJson('/api/run', {
      presetName: isGameSubject.value ? '' : subject.value,
      baseRtpBasisPoints: baseBp.value,
      freeSpinsRtpBasisPoints: freeSpinsBp.value,
      pickBonusRtpBasisPoints: pickBonusBp.value,
      seed: seed.value,
      workerCount: workers.value,
      targetSpins: targetSpins.value,
      stride: stride.value,
      gameFile: isGameSubject.value ? subject.value.slice('game:'.length) : '',
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Run failed to start.'
  } finally {
    busy.value = false
  }
}

async function cancel(): Promise<void> {
  await fetch('/api/run/cancel', { method: 'POST' }).catch(() => undefined)
}

const isGameSubject = computed(() => subject.value.startsWith('game:'))
const aggregateBp = computed(() => baseBp.value + freeSpinsBp.value + pickBonusBp.value)
const overCap = computed(() =>
  !isGameSubject.value &&
  limits.value !== null &&
  aggregateBp.value > limits.value.maxAggregateBasisPoints)

// ---- the chart ----
// Pure geometry lives in chart/convergence.ts (tested); this file only feeds it.

const W = 960
const H = 420
const PAD = { left: 64, right: 20, top: 16, bottom: 34 }

const chart = computed(() => {
  const r = run.value
  if (!r || curve.value.length === 0) return null
  const geometry = buildGeometry(
    { width: W, height: H, pad: PAD },
    {
      analyticRtp: r.analytic.totalRtp,
      sigma: r.analytic.sigma,
      targetSpins: r.config.targetSpins,
      curve: curve.value,
    },
  )
  if (!geometry) return null
  return { ...geometry, last: curve.value[curve.value.length - 1] }
})

const verdict = computed(() => {
  const r = run.value
  const last = curve.value[curve.value.length - 1]
  if (!r || !last) return null
  if (r.status === 'running') return null
  return { withinBand: last.withinBand, status: r.status, point: last }
})
</script>

<template>
  <article>
    <header class="chapter-head">
      <h2>{{ title }}</h2>
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <section class="lab">
      <h3>Configure the run</h3>
      <div class="controls">
        <label>
          Subject
          <select v-model="subject">
            <optgroup label="Shipped games — published paytable, enumerated reference">
              <option v-for="g in limits?.games" :key="g" :value="`game:${g}`">
                {{ g.replace('.json', '') }}
              </option>
            </optgroup>
            <optgroup label="Solved presets — pick the RTP, the solver builds the paytable">
              <option v-for="p in limits?.presets" :key="p.name" :value="p.name">
                {{ p.name }} ({{ p.reels }} reels, {{ p.paylines }} lines)
              </option>
            </optgroup>
          </select>
        </label>
        <template v-if="!isGameSubject">
          <label>
            Base RTP (bp)
            <input v-model.number="baseBp" type="number" min="1" max="9900" step="100" />
          </label>
          <label>
            Free spins (bp)
            <input v-model.number="freeSpinsBp" type="number" min="0" step="100" />
          </label>
          <label>
            Pick bonus (bp)
            <input v-model.number="pickBonusBp" type="number" min="0" step="100" />
          </label>
        </template>
        <label>
          Seed
          <input v-model.number="seed" type="number" min="0" />
        </label>
        <label>
          Workers
          <input v-model.number="workers" type="number" min="1" :max="limits?.workerCeiling ?? 64" />
        </label>
        <label>
          Spins
          <input v-model.number="targetSpins" type="number" min="100000" step="1000000" />
        </label>
        <label>
          Curve stride
          <input v-model.number="stride" type="number" min="1000" step="10000" />
          <small>one chart point per this many spins</small>
        </label>
        <button type="button" :disabled="busy || overCap" @click="start">
          {{ busy ? 'Starting…' : 'Run the proof' }}
        </button>
        <button type="button" class="ghost" @click="cancel">Stop</button>
      </div>

      <p v-if="isGameSubject" class="lab-note">
        A shipped game brings its paytable with it, so there is no RTP to choose. Its
        enumerated reference is shown as Analytic RTP below, and the run should settle into
        the band around it.
      </p>
      <p v-if="overCap" class="lab__error">
        Aggregate {{ aggregateBp }} bp is over the {{ limits?.maxAggregateBasisPoints }} bp cap.
        The server refuses the request instead of clamping it. Submit it to see the error.
      </p>
      <p v-if="error" class="lab__error">{{ error }}</p>
    </section>

    <!-- The dark zone: the machine proving itself. -->
    <section class="proving-ground">
      <div class="proving-ground__head">
        <div class="readout">
          <span class="readout__label">Spins</span>
          <span class="readout__value">{{ liveSpins.toLocaleString() }}</span>
        </div>
        <div class="readout">
          <span class="readout__label">Measured RTP</span>
          <span class="readout__value">{{ (liveRtp * 100).toFixed(4) }}%</span>
        </div>
        <div class="readout">
          <span class="readout__label">Analytic RTP</span>
          <span class="readout__value">{{ ((run?.analytic.totalRtp ?? 0) * 100).toFixed(4) }}%</span>
        </div>
        <div class="readout">
          <span class="readout__label">Hit frequency (any award)</span>
          <span class="readout__value">{{ (liveHitFrequency * 100).toFixed(2) }}%</span>
        </div>
        <div class="readout">
          <span class="readout__label">Status</span>
          <span class="readout__value">{{ run?.status ?? 'idle' }}</span>
        </div>
      </div>

      <svg
        v-if="chart"
        class="chart"
        :viewBox="`0 0 ${W} ${H}`"
        role="img"
        aria-label="Measured RTP converging inside the analytic confidence band"
      >
        <!-- band funnel -->
        <polygon :points="chart.funnel" class="chart__band" />
        <!-- analytic centre line -->
        <line
          :x1="PAD.left" :x2="W - PAD.right"
          :y1="chart.analyticY" :y2="chart.analyticY"
          class="chart__analytic"
        />
        <!-- measured walk -->
        <polyline :points="chart.measured" class="chart__measured" />
        <circle :cx="chart.lastX" :cy="chart.lastY" r="4" class="chart__tip" />

        <!-- axes -->
        <g v-for="t in chart.ticksY" :key="t.y">
          <line :x1="PAD.left - 6" :x2="PAD.left" :y1="t.y" :y2="t.y" class="chart__tick" />
          <text :x="PAD.left - 10" :y="t.y + 3" class="chart__label" text-anchor="end">{{ t.label }}</text>
        </g>
        <g v-for="t in chart.ticksX" :key="t.x">
          <line :x1="t.x" :x2="t.x" :y1="H - PAD.bottom" :y2="H - PAD.bottom + 6" class="chart__tick" />
          <text :x="t.x" :y="H - PAD.bottom + 20" class="chart__label" text-anchor="middle">{{ t.label }}</text>
        </g>
      </svg>

      <p v-else class="proving-ground__empty">
        No run yet. Configure above and run the proof. The shaded funnel is the range
        probability theory allows; the line is the measured RTP inside it.
      </p>

      <div v-if="verdict" class="verdict-banner" :class="{ 'verdict-banner--failed': !verdict.withinBand }">
        <span class="verdict-banner__word">
          {{ verdict.withinBand ? 'WITHIN BAND' : 'OUTSIDE BAND' }}
        </span>
        <span class="mono">
          {{ verdict.point.spins.toLocaleString() }} spins ·
          measured {{ (verdict.point.measuredRtp * 100).toFixed(4) }}% ·
          band ±{{ (verdict.point.bandHalfWidth * 100).toFixed(4) }}pp
          {{ verdict.status === 'cancelled' ? '· stopped early' : '' }}
        </span>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>What this page is doing</h3>
      <p>
        Workers count exact integer millicents and publish absolute snapshots into a
        bounded, drop-oldest channel. The server consolidates those into one curve point
        per {{ (run?.stride ?? stride).toLocaleString() }} spins, each carrying its own
        z·σ/√N half-width, and streams the points here over SSE. Ten million spins become
        a couple hundred points. The browser gets those points while the workers run flat
        out. Hit frequency here counts any winning spin, bonus included, so it runs about a
        point above the PAR sheet's line-only 10.26%. The run also logs to the stream below
        through Herald.
      </p>
    </section>
  </article>
</template>

<style scoped>
/* The dark zone is the site's only inverted palette. */
.proving-ground {
  background: #0b0e14;
  border: 1px solid #232a38;
  padding: var(--space-md);
  margin-bottom: var(--space-lg);
}

.proving-ground__head {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-lg);
  margin-bottom: var(--space-md);
}

.readout {
  display: grid;
  gap: 0.15rem;
}

.readout__label {
  font-family: var(--font-display);
  font-size: 0.66rem;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: #5b667f;
}

.readout__value {
  font-family: var(--font-mono);
  font-size: 1.15rem;
  color: #e8ecf5;
}

.chart {
  width: 100%;
  height: auto;
  display: block;
}

.chart__band {
  fill: rgba(96, 165, 250, 0.12);
  stroke: rgba(96, 165, 250, 0.35);
  stroke-width: 1;
}

.chart__analytic {
  stroke: rgba(148, 163, 184, 0.7);
  stroke-width: 1;
  stroke-dasharray: 6 5;
}

.chart__measured {
  fill: none;
  stroke: #f5b84a;
  stroke-width: 2;
}

.chart__tip {
  fill: #f5b84a;
}

.chart__tick {
  stroke: #3a4459;
  stroke-width: 1;
}

.chart__label {
  fill: #8a94ab;
  font-family: var(--font-mono);
  font-size: 11px;
}

.proving-ground__empty {
  color: #8a94ab;
  font-size: 0.9rem;
  max-width: 60ch;
  line-height: 1.6;
}

.verdict-banner {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--space-md);
  margin-top: var(--space-md);
  padding: 0.7rem var(--space-md);
  border-left: 3px solid #3fb96f;
  background: rgba(63, 185, 111, 0.08);
  color: #e8ecf5;
}

.verdict-banner--failed {
  border-left-color: #e05252;
  background: rgba(224, 82, 82, 0.08);
}

.verdict-banner__word {
  font-family: var(--font-display);
  letter-spacing: 0.24em;
  font-size: 1rem;
}

.verdict-banner .mono {
  font-size: 0.8rem;
  color: #8a94ab;
}
</style>
