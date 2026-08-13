<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import type { OptimizationBenchmarkView, SourceView } from '../api/labs'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'

defineProps<{ title: string; blurb: string }>()

const sources = ref<SourceView[]>([])
const sourceId = ref('Video5x64')
const seed = ref(20260812)
const spins = ref(2_000_000)
const result = ref<OptimizationBenchmarkView | null>(null)
const busy = ref(false)
const error = ref('')

const maxRate = computed(() => Math.max(
  result.value?.baseline.medianSpinsPerSecond ?? 1,
  result.value?.optimized.medianSpinsPerSecond ?? 1,
))

function width(rate: number): string {
  return `${Math.max(2, rate / maxRate.value * 100)}%`
}

function rate(value: number): string {
  return `${(value / 1_000_000).toFixed(1)}M spins/s`
}

async function run(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    result.value = await postJson<OptimizationBenchmarkView>('/api/ch9/draw-window', {
      sourceId: sourceId.value,
      seed: seed.value,
      spins: spins.value,
      trials: 5,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Benchmark failed.'
  } finally {
    busy.value = false
  }
}

onMounted(async () => {
  try {
    sources.value = await getJson<SourceView[]>('/api/ch3/sources')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load reel sources.'
  }
})
</script>

<template>
  <article>
    <header class="chapter-head">
      <h2>{{ title }}</h2>
      <p class="chapter-blurb">{{ blurb }}</p>
    </header>

    <section class="chapter-brief">
      <h3>Correct first, fast second</h3>
      <p>
        The first eight episodes build and verify the machine before changing its hot
        loops. This follow-up keeps the original <code>DrawWindow</code> algorithm beside
        the optimized one. Both receive the same seed and must produce the same checksum.
        A faster wrong answer fails before its timing is reported.
      </p>
      <p>
        The baseline copies full <code>Symbol</code> values and calculates wraparound with
        remainder. The optimized path writes byte IDs and reads through a short wrapped
        extension built once for each reel.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — Race two versions of DrawWindow</h3>
      <p class="lab__lede">
        Five trials alternate which version runs first. The chart uses each version's
        median, reducing the effect of JIT warmup and temporary CPU scheduling changes.
        Run the server in Release mode when comparing absolute rates.
      </p>

      <div class="controls">
        <label>
          Reel source
          <select v-model="sourceId">
            <option v-for="source in sources" :key="source.id" :value="source.id">
              {{ source.name }} ({{ source.stopsPerReel.join('/') }})
            </option>
          </select>
        </label>
        <label>Seed <input v-model.number="seed" type="number" min="0" /></label>
        <label>
          Windows per trial
          <input v-model.number="spins" type="number" min="100000" max="10000000" step="100000" />
        </label>
        <button type="button" :disabled="busy" @click="run">
          {{ busy ? 'Measuring…' : 'Run benchmark' }}
        </button>
      </div>

      <p v-if="error" class="lab__error">{{ error }}</p>

      <div v-if="result" class="results">
        <div class="verdict" :class="result.outputsMatch ? '' : 'verdict--drift'">
          <div>
            <span class="verdict__label">Correctness gate</span>
            <span class="mono">{{ result.outputsMatch ? 'MATCH' : 'MISMATCH' }}</span>
          </div>
          <div>
            <span class="verdict__label">Measured speedup</span>
            <span class="mono">{{ result.speedup.toFixed(2) }}×</span>
          </div>
        </div>

        <div class="bench-row">
          <span>Initial</span>
          <div class="bench-track"><div class="bench-bar bench-bar--base" :style="{ width: width(result.baseline.medianSpinsPerSecond) }" /></div>
          <strong>{{ rate(result.baseline.medianSpinsPerSecond) }}</strong>
        </div>
        <div class="bench-row">
          <span>Optimized</span>
          <div class="bench-track"><div class="bench-bar" :style="{ width: width(result.optimized.medianSpinsPerSecond) }" /></div>
          <strong>{{ rate(result.optimized.medianSpinsPerSecond) }}</strong>
        </div>

        <table class="lab-table">
          <tbody>
            <tr><th>Random stop selections</th><td>{{ result.randomSelections.toLocaleString() }}</td></tr>
            <tr><th>Visible-cell writes</th><td>{{ result.visibleCellWrites.toLocaleString() }}</td></tr>
            <tr><th>Memory tradeoff</th><td>{{ result.memoryTradeoff }}</td></tr>
            <tr><th>Baseline samples</th><td>{{ result.baseline.samples.map(rate).join(' · ') }}</td></tr>
            <tr><th>Optimized samples</th><td>{{ result.optimized.samples.map(rate).join(' · ') }}</td></tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Experiments that lost</h3>
      <p>
        Unrolling separate three-, four-, and five-position methods was slower. Flattening
        all reels into one array did not beat the jagged layout reliably. Forced inlining
        also lost. Those versions stayed out of production because whole-run measurements,
        rather than intuition, decide the result.
      </p>
    </section>

    <ComprehensionCheck
      question="Why must the benchmark compare checksums before reporting a speedup?"
      :choices="['A wrong implementation can be fast.', 'Checksums warm the CPU cache.', 'The JIT requires a checksum to optimize loops.']"
      :answer="0"
      explanation="Performance matters only after both implementations perform the same work and produce the same result."
    />
  </article>
</template>

<style scoped>
.bench-row { display: grid; grid-template-columns: 6rem 1fr 9rem; gap: .75rem; align-items: center; margin: .8rem 0; }
.bench-track { height: 1.2rem; overflow: hidden; border-radius: .3rem; background: var(--surface-2, #182536); }
.bench-bar { height: 100%; background: #39c6a3; }
.bench-bar--base { background: #7792b8; }
.bench-row strong { text-align: right; font-variant-numeric: tabular-nums; }
@media (max-width: 700px) { .bench-row { grid-template-columns: 5rem 1fr; } .bench-row strong { grid-column: 2; text-align: left; } }
</style>
