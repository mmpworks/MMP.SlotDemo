<script setup lang="ts">
import { ref } from 'vue'
import { postJson } from '../api/labs'
import type { DeterminismView, TelemetryView } from '../api/labs'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'
import ReadTheArticle from '../components/ReadTheArticle.vue'

defineProps<{ title: string; blurb: string }>()

const presetName = ref('Video5x64')
const seed = ref(42)
const workerCount = ref(8)
const spins = ref(1_000_000)
const varySeed = ref(false)
const useOrca = ref(true)
const determinism = ref<DeterminismView | null>(null)

const telemetrySpins = ref(2_000_000)
const channelCapacity = ref(16)
const telemetry = ref<TelemetryView | null>(null)

const error = ref('')
const busy = ref(false)

async function runDeterminism(vary: boolean): Promise<void> {
  varySeed.value = vary
  busy.value = true
  error.value = ''
  try {
    determinism.value = await postJson<DeterminismView>('/api/ch5/determinism', {
      presetName: presetName.value,
      seed: seed.value,
      workerCount: workerCount.value,
      spins: spins.value,
      repeats: 3,
      varySeed: vary,
      gameFile: useOrca.value ? 'orca-dive.json' : '',
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Determinism run failed.'
  } finally {
    busy.value = false
  }
}

async function runTelemetry(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    telemetry.value = await postJson<TelemetryView>('/api/ch5/telemetry', {
      presetName: presetName.value,
      seed: seed.value,
      spins: telemetrySpins.value,
      channelCapacity: channelCapacity.value,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Telemetry run failed.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <article>
    <header class="chapter-head">
      <h2>{{ title }}</h2>
      <ReadTheArticle chapter="ch06" />
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <section class="chapter-brief">
      <h3>One run, divided among workers</h3>
      <p>
        Think of it as a factory floor. Each worker is handed a fixed stack of spins at the
        start of the shift, so the same worker always plays the same spins and a run
        replays. Each keeps a private tally and posts it to the shared board every 4,096
        spins, because the shared board is the slow part. The progress readout is a
        whiteboard: a worker writes its current total over the old one, so a missed reading
        costs a chart point.
      </p>
      <p>
        In the engine's own terms: workers get fixed spin quotas up front instead of stealing
        work, so the RNG partition never depends on scheduling luck. Totals are integer
        counters fed by one batched atomic add per 4,096 spins. Telemetry rides a bounded
        drop-oldest channel carrying absolute snapshots, so a dropped sample is superseded by
        the next one.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs</code>,
        <code>Simulation/RunTotals.cs</code>.
      </p>
    </section>

    <section class="chapter-brief">
      <h3>How the confidence band connects to Chapters 4 and 5</h3>
      <p>
        The band half-width is <code>z × sigma / square root of N</code>. Here,
        <code>N</code> is the completed spin count in <code>RunSnapshot.Spins</code>.
        Sigma is the typical one-spin swing calculated by <code>GameAnalyzer</code>.
        For the 99% band, <code>z</code> is about 2.576. The engine multiplies that number
        by sigma and divides by <code>Math.Sqrt(N)</code>.
      </p>
      <p>
        The engine places that half-width on both sides of the analytic RTP calculated
        before the run. The measured RTP is the value being checked; it does not choose the
        center or sigma. This keeps the mathematical reference independent of the random
        simulation.
      </p>
      <table class="lab-table">
        <thead><tr><th>Math symbol</th><th>Meaning</th><th>Source</th></tr></thead>
        <tbody>
          <tr><td><code>N</code></td><td>Completed spins</td><td><code>RunSnapshot.Spins</code></td></tr>
          <tr><td><code>sigma</code></td><td>One-spin payout swinginess</td><td><code>GameAnalysis.SigmaPerUnitWagered</code></td></tr>
          <tr><td><code>z</code></td><td>Confidence-level number; about 2.576 for 99%</td><td><code>NormalQuantile.TwoSided99</code></td></tr>
          <tr><td><code>√N</code></td><td>Square root of completed spins</td><td><code>Math.Sqrt(snapshot.Spins)</code></td></tr>
        </tbody>
      </table>
      <p class="lab-note">
        At 1,000,000 spins, <code>√N = 1,000</code>. One hundred times as many spins makes
        the band ten times narrower, because the square root of 100 is 10.
      </p>
      <h4>How that formula draws a funnel</h4>
      <p>
        The tightening comes from <code>1 / √N</code>. The square root grows as spins are
        completed, but it sits under the division line, so the band half-width gets smaller.
        The analytic RTP stays fixed through the middle. The renderer adds the half-width
        for the upper edge and subtracts it for the lower edge.
      </p>
      <table class="lab-table">
        <thead><tr><th>Graph mark</th><th>What it shows</th><th>How it is calculated</th></tr></thead>
        <tbody>
          <tr><td>Horizontal centerline</td><td>RTP calculated from the game rules</td><td><code>analyticRtp</code></td></tr>
          <tr><td>Upper funnel edge</td><td>Top of the 99% range</td><td><code>analyticRtp + halfWidth</code></td></tr>
          <tr><td>Lower funnel edge</td><td>Bottom of the 99% range</td><td><code>analyticRtp - halfWidth</code></td></tr>
          <tr><td>Moving line</td><td>RTP measured by the random run so far</td><td><code>RunSnapshot.MeasuredRtp</code></td></tr>
        </tbody>
      </table>
      <p class="lab-note">
        Read left to right for completed spins and bottom to top for RTP. Early funnel edges
        may begin outside the visible frame because the band is extremely wide when only a
        few spins have finished.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — Same seed, same answer</h3>
      <p class="lab__lede">
        Run Orca Dive three times with the same seed and worker count. Other programs may
        change the elapsed time, but the returned money and hit counts should match. The
        second button changes the seed for each run so you can compare the two cases.
      </p>

      <div class="controls">
        <label>
          Subject
          <select v-model="useOrca">
            <option :value="true">Orca Dive (full game)</option>
            <option :value="false">Preset below</option>
          </select>
        </label>
        <label v-if="!useOrca">
          Preset
          <select v-model="presetName">
            <option>Classic3</option><option>Video3</option><option>Line4</option>
            <option>Video5x64</option><option>Video5x128</option>
          </select>
        </label>
        <label>
          Seed
          <input v-model.number="seed" type="number" min="0" />
        </label>
        <label>
          Workers
          <input v-model.number="workerCount" type="number" min="1" max="64" />
        </label>
        <label>
          Spins
          <input v-model.number="spins" type="number" min="1000" max="5000000" step="100000" />
        </label>
        <button type="button" :disabled="busy" @click="runDeterminism(false)">Same seed ×3</button>
        <button type="button" class="ghost" :disabled="busy" @click="runDeterminism(true)">Vary seed ×3</button>
      </div>

      <p v-if="error" class="lab__error">{{ error }}</p>

      <div v-if="determinism" class="results">
        <div class="verdict" :class="determinism.identical ? '' : 'verdict--drift'">
          <div>
            <span class="verdict__label">Mode</span>
            <span class="mono">{{ determinism.varySeed ? 'different seeds' : 'same seed' }}</span>
          </div>
          <div>
            <span class="verdict__label">Snapshots identical</span>
            <span class="mono">{{ determinism.identical ? 'yes — bit for bit' : 'no' }}</span>
          </div>
        </div>
        <table class="lab-table">
          <thead>
            <tr><th>Run</th><th>Seed</th><th>Returned (mc)</th><th>Hits</th><th>RTP</th><th>Time</th><th>Spins/s</th></tr>
          </thead>
          <tbody>
            <tr v-for="r in determinism.runs" :key="r.attempt">
              <td>{{ r.attempt }}</td>
              <td>{{ r.seed }}</td>
              <td>{{ r.returnedMillicents.toLocaleString() }}</td>
              <td>{{ r.hits.toLocaleString() }}</td>
              <td>{{ (r.measuredRtp * 100).toFixed(4) }}%</td>
              <td>{{ r.elapsedMs.toFixed(0) }} ms</td>
              <td>{{ Math.round(r.spinsPerSecond).toLocaleString() }}</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          Compare the returned-money column across the three runs. Integer money, fixed
          worker quotas, and seeded worker streams make the result reproducible.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2 — Starve the telemetry lane</h3>
      <p class="lab__lede">
        A deliberately slow reader drains the snapshot channel while eight workers flood
        it. Shrink the capacity and more display updates are dropped. The final money and
        spin totals use a separate path, so dropped chart points cannot change them.
      </p>

      <div class="controls">
        <label>
          Spins
          <input v-model.number="telemetrySpins" type="number" min="10000" max="10000000" step="500000" />
        </label>
        <label>
          Channel capacity
          <input v-model.number="channelCapacity" type="number" min="1" max="4096" />
          <small>try 1, 16, 1024</small>
        </label>
        <button type="button" :disabled="busy" @click="runTelemetry">Run under pressure</button>
      </div>

      <div v-if="telemetry" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Samples produced ≈</span>
            <span class="mono">{{ telemetry.samplesProducedApprox.toLocaleString() }}</span>
          </div>
          <div>
            <span class="verdict__label">Delivered</span>
            <span class="mono">{{ telemetry.samplesDelivered.toLocaleString() }}</span>
          </div>
          <div>
            <span class="verdict__label">Dropped ≈</span>
            <span class="mono">{{ telemetry.samplesDroppedApprox.toLocaleString() }}</span>
          </div>
          <div>
            <span class="verdict__label">Throughput</span>
            <span class="mono">{{ Math.round(telemetry.spinsPerSecond).toLocaleString() }} spins/s</span>
          </div>
        </div>
        <div class="verdict">
          <div>
            <span class="verdict__label">Exact final RTP</span>
            <span class="mono">{{ (telemetry.exactFinal.measuredRtp * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Exact spins counted</span>
            <span class="mono">{{ telemetry.exactFinal.spins.toLocaleString() }}</span>
          </div>
          <div>
            <span class="verdict__label">Last sample carried</span>
            <span class="mono">{{ telemetry.lastDeliveredSample.spins.toLocaleString() }} spins</span>
          </div>
        </div>
        <p class="lab-note">
          Dropped samples are absolute snapshots, so losing one costs a chart point. The
          proving ground works the same way.
        </p>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 7</h3>
      <p>
        The engine takes a play function, so a game with its own rules (wilds, scatters, a
        pick bonus) plugs into the same workers, quotas, and telemetry. Episode 7 makes the
        game itself a document.
      </p>
    </section>
    <ComprehensionCheck
      question="A slow browser misses several progress updates. What happens to the final totals?"
      :choices="['They become smaller.', 'They remain exact.', 'The workers repeat the missed spins.']"
      :answer="1"
      explanation="Progress snapshots are copies. Workers keep the authoritative totals in separate counters."
    />
    <OptimizationPreview
      question="Would manual inlining or loop unrolling make workers faster?"
      later="Only a complete Release run can answer. Episode 9 shows both ideas losing to the JIT's compact loop."
    />
  </article>
</template>
