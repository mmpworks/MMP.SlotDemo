<script setup lang="ts">
import { ref } from 'vue'
import { postJson } from '../api/labs'
import type { DeterminismView, TelemetryView } from '../api/labs'

defineProps<{ title: string; blurb: string }>()

const presetName = ref('Video5x64')
const seed = ref(42)
const workerCount = ref(8)
const spins = ref(1_000_000)
const varySeed = ref(false)
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
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <section class="chapter-brief">
      <h3>What the episode builds</h3>
      <p>
        Workers get fixed spin quotas up front instead of stealing work, so the RNG
        partition never depends on scheduling luck. Totals are integer counters fed by one
        batched atomic add per 4,096 spins. Telemetry rides a bounded drop-oldest channel
        carrying absolute snapshots — the lossy lane can lose everything but the truth.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Simulation/SimulationEngine.cs</code>,
        <code>Simulation/RunTotals.cs</code>.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — Same seed, same answer, any day</h3>
      <p class="lab__lede">
        Three runs of the same configuration. Wall time varies with whatever else the
        machine is doing; the totals come back identical to the last millicent. Vary the
        seed instead to see what a real difference looks like.
      </p>

      <div class="controls">
        <label>
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
          The returned column is the claim: integer money (M2) plus fixed quotas plus
          seeded streams (R3) make an N-worker run reproducible, and the wall-time column
          shows the schedule had no say in the answer.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2 — Starve the telemetry, keep the truth</h3>
      <p class="lab__lede">
        A deliberately slow reader drains the snapshot channel while eight workers flood
        it. Shrink the capacity and the drop rate climbs — and the exact totals underneath
        never move, because the two lanes never touch.
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
          Dropped samples are absolute snapshots, so losing them costs chart points and
          nothing else. This is the design the finale page rides: the browser sees a
          consolidated curve while the integer counters stay lossless.
        </p>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 6</h3>
      <p>
        The engine takes a play function, so a game with its own rules — wilds, scatters, a
        pick bonus — plugs into the same workers, quotas, and telemetry. Episode 6 makes
        the game itself a document.
      </p>
    </section>
  </article>
</template>
