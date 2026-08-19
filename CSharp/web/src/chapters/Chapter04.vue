<script setup lang="ts">
import { ref } from 'vue'
import { postJson } from '../api/labs'
import type { BandView, PublishedView, SolveView } from '../api/labs'
import ComprehensionCheck from '../components/ComprehensionCheck.vue'
import OptimizationPreview from '../components/OptimizationPreview.vue'

defineProps<{ title: string; blurb: string }>()

const subject = ref('game:orca-dive.json')
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
    const isGame = subject.value.startsWith('game:')
    solve.value = await postJson<SolveView>('/api/ch4/solve', {
      presetName: isGame ? '' : subject.value,
      targetBaseRtpBasisPoints: targetBp.value,
      gameFile: isGame ? subject.value.slice('game:'.length) : '',
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
        This episode calculates a game's return before random spins begin. For each
        paytable row, multiply the payout by the chance of that result. Add those row
        contributions to get the theoretical RTP.
      </p>
      <p>
        The solver then compares that result with the requested target. If the unscaled
        table returns 50% and the target is 75%, every payout is multiplied by 1.5. The
        final payouts must be whole millicents, so the engine recalculates the realized RTP
        after rounding.
      </p>
      <p class="chapter-source">
        Source: <code>src/MMP.SlotGame.Core/Paytables/PaytableSolver.cs</code>,
        <code>Paytables/Paytable.cs</code>, <code>Rtp/AnalyticMath.cs</code>.
      </p>
    </section>

    <section class="lab">
      <h3>Lab 1: Solve a paytable</h3>
      <p class="lab__lede">
        Choose a target line RTP. The lab calculates each paytable row's probability and
        RTP contribution, adds the rows, and finds one scale factor for every payout. For
        Orca Dive, the published line RTP is 59.60%. Its separate 26.51% bonus raises the
        complete game to 86.11%.
      </p>

      <div class="controls">
        <label>
          Subject
          <select v-model="subject">
            <optgroup label="Games">
              <option value="game:orca-dive.json">Orca Dive</option>
              <option value="game:classic-three-reel.json">Classic Three Reel</option>
            </optgroup>
            <optgroup label="Presets">
              <option value="Classic3">Classic3</option><option value="Video3">Video3</option>
              <option value="Line4">Line4</option><option value="Video5x64">Video5x64</option>
              <option value="Video5x128">Video5x128</option>
            </optgroup>
          </select>
        </label>
        <label>
          Target line RTP (bp)
          <input v-model.number="targetBp" type="number" min="100" max="9900" step="100" />
          <small>7500 = 75.00%; Orca ships at 5960</small>
        </label>
        <button type="button" :disabled="busy" @click="runSolve">Solve</button>
      </div>

      <p v-if="error" class="lab__error">{{ error }}</p>

      <div v-if="solve" class="results">
        <div class="verdict verdict--info">
          <div>
            <span class="verdict__label">Requested target</span>
            <span class="mono">{{ (solve.targetBaseRtp * 100).toFixed(2) }}%</span>
          </div>
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
              <th>Exact probability</th><th>Solved payout</th><th>RTP contribution</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in solve.paytable" :key="`${row.symbolId}-${row.count}`">
              <td>{{ row.symbol }}</td>
              <td>{{ row.count }}</td>
              <td>{{ row.canonical.toFixed(3) }}</td>
              <td>{{ row.probability.toExponential(3) }}</td>
              <td>{{ row.scaledCredits.toFixed(2) }} credits</td>
              <td>{{ (row.rtpContribution * 100).toFixed(4) }}%</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          Read the last column as each row's share of line RTP. Add that column to get the
          realized line RTP shown above. The scale factor is target divided by unscaled EV.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 2: Calculate a confidence band</h3>
      <p class="lab__lede">
        RTP tells you the long-run average. It does not tell you how bumpy the trip will be.
        Sigma is the standard deviation of one spin's return: a larger sigma means payouts
        are spread farther from the average. The engine calculates it from the reel strips
        and paytable, including paylines that share visible positions.
      </p>
      <p class="lab-note">
        Picture two games with 50% RTP. One always pays 0.5 wager. The other pays zero nine
        times and 5 wagers once. Their averages match, but the second game is much more
        swingy. Variance measures that spread; sigma puts it back in ordinary wager units.
        Covariance records when two awards rise or fall together because they read the same
        reel window.
      </p>
      <p class="lab-note">
        The 99% value <code>z = 2.576</code> comes from the standard normal bell curve.
        The middle keeps 99%, leaving 0.5% outside on each side. The upper edge is therefore
        the curve's 99.5th percentile: <code>2.575829...</code>. It is a mathematical
        constant, not a number measured from the game.
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
          Compare 10,000 spins with 1,000,000 spins. Multiplying the spin count by 100
          divides the band width by 10 because the formula uses the square root of spins.
          Think of averaging repeated readings from a noisy scale: more readings quiet the
          noise, but the improvement is gradual.
        </p>
      </div>
    </section>

    <section class="lab">
      <h3>Lab 3: Price Orca Dive's published paytable</h3>
      <p class="lab__lede">
        Orca Dive already has approved payouts, so this lab does not change them. It counts
        every stop combination, multiplies each row's payout by its probability, and sorts
        the rows by RTP contribution.
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
            <span class="verdict__label">Exact total RTP</span>
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
              <td>{{ row.payMultiplier }}× wager</td>
              <td>{{ row.probability.toExponential(3) }}</td>
              <td>{{ (row.rtpContribution * 100).toFixed(3) }}%</td>
            </tr>
          </tbody>
        </table>
        <p class="lab-note">
          Lab 1 starts with a target and calculates payouts. This lab starts with published
          payouts and calculates RTP. Both use the same row calculation: payout times
          probability.
        </p>
      </div>
    </section>

    <section class="chapter-brief">
      <h3>Carried into episode 5</h3>
      <p>
        The analytic figures predict what the engine should measure. Episode 5 builds the
        engine that plays ten million spins fast enough to chart both.
      </p>
    </section>
    <ComprehensionCheck
      question="Why can realized RTP differ slightly from target RTP?"
      :choices="['Each payout is rounded to whole millicents.', 'The solver runs random spins.', 'RTP changes with the seed.']"
      :answer="0"
      explanation="The target sets the scale, but each finished payout must be rounded separately. The analyzer recalculates the actual result."
    />
    <OptimizationPreview
      question="Should a readable dictionary also be the spin loop's payout lookup?"
      later="Keep the dictionary while proving the paytable. Episode 9 compiles a dense execution view and measures tuple hashing against array indexing."
    />
  </article>
</template>
