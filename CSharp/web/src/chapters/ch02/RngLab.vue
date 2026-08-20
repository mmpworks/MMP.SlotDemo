<script setup lang="ts">
import { ref } from 'vue'
import { runRngLab, type RngResponse } from '../../api/chapters'

const seed = ref(20260810)
const workerCount = ref(4)
const draws = ref(6)
// 26: the strip length of Orca Dive's reel 1, so the stops here are that reel's stops.
const bound = ref(26)
const mixed = ref(true)

const result = ref<RngResponse | null>(null)
const error = ref('')
const busy = ref(false)

async function run(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    result.value = await runRngLab(seed.value, workerCount.value, draws.value, bound.value, mixed.value)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'RNG lab failed.'
  } finally {
    busy.value = false
  }
}

async function compare(useMixing: boolean): Promise<void> {
  mixed.value = useMixing
  await run()
}
</script>

<template>
  <section class="lab">
    <h3>Lab 2 — One seed, many workers</h3>
    <p class="lab__lede">
      Each simulation worker needs a repeatable random stream that does not copy a neighbor's
      early pattern. Run both seeding methods and compare workers 0 and 1. The shared-prefix
      count reports how many leading bits their first values have in common.
    </p>

    <div class="controls">
      <label>
        Master seed
        <input v-model.number="seed" type="number" min="0" step="1" />
      </label>
      <label>
        Workers
        <input v-model.number="workerCount" type="number" min="1" max="8" />
      </label>
      <label>
        Draws each
        <input v-model.number="draws" type="number" min="1" max="32" />
      </label>
      <label>
        Strip length
        <input v-model.number="bound" type="number" min="2" />
      </label>
      <button type="button" :disabled="busy" @click="compare(true)">SplitMix64</button>
      <button type="button" class="ghost" :disabled="busy" @click="compare(false)">
        seed + workerId
      </button>
    </div>

    <p v-if="error" class="lab__error">{{ error }}</p>

    <div v-if="result" class="results">
      <div class="verdict" :class="result.sharedPrefixBits >= 8 ? 'verdict--drift' : 'verdict--clean'">
        <div>
          <span class="verdict__label">Seeding</span>
          <span class="mono">{{ mixed ? 'SplitMix64' : 'seed + workerId' }}</span>
        </div>
        <div>
          <span class="verdict__label">Leading bits shared by workers 0 and 1</span>
          <span class="mono">{{ result.sharedPrefixBits }} / 64</span>
        </div>
        <div>
          <span class="verdict__label">Replay matched</span>
          <span class="mono">{{ result.reproduced ? 'yes' : 'no' }}</span>
        </div>
      </div>

      <div v-for="stream in result.streams" :key="stream.workerId" class="stream">
        <div class="stream__head">
          <span class="stream__id">worker {{ stream.workerId }}</span>
          <span class="mono stream__state">state {{ stream.state.join(' ') }}</span>
        </div>
        <div class="stream__draws">
          <span v-for="(raw, i) in stream.raw" :key="i" class="draw">
            <span class="mono draw__raw">{{ raw }}</span>
            <span class="mono draw__reduced">stop {{ stream.reduced[i] }}</span>
          </span>
        </div>
      </div>

      <p class="note">
        The reduced column is what a reel actually consumes: a stop index inside a strip of
        The reduced values are reel stops. At the default length of 26, they are valid stop
        numbers for Orca Dive's first reel. "Replay matched" means the same seed and worker
        ID rebuilt the same stream, which lets another run reproduce the result.
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
  font-size: 0.78rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  background: transparent;
  color: var(--color-accent);
  border: var(--rule-brass);
  padding: 0.55rem 1.2rem;
  cursor: pointer;
}

.controls button.ghost {
  color: var(--color-text-secondary);
  border: var(--rule-hairline);
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
  border-left: 3px solid var(--color-status-installed);
}

.verdict--drift {
  border-left-color: var(--color-log-warning);
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

.stream {
  display: grid;
  gap: 0.35rem;
  padding-bottom: var(--space-sm);
  border-bottom: var(--rule-hairline);
}

.stream__head {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-md);
  align-items: baseline;
}

.stream__id {
  font-family: var(--font-display);
  font-size: 0.72rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
  color: var(--color-accent);
}

.stream__state {
  font-size: 0.72rem;
  color: var(--color-text-muted);
}

.stream__draws {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-sm);
}

.draw {
  display: grid;
  gap: 0.1rem;
  padding: 0.3rem 0.5rem;
  background: var(--color-surface);
}

.draw__raw {
  font-size: 0.74rem;
  color: var(--color-log-value);
}

.draw__reduced {
  font-size: 0.7rem;
  color: var(--color-text-muted);
}

.note {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
  line-height: 1.55;
  max-width: 68ch;
}
</style>
