<script setup lang="ts">
import { ref } from 'vue'
import { postJson } from '../api/labs'
import type { EnumerateView, RefereeView } from '../api/labs'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'

defineProps<{ title: string; blurb: string }>()
const emit = defineEmits<{ (e: 'navigate', id: string): void }>()

const gameFile = ref('classic-three-reel.json')
const enumeration = ref<EnumerateView | null>(null)

const seed = ref(20260811)
const workerCount = ref(8)
const spins = ref(2_000_000)
const referee = ref<RefereeView | null>(null)

const error = ref('')
const busy = ref(false)

async function enumerate(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    enumeration.value = await postJson<EnumerateView>('/api/ch7/enumerate', { gameFile: gameFile.value })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Enumeration failed.'
  } finally {
    busy.value = false
  }
}

async function runReferee(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    referee.value = await postJson<RefereeView>('/api/ch7/referee', {
      gameFile: gameFile.value,
      seed: seed.value,
      workerCount: workerCount.value,
      spins: spins.value,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Referee run failed.'
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
      <h3>What the episode proves</h3>
      <p>
        Three implementations share nothing but the game data: closed-form analysis,
        Monte-Carlo simulation, and exhaustive enumeration. The enumerator walks every stop
        combination with no randomness and no sampling, and counts what each category pays.
        When the simulation's measured RTP lands inside the band around the enumerator's
        exact figure, all three methods agree.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Games/GameAnalyzer.cs</code>,
        <code>Games/GameRunner.cs</code>, and the ground-truth suite in
        <code>tests/MMP.SlotGame.Tests/ExhaustiveGroundTruthTests.cs</code>.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — The census</h3>
      <p class="lab__lede">
        The classic game's space is 11,616 combinations; Orca Dive's is 14,781,416. Both
        enumerate in well under a second.
      </p>

      <div class="controls">
        <label>
          Game
          <select v-model="gameFile">
            <option value="classic-three-reel.json">Classic Three Reel</option>
            <option value="orca-dive.json">Orca Dive</option>
          </select>
        </label>
        <button type="button" :disabled="busy" @click="enumerate">Enumerate</button>
      </div>

      <p v-if="error" class="lab__error">{{ error }}</p>

      <div v-if="enumeration?.supported" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Outcome space</span>
            <span class="mono">{{ enumeration.stopCombinations?.toLocaleString() }}</span>
          </div>
          <div>
            <span class="verdict__label">Line hit frequency</span>
            <span class="mono">{{ ((enumeration.hitFrequency ?? 0) * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Line RTP</span>
            <span class="mono">{{ ((enumeration.lineRtp ?? 0) * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Bonus RTP</span>
            <span class="mono">{{ ((enumeration.bonusRtp ?? 0) * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Total RTP — exact</span>
            <span class="mono">{{ ((enumeration.totalRtp ?? 0) * 100).toFixed(4) }}%</span>
          </div>
        </div>

        <table class="lab-table">
          <thead>
            <tr><th>Category</th><th>Count</th><th>Combinations</th><th>Probability</th></tr>
          </thead>
          <tbody>
            <tr v-for="c in enumeration.combinations" :key="`${c.category}-${c.count}`">
              <td>{{ c.category }}</td>
              <td>{{ c.count }}</td>
              <td>{{ c.combinations.toLocaleString() }}</td>
              <td>{{ c.probability.toExponential(4) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-else-if="enumeration" class="lab-note">{{ enumeration.reason }}</p>
    </section>

    <section class="lab">
      <h3>Lab 2 — Simulation against the referee</h3>
      <p class="lab__lede">
        Play real spins with real randomness, then set the measurement beside the exact
        figures. The band is z·σ/√N at the 99% level, computed from the enumerator's own
        sigma. Hit frequency here counts line wins only, the same convention the PAR sheet
        uses.
      </p>

      <div class="controls">
        <label>
          Seed
          <input v-model.number="seed" type="number" min="0" />
        </label>
        <label>
          Workers
          <input v-model.number="workerCount" type="number" min="1" max="32" />
        </label>
        <label>
          Spins
          <input v-model.number="spins" type="number" min="10000" max="20000000" step="500000" />
        </label>
        <button type="button" :disabled="busy" @click="runReferee">
          {{ busy ? 'Spinning…' : 'Run the match' }}
        </button>
      </div>

      <div v-if="referee?.supported && referee.measured && referee.exact" class="results">
        <div class="verdict" :class="referee.withinBand ? '' : 'verdict--drift'">
          <div>
            <span class="verdict__label">Verdict</span>
            <span class="mono">{{ referee.withinBand ? 'WITHIN BAND' : 'OUTSIDE BAND' }}</span>
          </div>
          <div>
            <span class="verdict__label">Band at N</span>
            <span class="mono">±{{ ((referee.bandHalfWidth ?? 0) * 100).toFixed(4) }}pp</span>
          </div>
        </div>

        <table class="lab-table">
          <thead>
            <tr><th>Figure</th><th>Measured</th><th>Exact</th></tr>
          </thead>
          <tbody>
            <tr>
              <td>Total RTP</td>
              <td>{{ (referee.measured.totalRtp * 100).toFixed(4) }}%</td>
              <td>{{ (referee.exact.totalRtp * 100).toFixed(4) }}%</td>
            </tr>
            <tr>
              <td>Line RTP</td>
              <td>{{ (referee.measured.lineRtp * 100).toFixed(4) }}%</td>
              <td>{{ (referee.exact.lineRtp * 100).toFixed(4) }}%</td>
            </tr>
            <tr>
              <td>Bonus RTP</td>
              <td>{{ (referee.measured.bonusRtp * 100).toFixed(4) }}%</td>
              <td>{{ (referee.exact.bonusRtp * 100).toFixed(4) }}%</td>
            </tr>
            <tr>
              <td>Line hit frequency</td>
              <td>{{ (referee.measured.hitFrequency * 100).toFixed(4) }}%</td>
              <td>{{ (referee.exact.hitFrequency * 100).toFixed(4) }}%</td>
            </tr>
            <tr>
              <td>Bonus trigger</td>
              <td>{{ (referee.measured.triggerFrequency * 100).toFixed(4) }}%</td>
              <td>{{ (referee.exact.triggerProbability * 100).toFixed(4) }}%</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-else-if="referee && !referee.supported" class="lab-note">{{ referee.reason }}</p>
    </section>

    <section class="chapter-brief">
      <h3>The full run</h3>
      <p>
        The finale runs the full ten-million-spin proof live, with the convergence curve
        settling into the narrowing band on screen.
        <a href="#/finale" @click.prevent="emit('navigate', 'finale')">Open the proving ground →</a>
      </p>
    </section>
    <ComprehensionCheck
      question="A simulation result is outside the confidence band once. What can you conclude?"
      :choices="['The engine is certainly broken.', 'The exact analyzer is certainly wrong.', 'Investigate; a correct random run can occasionally fall outside the band.']"
      :answer="2"
      explanation="A confidence band describes expected variation, so one excursion is a reason to look rather than a conclusion. Repeated seeds and independent exact checks provide stronger evidence."
    />
    <OptimizationPreview
      question="How do we prove a faster implementation still performs the same work?"
      later="Episode 9 starts both versions from the same seed, compares checksums, alternates trial order, and reports medians only after outputs match."
    />
  </article>
</template>
