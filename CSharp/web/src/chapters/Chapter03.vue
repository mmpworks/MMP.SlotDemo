<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import PaylinePattern from '../components/PaylinePattern.vue'
import type { CensusView, SourceView, SpinView } from '../api/labs'

defineProps<{ title: string; blurb: string }>()

const sources = ref<SourceView[]>([])
const sourceId = ref('game:orca-dive.json')
const seed = ref(20260811)
const spinIndex = ref(0)
const spin = ref<SpinView | null>(null)
const activeLine = ref(0)

const censusSpins = ref(200_000)
const censusSymbolId = ref(4) // Wild Orca
const census = ref<CensusView | null>(null)

const error = ref('')
const busy = ref(false)

const source = computed(() => sources.value.find((s) => s.id === sourceId.value))

onMounted(async () => {
  try {
    sources.value = await getJson<SourceView[]>('/api/ch3/sources')
    if (!source.value) sourceId.value = sources.value[0]?.id ?? ''
    await draw()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load sources.'
  }
})

async function onSourceChange(): Promise<void> {
  censusSymbolId.value = source.value?.symbols[0]?.id ?? 0
  census.value = null
  await draw()
}

async function draw(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    spin.value = await postJson<SpinView>('/api/ch3/spin', {
      sourceId: sourceId.value,
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
      sourceId: sourceId.value,
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

function symbolName(id: number): string {
  return source.value?.symbols.find((s) => s.id === id)?.name ?? String(id)
}

function mark(cell: { isWild: boolean; isScatter: boolean }): string {
  if (cell.isWild) return ' ★'
  if (cell.isScatter) return ' ◆'
  return ''
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
        A reel is a strip: an ordered cycle of symbols the window slides over. That is a
        different object from a weighted die. Adjacent stops travel together into the
        window, so the strip's layout shapes what a multi-row window can show. In Orca Dive
        the reels are 26/29/26/29/26 stops, every reel its own strip, and the Penguin
        scatter exists only on reels 1, 3, and 5. A payline is a row path across the window,
        and the evaluator walks it left to right.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Reels/StripReelSet.cs</code>,
        <code>Reels/Payline.cs</code>, <code>Reels/ReelPreset.cs</code>, and the shipped
        <code>games/orca-dive.json</code>. Both labs run those types on the server.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — The window over the strip</h3>
      <p class="lab__lede">
        Pick a source, draw a window, and step through the deterministic spin stream. The
        same seed and index always produce the same window, because the generator is passed
        explicitly the way episode 2 set it up.
        On Orca Dive, ★ marks the Wild Orca and ◆ the Penguin scatter.
      </p>

      <div class="controls">
        <label>
          Source
          <select v-model="sourceId" @change="onSourceChange">
            <optgroup label="Games">
              <option v-for="s in sources.filter((x) => x.isGame)" :key="s.id" :value="s.id">
                {{ s.name }} ({{ s.stopsPerReel.join('/') }})
              </option>
            </optgroup>
            <optgroup label="Presets">
              <option v-for="s in sources.filter((x) => !x.isGame)" :key="s.id" :value="s.id">
                {{ s.name }} ({{ s.reelCount }}×{{ s.rows }})
              </option>
            </optgroup>
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

      <div v-if="spin && source" class="results">
        <div class="window-grid" :style="{ gridTemplateColumns: `repeat(${source.reelCount}, 1fr)` }">
          <template v-for="row in source.rows" :key="row">
            <div
              v-for="reel in source.reelCount"
              :key="`${reel}-${row}`"
              class="cell"
              :class="{ 'cell--line': onLineCell(reel - 1, row - 1) }"
            >
              <template v-for="cell in [spin.window.find((c) => c.reel === reel - 1 && c.row === row - 1)]" :key="0">
                {{ cell?.symbol }}{{ cell ? mark(cell) : '' }}
              </template>
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
            <PaylinePattern :rows="line.rows" :window-rows="source?.rows ?? 3" :active="i === activeLine" />
            {{ line.name }}
          </button>
        </div>

        <p class="lab-note">
          {{ spin.lines[activeLine]?.name }} reads
          <span class="mono">{{ spin.lines[activeLine]?.cells.map((c) => c.symbol).join(' · ') }}</span>
          on row path <span class="mono">[{{ spin.lines[activeLine]?.rows.join(', ') }}]</span>.
          The evaluator scores the leading run from reel 0; on Orca the Wild Orca extends
          a run of any of the four fish; other symbols it leaves alone.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2 — Counting what the strip produces</h3>
      <p class="lab__lede">
        Count how often a symbol lands in the centre row over many spins and compare with
        the strip's exact ratio. The engine holds no probability table. The strip layout
        sets the odds. On Orca Dive the expectation differs
        per reel: Wild Orca sits at 2/26 on reel 1 and 1/29 on reel 2, and Penguin's
        column reads zero on reels 2 and 4 because the symbol is absent from those strips.
      </p>

      <div class="controls">
        <label>
          Symbol
          <select v-model.number="censusSymbolId">
            <option v-for="s in source?.symbols" :key="s.id" :value="s.id">
              {{ s.name }}{{ s.isWild ? ' ★' : s.isScatter ? ' ◆' : '' }}
              ({{ s.perReel.join('/') }})
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
              <td>{{ r.reel + 1 }}</td>
              <td>{{ (r.observed * 100).toFixed(3) }}%</td>
              <td>{{ (r.expected * 100).toFixed(3) }}%</td>
              <td>{{ ((r.observed - r.expected) * 100).toFixed(3) }}pp</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          {{ symbolName(census.symbolId) }} over {{ census.spins.toLocaleString() }} spins.
          Push the spin count up and the gap column shrinks, the same convergence the
          proving ground shows for the whole game.
        </p>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 4</h3>
      <p>
        Episode 4 uses <code>ProbabilityOf</code> and <code>JointProbabilityOf</code> on the
        strip directly. The paytable solver, the sigma calculation, and Orca's exhaustive
        enumeration all read the strips, so a game can be priced without running it.
      </p>
    </section>
  </article>
</template>

<style scoped>
.window-grid {
  display: grid;
  gap: 4px;
  max-width: 40rem;
}

.cell {
  font-family: var(--font-mono);
  font-size: 0.78rem;
  text-align: center;
  padding: 0.7rem 0.3rem;
  background: var(--color-surface);
  border: var(--rule-hairline);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
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
  display: grid;
  gap: 0.3rem;
  justify-items: center;
  font-family: var(--font-mono);
  font-size: 0.72rem;
  background: transparent;
  color: var(--color-text-secondary);
  border: var(--rule-hairline);
  padding: 0.45rem 0.7rem 0.35rem;
  cursor: pointer;
}

.line-chip--active {
  color: var(--color-accent);
  border: var(--rule-brass);
}
</style>
