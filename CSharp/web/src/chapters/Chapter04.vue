<script setup lang="ts">
import { ref } from 'vue'
import { postJson } from '../api/labs'
import type { BandView, PublishedView, SolveView } from '../api/labs'

defineProps<{ title: string; blurb: string }>()

const presetName = ref('Video5x64')
const targetBp = ref(7500)
const solve = ref<SolveView | null>(null)

const freeSpinsBp = ref(1300)
const pickBonusBp = ref(1000)
const band = ref<BandView | null>(null)

const published = ref<PublishedView | null>(null)

const error = ref('')
const busy = ref(false)

async function runPublished(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    published.value = await postJson<PublishedView>('/api/ch4/published', {
      gameFile: 'orca-dive.json',
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Pricing failed.'
  } finally {
    busy.value = false
  }
}

async function runSolve(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    solve.value = await postJson<SolveView>('/api/ch4/solve', {
      presetName: presetName.value,
      targetBaseRtpBasisPoints: targetBp.value,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Solve failed.'
  } finally {
    busy.value = false
  }
}

async function runBand(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    band.value = await postJson<BandView>('/api/ch4/band', {
      presetName: presetName.value,
      baseRtpBasisPoints: targetBp.value,
      freeSpinsRtpBasisPoints: freeSpinsBp.value,
      pickBonusRtpBasisPoints: pickBonusBp.value,
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Band failed.'
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
        The canonical paytable carries only ratios. The solver computes the game's expected
        value once from the strips, divides the target RTP by it, and scales every pay by
        that one number — a closed form, no search loop. Each scaled pay then rounds to
        integer millicents independently, so the realized RTP drifts a hair from the
        target, and only the analytic recomputation is authoritative.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Paytables/PaytableSolver.cs</code>,
        <code>Paytables/Paytable.cs</code>, <code>Rtp/AnalyticMath.cs</code>.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1 — Solve a paytable</h3>
      <p class="lab__lede">
        Pick a target and watch the whole path: canonical ratios, the single scale factor,
        the rounded integer pays, and the drift the rounding left behind.
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
          Target base RTP (bp)
          <input v-model.number="targetBp" type="number" min="100" max="9900" step="100" />
          <small>7500 = 75.00%</small>
        </label>
        <button type="button" :disabled="busy" @click="runSolve">Solve</button>
      </div>

      <p v-if="error" class="lab__error">{{ error }}</p>

      <div v-if="solve" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Unscaled EV</span>
            <span class="mono">{{ solve.unscaledEvMultiplier.toFixed(6) }}× wager</span>
          </div>
          <div>
            <span class="verdict__label">Scale factor</span>
            <span class="mono">{{ solve.scaleFactor.toFixed(6) }}</span>
          </div>
          <div>
            <span class="verdict__label">Realized base RTP</span>
            <span class="mono">{{ (solve.realizedBaseRtp * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Drift from target</span>
            <span class="mono">{{ solve.driftBasisPoints.toFixed(3) }} bp</span>
          </div>
        </div>

        <table class="lab-table">
          <thead>
            <tr>
              <th>Symbol</th><th>Count</th><th>Canonical</th>
              <th>P(exactly k)</th><th>Millicents</th><th>Credits</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in solve.paytable" :key="`${row.symbolId}-${row.count}`">
              <td>{{ row.symbol }}</td>
              <td>{{ row.count }}</td>
              <td>{{ row.canonical.toFixed(3) }}</td>
              <td>{{ row.probability.toExponential(3) }}</td>
              <td>{{ row.scaledMillicents.toLocaleString() }}</td>
              <td>{{ row.scaledCredits.toFixed(2) }}</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          Rare rows carry big pays, common rows small ones; the product of each row's pay
          and probability, summed, is the RTP. That sum is what the solver scaled.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2 — The band, priced before any spin</h3>
      <p class="lab__lede">
        Sigma comes from the strips and the paytable in closed form, covariance included.
        The band the finale draws is z·σ/√N — this table is that funnel as numbers.
      </p>

      <div class="controls">
        <label>
          Free spins (bp)
          <input v-model.number="freeSpinsBp" type="number" min="0" max="3000" step="100" />
        </label>
        <label>
          Pick bonus (bp)
          <input v-model.number="pickBonusBp" type="number" min="0" max="3000" step="100" />
        </label>
        <button type="button" :disabled="busy" @click="runBand">Price it</button>
      </div>

      <div v-if="band" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Total RTP</span>
            <span class="mono">{{ (band.analytic.totalRtp * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Sigma / unit wagered</span>
            <span class="mono">{{ band.analytic.sigma.toFixed(4) }}</span>
          </div>
          <div v-for="f in band.analytic.features" :key="f.name">
            <span class="verdict__label">{{ f.name }}</span>
            <span class="mono">{{ (f.rtp * 100).toFixed(2) }}%</span>
          </div>
        </div>

        <table class="lab-table">
          <thead>
            <tr><th>Spins</th><th>±99% band</th><th>±99.9% band</th></tr>
          </thead>
          <tbody>
            <tr v-for="b in band.bands" :key="b.spins">
              <td>{{ b.spins.toLocaleString() }}</td>
              <td>±{{ (b.halfWidth99 * 100).toFixed(4) }}pp</td>
              <td>±{{ (b.halfWidth999 * 100).toFixed(4) }}pp</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          Each factor of 100 in spins buys one decimal place of certainty — the square
          root in the denominator is why proving an RTP takes millions of spins, and why
          the simulation exists.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 3 — Orca Dive: the paytable that arrived fixed</h3>
      <p class="lab__lede">
        Orca Dive ships its paytable published; no solver runs. The same arithmetic still
        decides everything — each row's pay times its probability is that row's slice of
        the RTP, and the exhaustive enumerator supplies the probabilities exactly. Rows
        sort by contribution: the 2× Wild Orca single carries more of the game than the
        5000× Red 7 jackpot.
      </p>

      <div class="controls">
        <button type="button" :disabled="busy" @click="runPublished">Price Orca Dive</button>
      </div>

      <div v-if="published?.supported" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Line RTP</span>
            <span class="mono">{{ ((published.lineRtp ?? 0) * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Bonus RTP</span>
            <span class="mono">{{ ((published.bonusRtp ?? 0) * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Total RTP — exact</span>
            <span class="mono">{{ ((published.totalRtp ?? 0) * 100).toFixed(4) }}%</span>
          </div>
          <div>
            <span class="verdict__label">Outcome space</span>
            <span class="mono">{{ published.stopCombinations?.toLocaleString() }}</span>
          </div>
        </div>

        <table class="lab-table">
          <thead>
            <tr><th>Category</th><th>Count</th><th>Pays</th><th>Probability</th><th>RTP slice</th></tr>
          </thead>
          <tbody>
            <tr v-for="row in published.rows" :key="`${row.category}-${row.count}`">
              <td>{{ row.category }}</td>
              <td>{{ row.count }}</td>
              <td>{{ row.payMultiplier }}×</td>
              <td>{{ row.probability.toExponential(3) }}</td>
              <td>{{ (row.rtpContribution * 100).toFixed(3) }}%</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          Read this table against Lab 1: the solver walks from a target RTP to the pays;
          a published game walks from the pays to the RTP. Same equation, opposite
          direction — and the enumeration behind these probabilities is episode 7's
          referee.
        </p>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 5</h3>
      <p>
        The analytic twin predicts; the engine measures. Episode 5 builds the machine that
        plays ten million spins fast enough to put the two on one chart.
      </p>
    </section>
  </article>
</template>
