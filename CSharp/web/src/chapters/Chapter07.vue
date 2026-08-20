<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getJson, postJson } from '../api/labs'
import type { GameSummary, ValidateView } from '../api/labs'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'

defineProps<{ title: string; blurb: string }>()

const games = ref<GameSummary[]>([])
const selected = ref<GameSummary | null>(null)

const draft = ref('')
const validation = ref<ValidateView | null>(null)

const error = ref('')
const busy = ref(false)

onMounted(async () => {
  try {
    games.value = await getJson<GameSummary[]>('/api/ch6/games')
    selected.value = games.value.find((game) => game.file === 'orca-dive.json') ?? games.value[0] ?? null
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load games.'
  }
})

const brokenExample = `{
  "name": "Broken Example",
  "symbols": [ { "name": "Seven" }, { "name": "Cherry", "scatter": true } ],
  "reels": [ ["Seven", "Cherry", "Seven"], ["Cherry", "Missing", "Seven"] ],
  "reelStops": [3, 4],
  "paylines": [ { "name": "Center", "rows": [1, 1, 1] } ],
  "paytable": [ { "symbol": "Bell", "pays": { "3": 2.25 } } ]
}`

function loadBroken(): void {
  draft.value = brokenExample
}

async function validate(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    validation.value = await postJson<ValidateView>('/api/ch6/validate', { json: draft.value })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Validation failed.'
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
      <h3>Load Orca Dive from JSON</h3>
      <p>
        Orca Dive lives in <code>games/orca-dive.json</code>. The document contains its
        symbols, ordered reel strips, payline, paytable, wild rule, and Penguin Bonus. The
        loader turns that document into a <code>GameDefinition</code>. It also recounts the
        stops and symbols, catching a PAR-sheet transcription error before a simulation
        reports the wrong RTP.
      </p>
      <table class="lab-table">
        <thead><tr><th>Stage</th><th>Source</th><th>Result</th></tr></thead>
        <tbody>
          <tr><td>Read JSON</td><td><code>GameDefinitionLoader.TryLoad()</code></td><td>Nullable <code>GameDocument</code></td></tr>
          <tr><td>Validate and compile</td><td><code>GameDefinitionBuilder.TryBuild()</code></td><td>Validated <code>GameDefinition</code></td></tr>
          <tr><td>Prepare payouts</td><td><code>WinningOutcomeTable.Build()</code></td><td><code>WinningOutcome</code> records</td></tr>
          <tr><td>Prepare spin lookup</td><td><code>ProgressiveOutcomeTable.Build()</code></td><td>Reel-prefix arrays</td></tr>
          <tr><td>Run spins</td><td><code>GameRunner.CreatePlay()</code></td><td><code>SpinOutcome</code> values</td></tr>
        </tbody>
      </table>
      <p class="lab-note">
        The loader reads names for people. The prepared spin path uses byte-sized reel stops
        and previously calculated outcomes. It does not parse JSON or rescore every payline
        during each spin.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Games/Definition/</code> and the shipped
        documents in <code>games/</code>.
      </p>
    </section>

    <section class="chapter-brief">
      <h3>Parsing and validation answer different questions</h3>
      <p>
        Parsing checks JSON punctuation and turns the text into a <code>GameDocument</code>.
        Validation checks the game rules. A file can have correct braces and commas while
        its reel strip names an undeclared symbol, its payline has the wrong number of row
        positions, or its declared stop count disagrees with the strip.
      </p>
      <p>
        Paylines in this schema contain one visible row number per reel. They do not name
        symbols. Unknown symbol checks belong to reel strips, groups, wild substitutions,
        paytable categories, and feature rules.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — Read the shipped game files</h3>
      <p v-if="error" class="lab__error">{{ error }}</p>

      <div class="game-picker">
        <button
          v-for="g in games"
          :key="g.file"
          type="button"
          class="lab-button"
          :class="{ inactive: selected?.file !== g.file }"
          @click="selected = g"
        >
          {{ g.name ?? g.file }}
        </button>
      </div>

      <div v-if="selected?.valid" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Geometry</span>
            <span class="mono">{{ selected.reels }} reels × {{ selected.rows }} rows</span>
          </div>
          <div>
            <span class="verdict__label">Stops per reel</span>
            <span class="mono">{{ selected.stopsPerReel?.join(' · ') }}</span>
          </div>
          <div>
            <span class="verdict__label">Outcome space</span>
            <span class="mono">{{ selected.stopCombinations?.toLocaleString() }}</span>
          </div>
          <div v-if="selected.bonus">
            <span class="verdict__label">Bonus</span>
            <span class="mono">{{ selected.bonus.name }} (max {{ selected.bonus.maxAward }}×)</span>
          </div>
        </div>

        <table class="lab-table">
          <thead>
            <tr><th>Category</th><th>Kind</th><th>Pays</th></tr>
          </thead>
          <tbody>
            <tr v-for="c in selected.categories" :key="c.name">
              <td>{{ c.name }}</td>
              <td>{{ c.kind }}</td>
              <td>{{ c.pays.map((p) => `${p.count}→${p.payHundredths / 100}×`).join('  ') }}</td>
            </tr>
          </tbody>
        </table>

        <p class="lab-note">
          Symbols:
          <span class="mono">
            {{ selected.symbols?.map((s) => s.name + (s.isWild ? ' (wild)' : s.isScatter ? ' (scatter)' : '')).join(', ') }}
          </span>
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2 — Feed the loader anything</h3>
      <p class="lab__lede">
        Paste a game definition and validate it. A valid document becomes a compiled game.
        An invalid document reports all independent problems found in that pass, so related
        mistakes can be fixed together.
      </p>

      <div class="controls">
        <button type="button" class="ghost" @click="loadBroken">Load a broken example</button>
        <button type="button" :disabled="busy || !draft" @click="validate">Validate</button>
      </div>

      <textarea
        v-model="draft"
        class="editor mono"
        rows="12"
        spellcheck="false"
        placeholder="Paste a game definition JSON here"
      />

      <div v-if="validation" class="results">
        <div v-if="validation.valid" class="verdict">
          <div>
            <span class="verdict__label">Compiled</span>
            <span class="mono">{{ validation.name }}</span>
          </div>
          <div>
            <span class="verdict__label">Outcome space</span>
            <span class="mono">{{ validation.stopCombinations?.toLocaleString() }}</span>
          </div>
        </div>
        <div v-else class="refused">
          <span class="verdict__label">Refused — {{ validation.errors?.length }} problem(s)</span>
          <ul>
            <li v-for="(problem, i) in validation.errors" :key="i" class="mono">{{ problem }}</li>
          </ul>
        </div>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 8</h3>
      <p>
        Because the game is data, every stop combination can be walked. Episode 8 does that
        and uses the exhaustive census to referee the simulation.
      </p>
    </section>
    <ComprehensionCheck
      question="Why does the loader report several validation errors at once?"
      :choices="['JSON requires it.', 'The author can fix related problems in one pass.', 'It makes the game run faster.']"
      :answer="1"
      explanation="Independent checks can all run after parsing, so the author does not need a separate edit-and-run cycle for each problem."
    />
    <OptimizationPreview
      question="Does a worker need complete symbols after the game has been compiled?"
      later="Configuration keeps names and flags. Episode 9 measures a byte-ID execution view while preserving the rich domain model."
    />
  </article>
</template>

<style scoped>
.game-picker {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-sm);
  margin-bottom: var(--space-md);
}

.lab-button.inactive {
  color: var(--color-text-secondary);
  border: var(--rule-hairline);
}

.editor {
  width: 100%;
  background: var(--color-surface);
  border: var(--rule-hairline);
  color: var(--color-text-primary);
  font-size: 0.8rem;
  padding: var(--space-sm);
  resize: vertical;
}

.refused ul {
  margin: 0.4rem 0 0;
  padding-left: 1.2rem;
}

.refused li {
  font-size: 0.8rem;
  color: var(--color-log-warning);
  line-height: 1.6;
}
</style>
