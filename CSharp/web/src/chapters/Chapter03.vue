<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import PaylinePattern from '../components/PaylinePattern.vue'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'
import type { CensusView, ReelSnapshotView, SourceView, SpinView } from '../api/labs'

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
const reelSnapshots = ref<ReelSnapshotView | null>(null)

const error = ref('')
const busy = ref(false)

const source = computed(() => sources.value.find((s) => s.id === sourceId.value))

// The API answers "did the snapshot keep its original first symbol"; the lab note asks
// the opposite question, so the polarity flips once here instead of inside the template.
const sourceEditChangedSnapshot = computed(() =>
  reelSnapshots.value !== null && !reelSnapshots.value.shortSnapshotKeptOriginalFirstSymbol)

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

async function compareReelSnapshots(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    reelSnapshots.value = await postJson<ReelSnapshotView>('/api/ch3/reel-snapshots', { seed: seed.value })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Reel snapshot comparison failed.'
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

function payFor(category: SourceView['paytable'][number], count: number): string {
  const pay = category.pays.find((entry) => entry.count === count)
  if (!pay) return '-'
  const multiplier = pay.payHundredths / 100
  return `${Number.isInteger(multiplier) ? multiplier.toFixed(0) : multiplier.toFixed(2)}×`
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
        Each reel has its own ordered strip. One random stop fills that reel's visible
        column with neighboring symbols from the strip. A five-reel, 3-position game draws
        five stops and displays fifteen symbols. A payline then reads one position from each
        reel, from left to right.
      </p>
      <p>
        Separate weighted choices could reproduce the chance of one symbol in one position,
        but they would lose the neighbor relationships within a reel. Those relationships
        matter when paylines overlap or a scatter rule checks the full window.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Reels/StripReelSet.cs</code>,
        <code>Reels/Payline.cs</code>, <code>Reels/ReelPreset.cs</code>, and the shipped
        <code>games/orca-dive.json</code>. Both labs run those types on the server.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1: The window over the strip</h3>
      <p class="lab__lede">
        Pick a game or preset and draw a window. The three positions in one column are
        neighbors on one reel strip. The next column comes from another strip and another
        stop number. Reusing the seed and spin index reproduces the same window. On Orca
        Dive, ★ marks Wild Orca and ◆ marks the Penguin scatter.
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

      <p v-if="source" class="lab-note">
        Configuration source: <strong>{{ source.configurationSource }}</strong>. Both paths
        produce the same reel-set and payline types before a spin begins.
      </p>

      <div v-if="source" class="par-summary">
        <div>
          <h4>PAR reel counts</h4>
          <table class="lab-table">
            <thead>
              <tr>
                <th>Symbol</th>
                <th v-for="reel in source.reelCount" :key="reel">Reel {{ reel }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="symbol in source.symbols" :key="symbol.id">
                <td>{{ symbol.name }}{{ symbol.isWild ? ' ★' : symbol.isScatter ? ' ◆' : '' }}</td>
                <td v-for="(count, reel) in symbol.perReel" :key="reel">{{ count }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div>
          <h4>PAR line payouts</h4>
          <table v-if="source.paytable.length" class="lab-table">
            <thead>
              <tr>
                <th>Pay category</th>
                <th v-for="count in source.reelCount" :key="count">{{ count }} reel{{ count === 1 ? '' : 's' }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="category in source.paytable" :key="category.name">
                <td>{{ category.name }}</td>
                <td v-for="count in source.reelCount" :key="count">{{ payFor(category, count) }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="lab-note">
            This built-in geometry preset has no PAR paytable. Select a game source to see
            its configured payout multipliers.
          </p>
        </div>
      </div>

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
      <h3>Lab 2: Counting what the strip produces</h3>
      <p class="lab__lede">
        Count how often a symbol lands in the center position and compare the result with
        its exact strip ratio. Orca Dive has two Wild Orcas among 26 stops on reel 1, but
        one among 29 stops on reel 2. Penguin is absent from reels 2 and 4, so its expected
        count on those reels is zero.
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
          Increasing the spin count usually reduces the difference between the observed
          result and the strip ratio.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 3: Replace a reel between runs</h3>
      <p class="lab__lede">
        Build one reel set from a 26-stop strip and another from a 36-stop strip. The same
        seed repeats the first result. The second reel set changes the next run without
        changing the first set.
      </p>
      <button type="button" :disabled="busy" @click="compareReelSnapshots">Compare snapshots</button>

      <div v-if="reelSnapshots" class="results">
        <table class="lab-table">
          <thead><tr><th>Snapshot</th><th>Stops</th><th>Visible symbol positions</th></tr></thead>
          <tbody>
            <tr><td>Run A</td><td>26</td><td>{{ reelSnapshots.shortSnapshot.window.join(' · ') }}</td></tr>
            <tr><td>Run A repeated</td><td>26</td><td>{{ reelSnapshots.repeatedShortSnapshot.window.join(' · ') }}</td></tr>
            <tr><td>Run B</td><td>36</td><td>{{ reelSnapshots.longSnapshot.window.join(' · ') }}</td></tr>
          </tbody>
        </table>
        <p class="lab-note">
          Run A repeated exactly: <strong>{{ reelSnapshots.shortSnapshotRepeatedExactly ? 'yes' : 'no' }}</strong>.
          Editing the original 26-stop source array changed the snapshot: <strong>{{ sourceEditChangedSnapshot ? 'yes' : 'no' }}</strong>.
        </p>
      </div>

      <ComprehensionCheck
        question="Why create a new reel-set snapshot instead of editing the active strip array?"
        :choices="['To preserve deterministic runs and safe sharing between workers.', 'Because C# arrays cannot be edited.', 'To force every reel to have the same length.']"
        :answer="0"
        explanation="The analyzer and all workers must read one stable geometry. The next run may receive a different immutable snapshot."
      />
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 4</h3>
      <p>
        Episode 4 uses <code>ProbabilityOf</code> for one symbol and
        <code>JointProbabilityOf</code> for two positions on the same reel. Those exact
        strip counts feed the paytable solver and variance calculation.
      </p>
    </section>
    <ComprehensionCheck
      question="A symbol appears twice on a 20-stop reel. What is its chance of landing on one chosen row?"
      :choices="['2%', '10%', '20%']"
      :answer="1"
      explanation="Two matching stops divided by 20 total stops equals 0.10, or 10%."
    />
    <OptimizationPreview
      question="How much does wraparound cost across 150 million visible-cell writes?"
      later="The direct modulo formula stays until its geometry tests pass. Episode 9 races it against short wrapped drawing strips and byte-ID windows."
    />
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

.par-summary {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 25rem), 1fr));
  gap: 1rem;
  align-items: start;
  margin: 1rem 0;
}

.par-summary h4 {
  margin: 0 0 0.5rem;
}
</style>
