<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import type { CensusView, PresetView, SpinView } from '../api/labs'

defineProps<{ title: string; blurb: string }>()

const presets = ref<PresetView[]>([])
const presetName = ref('Classic3')
const seed = ref(20260811)
const spinIndex = ref(0)
const spin = ref<SpinView | null>(null)
const activeLine = ref(0)

const censusSpins = ref(200_000)
const censusSymbolId = ref(0)
const census = ref<CensusView | null>(null)

const error = ref('')
const busy = ref(false)

const preset = computed(() => presets.value.find((p) => p.name === presetName.value))

onMounted(async () => {
  try {
    presets.value = await getJson<PresetView[]>('/api/ch3/presets')
    await draw()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load presets.'
  }
})

async function draw(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    spin.value = await postJson<SpinView>('/api/ch3/spin', {
      presetName: presetName.value,
      seed: seed.value,
      spinIndex: spinIndex.value,
    })
    activeLine.value = Math.min(activeLine.value, (spin.value?.lines.length ?? 1) - 1)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Spin failed.'
  } finally {
    busy.value = false
  }
}

async function step(delta: number): Promise<void> {
  spinIndex.value = Math.max(0, spinIndex.value + delta)
  await draw()
}

async function runCensus(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    census.value = await postJson<CensusView>('/api/ch3/census', {
      presetName: presetName.value,
      seed: seed.value,
      spins: censusSpins.value,
      symbolId: censusSymbolId.value,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Census failed.'
  } finally {
    busy.value = false
  }
}

function onLineCell(reel: number, row: number): boolean {
  const line = spin.value?.lines[activeLine.value]
  return line !== undefined && line.rows[reel] === row
}

const symbolName = computed(() => {
  const p = preset.value
  return (id: number) => p?.symbols.find((s) => s.id === id)?.name ?? String(id)
})
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
        A reel is a strip: an ordered cycle of symbols the window slides over. That is a
        different object from a weighted die — adjacent stops travel together into the
        window, so the strip's layout shapes what multi-row windows can show. A payline is
        a row path across that window; the evaluator walks it left to right.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Reels/StripReelSet.cs</code>,
        <code>Reels/Payline.cs</code>, <code>Reels/ReelPreset.cs</code>. Both labs run those
        types on the server.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — The window over the strip</h3>
      <p class="lab__lede">
        Pick a preset, draw a window, and step through the deterministic spin stream. The
        same seed and index always produce the same window — that is invariant R3 doing
        its job. Click a payline to see which cells it reads.
      </p>

      <div class="controls">
        <label>
          Preset
          <select v-model="presetName" @change="draw">
            <option v-for="p in presets" :key="p.name" :value="p.name">
              {{ p.name }} ({{ p.reelCount }}×{{ p.rows }})
            </option>
          </select>
        </label>
        <label>
          Seed
          <input v-model.number="seed" type="number" min="0" @change="draw" />
        </label>
        <label>
          Spin index
          <input v-model.number="spinIndex" type="number" min="0" max="10000" @change="draw" />
        </label>
        <button type="button" :disabled="busy" @click="step(-1)">&#8592; Prev</button>
        <button type="button" :disabled="busy" @click="step(1)">Next &#8594;</button>
      </div>

      <p v-if="error" class="lab__error">{{ error }}</p>

      <div v-if="spin && preset" class="results">
        <div class="window-grid" :style="{ gridTemplateColumns: `repeat(${preset.reelCount}, 1fr)` }">
          <template v-for="row in preset.rows" :key="row">
            <div
              v-for="reel in preset.reelCount"
              :key="`${reel}-${row}`"
              class="cell"
              :class="{ 'cell--line': onLineCell(reel - 1, row - 1) }"
            >
              {{ spin.window.find((c) => c.reel === reel - 1 && c.row === row - 1)?.symbol }}
            </div>
          </template>
        </div>

        <div class="line-picker">
          <button
            v-for="(line, i) in spin.lines"
            :key="line.name"
            type="button"
            class="line-chip"
            :class="{ 'line-chip--active': i === activeLine }"
            @click="activeLine = i"
          >
            {{ line.name }}
          </button>
        </div>

        <p class="lab-note">
          {{ spin.lines[activeLine]?.name }} reads
          <span class="mono">{{ spin.lines[activeLine]?.cells.map((c) => c.symbol).join(' · ') }}</span>
          — row path <span class="mono">[{{ spin.lines[activeLine]?.rows.join(', ') }}]</span>.
          The evaluator scores the leading run from reel 0; three or more of a kind pays.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2 — The strip is the distribution</h3>
      <p class="lab__lede">
        Count how often a symbol lands in the centre row over many spins and compare with
        the strip's exact ratio. No probability table exists anywhere in the engine — the
        strip's layout is the only source of odds, and the census converges on it.
      </p>

      <div class="controls">
        <label>
          Symbol
          <select v-model.number="censusSymbolId">
            <option v-for="s in preset?.symbols" :key="s.id" :value="s.id">
              {{ s.name }} ({{ s.weight }}/{{ preset?.stopsPerReel }})
            </option>
          </select>
        </label>
        <label>
          Spins
          <input v-model.number="censusSpins" type="number" min="100" max="1000000" step="50000" />
        </label>
        <button type="button" :disabled="busy" @click="runCensus">Count</button>
      </div>

      <div v-if="census" class="results">
        <table class="lab-table">
          <thead>
            <tr><th>Reel</th><th>Observed</th><th>Strip ratio</th><th>Gap</th></tr>
          </thead>
          <tbody>
            <tr v-for="r in census.perReel" :key="r.reel">
              <td>{{ r.reel }}</td>
              <td>{{ (r.observed * 100).toFixed(3) }}%</td>
              <td>{{ (r.expected * 100).toFixed(3) }}%</td>
              <td>{{ ((r.observed - r.expected) * 100).toFixed(3) }}pp</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          {{ symbolName(census.symbolId) }} over {{ census.spins.toLocaleString() }} spins.
          Push the spin count up and the gap column shrinks — the same convergence the
          finale page shows for the whole game.
        </p>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 4</h3>
      <p>
        <code>ProbabilityOf</code> and <code>JointProbabilityOf</code> on the strip are the
        seam the math chapter stands on: the paytable solver and the sigma calculation read
        the strips directly, which is how the analytic twin prices the game without playing
        a single spin.
      </p>
    </section>
  </article>
</template>

<style scoped>
.window-grid {
  display: grid;
  gap: 4px;
  max-width: 34rem;
}

.cell {
  font-family: var(--font-mono);
  font-size: 0.82rem;
  text-align: center;
  padding: 0.7rem 0.3rem;
  background: var(--color-surface);
  border: var(--rule-hairline);
}

.cell--line {
  border: var(--rule-brass);
  color: var(--color-accent-bright);
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
}

.line-picker {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.line-chip {
  font-family: var(--font-mono);
  font-size: 0.72rem;
  background: transparent;
  color: var(--color-text-secondary);
  border: var(--rule-hairline);
  padding: 0.3rem 0.7rem;
  cursor: pointer;
}

.line-chip--active {
  color: var(--color-accent);
  border: var(--rule-brass);
}
</style>
