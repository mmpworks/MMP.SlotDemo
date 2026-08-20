<script setup lang="ts">
import { computed, ref } from 'vue'
import { runBiasLab, type BiasResponse } from '../../api/chapters'

const seed = ref(7)
const bound = ref(26)
const bits = ref(8)
const samples = ref(200_000)

const result = ref<BiasResponse | null>(null)
const error = ref('')
const busy = ref(false)

async function run(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    result.value = await runBiasLab(seed.value, bound.value, bits.value, samples.value)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Bias lab failed.'
  } finally {
    busy.value = false
  }
}

/** Bar height as a percentage of the tallest bucket across both histograms. */
const peak = computed(() => {
  if (!result.value) return 1
  return Math.max(...result.value.moduloCounts, ...result.value.lemireCounts)
})

function height(count: number): string {
  return `${Math.max(2, (count / peak.value) * 100)}%`
}

function tilt(count: number): number {
  if (!result.value) return 0
  return (count - result.value.expected) / result.value.expected
}
</script>

<template>
  <section class="lab">
    <h3>Lab 4 — Where modulo bias hides</h3>
    <p class="lab__lede">
      Orca Dive's first reel has 26 stops. A 64-bit random source is too large for its
      modulo bias to show on a chart, so this lab temporarily uses a smaller source. Both
      charts use the same random draws. Only the method that turns a draw into a reel stop
      changes.
    </p>

    <div class="controls">
      <label>
        Seed
        <input v-model.number="seed" type="number" min="0" />
      </label>
      <label>
        Buckets (strip length)
        <input v-model.number="bound" type="number" min="2" />
      </label>
      <label>
        Draw space bits
        <input v-model.number="bits" type="number" min="4" max="16" />
        <small>{{ 2 ** bits }} values</small>
      </label>
      <label>
        Samples
        <input v-model.number="samples" type="number" min="100" step="50000" />
      </label>
      <button type="button" :disabled="busy" @click="run">
        {{ busy ? 'Sampling…' : 'Sample' }}
      </button>
    </div>

    <p v-if="error" class="lab__error">{{ error }}</p>

    <div v-if="result" class="results">
      <div class="verdict">
        <div>
          <span class="verdict__label">Draw space</span>
          <span class="mono">{{ result.space }} values into {{ bound }} buckets</span>
        </div>
        <div>
          <span class="verdict__label">Modulo worst bucket</span>
          <span class="mono warn">{{ result.moduloWorstErrorPercent.toFixed(2) }}% off</span>
        </div>
        <div>
          <span class="verdict__label">Lemire worst bucket</span>
          <span class="mono good">{{ result.lemireWorstErrorPercent.toFixed(2) }}% off</span>
        </div>
        <div>
          <span class="verdict__label">Draws rejected</span>
          <span class="mono">{{ result.rejections.toLocaleString() }}</span>
        </div>
      </div>

      <div class="charts">
        <figure>
          <figcaption>Modulo reduction (<code>draw % bound</code>)</figcaption>
          <div class="bars">
            <span
              v-for="(count, i) in result.moduloCounts"
              :key="i"
              class="bar bar--modulo"
              :class="{ 'bar--hot': Math.abs(tilt(count)) > 0.02 }"
              :style="{ height: height(count) }"
              :title="`bucket ${i}: ${count.toLocaleString()}`"
            />
          </div>
        </figure>
        <figure>
          <figcaption>Lemire multiply-shift with rejection</figcaption>
          <div class="bars">
            <span
              v-for="(count, i) in result.lemireCounts"
              :key="i"
              class="bar"
              :style="{ height: height(count) }"
              :title="`bucket ${i}: ${count.toLocaleString()}`"
            />
          </div>
        </figure>
      </div>

      <p class="note">
        Buckets below {{ result.space % bound }} receive one extra source value in the modulo
        chart. Those reel stops become slightly more likely than the PAR sheet says. Lemire's
        method rejects the leftover draws counted above, then redraws, so every stop keeps
        the same chance.
      </p>
    </div>
  </section>
</template>

<style scoped>
.lab {
  border: var(--rule-hairline);
  padding: var(--space-lg);
  margin-bottom: var(--space-lg);
}

.lab h3 {
  margin: 0 0 var(--space-xs);
  font-size: 1.1rem;
}

.lab__lede {
  color: var(--color-text-secondary);
  max-width: 68ch;
  line-height: 1.6;
  font-size: 0.9rem;
}

.controls {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: var(--space-md);
  margin: var(--space-md) 0;
}

.controls label {
  display: grid;
  gap: 0.25rem;
  font-size: 0.78rem;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.controls small {
  text-transform: none;
  letter-spacing: 0;
  font-size: 0.7rem;
}

.controls input {
  font-family: var(--font-mono);
  font-size: 0.9rem;
  background: var(--color-surface);
  border: var(--rule-hairline);
  color: var(--color-text-primary);
  padding: 0.4rem 0.5rem;
  width: 9rem;
}

.controls button {
  font-family: var(--font-display);
  letter-spacing: 0.2em;
  text-transform: uppercase;
  background: transparent;
  color: var(--color-accent);
  border: var(--rule-brass);
  padding: 0.55rem 1.6rem;
  cursor: pointer;
}

.lab__error {
  color: var(--color-status-error);
  font-family: var(--font-mono);
  font-size: 0.8rem;
}

.results {
  display: grid;
  gap: var(--space-md);
}

.verdict {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-lg);
  padding: var(--space-sm) var(--space-md);
  background: var(--color-surface);
  border-left: 3px solid var(--color-accent);
}

.verdict div {
  display: grid;
  gap: 0.2rem;
}

.verdict__label {
  font-family: var(--font-display);
  font-size: 0.68rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.mono {
  font-family: var(--font-mono);
}

.warn {
  color: var(--color-log-warning);
}

.good {
  color: var(--color-status-installed);
}

.charts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: var(--space-lg);
}

figure {
  margin: 0;
  display: grid;
  gap: 0.4rem;
}

figcaption {
  font-family: var(--font-mono);
  font-size: 0.74rem;
  color: var(--color-text-muted);
}

.bars {
  display: flex;
  align-items: flex-end;
  gap: 1px;
  height: 160px;
  background: var(--color-surface);
  padding: 0.35rem;
}

.bar {
  flex: 1 1 0;
  background: var(--color-accent-dim);
  min-width: 2px;
}

.bar--modulo {
  background: color-mix(in srgb, var(--color-accent) 60%, transparent);
}

.bar--hot {
  background: var(--color-log-warning);
}

.note {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
  line-height: 1.55;
  max-width: 70ch;
}
</style>
